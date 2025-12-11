using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using SLSKDONET.Models;
using SLSKDONET.Services;
using SLSKDONET.Views; // Assuming RelayCommand is here or accessible via project usage

namespace SLSKDONET.ViewModels;

public class AlbumResultViewModel
{
    private readonly DownloadManager _downloadManager;

    public string AlbumTitle { get; private set; } = string.Empty;
    public string Artist { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string Directory { get; private set; } = string.Empty;
    public int TrackCount { get; private set; }
    public string QualitySummary { get; private set; } = string.Empty;
    public bool HasFreeSlot { get; private set; }
    public int UploadSpeed { get; private set; }
    public int QueueLength { get; private set; }
    public double TotalSizeMb { get; private set; }

    public ICommand DownloadAlbumCommand { get; }

    public List<Track> Tracks { get; }

    public AlbumResultViewModel(List<Track> tracks, DownloadManager downloadManager)
    {
        Tracks = tracks;
        _downloadManager = downloadManager;

        if (tracks.Any())
        {
            var first = tracks.First();
            // Use directory name as Album Title if Album metadata is missing? 
            // Better to prefer metadata if common, else directory name.
            // For now, let's try to find the most common non-empty Album tag
            var commonAlbum = tracks
                .GroupBy(t => t.Album)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault(g => !string.IsNullOrEmpty(g.Key))?.Key;

            Directory = first.Directory ?? string.Empty;
            AlbumTitle = !string.IsNullOrEmpty(commonAlbum) ? commonAlbum : System.IO.Path.GetFileName(Directory);
            Artist = tracks.GroupBy(t => t.Artist).OrderByDescending(g => g.Count()).First().Key ?? "Unknown";
            Username = first.Username ?? "Unknown";
            
            // Metrics
            HasFreeSlot = tracks.Any(t => t.HasFreeUploadSlot);
            UploadSpeed = tracks.Max(t => t.UploadSpeed); // Optimistic
            QueueLength = tracks.Min(t => t.QueueLength); // Optimistic
            
            TrackCount = tracks.Count;
            TotalSizeMb = tracks.Sum(t => t.Size ?? 0) / 1024d / 1024d;
            
            // Quality Summary (e.g. "320kbps MP3")
            var avgBitrate = (int)tracks.Average(t => t.Bitrate);
            var formats = tracks.Select(t => t.GetExtension()).Distinct();
            QualitySummary = $"{avgBitrate}kbps {string.Join("/", formats)}";
        }
        
        DownloadAlbumCommand = new RelayCommand(DownloadAlbum_Execute);
    }

    private void DownloadAlbum_Execute()
    {
        if (Tracks == null || !Tracks.Any()) return;
        
        // Convert to PlaylistTrack for the manager
        // Logic similar to EnqueueTrack but for a batch
        var playlistTracks = new List<PlaylistTrack>();
        
        foreach(var t in Tracks)
        {
            playlistTracks.Add(new PlaylistTrack
            {
                Id = System.Guid.NewGuid(),
                Artist = t.Artist,
                Title = t.Title,
                Album = AlbumTitle,
                Status = TrackStatus.Missing,
                TrackUniqueHash = t.UniqueHash,
                // We let DownloadManager handle path resolution but we can pass hints if needed
                ResolvedFilePath = null 
            });
        }
        
        _downloadManager.QueueProject(playlistTracks);
    }
}
