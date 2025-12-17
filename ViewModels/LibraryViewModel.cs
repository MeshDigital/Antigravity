using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;
using SLSKDONET.Services;
using SLSKDONET.Views;
using Avalonia.Threading;

namespace SLSKDONET.ViewModels;

/// <summary>
/// Coordinator ViewModel for the Library page.
/// Delegates responsibilities to child ViewModels following Single Responsibility Principle.
/// </summary>
public class LibraryViewModel : INotifyPropertyChanged
{
    private readonly ILogger<LibraryViewModel> _logger;
    private readonly INavigationService _navigationService;
    private readonly ImportHistoryViewModel _importHistoryViewModel;
    private readonly ILibraryService _libraryService; // Session 1: Critical bug fixes
    private Views.MainViewModel? _mainViewModel;

    // Child ViewModels (Phase 0: ViewModel Refactoring)
    public Library.ProjectListViewModel Projects { get; }
    public Library.TrackListViewModel Tracks { get; }
    public Library.TrackOperationsViewModel Operations { get; }
    public Library.SmartPlaylistViewModel SmartPlaylists { get; }

    // Expose commonly used child properties for backward compatibility
    public PlaylistJob? SelectedProject 
    { 
        get => Projects.SelectedProject;
        set => Projects.SelectedProject = value;
    }
    
