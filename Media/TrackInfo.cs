namespace AppleMusicDiscordRPC.Media;

public enum PlaybackState
{
    Stopped = 0,
    Playing = 1,
    Paused = 2
}

public class TrackInfo
{
    public string Title { get; set; } = string.Empty;
    public string RawArtist { get; set; } = string.Empty;
    public string RawAlbum { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public TimeSpan Position { get; set; } = TimeSpan.Zero;
    public PlaybackState Status { get; set; } = PlaybackState.Stopped;
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;

    public string Artist
    {
        get
        {
            ParseArtistAndAlbum(out var artist, out _);
            return artist;
        }
    }

    public string Album
    {
        get
        {
            ParseArtistAndAlbum(out _, out var album);
            return album;
        }
    }

    private void ParseArtistAndAlbum(out string artist, out string album)
    {
        if (!string.IsNullOrWhiteSpace(RawAlbum))
        {
            artist = RawArtist.Trim();
            album = RawAlbum.Trim();
            return;
        }

        // Apple Music Windows app often puts "Artist — Album" into the Artist field
        var separators = new[] { " — ", " – ", " - " };
        foreach (var sep in separators)
        {
            var idx = RawArtist.IndexOf(sep, StringComparison.Ordinal);
            if (idx > 0)
            {
                artist = RawArtist.Substring(0, idx).Trim();
                album = RawArtist.Substring(idx + sep.Length).Trim();
                return;
            }
        }

        artist = RawArtist.Trim();
        album = string.Empty;
    }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(RawArtist);

    public override bool Equals(object? obj)
    {
        if (obj is not TrackInfo other) return false;
        return Title == other.Title &&
               RawArtist == other.RawArtist &&
               RawAlbum == other.RawAlbum &&
               Status == other.Status;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Title, RawArtist, RawAlbum, Status);
    }
}
