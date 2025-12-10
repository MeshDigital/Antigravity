using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Models;

namespace SLSKDONET.Services.InputParsers;

/// <summary>
/// Scrapes public Spotify playlists and albums without requiring API keys.
/// Parses HTML/JS content to extract track metadata.
/// Primary method for accessing public playlists without user configuration.
/// </summary>
public class SpotifyScraperInputSource
{
    private readonly ILogger<SpotifyScraperInputSource> _logger;
    private readonly HttpClient _httpClient;

    public SpotifyScraperInputSource(ILogger<SpotifyScraperInputSource> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
    }

    /// <summary>
    /// Extracts playlist ID from Spotify URL.
    /// Supports: https://open.spotify.com/playlist/ID and spotify:playlist:ID formats.
    /// </summary>
    public static string? ExtractPlaylistId(string url)
    {
        // Format: https://open.spotify.com/playlist/PLAYLIST_ID
        if (url.Contains("open.spotify.com/playlist/"))
        {
            var parts = url.Split('/');
            var playlistPart = parts.FirstOrDefault(p => p.StartsWith("playlist:") || parts[Array.IndexOf(parts, "playlist") + 1] != null);
            if (parts.Contains("playlist"))
            {
                var idx = Array.IndexOf(parts, "playlist");
                if (idx + 1 < parts.Length)
                {
                    var id = parts[idx + 1].Split('?')[0];
                    return string.IsNullOrEmpty(id) ? null : id;
                }
            }
        }

        // Format: spotify:playlist:PLAYLIST_ID
        if (url.StartsWith("spotify:playlist:"))
        {
            return url.Replace("spotify:playlist:", "");
        }

        return null;
    }

    /// <summary>
    /// Extracts album ID from Spotify URL.
    /// Supports: https://open.spotify.com/album/ID and spotify:album:ID formats.
    /// </summary>
    public static string? ExtractAlbumId(string url)
    {
        // Format: https://open.spotify.com/album/ALBUM_ID
        if (url.Contains("open.spotify.com/album/"))
        {
            var parts = url.Split('/');
            if (parts.Contains("album"))
            {
                var idx = Array.IndexOf(parts, "album");
                if (idx + 1 < parts.Length)
                {
                    var id = parts[idx + 1].Split('?')[0];
                    return string.IsNullOrEmpty(id) ? null : id;
                }
            }
        }

        // Format: spotify:album:ALBUM_ID
        if (url.StartsWith("spotify:album:"))
        {
            return url.Replace("spotify:album:", "");
        }

        return null;
    }

    /// <summary>
    /// Parses a Spotify URL (playlist or album) and scrapes track metadata.
    /// </summary>
    public async Task<List<SearchQuery>> ParseAsync(string url)
    {
        try
        {
            _logger.LogDebug("Attempting to scrape Spotify content from: {Url}", url);

            // Determine content type and extract ID
            if (url.Contains("/playlist/") || url.Contains(":playlist:"))
            {
                var playlistId = ExtractPlaylistId(url);
                if (playlistId != null)
                    return await ScrapePlaylistAsync(playlistId, url);
            }
            else if (url.Contains("/album/") || url.Contains(":album:"))
            {
                var albumId = ExtractAlbumId(url);
                if (albumId != null)
                    return await ScrapeAlbumAsync(albumId, url);
            }
            else if (url.Contains("/track/") || url.Contains(":track:"))
            {
                // Single track
                return await ScrapeTrackAsync(url);
            }

            throw new InvalidOperationException("Unable to parse Spotify URL. Supported formats: playlist, album, or track URLs.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape Spotify content");
            throw;
        }
    }

    /// <summary>
    /// Scrapes a Spotify playlist to extract all track metadata.
    /// </summary>
    private async Task<List<SearchQuery>> ScrapePlaylistAsync(string playlistId, string sourceUrl)
    {
        try
        {
            var url = $"https://open.spotify.com/playlist/{playlistId}";
            _logger.LogInformation("Scraping Spotify playlist: {PlaylistId}", playlistId);

            var html = await _httpClient.GetStringAsync(url);
            return ExtractTracksFromHtml(html, sourceUrl);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch Spotify playlist page");
            throw new InvalidOperationException($"Unable to fetch Spotify playlist. The playlist may be private or deleted.");
        }
    }

