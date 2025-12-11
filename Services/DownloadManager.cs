
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;
using SLSKDONET.Models;
using SLSKDONET.Services.InputParsers;
using SLSKDONET.Utils;
using SLSKDONET.ViewModels;

namespace SLSKDONET.Services;

/// <summary>
/// Orchestrates the download process for projects and individual tracks.
/// Manages the global state of all active and past downloads.
/// </summary>
public class DownloadManager : INotifyPropertyChanged, IDisposable
{
    private readonly ILogger<DownloadManager> _logger;
    private readonly AppConfig _config;
    private readonly SoulseekAdapter _soulseek;
    private readonly FileNameFormatter _fileNameFormatter;
    private readonly ITaggerService _taggerService;

    // Concurrency control
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly CancellationTokenSource _globalCts = new();
    private Task? _processingTask;

    // Global State
    // Using BindingOperations for thread safety is best practice for ObservableCollection accessed from threads
    public ObservableCollection<PlaylistTrackViewModel> AllGlobalTracks { get; } = new();
    private readonly object _collectionLock = new object();

    public DownloadManager(
        ILogger<DownloadManager> logger,
        AppConfig config,
        SoulseekAdapter soulseek,
        FileNameFormatter fileNameFormatter,
        ITaggerService taggerService)
    {
        _logger = logger;
        _config = config;
        _soulseek = soulseek;
        _fileNameFormatter = fileNameFormatter;
        _taggerService = taggerService;

        _concurrencySemaphore = new SemaphoreSlim(_config.MaxConcurrentDownloads);

        // Enable cross-thread collection access
        System.Windows.Data.BindingOperations.EnableCollectionSynchronization(AllGlobalTracks, _collectionLock);
    }

    /// <summary>
    /// Queues a project (list of tracks) for processing.
    /// </summary>
    public void QueueProject(List<PlaylistTrack> tracks)
    {
        _logger.LogInformation("Queueing project with {Count} tracks", tracks.Count);
        lock (_collectionLock)
        {
            foreach (var track in tracks)
            {
                var vm = new PlaylistTrackViewModel(track);
                AllGlobalTracks.Add(vm);
            }
        }
        // Processing loop picks this up automatically
    }

    /// <summary>
    /// Helper to enqueue a single ad-hoc track (e.g. from search results).
    /// </summary>
    public void EnqueueTrack(Track track)
    {
        // Wrap the standard Track in a PlaylistTrack
        var playlistTrack = new PlaylistTrack
        {
             Id = Guid.NewGuid(),
             Artist = track.Artist ?? "Unknown",
             Title = track.Title ?? "Unknown",
             Album = track.Album ?? "Unknown",
             Status = TrackStatus.Missing, // Assume missing until downloaded
             ResolvedFilePath = Path.Combine(_config.DownloadDirectory!, _fileNameFormatter.Format(_config.NameFormat ?? "{artist} - {title}", track) + "." + track.GetExtension()),
             TrackUniqueHash = track.UniqueHash
        };
        
        // If we already know the file details (from search), we can pre-populate the model 
        // effectively skipping the 'Searching' phase if we implement that check in ProcessTrackAsync
        // For now, we'll let it flow through the state machine.
        // Actually, if it's from search, we WANT to download THIS specific file.
        // We'll attach the SoulseekFile if possible or just use the username/filename
        
        // TODO: Pass specific file info to ViewModel so it skips search?
        // For now, adhering to the "Project" architecture which implies "I want this song", not "I want this file".
        // But for direct downloads, that's inefficient. 
        // We can handle this by checking if the passed track has Username/Filename and using it.
        
        QueueProject(new List<PlaylistTrack> { playlistTrack });
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_processingTask != null) return;

        _logger.LogInformation("DownloadManager Orchestrator started.");
        
