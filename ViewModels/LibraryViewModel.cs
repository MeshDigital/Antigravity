using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;
using SLSKDONET.Services;
using SLSKDONET.Services.LibraryActions;
using SLSKDONET.Views;

namespace SLSKDONET.ViewModels;

public class LibraryViewModel : INotifyPropertyChanged
{
    private readonly ILogger<LibraryViewModel> _logger;
    private readonly DownloadManager _downloadManager;
    private readonly ILibraryService _libraryService;
    private readonly LibraryActionProvider _actionProvider;

    // Master/Detail pattern properties
    private PlaylistJob? _selectedProject;
    private ObservableCollection<PlaylistTrackViewModel> _currentProjectTracks = new();
    private string _noProjectSelectedMessage = "Select an import job to view its tracks";

    public ICommand HardRetryCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand DeleteProjectCommand { get; }
    public ICommand ExecuteActionCommand { get; }
    public ICommand RefreshLibraryCommand { get; }

    /// <summary>
    /// Master List: REACTIVE binding to LibraryService.Playlists
    /// This automatically updates when playlists are added/removed from the database
    /// </summary>
    public ObservableCollection<PlaylistJob> AllProjects => _libraryService.Playlists;

    // Selected project
    public PlaylistJob? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (_selectedProject != value)
            {
                _selectedProject = value;
                OnPropertyChanged();
                if (value != null)
                    _ = LoadProjectTracksAsync(value);
            }
        }
    }

    // Detail List: Tracks for selected project (Project Manifest)
    public ObservableCollection<PlaylistTrackViewModel> CurrentProjectTracks
    {
        get => _currentProjectTracks;
        set { _currentProjectTracks = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Message to display when no project is selected.
    /// </summary>
    public string NoProjectSelectedMessage
    {
        get => _noProjectSelectedMessage;
        set { if (_noProjectSelectedMessage != value) { _noProjectSelectedMessage = value; OnPropertyChanged(); } }
    }

    private bool _isGridView;
    public bool IsGridView
    {
        get => _isGridView;
        set
        {
            if (_isGridView != value)
            {
                _isGridView = value;
                OnPropertyChanged();
            }
        }
    }

    public LibraryViewModel(
        ILogger<LibraryViewModel> logger,
        DownloadManager downloadManager,
        ILibraryService libraryService,
        LibraryActionProvider actionProvider)
    {
        _logger = logger;
        _downloadManager = downloadManager;
        _libraryService = libraryService;
        _actionProvider = actionProvider;

        // Commands
        HardRetryCommand = new RelayCommand<PlaylistTrackViewModel>(ExecuteHardRetry);
        PauseCommand = new RelayCommand<PlaylistTrackViewModel>(ExecutePause);
        ResumeCommand = new RelayCommand<PlaylistTrackViewModel>(ExecuteResume);
        CancelCommand = new RelayCommand<PlaylistTrackViewModel>(ExecuteCancel);
        OpenProjectCommand = new RelayCommand<PlaylistJob>(project => SelectedProject = project);
        DeleteProjectCommand = new AsyncRelayCommand<PlaylistJob>(ExecuteDeleteProjectAsync);
        ExecuteActionCommand = new AsyncRelayCommand<ILibraryAction>(ExecuteLibraryActionAsync);
        RefreshLibraryCommand = new AsyncRelayCommand(RefreshLibraryAsync);

        // Subscribe to global track updates for live project track status
        _downloadManager.TrackUpdated += OnGlobalTrackUpdated;

        // Subscribe to project added events to auto-select new imports
        _downloadManager.ProjectAdded += OnProjectAdded;
        
        // NEW: Subscribe to updates
        _downloadManager.ProjectUpdated += OnProjectUpdated;

        // Subscribe to project deletion events for real-time Library updates
        _libraryService.ProjectDeleted += OnProjectDeleted;

        _logger.LogInformation("LibraryViewModel initialized with reactive Playlists binding");
    }

    private async void OnProjectUpdated(object? sender, Guid jobId)
    {
        // Fetch the freshest data from DB
        var updatedJob = await _libraryService.FindPlaylistJobAsync(jobId);
        if (updatedJob == null) return;

        if (System.Windows.Application.Current is null) return;
        
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // Update the existing object in the list so the UI binding triggers
            var existingJob = AllProjects.FirstOrDefault(j => j.Id == jobId);
            if (existingJob != null)
            {
                existingJob.SuccessfulCount = updatedJob.SuccessfulCount;
                existingJob.FailedCount = updatedJob.FailedCount;
                existingJob.MissingCount = updatedJob.MissingCount;
                
                // Force UI refresh if needed (ProgressPercentage relies on these)
                _logger.LogDebug("Refreshed UI counts for project {Title}: {Succ}/{Total}", existingJob.SourceTitle, existingJob.SuccessfulCount, existingJob.TotalTracks);
            }
        });
    }

    private async void OnProjectDeleted(object? sender, Guid projectId)
    {
        _logger.LogInformation("OnProjectDeleted event received for job {JobId}", projectId);
        if (System.Windows.Application.Current is null) return;

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // REACTIVE: AllProjects is bound to LibraryService.Playlists, which auto-removes
            // We just need to update selection if the deleted project was selected
            if (SelectedProject?.Id == projectId)
            {
                SelectedProject = AllProjects.FirstOrDefault();
                _logger.LogInformation("Deleted project was selected, auto-selected next project");
            }
        });
    }
    private async void OnProjectAdded(object? sender, ProjectEventArgs e)
    {
        _logger.LogInformation("OnProjectAdded event for job {JobId}", e.Job.Id);
        if (System.Windows.Application.Current is null) return;
        
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // REACTIVE: AllProjects is bound to LibraryService.Playlists, which auto-updates
            // We just need to auto-select the newly added project
            
            // Find the project in the reactive collection (it should already be there)
            var addedProject = AllProjects.FirstOrDefault(p => p.Id == e.Job.Id);
            if (addedProject != null)
            {
                SelectedProject = addedProject;
                _logger.LogInformation("Auto-selected new project '{Title}' in Library view", addedProject.SourceTitle);
            }
            else
            {
                _logger.LogWarning("Project {JobId} not found in reactive collection after ProjectAdded event", e.Job.Id);
            }
        });
    }

    public void ReorderTrack(PlaylistTrackViewModel source, PlaylistTrackViewModel target)
    {
        if (source == null || target == null || source == target) return;

        // Simple implementation: Swap SortOrder
        // Better implementation: Insert
        // Renumbering everything is safest for consistency

        // Find current indices in the underlying collection? 
        // We really want to change SortOrder values.

        // Let's adopt a "dense rank" approach.
        // First, ensure everyone has a SortOrder. if 0, assign based on current index.

        var allTracks = _downloadManager.AllGlobalTracks; // This is the source
        // But we are only reordering within "Warehouse" view ideally. 
        // Mixing active/warehouse reordering is tricky.
        // Assuming we drag pending items.

        int oldIndex = source.SortOrder;
        int newIndex = target.SortOrder;

        if (oldIndex == newIndex) return;

        // Shift items
        foreach (var track in allTracks)
        {
            if (oldIndex < newIndex)
            {
                // Moving down: shift items between old and new UP (-1)
                if (track.SortOrder > oldIndex && track.SortOrder <= newIndex)
                {
                    track.SortOrder--;
                }
            }
            else
            {
                // Moving up: shift items between new and old DOWN (+1)
                if (track.SortOrder >= newIndex && track.SortOrder < oldIndex)
                {
                    track.SortOrder++;
                }
            }
        }

        source.SortOrder = newIndex;
        // Verify uniqueness? If we started with unique 0..N, we end with unique 0..N
    }

    private void ExecuteHardRetry(PlaylistTrackViewModel? vm)
    {
        if (vm == null) return;

        _logger.LogInformation("Hard Retry requested for {Artist} - {Title}", vm.Artist, vm.Title);
        _downloadManager.HardRetryTrack(vm.GlobalId);
    }

    private void ExecutePause(PlaylistTrackViewModel? vm)
    {
        if (vm == null) return;

        _logger.LogInformation("Pause requested for {Artist} - {Title}", vm.Artist, vm.Title);
        _downloadManager.PauseTrack(vm.GlobalId);
    }

    private void ExecuteResume(PlaylistTrackViewModel? vm)
    {
        if (vm == null) return;

        _logger.LogInformation("Resume requested for {Artist} - {Title}", vm.Artist, vm.Title);
        _downloadManager.ResumeTrack(vm.GlobalId);
    }

    private void ExecuteCancel(PlaylistTrackViewModel? vm)
    {
        if (vm == null) return;

        _logger.LogInformation("Cancel requested for {Artist} - {Title}", vm.Artist, vm.Title);
        _downloadManager.CancelTrack(vm.GlobalId);
    }

    private async Task ExecuteDeleteProjectAsync(PlaylistJob? job)
    {
        if (job == null) return;

        _logger.LogInformation("Soft-deleting project: {Title} ({Id})", job.SourceTitle, job.Id);

        try
        {
            // Soft-delete via database service
            await _libraryService.DeletePlaylistJobAsync(job.Id);
            // The UI update will now be handled by the OnProjectDeleted event handler.
            _logger.LogInformation("Deletion request for project {Title} processed. Event will trigger UI update.", job.SourceTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete project {Id}", job.Id);
        }
    }

    private async Task RefreshLibraryAsync()
    {
        _logger.LogInformation("Manual library refresh requested");
        await _libraryService.RefreshPlaylistsAsync();
    }

    private async Task LoadProjectTracksAsync(PlaylistJob job)
    {
        try
        {
            _logger.LogInformation("Loading tracks for project: {Name}", job.SourceTitle);
            var tracks = new ObservableCollection<PlaylistTrackViewModel>();

            // N+1 Query Fix: Use the eagerly loaded tracks from the job object itself.
            foreach (var track in job.PlaylistTracks.OrderBy(t => t.TrackNumber))
            {
                var vm = new PlaylistTrackViewModel(track);

                // Sync with live DownloadManager state for real-time progress
                var liveTrack = _downloadManager.AllGlobalTracks
                    .FirstOrDefault(t => t.GlobalId == track.TrackUniqueHash);

                if (liveTrack != null)
                {
                    vm.State = liveTrack.State;
                    vm.Progress = liveTrack.Progress;
                    vm.CurrentSpeed = liveTrack.CurrentSpeed;
                    vm.ErrorMessage = liveTrack.ErrorMessage;
                }

                tracks.Add(vm);
            }

            if (System.Windows.Application.Current is null) return;
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                CurrentProjectTracks = tracks;
            });
            _logger.LogInformation("Loaded {Count} tracks for project {Title}", tracks.Count, job.SourceTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tracks for project {Id}", job.Id);
        }
    }

    // LoadProjectsAsync method REMOVED - AllProjects now reactively bound to LibraryService.Playlists
    private void OnGlobalTrackUpdated(object? sender, PlaylistTrackViewModel? updatedTrack)
    {
        if (updatedTrack == null || CurrentProjectTracks == null) return;

        if (System.Windows.Application.Current is null) return;
        // Use Dispatcher for UI thread safety
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var localTrack = CurrentProjectTracks
                .FirstOrDefault(t => t.GlobalId == updatedTrack.GlobalId);

            if (localTrack != null)
            {
                localTrack.State = updatedTrack.State;
                localTrack.Progress = updatedTrack.Progress;
                localTrack.CurrentSpeed = updatedTrack.CurrentSpeed;
                localTrack.ErrorMessage = updatedTrack.ErrorMessage;
            }
        });
    }

    /// <summary>
    /// Get list of actions available for current selection
    /// </summary>
    public List<ILibraryAction> AvailableActions
    {
        get
        {
            var context = new LibraryContext
            {
                SelectedPlaylist = SelectedProject,
                SelectedTracks = CurrentProjectTracks.ToList(),
                ViewModel = this
            };

            return _actionProvider.GetAvailableActions(context);
        }
    }

    /// <summary>
    /// Execute a library action
    /// </summary>
    private async Task ExecuteLibraryActionAsync(ILibraryAction? action)
    {
        if (action == null) return;

        try
        {
            var context = new LibraryContext
            {
                SelectedPlaylist = SelectedProject,
                SelectedTracks = CurrentProjectTracks.ToList(),
                ViewModel = this
            };

            _logger.LogInformation("Executing library action: {ActionName}", action.Name);
            await action.ExecuteAsync(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute library action {ActionName}", action.Name);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
