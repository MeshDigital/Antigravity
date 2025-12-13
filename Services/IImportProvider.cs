using System.Collections.Generic;
using System.Threading.Tasks;
using SLSKDONET.Models;

namespace SLSKDONET.Services;

/// <summary>
/// Interface for import source plugins (Spotify, CSV, YouTube, etc.).
/// Implementations provide a consistent way to import tracks from different sources.
/// </summary>
public interface IImportProvider
{
    /// <summary>
    /// Display name of this import source (e.g., "Spotify", "CSV File", "YouTube").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Icon or emoji to display in UI (e.g., "🎵", "📄", "▶️").
    /// </summary>
    string IconGlyph { get; }

    /// <summary>
    /// Optional: Pattern to detect if this provider can handle the input.
    /// For example, Spotify URLs start with "https://open.spotify.com".
    /// Return true if this provider should handle the given input.
    /// </summary>
    bool CanHandle(string input);

    /// <summary>
    /// Import tracks from the given input (URL, file path, etc.).
    /// </summary>
    /// <param name="input">Source-specific input (playlist URL, file path, etc.)</param>
    /// <returns>Result containing tracks or error information</returns>
    Task<ImportResult> ImportAsync(string input);
}
