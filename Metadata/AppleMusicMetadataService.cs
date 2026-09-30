using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AppleMusicDiscordRPC.Media;

namespace AppleMusicDiscordRPC.Metadata;

public class TrackMetadata
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string? ArtworkUrl { get; set; }
    public string SongUrl { get; set; } = string.Empty;
}

public class AppleMusicMetadataService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private readonly ConcurrentDictionary<string, TrackMetadata> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<TrackMetadata> GetMetadataAsync(TrackInfo track)
    {
        var cleanTitle = CleanTrackTitle(track.Title);
        var cleanArtist = track.Artist;
        var cleanAlbum = track.Album;

        var cacheKey = $"{cleanTitle}|{cleanArtist}";
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var meta = new TrackMetadata
        {
            Title = !string.IsNullOrWhiteSpace(cleanTitle) ? cleanTitle : track.Title,
            Artist = !string.IsNullOrWhiteSpace(cleanArtist) ? cleanArtist : track.RawArtist,
            Album = !string.IsNullOrWhiteSpace(cleanAlbum) ? cleanAlbum : track.RawAlbum,
            SongUrl = $"https://music.apple.com/search?term={Uri.EscapeDataString($"{cleanTitle} {cleanArtist}")}"
        };

        try
        {
            var searchResult = await QueryItunesApiAsync(cleanTitle, cleanArtist, cleanAlbum);
            if (searchResult == null && !string.IsNullOrWhiteSpace(cleanArtist))
            {
                // Try searching with just title and artist if initial search failed
                searchResult = await QueryItunesApiAsync(cleanTitle, cleanArtist, string.Empty);
            }

            if (searchResult != null)
            {
                if (!string.IsNullOrWhiteSpace(searchResult.ArtworkUrl100))
                {
                    // Upgrade artwork resolution from 100x100 to 600x600 for crisp Spotify-like Discord presence
                    meta.ArtworkUrl = searchResult.ArtworkUrl100
                        .Replace("100x100bb.jpg", "600x600bb.jpg")
                        .Replace("100x100bb.png", "600x600bb.png");
                }

                if (!string.IsNullOrWhiteSpace(searchResult.TrackViewUrl))
                {
                    meta.SongUrl = searchResult.TrackViewUrl;
                }
                else if (!string.IsNullOrWhiteSpace(searchResult.CollectionViewUrl))
                {
                    meta.SongUrl = searchResult.CollectionViewUrl;
                }

                if (!string.IsNullOrWhiteSpace(searchResult.CollectionName))
                {
                    meta.Album = searchResult.CollectionName;
                }

                if (!string.IsNullOrWhiteSpace(searchResult.ArtistName))
                {
                    meta.Artist = searchResult.ArtistName;
                }

                if (!string.IsNullOrWhiteSpace(searchResult.TrackName))
                {
                    meta.Title = searchResult.TrackName;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MetadataService] iTunes query failed: {ex.Message}");
        }

        _cache[cacheKey] = meta;
        return meta;
    }

    private static async Task<ItunesTrackResult?> QueryItunesApiAsync(string title, string artist, string album)
    {
        var query = $"{title} {artist} {album}".Trim();
        if (string.IsNullOrWhiteSpace(query)) return null;

        var url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&media=music&entity=song&limit=5";
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var searchResponse = JsonSerializer.Deserialize<ItunesSearchResponse>(json);
        if (searchResponse == null || searchResponse.Results == null || searchResponse.Results.Count == 0)
        {
            return null;
        }

        // Exact or fuzzy match against results
        var lowerTitle = title.ToLowerInvariant();
        var lowerArtist = artist.ToLowerInvariant();

        foreach (var item in searchResponse.Results)
        {
            var itemTrack = item.TrackName?.ToLowerInvariant() ?? string.Empty;
            var itemArtist = item.ArtistName?.ToLowerInvariant() ?? string.Empty;

            if (itemTrack.Contains(lowerTitle) || lowerTitle.Contains(itemTrack))
            {
                if (string.IsNullOrWhiteSpace(lowerArtist) ||
                    itemArtist.Contains(lowerArtist) ||
                    lowerArtist.Contains(itemArtist))
                {
                    return item;
                }
            }
        }

        // Fallback to first result if close enough
        return searchResponse.Results[0];
    }

    private static string CleanTrackTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;
        // Clean common noise like file extensions or trailing indicators
        var clean = title.Trim();
        if (clean.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
            clean.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(0, clean.Length - 4).Trim();
        }
        return clean;
    }

    private class ItunesSearchResponse
    {
        [JsonPropertyName("resultCount")]
        public int ResultCount { get; set; }

        [JsonPropertyName("results")]
        public System.Collections.Generic.List<ItunesTrackResult> Results { get; set; } = new();
    }

    private class ItunesTrackResult
    {
        [JsonPropertyName("trackName")]
        public string? TrackName { get; set; }

        [JsonPropertyName("artistName")]
        public string? ArtistName { get; set; }

        [JsonPropertyName("collectionName")]
        public string? CollectionName { get; set; }

        [JsonPropertyName("artworkUrl100")]
        public string? ArtworkUrl100 { get; set; }

        [JsonPropertyName("trackViewUrl")]
        public string? TrackViewUrl { get; set; }

        [JsonPropertyName("collectionViewUrl")]
        public string? CollectionViewUrl { get; set; }
    }
}
