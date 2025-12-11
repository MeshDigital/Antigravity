using System;
using System.ComponentModel.DataAnnotations;

namespace SLSKDONET.Data;

/// <summary>
/// Database entity for a track in the persisted queue.
/// </summary>
public class TrackEntity
{
    [Key]
    public string GlobalId { get; set; } = string.Empty; // TrackUniqueHash

    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = "Pending";
    public string Filename { get; set; } = string.Empty;
    public string SoulseekUsername { get; set; } = string.Empty;
    public long Size { get; set; }
    
    // Metadata for re-hydration
    public DateTime AddedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