    /// <summary>
    /// Scrapes a Spotify album to extract all track metadata.
    /// </summary>
    private async Task<List<SearchQuery>> ScrapeAlbumAsync(string albumId, string sourceUrl)
    {
        try
        {
            var url = $"https://open.spotify.com/album/{albumId}";
            _logger.LogInformation("Scraping Spotify album: {AlbumId}", albumId);

            var html = await _httpClient.GetStringAsync(url);
            return ExtractTracksFromHtml(html, sourceUrl);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch Spotify album page");
            throw new InvalidOperationException($"Unable to fetch Spotify album. The album may be unavailable or deleted.");
        }
    }

    /// <summary>
    /// Scrapes a single Spotify track.
    /// </summary>
    private async Task<List<SearchQuery>> ScrapeTrackAsync(string sourceUrl)
    {
        try
        {
            _logger.LogInformation("Scraping Spotify track: {Url}", sourceUrl);
            var html = await _httpClient.GetStringAsync(sourceUrl);
            return ExtractTracksFromHtml(html, sourceUrl);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch Spotify track page");
            throw new InvalidOperationException($"Unable to fetch Spotify track. The track may be unavailable.");
        }
    }

    /// <summary>
    /// Extracts track metadata from Spotify HTML page.
    /// Looks for __NEXT_DATA__ script tag containing embedded JSON with track information.
    /// </summary>
    private List<SearchQuery> ExtractTracksFromHtml(string html, string sourceUrl)
    {
        var tracks = new List<SearchQuery>();

        try
        {
            // Find the __NEXT_DATA__ script tag content
            var scriptStart = html.IndexOf("\"__NEXT_DATA__\"", StringComparison.OrdinalIgnoreCase);
            if (scriptStart == -1)
            {
                // Try alternative approach: look for id="__NEXT_DATA__" followed by script content
                scriptStart = html.IndexOf("id=\"__NEXT_DATA__\"", StringComparison.OrdinalIgnoreCase);
                if (scriptStart == -1)
                {
                    _logger.LogWarning("Could not find __NEXT_DATA__ script tag in Spotify page");
                    return new List<SearchQuery>();
                }

                // Find the script content after the id attribute
                var contentStart = html.IndexOf(">", scriptStart);
                var contentEnd = html.IndexOf("</script>", contentStart);
                if (contentStart == -1 || contentEnd == -1)
                {
                    _logger.LogWarning("Could not extract script content");
                    return new List<SearchQuery>();
                }

                var jsonContent = html.Substring(contentStart + 1, contentEnd - contentStart - 1).Trim();
                if (string.IsNullOrEmpty(jsonContent))
                {
                    _logger.LogWarning("Script tag content is empty");
                    return new List<SearchQuery>();
                }

                // Parse JSON using System.Text.Json
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;

                var playlistTitle = ExtractPlaylistTitle(root);
                tracks = ExtractTracksFromJson(root, playlistTitle ?? "Spotify Playlist", sourceUrl);
            }
            else
            {
                // Alternative: parse as JSON if the content is directly a JSON object
                var jsonStart = html.IndexOf("{", scriptStart);
                var jsonEnd = FindMatchingBrace(html, jsonStart);
                if (jsonStart == -1 || jsonEnd == -1)
                {
                    _logger.LogWarning("Could not extract JSON from script tag");
                    return new List<SearchQuery>();
                }

                var jsonContent = html.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(jsonContent);
                var root = doc.RootElement;

                var playlistTitle = ExtractPlaylistTitle(root);
                tracks = ExtractTracksFromJson(root, playlistTitle ?? "Spotify Playlist", sourceUrl);
            }

            _logger.LogInformation("Extracted {Count} tracks from Spotify content", tracks.Count);
            return tracks;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Spotify JSON data");
            throw new InvalidOperationException("Failed to parse Spotify page content. The page structure may have changed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting tracks from HTML");
            throw new InvalidOperationException("Error extracting track data from Spotify page.");
        }
    }

