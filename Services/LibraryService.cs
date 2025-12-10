using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;
using SLSKDONET.Models;

namespace SLSKDONET.Services;

/// <summary>
/// Concrete implementation of ILibraryService.
/// Manages three persistent indexes:
/// 1. LibraryEntry (main global index of unique files)
/// 2. PlaylistJob (playlist headers)
/// 3. PlaylistTrack (relational index linking playlists to tracks)
/// Currently uses JSON files as backing store; ready for database upgrade.
/// </summary>
public class LibraryService : ILibraryService
{
    private readonly ILogger<LibraryService> _logger;
    private readonly DownloadLogService _downloadLogService;
    private readonly string _libraryIndexPath;
    private readonly string _playlistJobsPath;
    private readonly string _playlistTracksPath;

    // In-memory caches (for performance)
    private List<LibraryEntry> _libraryCache = new();
    private List<PlaylistJob> _playlistJobsCache = new();
    private List<PlaylistTrack> _playlistTracksCache = new();
    private DateTime _lastLibraryCacheTime = DateTime.MinValue;

    public LibraryService(ILogger<LibraryService> logger, DownloadLogService downloadLogService)
    {
        _logger = logger;
        _downloadLogService = downloadLogService;
        
        var configDir = Path.GetDirectoryName(ConfigManager.GetDefaultConfigPath());
        var dataDir = Path.Combine(configDir ?? AppContext.BaseDirectory, "library_data");
        Directory.CreateDirectory(dataDir);

        _libraryIndexPath = Path.Combine(dataDir, "library_entries.json");
        _playlistJobsPath = Path.Combine(dataDir, "playlist_jobs.json");
        _playlistTracksPath = Path.Combine(dataDir, "playlist_tracks.json");

        _logger.LogDebug("LibraryService initialized with data directory: {DataDir}", dataDir);
    }

    // ===== INDEX 1: LibraryEntry (Main Global Index) =====

    /// <summary>
    /// Finds a library entry by unique hash (synchronous).
    /// </summary>
    public LibraryEntry? FindLibraryEntry(string uniqueHash)
    {
        var entries = LoadDownloadedTracks();
        return entries.FirstOrDefault(e => e.UniqueHash == uniqueHash);
    }

    /// <summary>
    /// Finds a library entry by unique hash (asynchronous).
    /// </summary>
    public async Task<LibraryEntry?> FindLibraryEntryAsync(string uniqueHash)
    {
        var entries = await LoadDownloadedTracksAsync();
        return entries.FirstOrDefault(e => e.UniqueHash == uniqueHash);
    }

    /// <summary>
    /// Loads all library entries (main global index).
    /// </summary>
    public async Task<List<LibraryEntry>> LoadAllLibraryEntriesAsync()
    {
        return await LoadDownloadedTracksAsync();
    }