        // We link the passed CT with our global CT to allow stopping from either source
        _processingTask = ProcessQueueLoop(_globalCts.Token); // Use global token for the long-running task
        await Task.CompletedTask;
    }

    private async Task ProcessQueueLoop(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                PlaylistTrackViewModel? nextTrack = null;

                lock (_collectionLock)
                {
                    // Find the next Pending track
                    nextTrack = AllGlobalTracks.FirstOrDefault(t => t.State == PlaylistTrackState.Pending);
                }

                if (nextTrack == null)
                {
                    await Task.Delay(500, token);
                    continue;
                }

                // Acquire a concurrency slot
                await _concurrencySemaphore.WaitAsync(token);

                // Double check status (race condition)
                if (nextTrack.State != PlaylistTrackState.Pending)
                {
                    _concurrencySemaphore.Release();
                    continue;
                }

                // Start processing in background (Fire & Forget)
                // We don't await this; we want to continue the loop to find more work if concurrency allows
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessTrackAsync(nextTrack, token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "CRITICAL: Error in ProcessTrack wrapper for {Artist} - {Title}", nextTrack.Artist, nextTrack.Title);
                        nextTrack.State = PlaylistTrackState.Failed;
                        nextTrack.ErrorMessage = "Internal Error: " + ex.Message;
                    }
                    finally
                    {
                        _concurrencySemaphore.Release();
                    }
                }, token);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("DownloadManager processing loop cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DownloadManager processing loop crashed!");
        }
    }

    private async Task ProcessTrackAsync(PlaylistTrackViewModel track, CancellationToken ct)
    {
        track.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var trackCt = track.CancellationTokenSource.Token;

        try
        {
            // --- 0. Pre-check ---
            if (track.Model.Status == TrackStatus.Downloaded && File.Exists(track.Model.ResolvedFilePath))
            {
                track.State = PlaylistTrackState.Completed;
                track.Progress = 100;
                return;
            }

            // --- 1. Search Phase ---
            // If the model doesn't have specific file info, we must search.
            // If it DOES (e.g. from manual search), we might skip this. 
            // For Bundle 1, let's assume we always search if it's "Pending" to be robust for projects.
            
            track.State = PlaylistTrackState.Searching;
            track.Progress = 0; // Infinite spinner logic in UI often checks IsActive
            
            var query = $"{track.Artist} {track.Title}";
            var results = new ConcurrentBag<Track>();
            
            // Search with 30s timeout
            using var searchCts = CancellationTokenSource.CreateLinkedTokenSource(trackCt);
            searchCts.CancelAfter(TimeSpan.FromSeconds(30)); 

            try 
            {
                await _soulseek.SearchAsync(
                    query,
                    null, // TODO: Get format filter from Config
                    (null, null), // TODO: Get bitrate filter from Config
                    DownloadMode.Normal,
                    (found) => {
                        foreach (var f in found) results.Add(f);
                    },
                    searchCts.Token
                );
            }
            catch (OperationCanceledException) { /* Timeout or Cancelled */ }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Search error for {Query}", query);
                // Continue if we have any results, else fail
            }

            if (results.IsEmpty)
            {
                track.State = PlaylistTrackState.Failed;
                track.ErrorMessage = "No results found";
                return;
            }

            // Select Best Match
            var preferredFormat = _config.PreferredFormats?.FirstOrDefault() ?? "mp3";
            var minBitrate = _config.MinBitrate ?? 128;
            
            var bestMatch = results
                .Where(t => t.Bitrate >= minBitrate)
                // .Where(t => t.GetExtension().Contains(preferredFormat)) // Simple filter
                .OrderByDescending(t => t.Bitrate)
                .ThenByDescending(t => t.Length ?? 0)
                .FirstOrDefault();

            if (bestMatch == null)
            {
                // Relaxed fallback
                 bestMatch = results.OrderByDescending(t => t.Bitrate).FirstOrDefault();
            }
            
            if (bestMatch == null)
            {
                track.State = PlaylistTrackState.Failed;
                track.ErrorMessage = "No suitable match found";
                return;
            }

            // --- 2. Download Phase ---
            track.State = PlaylistTrackState.Downloading;
            
            // Update model with the specific file info we found
            track.Model.Status = TrackStatus.Missing; // Still missing until done
            
            // Use resolved path or fallback
            var finalPath = track.Model.ResolvedFilePath;
            if (string.IsNullOrEmpty(finalPath))
            {
                finalPath = Path.Combine(_config.DownloadDirectory ?? "Downloads", 
                    $"{track.Artist} - {track.Title}.{bestMatch.GetExtension()}");
            }
             // Ensure directory exists
            var dir = Path.GetDirectoryName(finalPath);
            if (dir != null) Directory.CreateDirectory(dir);

            var progress = new Progress<double>(p => track.Progress = p * 100);
            
            var success = await _soulseek.DownloadAsync(
                bestMatch.Username!,
                bestMatch.Filename!,
                finalPath,
                bestMatch.Size,
                progress,
                trackCt
            );

            if (success)
            {
                track.State = PlaylistTrackState.Completed;
                track.Progress = 100;
                track.Model.Status = TrackStatus.Downloaded;
                track.Model.ResolvedFilePath = finalPath;
                
                // Tagging
                try 
                {
                    await _taggerService.TagFileAsync(bestMatch, finalPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Tagging error: {Msg}", ex.Message);
                }
            }
            else
            {
                track.State = PlaylistTrackState.Failed;
                track.ErrorMessage = "Download failed (Transfer)";
            }
        }
        catch (OperationCanceledException)
        {
            track.State = PlaylistTrackState.Cancelled;
        }
        catch (Exception ex)
        {
            track.State = PlaylistTrackState.Failed;
            track.ErrorMessage = ex.Message;
            _logger.LogError(ex, "ProcessTrackAsync fatal error");
        }
    }
    
    // Properties for UI Summary (Aggregated from Collection)
    // Determining these efficiently is tricky with ObservableCollection. 
    // Ideally the UI binds to the Collection directly and filters count.
    // Keeping existing properties for backward compat if needed, but they might be expensive to calc on every change.
    // For Bundle 1, I'll rely on the VM collection.

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _globalCts.Cancel();
        _concurrencySemaphore.Dispose();
    }
}