    /// <summary>
    /// Finds the matching closing brace for an opening brace at the given position.
    /// </summary>
    private int FindMatchingBrace(string text, int startIndex)
    {
        if (startIndex < 0 || startIndex >= text.Length || text[startIndex] != '{')
            return -1;

        int depth = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = startIndex; i < text.Length; i++)
        {
            char c = text[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Extracts the playlist/album title from JSON data.
    /// </summary>
    private string? ExtractPlaylistTitle(JsonElement root)
    {
        try
        {
            // Try multiple paths to find the title
            if (root.TryGetProperty("props", out var props) &&
                props.TryGetProperty("pageProps", out var pageProps))
            {
                if (pageProps.TryGetProperty("initialState", out var state) &&
                    state.TryGetProperty("headerData", out var header))
                {
                    if (header.TryGetProperty("title", out var titleElem))
                        return titleElem.GetString();
                }
            }

            // Alternative path
            if (root.TryGetProperty("initialState", out var initialState) &&
                initialState.TryGetProperty("headerData", out var headerData) &&
                headerData.TryGetProperty("title", out var title))
            {
                return title.GetString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not extract title from JSON");
        }

        return null;
    }

    /// <summary>
    /// Recursively extracts all tracks from the JSON object.
    /// Handles different JSON structures for playlists, albums, and tracks.
    /// </summary>
    private List<SearchQuery> ExtractTracksFromJson(JsonElement element, string sourceTitle, string sourceUrl)
    {
        var tracks = new List<SearchQuery>();

        try
        {
            // Recursively search for track objects
            ExtractTracksRecursive(element, tracks, sourceTitle);

            // Also try to find tracks in common structures
            if (element.TryGetProperty("props", out var props) &&
                props.TryGetProperty("pageProps", out var pageProps))
            {
                ExtractTracksRecursive(pageProps, tracks, sourceTitle);
            }

            // Deduplicate by artist + title
            var deduped = tracks
                .GroupBy(t => $"{t.Artist}|{t.Title}")
                .Select(g => g.First())
                .ToList();

            _logger.LogDebug("Found {Count} unique tracks (deduplicated from {Original})", 
                deduped.Count, tracks.Count);

            return deduped;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting tracks from JSON");
            return new List<SearchQuery>();
        }
    }

    /// <summary>
    /// Recursively searches JSON element for track objects.
    /// Extracts artist, title, album, and track number when found.
    /// </summary>
    private void ExtractTracksRecursive(JsonElement element, List<SearchQuery> tracks, string sourceTitle)
    {
        try
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    {
                        // Check if this is a track object
                        if (IsTrackObject(element))
                        {
                            var track = ExtractTrackFromObject(element, sourceTitle);
                            if (track != null)
                                tracks.Add(track);
                        }

                        // Recurse into object properties
                        foreach (var prop in element.EnumerateObject())
                        {
                            ExtractTracksRecursive(prop.Value, tracks, sourceTitle);
                        }
                        break;
                    }

                case JsonValueKind.Array:
                    {
                        // Recurse into array elements
                        foreach (var item in element.EnumerateArray())
                        {
                            ExtractTracksRecursive(item, tracks, sourceTitle);
                        }
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error in recursive track extraction");
        }
    }

    /// <summary>
    /// Determines if a JSON object represents a track.
    /// </summary>
    private bool IsTrackObject(JsonElement obj)
    {
        try
        {
            var hasName = obj.TryGetProperty("name", out _);
            var hasArtist = obj.TryGetProperty("artists", out _) || obj.TryGetProperty("artist", out _);
            var hasId = obj.TryGetProperty("id", out _) || obj.TryGetProperty("uri", out _);

            return hasName && hasArtist && hasId;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts track information from a JSON object.
    /// </summary>
    private SearchQuery? ExtractTrackFromObject(JsonElement obj, string sourceTitle)
    {
        try
        {
            var title = obj.TryGetProperty("name", out var titleElem) ? titleElem.GetString() : null;
            if (string.IsNullOrEmpty(title))
                return null;

            string? artist = null;
            // Artists can be either an array or a single object
            if (obj.TryGetProperty("artists", out var artistsElem))
            {
                if (artistsElem.ValueKind == JsonValueKind.Array)
                {
                    var artistArray = artistsElem.EnumerateArray().FirstOrDefault();
                    artist = artistArray.TryGetProperty("name", out var artistName) ? artistName.GetString() : null;
                }
                else
                {
                    artist = artistsElem.TryGetProperty("name", out var artistName) ? artistName.GetString() : null;
                }
            }
            else if (obj.TryGetProperty("artist", out var artistElem))
            {
                artist = artistElem.GetString();
            }

            var album = obj.TryGetProperty("album", out var albumElem) 
                ? (albumElem.ValueKind == JsonValueKind.Object 
                    ? (albumElem.TryGetProperty("name", out var albumName) ? albumName.GetString() : null)
                    : albumElem.GetString())
                : null;

            var trackNumber = obj.TryGetProperty("track_number", out var trackNumElem) 
                ? trackNumElem.GetInt32() 
                : 0;

            return new SearchQuery
            {
                Artist = artist ?? "Unknown",
                Title = title,
                Album = album ?? "Unknown",
                SourceTitle = sourceTitle,
                TrackHash = $"{artist}|{title}".ToLowerInvariant()
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error extracting track from JSON object");
            return null;
        }
    }
}
