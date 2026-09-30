using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace AppleMusicDiscordRPC.Media;

public class AppleMusicMonitor : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private TrackInfo? _lastTrack;

    public event Action<TrackInfo?>? TrackChanged;

    public async Task StartAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.SessionsChanged += OnSessionsChanged;
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            await UpdateActiveSessionAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppleMusicMonitor] Failed to initialize SMTC: {ex.Message}");
        }

        // Periodic heartbeat poll (every 4 seconds) to ensure sync in case SMTC events are dropped
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(4));
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(_cts.Token);
                    await RefreshCurrentTrackAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AppleMusicMonitor] Heartbeat error: {ex.Message}");
                }
            }
        }, _cts.Token);
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        _ = UpdateActiveSessionAsync();
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        _ = UpdateActiveSessionAsync();
    }

    private async Task UpdateActiveSessionAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_manager == null) return;

            var sessions = _manager.GetSessions();
            var appleSession = sessions.FirstOrDefault(s => IsAppleMusic(s.SourceAppUserModelId));

            if (appleSession != _currentSession)
            {
                UnsubscribeCurrentSession();
                _currentSession = appleSession;
                if (_currentSession != null)
                {
                    _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                    _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    _currentSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
                }
            }

            await RefreshCurrentTrackInternalAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppleMusicMonitor] UpdateActiveSession error: {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    private void UnsubscribeCurrentSession()
    {
        if (_currentSession != null)
        {
            try
            {
                _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            }
            catch { }
            _currentSession = null;
        }
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        _ = RefreshCurrentTrackAsync();
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = RefreshCurrentTrackAsync();
    }

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        _ = RefreshCurrentTrackAsync();
    }

    public async Task RefreshCurrentTrackAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await RefreshCurrentTrackInternalAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task RefreshCurrentTrackInternalAsync()
    {
        if (_currentSession == null)
        {
            // Apple Music is not running or no active session
            if (_lastTrack != null)
            {
                _lastTrack = null;
                TrackChanged?.Invoke(null);
            }
            return;
        }

        try
        {
            var playbackInfo = _currentSession.GetPlaybackInfo();
            var mediaProperties = await _currentSession.TryGetMediaPropertiesAsync();
            var timeline = _currentSession.GetTimelineProperties();

            var status = playbackInfo?.PlaybackStatus switch
            {
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackState.Playing,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackState.Paused,
                _ => PlaybackState.Stopped
            };

            var track = new TrackInfo
            {
                Title = mediaProperties?.Title ?? string.Empty,
                RawArtist = mediaProperties?.Artist ?? string.Empty,
                RawAlbum = mediaProperties?.AlbumTitle ?? string.Empty,
                Position = timeline?.Position ?? TimeSpan.Zero,
                Duration = timeline?.EndTime ?? TimeSpan.Zero,
                Status = status,
                LastUpdated = timeline?.LastUpdatedTime ?? DateTimeOffset.UtcNow
            };

            if (track.IsEmpty || status == PlaybackState.Stopped)
            {
                if (_lastTrack != null)
                {
                    _lastTrack = null;
                    TrackChanged?.Invoke(null);
                }
                return;
            }

            // Check if track changed or position drifted significantly
            bool changed = _lastTrack == null ||
                           _lastTrack.Title != track.Title ||
                           _lastTrack.RawArtist != track.RawArtist ||
                           _lastTrack.Status != track.Status ||
                           Math.Abs((track.Duration - _lastTrack.Duration).TotalSeconds) > 2;

            _lastTrack = track;
            TrackChanged?.Invoke(track);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppleMusicMonitor] Refresh error: {ex.Message}");
        }
    }

    private static bool IsAppleMusic(string? sourceAppId)
    {
        if (string.IsNullOrEmpty(sourceAppId)) return false;
        return sourceAppId.Contains("AppleMusic", StringComparison.OrdinalIgnoreCase) ||
               sourceAppId.Contains("AppleInc.AppleMusicWin", StringComparison.OrdinalIgnoreCase) ||
               sourceAppId.Contains("iTunes", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        UnsubscribeCurrentSession();
        if (_manager != null)
        {
            try
            {
                _manager.SessionsChanged -= OnSessionsChanged;
                _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            }
            catch { }
            _manager = null;
        }
        _lock.Dispose();
    }
}
