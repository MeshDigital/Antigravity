using System;
using Microsoft.Extensions.Logging;

namespace SLSKDONET.Services.Musical;

/// <summary>
/// Phase 4.2: Cue Generation Engine.
/// Calculates DJ cue points based on detected drop time and track BPM.
/// Implements 32-bar phrase structure typical of EDM/DnB production.
/// </summary>
public class CueGenerationEngine
{
    private readonly ILogger<CueGenerationEngine> _logger;

    public CueGenerationEngine(ILogger<CueGenerationEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates DJ cue points from drop detection results.
    /// </summary>
    /// <param name="dropTime">Detected drop time in seconds</param>
    /// <param name="bpm">Track BPM</param>
    /// <returns>Set of 4 cue points (Intro, Build, Drop, PhraseStart)</returns>
    public CuePointSet GenerateCues(float dropTime, float bpm)
    {
        if (bpm <= 0)
        {
            _logger.LogWarning("Invalid BPM {Bpm} - cannot generate cues", bpm);
            return new CuePointSet();
        }

        // Calculate bar duration
        float barDuration = 60f / bpm;

        // Phase 4.2: 32-bar phrase structure
        // Standard EDM/DnB: Intro (8) → Build (8) → Drop (16)
        
        var cues = new CuePointSet
        {
            // Always start at 0
            Intro = 0f,
            
            // Drop is exactly where detected
            Drop = dropTime,
            
            // Build-up: 16 bars before drop
            Build = dropTime - (barDuration * 16),
            
            // Phrase start: 32 bars before drop
            PhraseStart = dropTime - (barDuration * 32)
        };

        // Phase 4.2: Clamp negative values to 0
        if (cues.Build < 0) cues.Build = 0;
        if (cues.PhraseStart < 0) cues.PhraseStart = 0;

        // Phase 4.2: Beat-grid alignment (round to nearest beat)
        cues.Build = AlignToBeat(cues.Build, barDuration);
        cues.PhraseStart = AlignToBeat(cues.PhraseStart, barDuration);
        cues.Drop = AlignToBeat(cues.Drop, barDuration);

        _logger.LogInformation("Generated cues for {Bpm} BPM track: " +
            "PhraseStart={PS:F1}s, Build={B:F1}s, Drop={D:F1}s",
            bpm, cues.PhraseStart, cues.Build, cues.Drop);

        return cues;
    }

    /// <summary>
    /// Aligns a timestamp to the nearest beat boundary.
    /// Ensures cues land exactly on beats for DJ software compatibility.
    /// </summary>
    private float AlignToBeat(float timestamp, float beatDuration)
    {
        if (timestamp <= 0) return 0;
        
        // Round to nearest beat
        float beats = timestamp / beatDuration;
        float alignedBeats = (float)Math.Round(beats);
        
        return alignedBeats * beatDuration;
    }
}

/// <summary>
/// Container for the 4 standard DJ cue points.
/// </summary>
public class CuePointSet
{
    /// <summary>
    /// Intro cue (always 0 - start of track).
    /// </summary>
    public float Intro { get; set; } = 0f;

    /// <summary>
    /// Build-up cue (16 bars before drop).
    /// Marks where tension starts building.
    /// </summary>
    public float Build { get; set; }

    /// <summary>
    /// Drop cue (main energy peak).
    /// Marks where the bass/kick enters.
    /// </summary>
    public float Drop { get; set; }

    /// <summary>
    /// Phrase start cue (32 bars before drop).
    /// Marks the beginning of the complete phrase.
    /// </summary>
    public float PhraseStart { get; set; }
}
