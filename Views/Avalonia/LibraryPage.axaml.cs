using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SLSKDONET.Models;
using SLSKDONET.Services;
using SLSKDONET.ViewModels;

namespace SLSKDONET.Views.Avalonia;

public partial class LibraryPage : UserControl
{
    public LibraryPage()
    {
        InitializeComponent();
        
        // Enable drag-drop on playlist ListBox
        AddHandler(DragDrop.DragOverEvent, OnPlaylistDragOver);
        AddHandler(DragDrop.DropEvent, OnPlaylistDrop);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        // Find the playlist ListBox and enable drop
        var playlistListBox = this.FindControl<ListBox>("PlaylistListBox");
        if (playlistListBox != null)
        {
            DragDrop.SetAllowDrop(playlistListBox, true);
        }
        
        // Find the track DataGrid and enable drag
        var trackDataGrid = this.FindControl<DataGrid>("TrackDataGrid");
        if (trackDataGrid != null)
        {
            trackDataGrid.AddHandler(PointerPressedEvent, OnTrackPointerPressed, RoutingStrategies.Tunnel);
            trackDataGrid.AddHandler(PointerMovedEvent, OnTrackPointerMoved, RoutingStrategies.Tunnel);
            trackDataGrid.AddHandler(PointerReleasedEvent, OnTrackPointerReleased, RoutingStrategies.Tunnel);
        }
    }

    private Point? _dragStartPoint;
    private PlaylistTrackViewModel? _draggedTrack;

    private void OnTrackPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var row = (e.Source as Control)?.FindAncestorOfType<DataGridRow>();
            if (row?.DataContext is PlaylistTrackViewModel track)
            {
                _dragStartPoint = e.GetPosition(this);
                _draggedTrack = track;
            }
        }
    }

    private async void OnTrackPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStartPoint.HasValue && _draggedTrack != null && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var currentPoint = e.GetPosition(this);
            var diff = currentPoint - _dragStartPoint.Value;
            
            // Check if moved past threshold (5 pixels)
            if (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5)
            {
                // Create drag data
                var data = new DataObject();
                data.Set(DragContext.LibraryTrackFormat, _draggedTrack.GlobalId);
                
                // Start drag operation
                await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
                
                // Clean up
                _dragStartPoint = null;
                _draggedTrack = null;
            }
        }
    }

    private void OnTrackPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragStartPoint = null;
        _draggedTrack = null;
    }

    private void OnPlaylistDragOver(object? sender, DragEventArgs e)
    {
        // Accept tracks from library or queue
        if (e.Data.Contains(DragContext.LibraryTrackFormat) || e.Data.Contains(DragContext.QueueTrackFormat))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnPlaylistDrop(object? sender, DragEventArgs e)
    {
        // Get the target playlist
        var listBoxItem = (e.Source as Control)?.FindAncestorOfType<ListBoxItem>();
        if (listBoxItem?.DataContext is not PlaylistJob targetPlaylist)
            return;

        // Get the dragged track GlobalId
        string? trackGlobalId = null;
        if (e.Data.Contains(DragContext.LibraryTrackFormat))
        {
            trackGlobalId = e.Data.Get(DragContext.LibraryTrackFormat) as string;
        }
        else if (e.Data.Contains(DragContext.QueueTrackFormat))
        {
            trackGlobalId = e.Data.Get(DragContext.QueueTrackFormat) as string;
        }

        if (string.IsNullOrEmpty(trackGlobalId))
            return;

        // Find the track in the library
        if (DataContext is not LibraryViewModel libraryViewModel)
            return;

        var sourceTrack = libraryViewModel.CurrentProjectTracks
            .FirstOrDefault(t => t.GlobalId == trackGlobalId);

        if (sourceTrack == null)
        {
            // Try to find in player queue
            var playerViewModel = libraryViewModel.GetType()
                .GetProperty("PlayerViewModel")
                ?.GetValue(libraryViewModel) as PlayerViewModel;
            
            sourceTrack = playerViewModel?.Queue
                .FirstOrDefault(t => t.GlobalId == trackGlobalId);
        }

        if (sourceTrack != null && targetPlaylist != null)
        {
            // Use existing AddToPlaylist method (includes deduplication)
            libraryViewModel.AddToPlaylist(targetPlaylist, sourceTrack);
        }
    }
}
