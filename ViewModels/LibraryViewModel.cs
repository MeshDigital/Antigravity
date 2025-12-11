
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using SLSKDONET.Services;
using SLSKDONET.Views; // For RelayCommand if needed, or stick to CommunityToolkit if avail? Using RelayCommand from Views based on existing code.

namespace SLSKDONET.ViewModels;

public class LibraryViewModel : INotifyPropertyChanged
{
    private readonly ILogger<LibraryViewModel> _logger;
    private readonly DownloadManager _downloadManager;
    
    public CollectionViewSource ActiveTracksInit { get; } = new();
    public ICollectionView ActiveTracksView => ActiveTracksInit.View;

    public CollectionViewSource WarehouseTracksInit { get; } = new();
    public ICollectionView WarehouseTracksView => WarehouseTracksInit.View;

    public ICommand HardRetryCommand { get; }

    public LibraryViewModel(ILogger<LibraryViewModel> logger, DownloadManager downloadManager)
    {
        _logger = logger;
        _downloadManager = downloadManager;

        // Initialize Active View
        ActiveTracksInit.Source = _downloadManager.AllGlobalTracks;
        ActiveTracksInit.IsLiveFilteringRequested = true;
        ActiveTracksInit.LiveFilteringProperties.Add("State");
        ActiveTracksInit.IsLiveSortingRequested = true;
        ActiveTracksInit.LiveSortingProperties.Add("Progress");
        ActiveTracksInit.Filter += ActiveTracks_Filter;
        ActiveTracksInit.SortDescriptions.Add(new SortDescription("State", ListSortDirection.Ascending));

        // Initialize Warehouse View
        WarehouseTracksInit.Source = _downloadManager.AllGlobalTracks;
        WarehouseTracksInit.IsLiveFilteringRequested = true;
        WarehouseTracksInit.LiveFilteringProperties.Add("State");
        WarehouseTracksInit.IsLiveSortingRequested = true; // Optional for warehouse
        WarehouseTracksInit.LiveSortingProperties.Add("Artist");
        WarehouseTracksInit.Filter += WarehouseTracks_Filter;
        WarehouseTracksInit.SortDescriptions.Add(new SortDescription("AddedAt", ListSortDirection.Descending)); // Assuming AddedAt or similar exists? PlaylistTrack usually implies order. 
        // We'll sort by SourceId (Project) then TrackNumber for now if possible, or just implicit.
        
        HardRetryCommand = new RelayCommand<PlaylistTrackViewModel>(ExecuteHardRetry);
    }

    private void ActiveTracks_Filter(object sender, FilterEventArgs e)
    {
        if (e.Item is PlaylistTrackViewModel vm)
        {
            // Active: Searching, Downloading, Queued
            e.Accepted = vm.State == PlaylistTrackState.Searching ||
                         vm.State == PlaylistTrackState.Downloading ||
                         vm.State == PlaylistTrackState.Queued;
        }
    }

    private void WarehouseTracks_Filter(object sender, FilterEventArgs e)
    {
        if (e.Item is PlaylistTrackViewModel vm)
        {
            // Warehouse: Pending, Completed, Failed, Cancelled
            // Essentially !Active
            e.Accepted = vm.State == PlaylistTrackState.Pending ||
                         vm.State == PlaylistTrackState.Completed ||
                         vm.State == PlaylistTrackState.Failed ||
                         vm.State == PlaylistTrackState.Cancelled;
        }
    }

    private void ExecuteHardRetry(PlaylistTrackViewModel? vm)
    {
        if (vm == null) return;
        
        _logger.LogInformation("Hard Retry requested for {Artist} - {Title}", vm.Artist, vm.Title);
        
        // Step A: Cancel existing operation
        vm.CancellationTokenSource?.Cancel();
        vm.State = PlaylistTrackState.Cancelled; // Temporary state before reset

        // Step B: File System Cleanup
        try 
        {
            var path = vm.Model.ResolvedFilePath;
            if (!string.IsNullOrEmpty(path))
            {
                // Delete incomplete .part file if it exists (SoulseekDL specific, or just the main file if partial)
                // Assuming the adapter writes directly to the path.
                if (File.Exists(path)) 
                {
                    try { File.Delete(path); _logger.LogInformation("Deleted partial file: {Path}", path); }
                    catch (Exception ex) { _logger.LogWarning("Could not delete file {Path}: {Msg}", path, ex.Message); }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Hard Retry cleanup");
        }

        // Step C: Reset State
        vm.Reset(); // Resets to Pending, clears CT
        
        // Step D: Re-queue logic is implicit because the DownloadManager loop 
        // constantly scans for 'Pending' items in the global collection.
        // By setting it to Pending, we effectively re-queue it.
        _logger.LogInformation("Reset track state to Pending. Background loop will pick it up.");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