    public System.Collections.ObjectModel.ObservableCollection<PlaylistTrackViewModel> CurrentProjectTracks
    {
        get => Tracks.CurrentProjectTracks;
        set => Tracks.CurrentProjectTracks = value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // UI State Properties
    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (_isEditMode != value)
            {
                _isEditMode = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isActiveDownloadsVisible;
    public bool IsActiveDownloadsVisible
    {
        get => _isActiveDownloadsVisible;
        set
        {
            if (_isActiveDownloadsVisible != value)
            {
                _isActiveDownloadsVisible = value;
                OnPropertyChanged();
            }
        }
    }

    // Commands that delegate to child ViewModels or handle coordination
    public System.Windows.Input.ICommand ViewHistoryCommand { get; }
    public System.Windows.Input.ICommand ToggleEditModeCommand { get; }
    public System.Windows.Input.ICommand ToggleActiveDownloadsCommand { get; }
    
    // Session 1: Critical bug fixes (3 commands to unblock user)
    public System.Windows.Input.ICommand PlayTrackCommand { get; }
    public System.Windows.Input.ICommand RefreshLibraryCommand { get; }
    public System.Windows.Input.ICommand DeleteProjectCommand { get; }

    public LibraryViewModel(
        ILogger<LibraryViewModel> logger,
        Library.ProjectListViewModel projects,
        Library.TrackListViewModel tracks,
        Library.TrackOperationsViewModel operations,
        Library.SmartPlaylistViewModel smartPlaylists,
        INavigationService navigationService,
        ImportHistoryViewModel importHistoryViewModel,
        ILibraryService libraryService) // Session 1: Inject service
    {
        _logger = logger;
        _navigationService = navigationService;
        _importHistoryViewModel = importHistoryViewModel;
        _libraryService = libraryService;
        
        // Assign child ViewModels
        Projects = projects;
        Tracks = tracks;
        Operations = operations;
        SmartPlaylists = smartPlaylists;
        
        // Initialize commands
        ViewHistoryCommand = new AsyncRelayCommand(ExecuteViewHistoryAsync);
        ToggleEditModeCommand = new RelayCommand<object>(_ => IsEditMode = !IsEditMode);
        ToggleActiveDownloadsCommand = new RelayCommand<object>(_ => IsActiveDownloadsVisible = !IsActiveDownloadsVisible);
        
        // Session 1: Critical bug fixes
        PlayTrackCommand = new AsyncRelayCommand<PlaylistTrackViewModel>(ExecutePlayTrackAsync);
        RefreshLibraryCommand = new AsyncRelayCommand(ExecuteRefreshLibraryAsync);
        DeleteProjectCommand = new AsyncRelayCommand<PlaylistJob>(ExecuteDeleteProjectAsync);
        
        // Wire up events between child ViewModels
        Projects.ProjectSelected += OnProjectSelected;
        SmartPlaylists.SmartPlaylistSelected += OnSmartPlaylistSelected;
        
        _logger.LogInformation("LibraryViewModel initialized with child ViewModels");
    }

    /// <summary>
    /// Set MainViewModel after construction to avoid circular dependency.
    /// </summary>
    public void SetMainViewModel(Views.MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        Tracks.SetMainViewModel(mainViewModel);
        Operations.SetMainViewModel(mainViewModel);
        SmartPlaylists.SetMainViewModel(mainViewModel);
    }

    /// <summary>
    /// Loads all projects from the database.
    /// Delegates to ProjectListViewModel.
    /// </summary>
    public async Task LoadProjectsAsync()
    {
        await Projects.LoadProjectsAsync();
    }

    /// <summary>
    /// Handles project selection event from ProjectListViewModel.
    /// Coordinates loading tracks in TrackListViewModel.
    /// </summary>
    private async void OnProjectSelected(object? sender, PlaylistJob? project)
    {
        if (project == null) return;

        _logger.LogInformation("Project selected: {Title}", project.SourceTitle);
        
        // Deselect smart playlist without triggering its "Load" logic if possible.
        // But since we can't easily suppress events without a flag, we just check properties.
        if (SmartPlaylists.SelectedSmartPlaylist != null)
        {
            SmartPlaylists.SelectedSmartPlaylist = null;
        }
        
        // Load tracks for selected project
        await Tracks.LoadProjectTracksAsync(project);
    }

    /// <summary>
    /// Handles smart playlist selection event from SmartPlaylistViewModel.
    /// Coordinates updating track list.
    /// </summary>
    private void OnSmartPlaylistSelected(object? sender, Library.SmartPlaylist? playlist)
    {
        if (playlist == null) return;

        _logger.LogInformation("Smart playlist selected: {Name}", playlist.Name);
        
        // Deselect project
        if (Projects.SelectedProject != null)
        {
            Projects.SelectedProject = null;
        }
        
        // Refresh smart playlist tracks
        var tracks = SmartPlaylists.RefreshSmartPlaylist(playlist);
        Tracks.CurrentProjectTracks = tracks;
    }

    /// <summary>
    /// Opens the import history view.
    /// </summary>
    private async Task ExecuteViewHistoryAsync()
    {
        try
        {
            _logger.LogInformation("Opening import history");
            _navigationService.NavigateTo("ImportHistory");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open import history");
        }
    }

    /// <summary>
    /// Adds selected tracks to a playlist.
    /// Used by drag-drop operations in LibraryPage.
    /// </summary>
    public void AddToPlaylist(PlaylistJob sourcePlaylist, PlaylistTrackViewModel track)
    {
        _logger.LogInformation("AddToPlaylist called: moving track {Title} from playlist {Source}", 
            track?.Title, sourcePlaylist?.SourceTitle);
        // TODO: Implement playlist track addition logic
        // This would need to be coordinated with child ViewModels
    }
    
    // Session 1: Critical command implementations
    
    /// <summary>
    /// Plays a track from the library.
    /// </summary>
    private async Task ExecutePlayTrackAsync(PlaylistTrackViewModel? track)
    {
        if (track == null)
        {
            _logger.LogWarning("PlayTrack called with null track");
            return;
        }
        
        if (string.IsNullOrEmpty(track.Model.ResolvedFilePath))
        {
            _logger.LogWarning("Cannot play track without file path: {Title}", track.Title);
            return;
        }
        
        try
        {
            _logger.LogInformation("Playing track: {Title} from {Path}", track.Title, track.Model.ResolvedFilePath);
            
            if (_mainViewModel?.PlayerViewModel != null)
            {
                _mainViewModel.PlayerViewModel.PlayTrack(track.Model.ResolvedFilePath, track.Title ?? "Unknown", track.Artist ?? "Unknown");
            }
            else
            {
                _logger.LogError("PlayerViewModel not available");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to play track: {Title}", track.Title);
        }
    }
    
    /// <summary>
    /// Refreshes the library by reloading projects from database.
    /// </summary>
    private async Task ExecuteRefreshLibraryAsync()
    {
        try
        {
            _logger.LogInformation("Refreshing library...");
            await Projects.LoadProjectsAsync();
            
            // If a project is selected, reload its tracks
            if (SelectedProject != null)
            {
                await Tracks.LoadProjectTracksAsync(SelectedProject);
            }
            
            _logger.LogInformation("Library refreshed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh library");
        }
    }
    
    /// <summary>
    /// Deletes a project/playlist from the library.
    /// </summary>
    private async Task ExecuteDeleteProjectAsync(PlaylistJob? project)
    {
        if (project == null)
        {
            _logger.LogWarning("DeleteProject called with null project");
            return;
        }
        
        try
        {
            _logger.LogInformation("Deleting project: {Title}", project.SourceTitle);
            
            // TODO: Add confirmation dialog in Phase 6 redesign
            // For now, delete directly
            await _libraryService.DeletePlaylistJobAsync(project.Id);
            
            // Reload projects list
            await Projects.LoadProjectsAsync();
            
            // Clear selected project if it was deleted
            if (SelectedProject?.Id == project.Id)
            {
                SelectedProject = null;
                Tracks.CurrentProjectTracks.Clear();
            }
            
            _logger.LogInformation("Project deleted successfully: {Title}", project.SourceTitle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete project: {Title}", project.SourceTitle);
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