    /// <summary>
    /// Adds a new entry to the main library.
    /// </summary>
    public async Task AddLibraryEntryAsync(LibraryEntry entry)
    {
        try
        {
            var entries = LoadDownloadedTracks();
            var existing = entries.FirstOrDefault(e => e.UniqueHash == entry.UniqueHash);
            
            if (existing != null)
                entries.Remove(existing);
            
            entry.AddedAt = DateTime.UtcNow;
            entries.Add(entry);
            
            await Task.Run(() => SaveLibraryIndex(entries));
            _lastLibraryCacheTime = DateTime.MinValue; // Invalidate cache
            _logger.LogDebug("Added library entry: {Hash}", entry.UniqueHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add library entry");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing library entry.
    /// </summary>
    public async Task UpdateLibraryEntryAsync(LibraryEntry entry)
    {
        try
        {
            var entries = LoadDownloadedTracks();
            var index = entries.FindIndex(e => e.UniqueHash == entry.UniqueHash);
            
            if (index >= 0)
                entries[index] = entry;
            else
                entries.Add(entry);
            
            await Task.Run(() => SaveLibraryIndex(entries));
            _lastLibraryCacheTime = DateTime.MinValue;
            _logger.LogDebug("Updated library entry: {Hash}", entry.UniqueHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update library entry");
            throw;
        }
    }

    // ===== INDEX 2: PlaylistJob (Playlist Headers) =====

    /// <summary>
    /// Loads all playlist jobs.
    /// </summary>
    public async Task<List<PlaylistJob>> LoadAllPlaylistJobsAsync()
    {
        return await Task.Run(() => LoadPlaylistJobsFromDisk());
    }

    /// <summary>
    /// Finds a playlist job by ID (synchronous).
    /// </summary>
    public PlaylistJob? FindPlaylistJob(Guid playlistId)
    {
        var jobs = LoadPlaylistJobsFromDisk();
        return jobs.FirstOrDefault(j => j.Id == playlistId);
    }

    /// <summary>
    /// Finds a playlist job by ID with related tracks (asynchronous).
    /// </summary>
    public async Task<PlaylistJob?> FindPlaylistJobAsync(Guid playlistId)
    {
        var jobs = await LoadAllPlaylistJobsAsync();
        var job = jobs.FirstOrDefault(j => j.Id == playlistId);
        
        if (job != null)
        {
            job.PlaylistTracks = await LoadPlaylistTracksAsync(playlistId);
        }
        
        return job;
    }

    /// <summary>
    /// Saves a new or updated playlist job.
    /// </summary>
    public async Task SavePlaylistJobAsync(PlaylistJob job)
    {
        try
        {
            var jobs = LoadPlaylistJobsFromDisk();
            var existing = jobs.FirstOrDefault(j => j.Id == job.Id);
            
            if (existing != null)
                jobs.Remove(existing);
            
            jobs.Add(job);
            
            await Task.Run(() => SavePlaylistJobs(jobs));
            _logger.LogInformation("Saved playlist job: {Title} ({Id})", job.SourceTitle, job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save playlist job");
            throw;
        }
    }

    /// <summary>
    /// Deletes a playlist job and its related tracks.
    /// </summary>
    public async Task DeletePlaylistJobAsync(Guid playlistId)
    {
        try
        {
            var jobs = LoadPlaylistJobsFromDisk();
            jobs.RemoveAll(j => j.Id == playlistId);
            
            await Task.Run(() => SavePlaylistJobs(jobs));
            
            // Also delete related playlist tracks
            var tracks = LoadPlaylistTracksFromDisk();
            tracks.RemoveAll(t => t.PlaylistId == playlistId);
            await Task.Run(() => SavePlaylistTracks(tracks));
            
            _logger.LogInformation("Deleted playlist job: {Id}", playlistId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete playlist job");
            throw;
        }
    }

    // ===== INDEX 3: PlaylistTrack (Relational Index) =====

    /// <summary>
    /// Loads all tracks for a specific playlist.
    /// </summary>
    public async Task<List<PlaylistTrack>> LoadPlaylistTracksAsync(Guid playlistId)
    {
        return await Task.Run(() =>
        {
            var allTracks = LoadPlaylistTracksFromDisk();
            return allTracks.Where(t => t.PlaylistId == playlistId)
                           .OrderBy(t => t.TrackNumber)
                           .ToList();
        });
    }

    /// <summary>
    /// Saves a single playlist track.
    /// </summary>
    public async Task SavePlaylistTrackAsync(PlaylistTrack track)
    {
        try
        {
            var tracks = LoadPlaylistTracksFromDisk();
            var existing = tracks.FirstOrDefault(t => t.Id == track.Id);
            
            if (existing != null)
                tracks.Remove(existing);
            
            track.AddedAt = DateTime.UtcNow;
            tracks.Add(track);
            
            await Task.Run(() => SavePlaylistTracks(tracks));
            _logger.LogDebug("Saved playlist track: {PlaylistId}/{Hash}", track.PlaylistId, track.TrackUniqueHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save playlist track");
            throw;
        }
    }

    /// <summary>
    /// Updates a playlist track.
    /// </summary>
    public async Task UpdatePlaylistTrackAsync(PlaylistTrack track)
    {
        try
        {
            var tracks = LoadPlaylistTracksFromDisk();
            var index = tracks.FindIndex(t => t.Id == track.Id);
            
            if (index >= 0)
                tracks[index] = track;
            else
                tracks.Add(track);
            
            await Task.Run(() => SavePlaylistTracks(tracks));
            _logger.LogDebug("Updated playlist track status: {Hash} = {Status}", track.TrackUniqueHash, track.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update playlist track");
            throw;
        }
    }

    /// <summary>
    /// Bulk saves multiple playlist tracks.
    /// </summary>
    public async Task SavePlaylistTracksAsync(List<PlaylistTrack> tracks)
    {
        try
        {
            var allTracks = LoadPlaylistTracksFromDisk();
            
            // Remove any existing tracks from these playlists (for the affected playlist IDs)
            var playlistIds = tracks.Select(t => t.PlaylistId).Distinct();
            allTracks.RemoveAll(t => playlistIds.Contains(t.PlaylistId));
            
            // Add new tracks
            foreach (var track in tracks)
                track.AddedAt = DateTime.UtcNow;
            
            allTracks.AddRange(tracks);
            
            await Task.Run(() => SavePlaylistTracks(allTracks));
            _logger.LogInformation("Saved {Count} playlist tracks", tracks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save playlist tracks");
            throw;
        }
    }

    // ===== Legacy / Compatibility Methods =====

    /// <summary>
    /// Loads all downloaded tracks as LibraryEntry objects (synchronous).
    /// </summary>
    public List<LibraryEntry> LoadDownloadedTracks()
    {
        // Return cache if fresh (within 5 minutes)
        if (DateTime.UtcNow - _lastLibraryCacheTime < TimeSpan.FromMinutes(5) && _libraryCache.Any())
            return _libraryCache;

        _libraryCache = LoadLibraryIndexFromDisk();
        _lastLibraryCacheTime = DateTime.UtcNow;
        return _libraryCache;
    }

    /// <summary>
    /// Loads all downloaded tracks as LibraryEntry objects (asynchronous).
    /// </summary>
    public async Task<List<LibraryEntry>> LoadDownloadedTracksAsync()
    {
        return await Task.Run(() => LoadDownloadedTracks());
    }

    /// <summary>
    /// Adds a track to the library with source playlist reference (legacy).
    /// </summary>
    public async Task AddTrackAsync(Track track, string actualFilePath, Guid sourcePlaylistId)
    {
        try
        {
            var entry = new LibraryEntry
            {
                UniqueHash = track.UniqueHash,
                Artist = track.Artist ?? "Unknown",
                Title = track.Title ?? "Unknown",
                Album = track.Album ?? "Unknown",
                FilePath = actualFilePath,
                Bitrate = track.Bitrate,
                DurationSeconds = track.Length,
                Format = track.Format ?? "Unknown"
            };

            await AddLibraryEntryAsync(entry);
            _logger.LogDebug("Added track to library via legacy method: {Hash}", entry.UniqueHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add track");
            throw;
        }
    }

    // ===== Private Helper Methods =====

    private List<LibraryEntry> LoadLibraryIndexFromDisk()
    {
        if (!File.Exists(_libraryIndexPath))
            return new List<LibraryEntry>();

        try
        {
            var json = File.ReadAllText(_libraryIndexPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<LibraryEntry>>(json, options) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load library index from {Path}", _libraryIndexPath);
            return new List<LibraryEntry>();
        }
    }

    private void SaveLibraryIndex(List<LibraryEntry> entries)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(entries, options);
            File.WriteAllText(_libraryIndexPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save library index");
            throw;
        }
    }

    private List<PlaylistJob> LoadPlaylistJobsFromDisk()
    {
        if (!File.Exists(_playlistJobsPath))
            return new List<PlaylistJob>();

        try
        {
            var json = File.ReadAllText(_playlistJobsPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var jobs = JsonSerializer.Deserialize<List<PlaylistJob>>(json, options) ?? new();
            
            // Ensure OriginalTracks is ObservableCollection
            foreach (var job in jobs)
            {
                if (job.OriginalTracks is not ObservableCollection<Track>)
                {
                    var tracks = job.OriginalTracks?.ToList() ?? new();
                    job.OriginalTracks = new ObservableCollection<Track>(tracks);
                }
            }
            
            return jobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load playlist jobs from {Path}", _playlistJobsPath);
            return new List<PlaylistJob>();
        }
    }

    private void SavePlaylistJobs(List<PlaylistJob> jobs)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(jobs, options);
            File.WriteAllText(_playlistJobsPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save playlist jobs");
            throw;
        }
    }

    private List<PlaylistTrack> LoadPlaylistTracksFromDisk()
    {
        if (!File.Exists(_playlistTracksPath))
            return new List<PlaylistTrack>();

        try
        {
            var json = File.ReadAllText(_playlistTracksPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<PlaylistTrack>>(json, options) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load playlist tracks from {Path}", _playlistTracksPath);
            return new List<PlaylistTrack>();
        }
    }

    private void SavePlaylistTracks(List<PlaylistTrack> tracks)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(tracks, options);
            File.WriteAllText(_playlistTracksPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save playlist tracks");
            throw;
        }
    }
}
