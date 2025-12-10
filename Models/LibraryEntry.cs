using System;

namespace SLSKDONET.Models;

/// <summary>
/// The main global index for unique, downloaded files.
/// This is the single source of truth for all files in the library.
/// Primary Key: UniqueHash (Artist-Title, case-insensitive)
/// </summary>
public class LibraryEntry
{
    /// <summary>
    /// Unique hash of the track (Artist-Title, case-insensitive).
    /// Acts as the primary key for the global library index.
    /// </summary>
    public string UniqueHash { get; set; } = string.Empty;

    /// <summary>
    /// Artist name.
    /// </summary>
    public string Artist { get; set; } = string.Empty;

    /// <summary>
    /// Track title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Album name.
    /// </summary>
    public string Album { get; set; } = string.Empty;

    /// <summary>
    /// Absolute file path on disk where the track is stored.
    /// Used by Rekordbox exporter and deduplication logic.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Audio metadata (bitrate, duration, etc).
    /// Populated during library sync.
    /// </summary>
    public int Bitrate { get; set; }
    public int? DurationSeconds { get; set; }
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this entry was added to the library.
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
