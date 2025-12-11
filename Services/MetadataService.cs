using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SLSKDONET.Configuration;
using SpotifyAPI.Web;

namespace SLSKDONET.Services;

public interface IMetadataService
{
    Task<string?> GetAlbumArtUrlAsync(string artist, string album);
}

public class MetadataService : IMetadataService
{
    private readonly ILogger<MetadataService> _logger;
    private readonly AppConfig _config;
    // Simple memory cache: key="artist|album", value=url
    private readonly ConcurrentDictionary<string, string?> _cache = new();
    
    // Cached Spotify client
    private SpotifyClient? _spotifyClient;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public MetadataService(ILogger<MetadataService> logger, AppConfig config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task<string?> GetAlbumArtUrlAsync(string artist, string album)
    {
        if (string.IsNullOrWhiteSpace(artist) || string.IsNullOrWhiteSpace(album))
            return null;

        var key = $"{artist.ToLowerInvariant()}|{album.ToLowerInvariant()}";
        
        if (_cache.TryGetValue(key, out var cachedUrl))
            return cachedUrl;

        // Ensure Spotify is configured
        if (string.IsNullOrWhiteSpace(_config.SpotifyClientId) || 
            string.IsNullOrWhiteSpace(_config.SpotifyClientSecret))
        {
            return null; 
        }

        try
        {
            var client = await GetClientAsync();
             // Search for the album
            var request = new SearchRequest(SearchRequest.Types.Album, $"{artist} {album}");
            request.Limit = 1;
            
            var response = await client.Search.Item(request);
            if (response.Albums?.Items?.FirstOrDefault() is SimpleAlbum result)
            {
                 // Prefer Medium image (usually 300x300 or 640x640)
                 // Images are sorted by size descending usually. [0]=640, [1]=300, [2]=64
                 var image = result.Images?.FirstOrDefault();
                 if (image != null)
                 {
                     _cache[key] = image.Url;
                     return image.Url;
                 }
            }
            
            // If not found, cache null to avoid repeated lookups
            _cache[key] = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch metadata for {Artist} - {Album}", artist, album);
        }

        return null;
    }

    private async Task<SpotifyClient> GetClientAsync()
    {
        if (_spotifyClient != null && DateTime.UtcNow < _tokenExpiry)
            return _spotifyClient;

        var config = SpotifyClientConfig.CreateDefault();
        var request = new ClientCredentialsRequest(_config.SpotifyClientId!, _config.SpotifyClientSecret!);
        var response = await new OAuthClient(config).RequestToken(request);
        
        // Refresh 1 minute before actual expiry
        _tokenExpiry = DateTime.UtcNow.AddSeconds(response.ExpiresIn - 60);
        _spotifyClient = new SpotifyClient(config.WithToken(response.AccessToken));
        
        return _spotifyClient;
    }
}
