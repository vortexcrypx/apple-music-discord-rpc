using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using AppleMusicDiscordRPC.Discord;
using AppleMusicDiscordRPC.Media;
using AppleMusicDiscordRPC.Metadata;
using AppleMusicDiscordRPC.Resources;
using AppleMusicDiscordRPC.UI;

namespace AppleMusicDiscordRPC;

public class TrayApplicationContext : ApplicationContext
{
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "AppleMusicDiscordRPC";

    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly ToolStripMenuItem _rpcMenuItem;
    private readonly ToolStripMenuItem _startupMenuItem;

    private readonly AppleMusicMonitor _monitor;
    private readonly AppleMusicMetadataService _metadataService;
    private readonly DiscordIpcClient _discord;

    private SettingsForm? _settingsForm;

    public bool IsRpcEnabled { get; private set; } = true;
    public TrackInfo? CurrentTrack { get; private set; }
    public TrackMetadata? CurrentMetadata { get; private set; }

    private bool _isActivityActive = false;
    private string _currentActivityTrackKey = string.Empty;
    private long _currentActivityStartTimestamp = 0;

    public TrayApplicationContext(bool startMinimized = false)
    {
        _metadataService = new AppleMusicMetadataService();
        _discord = new DiscordIpcClient();
        _monitor = new AppleMusicMonitor();

        // Create Context Menu
        var contextMenu = new ContextMenuStrip();

        var titleItem = new ToolStripMenuItem("Apple Music RPC")
        {
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        titleItem.Click += (s, e) => ShowSettings();
        contextMenu.Items.Add(titleItem);

        _statusMenuItem = new ToolStripMenuItem("Status: Idle")
        {
            Enabled = false
        };
        contextMenu.Items.Add(_statusMenuItem);

        var settingsMenuItem = new ToolStripMenuItem("Settings & Dashboard...", null, (s, e) => ShowSettings())
        {
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        contextMenu.Items.Add(settingsMenuItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        _rpcMenuItem = new ToolStripMenuItem("Discord Presence", null, (s, e) =>
        {
            SetRpcEnabled(!IsRpcEnabled);
        })
        {
            Checked = IsRpcEnabled,
            CheckOnClick = true
        };
        contextMenu.Items.Add(_rpcMenuItem);

        var startupEnabled = IsStartupEnabled();
        if (!startupEnabled)
        {
            SetStartupEnabled(true);
            startupEnabled = IsStartupEnabled();
        }

        _startupMenuItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = startupEnabled,
            CheckOnClick = true
        };
        _startupMenuItem.Click += OnStartupToggle;
        contextMenu.Items.Add(_startupMenuItem);

        var openAppMenuItem = new ToolStripMenuItem("Open Apple Music", null, (s, e) => OpenAppleMusic());
        contextMenu.Items.Add(openAppMenuItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var exitMenuItem = new ToolStripMenuItem("Exit", null, (s, e) => ExitApplication());
        contextMenu.Items.Add(exitMenuItem);

        // Initialize NotifyIcon
        _notifyIcon = new NotifyIcon
        {
            Icon = IconHelper.GetAppIcon(),
            ContextMenuStrip = contextMenu,
            Text = TruncateTooltip("Apple Music RPC - Idle"),
            Visible = true
        };

        _notifyIcon.DoubleClick += (s, e) => ShowSettings();
        _notifyIcon.Click += (s, e) =>
        {
            if (e is MouseEventArgs me && me.Button == MouseButtons.Left)
            {
                ShowSettings();
            }
        };

        // Hook monitor events
        _monitor.TrackChanged += OnTrackChanged;

        Program.Log("TrayApplicationContext initialized.");

        // Initialize monitoring and Discord connection in background
        _ = InitializeAsync();

        if (!startMinimized)
        {
            ShowSettings();
        }
    }

    public void ShowSettings()
    {
        if (_settingsForm == null || _settingsForm.IsDisposed)
        {
            _settingsForm = new SettingsForm(this);
        }

        _settingsForm.SyncToggles();
        _settingsForm.UpdateTrackDisplay(CurrentTrack, CurrentMetadata);
        _settingsForm.Show();
        _settingsForm.BringToFront();
        _settingsForm.Activate();
    }

    public void SetRpcEnabled(bool enabled)
    {
        IsRpcEnabled = enabled;
        _rpcMenuItem.Checked = enabled;

        Program.Log($"RPC Enabled toggled to: {enabled}");

        if (!enabled)
        {
            _ = _discord.ClearActivityAsync();
            _isActivityActive = false;
            _currentActivityTrackKey = string.Empty;
            _currentActivityStartTimestamp = 0;
            UpdateStatusUI("Disabled", null);
        }
        else
        {
            _ = HandleTrackChangedAsync(CurrentTrack);
        }

        _settingsForm?.SyncToggles();
    }

    private async Task InitializeAsync()
    {
        try
        {
            Program.Log("Starting AppleMusicMonitor...");
            await _monitor.StartAsync();
            Program.Log("AppleMusicMonitor started.");

            Program.Log("Connecting to Discord...");
            await _discord.EnsureConnectedAsync();
            Program.Log($"Discord connected: {_discord.IsConnected}");
        }
        catch (Exception ex)
        {
            Program.Log($"[TrayContext] Init error: {ex}");
        }
    }

    private void OnTrackChanged(TrackInfo? track)
    {
        _ = HandleTrackChangedAsync(track);
    }

    private async Task HandleTrackChangedAsync(TrackInfo? track)
    {
        CurrentTrack = track;

        if (!IsRpcEnabled)
        {
            UpdateStatusUI("Disabled", track);
            _settingsForm?.UpdateTrackDisplay(track, CurrentMetadata);
            return;
        }

        if (track == null || track.IsEmpty || track.Status != PlaybackState.Playing)
        {
            if (_isActivityActive)
            {
                Program.Log($"Track state: {(track == null ? "None" : track.Status.ToString())} - Clearing activity");
                await _discord.ClearActivityAsync();
                _isActivityActive = false;
                _currentActivityTrackKey = string.Empty;
                _currentActivityStartTimestamp = 0;
            }
            UpdateStatusUI("Idle / Paused", track);
            _settingsForm?.UpdateTrackDisplay(track, CurrentMetadata);
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var posSeconds = (long)Math.Max(0, track.Position.TotalSeconds);
            var durSeconds = (long)Math.Max(0, track.Duration.TotalSeconds);
            var startTimestamp = now - posSeconds;

            var trackKey = $"{track.Title}|{track.Artist}|{track.Status}";
            // If already playing this track and timestamp hasn't drifted > 3 seconds, let Discord's timeline interpolation run
            if (_isActivityActive &&
                _currentActivityTrackKey == trackKey &&
                Math.Abs(startTimestamp - _currentActivityStartTimestamp) < 3)
            {
                return;
            }

            Program.Log($"Playing track: '{track.Title}' by '{track.Artist}' (Album: '{track.Album}')");
            var meta = await _metadataService.GetMetadataAsync(track);
            CurrentMetadata = meta;
            Program.Log($"Metadata resolved: Art='{meta.ArtworkUrl}', SongUrl='{meta.SongUrl}'");

            long? endTimestamp = durSeconds > 0 ? (startTimestamp + durSeconds) : null;

            var title = !string.IsNullOrWhiteSpace(meta.Title) ? meta.Title : track.Title;
            var artist = !string.IsNullOrWhiteSpace(meta.Artist) ? meta.Artist : track.Artist;
            var album = !string.IsNullOrWhiteSpace(meta.Album) ? meta.Album : track.Album;

            // Ensure string bounds (Discord RPC min 2, max 128 chars)
            title = EnsureLength(title, 2, 128);
            var displayArtist = EnsureLength($"by {artist}", 2, 128);
            if (string.IsNullOrWhiteSpace(album)) album = title;

            var activity = new DiscordActivity
            {
                Type = 2, // 2 = Listening to
                Details = title,
                State = displayArtist,
                Timestamps = new DiscordTimestamps
                {
                    Start = startTimestamp,
                    End = endTimestamp
                },
                Assets = new DiscordAssets
                {
                    LargeImage = !string.IsNullOrWhiteSpace(meta.ArtworkUrl) ? meta.ArtworkUrl : "apple",
                    LargeText = EnsureLength(album, 2, 128),
                    SmallImage = "apple",
                    SmallText = "Apple Music"
                },
                Buttons = new System.Collections.Generic.List<DiscordButton>
                {
                    new()
                    {
                        Label = "Listen on Apple Music",
                        Url = meta.SongUrl
                    }
                }
            };

            await _discord.SetActivityAsync(activity);
            _isActivityActive = true;
            _currentActivityTrackKey = trackKey;
            _currentActivityStartTimestamp = startTimestamp;

            Program.Log($"Activity dispatched to Discord for '{title}'");
            UpdateStatusUI($"{title} - {artist}", track);
            _settingsForm?.UpdateTrackDisplay(track, meta);
        }
        catch (Exception ex)
        {
            Program.Log($"[TrayContext] HandleTrackChanged error: {ex}");
        }
    }

    private void UpdateStatusUI(string displayText, TrackInfo? track)
    {
        try
        {
            if (_notifyIcon.ContextMenuStrip?.InvokeRequired == true)
            {
                _notifyIcon.ContextMenuStrip.BeginInvoke(new Action(() => UpdateStatusUI(displayText, track)));
                return;
            }

            _statusMenuItem.Text = $"Status: {displayText}";
            _notifyIcon.Text = TruncateTooltip($"Apple Music - {displayText}");
        }
        catch { }
    }

    private static string TruncateTooltip(string text)
    {
        if (text.Length <= 63) return text;
        return text.Substring(0, 60) + "...";
    }

    private static string EnsureLength(string text, int min, int max)
    {
        if (string.IsNullOrEmpty(text)) text = "Unknown";
        if (text.Length < min) text = text.PadRight(min);
        if (text.Length > max) text = text.Substring(0, max - 3) + "...";
        return text;
    }

    private void OnStartupToggle(object? sender, EventArgs e)
    {
        SetStartupEnabled(_startupMenuItem.Checked);
        _settingsForm?.SyncToggles();
    }

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
            return key?.GetValue(AppRegistryName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetStartupEnabled(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(exePath))
                {
                    key.SetValue(AppRegistryName, $"\"{exePath}\" --silent");
                }
            }
            else
            {
                key.DeleteValue(AppRegistryName, false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TrayContext] Failed to update startup registry: {ex.Message}");
        }
    }

    public static void OpenAppleMusic()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "apple-music:",
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "itms-stream:",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    public void ExitApplication()
    {
        _ = ShutdownAndExitAsync();
    }

    private async Task ShutdownAndExitAsync()
    {
        try
        {
            await _discord.ClearActivityAsync();
        }
        catch { }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _settingsForm?.Dispose();
        _monitor.Dispose();
        _discord.Dispose();

        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon?.Dispose();
            _settingsForm?.Dispose();
            _monitor?.Dispose();
            _discord?.Dispose();
        }
        base.Dispose(disposing);
    }
}
