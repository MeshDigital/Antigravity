using System.IO;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using SLSKDONET.Configuration;
using SLSKDONET.Models;

namespace SLSKDONET.Services;

/// <summary>
/// Manages download jobs and orchestrates the download process.
/// </summary>
public class DownloadManager : INotifyPropertyChanged, IDisposable
{
    private readonly ILogger<DownloadManager> _logger;
    private readonly AppConfig _config;
    private readonly SoulseekAdapter _soulseek;
    private readonly FileNameFormatter _fileNameFormatter;
    private readonly ConcurrentDictionary<string, DownloadJob> _jobs = new();
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly Channel<DownloadJob> _jobChannel;
    private CancellationTokenSource _cts = new();
    private readonly List<Task> _runningTasks = new();

    public event EventHandler<DownloadJob>? JobUpdated;
    public event EventHandler<DownloadJob>? JobCompleted;
    public event PropertyChangedEventHandler? PropertyChanged;

    public DownloadManager(
        ILogger<DownloadManager> logger,
        AppConfig config,
        SoulseekAdapter soulseek,
        FileNameFormatter fileNameFormatter)
    {
        _logger = logger;
        _config = config;
        _soulseek = soulseek;
        _fileNameFormatter = fileNameFormatter;
        _concurrencySemaphore = new SemaphoreSlim(_config.MaxConcurrentDownloads);
        _jobChannel = Channel.CreateUnbounded<DownloadJob>();
    }

    /// <summary>
    /// Enqueues a track for download.
    /// </summary>
    public DownloadJob EnqueueDownload(Track track, string? outputPath = null, string? sourceTitle = null)
    {
        var job = new DownloadJob
        {
            Track = track,
            OutputPath = outputPath ?? Path.Combine(
                _config.DownloadDirectory!, // App.xaml.cs ensures this is not null
                FormatFilename(track)
            ),
            DestinationPath = outputPath ?? Path.Combine(
                _config.DownloadDirectory!,
                FormatFilename(track)
            ),
            SourceTitle = sourceTitle
        };

        _jobs.TryAdd(job.Id, job);
        job.PropertyChanged += Job_PropertyChanged;
        _logger.LogInformation("Enqueued download: {TrackId}", job.Id);
        OnPropertyChanged(nameof(SuccessfulCount));
        OnPropertyChanged(nameof(FailedCount));
        OnPropertyChanged(nameof(TodoCount));
        
        // Post the job to the channel for processing
        _jobChannel.Writer.TryWrite(job);

        return job;
    }

    /// <summary>
    /// Starts the long-running task that processes jobs from the channel.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _logger.LogInformation("Download manager started. Waiting for jobs...");

        try
        {
            // Continuously read from the channel until it's completed.
            await foreach (var job in _jobChannel.Reader.ReadAllAsync(_cts.Token))
            {
                // Start the job and track it to prevent unobserved exceptions
                var task = ProcessJobAsync(job, _cts.Token);
                lock (_runningTasks)
                {
                    _runningTasks.Add(task);
                    // Clean up completed tasks
                    _runningTasks.RemoveAll(t => t.IsCompleted);
                }
                // Observe the task to prevent unobserved exceptions and mark observed
                _ = task.ContinueWith(t =>
                {
                    if (t.IsFaulted && t.Exception != null)
                    {
                        _logger.LogError(t.Exception, "Unhandled exception in ProcessJobAsync");
                        var _ = t.Exception.Flatten(); // mark observed
                    }
                }, TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Download manager processing loop cancelled.");
        }
        _logger.LogInformation("Download manager stopped.");
    }

    /// <summary>
    /// Processes a single download job.
    /// </summary>
    private async Task ProcessJobAsync(DownloadJob job, CancellationToken ct)
    {        
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, job.CancellationTokenSource.Token);
        var effectiveCt = linkedCts.Token;

        await _concurrencySemaphore.WaitAsync(effectiveCt);
        try
        {
            // Download the file
            var progress = new Progress<double>(p =>
            {
                job.Progress = p;
                job.BytesDownloaded = (long?)((job.Track.Size ?? 0) * p);
                JobUpdated?.Invoke(this, job);
            });

            job.State = DownloadState.Downloading;
            job.StartedAt = DateTime.UtcNow;
            JobUpdated?.Invoke(this, job);

            var success = await _soulseek.DownloadAsync(
                job.Track.Username!,
                job.Track.Filename!,
                job.OutputPath!,
                job.Track.Size,
                progress,
                effectiveCt
            );

            job.Progress = 1.0;
            job.Track.LocalPath = job.OutputPath; // persist local path for history
            job.State = success ? DownloadState.Completed : DownloadState.Failed;
            job.CompletedAt = DateTime.UtcNow;

            if (!success)
                job.ErrorMessage = "Download failed";

            _logger.LogInformation("Job completed: {JobId} - {State}", job.Id, job.State);
        }
        catch (OperationCanceledException)
        {
            job.State = DownloadState.Cancelled;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogWarning("Job cancelled: {JobId}", job.Id);
        }
        catch (Exception ex)
        {
            job.State = DownloadState.Failed;
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            _logger.LogError(ex, "Job error: {JobId}", job.Id);
        }
        finally
        {
            JobCompleted?.Invoke(this, job);
            _concurrencySemaphore.Release();
            OnPropertyChanged(nameof(SuccessfulCount));
            OnPropertyChanged(nameof(FailedCount));
            OnPropertyChanged(nameof(TodoCount));
        }
    }

    /// <summary>
    /// Cancels all pending downloads.
    /// </summary>
    public void CancelAll()
    {
        _logger.LogInformation("Cancelling all downloads");
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }
        // Complete the channel to stop the processing loop.
        _jobChannel.Writer.TryComplete();
    }

    /// <summary>
    /// Gets all download jobs.
    /// </summary>
    public IEnumerable<DownloadJob> GetJobs() => _jobs.Values;

    /// <summary>
    /// Gets a specific download job.
    /// </summary>
    public DownloadJob? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    /// <summary>
    /// Formats a track filename using the configured name format.
    /// </summary>
    private string FormatFilename(Track track)
    {
        var template = _config.NameFormat ?? "{artist} - {title}";
        var filename = _fileNameFormatter.Format(template, track);

        var ext = track.GetExtension();
        return string.IsNullOrEmpty(ext) ? filename : $"{filename}.{ext}";
    }

    private void Job_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DownloadJob.State) || e.PropertyName == nameof(DownloadJob.Status))
        {
            OnPropertyChanged(nameof(SuccessfulCount));
            OnPropertyChanged(nameof(FailedCount));
            OnPropertyChanged(nameof(TodoCount));
        }
    }

    public int SuccessfulCount => _jobs.Values.Count(j => j.State == DownloadState.Completed);
    public int FailedCount => _jobs.Values.Count(j => j.State == DownloadState.Failed || j.State == DownloadState.Cancelled);
    public int TodoCount => _jobs.Values.Count(j => j.State == DownloadState.Pending || j.State == DownloadState.Downloading || j.State == DownloadState.Searching);

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _jobChannel.Writer.TryComplete();
        _concurrencySemaphore?.Dispose();
    }
}
