using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AppleMusicDiscordRPC.Media;
using AppleMusicDiscordRPC.Metadata;
using AppleMusicDiscordRPC.Resources;

namespace AppleMusicDiscordRPC.UI;

public class SettingsForm : Form
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    private readonly TrayApplicationContext _context;
    private readonly GlassToggle _rpcToggle;
    private readonly GlassToggle _startupToggle;

    public SettingsForm(TrayApplicationContext context)
    {
        _context = context;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(570, 440);
        BackColor = Color.FromArgb(22, 19, 34); // Deep dark slate violet
        ShowInTaskbar = false;
        Icon = IconHelper.GetAppIcon();
        Text = "Apple Music RPC";

        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        ApplyRoundedCorners(28);

        // Header Panel (Draggable)
        var headerPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(Width, 85),
            BackColor = Color.Transparent
        };
        headerPanel.MouseDown += OnWindowDrag;

        // Logo
        var logoBox = new PictureBox
        {
            Location = new Point(28, 20),
            Size = new Size(48, 48),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = IconHelper.GetLogoImage(),
            BackColor = Color.Transparent
        };
        logoBox.MouseDown += OnWindowDrag;
        headerPanel.Controls.Add(logoBox);

        // App Title
        var appTitle = new Label
        {
            Text = "Apple Music RPC",
            Font = new Font("Segoe UI", 15f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(88, 18),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        appTitle.MouseDown += OnWindowDrag;
        headerPanel.Controls.Add(appTitle);

        // App Subtitle
        var appSubtitle = new Label
        {
            Text = "Discord Rich Presence",
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = Color.FromArgb(160, 165, 185),
            Location = new Point(90, 47),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        appSubtitle.MouseDown += OnWindowDrag;
        headerPanel.Controls.Add(appSubtitle);

        // Top-Right Window Controls Capsule (― and ✕)
        var windowControlsCapsule = new Panel
        {
            Location = new Point(Width - 116, 24),
            Size = new Size(88, 38),
            BackColor = Color.Transparent
        };
        windowControlsCapsule.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, windowControlsCapsule.Width - 1, windowControlsCapsule.Height - 1);
            using var path = CreateRoundedRectangle(r, r.Height / 2);
            using var brush = new SolidBrush(Color.FromArgb(35, 255, 255, 255));
            e.Graphics.FillPath(brush, path);
            using var borderPen = new Pen(Color.FromArgb(45, 255, 255, 255), 1.2f);
            e.Graphics.DrawPath(borderPen, path);
        };

        var minBtn = new Label
        {
            Text = "―",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(195, 200, 220),
            Location = new Point(4, 3),
            Size = new Size(38, 32),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent
        };
        minBtn.MouseEnter += (s, e) => minBtn.ForeColor = Color.White;
        minBtn.MouseLeave += (s, e) => minBtn.ForeColor = Color.FromArgb(195, 200, 220);
        minBtn.Click += (s, e) => HideToTray();
        windowControlsCapsule.Controls.Add(minBtn);

        var closeBtn = new Label
        {
            Text = "✕",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(195, 200, 220),
            Location = new Point(46, 3),
            Size = new Size(38, 32),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent
        };
        closeBtn.MouseEnter += (s, e) => closeBtn.ForeColor = Color.White;
        closeBtn.MouseLeave += (s, e) => closeBtn.ForeColor = Color.FromArgb(195, 200, 220);
        closeBtn.Click += (s, e) => HideToTray();
        windowControlsCapsule.Controls.Add(closeBtn);

        headerPanel.Controls.Add(windowControlsCapsule);
        Controls.Add(headerPanel);

        // Center Settings Card (Spacious, perfectly proportioned)
        var settingsCard = new Panel
        {
            Location = new Point(28, 95),
            Size = new Size(Width - 56, 195),
            BackColor = Color.Transparent
        };
        settingsCard.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, settingsCard.Width - 1, settingsCard.Height - 1);
            using var path = CreateRoundedRectangle(r, 20);

            // Frosted translucent card fill
            using (var fill = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
            {
                e.Graphics.FillPath(fill, path);
            }

            // Glass stroke
            using (var border = new Pen(Color.FromArgb(32, 255, 255, 255), 1.2f))
            {
                e.Graphics.DrawPath(border, path);
            }

            // Divider line between row 1 and row 2
            using (var dividerPen = new Pen(Color.FromArgb(18, 255, 255, 255), 1f))
            {
                e.Graphics.DrawLine(dividerPen, 20, 97, settingsCard.Width - 20, 97);
            }
        };

        // --- ROW 1: Discord Rich Presence ---
        var discordIconBox = new Panel
        {
            Location = new Point(20, 22),
            Size = new Size(54, 54),
            BackColor = Color.Transparent
        };
        discordIconBox.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, discordIconBox.Width - 1, discordIconBox.Height - 1);
            using var p = CreateRoundedRectangle(r, 14);
            using var b = new SolidBrush(Color.FromArgb(30, 255, 255, 255));
            e.Graphics.FillPath(b, p);
            using var borderPen = new Pen(Color.FromArgb(38, 255, 255, 255), 1.2f);
            e.Graphics.DrawPath(borderPen, p);

            // Draw Discord Face
            DrawDiscordIcon(e.Graphics, new Rectangle(13, 15, 28, 24));
        };
        settingsCard.Controls.Add(discordIconBox);

        var rpcTitle = new Label
        {
            Text = "Discord Rich Presence",
            Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(88, 22),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        settingsCard.Controls.Add(rpcTitle);

        var rpcSubtitle = new Label
        {
            Text = "Show listening activity on your Discord profile",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(160, 165, 185),
            Location = new Point(89, 49),
            Size = new Size(330, 22),
            AutoEllipsis = true,
            BackColor = Color.Transparent
        };
        settingsCard.Controls.Add(rpcSubtitle);

        _rpcToggle = new GlassToggle
        {
            Location = new Point(settingsCard.Width - 84, 30),
            Checked = _context.IsRpcEnabled
        };
        _rpcToggle.CheckedChanged += (s, e) =>
        {
            _context.SetRpcEnabled(_rpcToggle.Checked);
        };
        settingsCard.Controls.Add(_rpcToggle);

        // --- ROW 2: Start with Windows ---
        var windowsIconBox = new Panel
        {
            Location = new Point(20, 118),
            Size = new Size(54, 54),
            BackColor = Color.Transparent
        };
        windowsIconBox.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, windowsIconBox.Width - 1, windowsIconBox.Height - 1);
            using var p = CreateRoundedRectangle(r, 14);
            using var b = new SolidBrush(Color.FromArgb(30, 255, 255, 255));
            e.Graphics.FillPath(b, p);
            using var borderPen = new Pen(Color.FromArgb(38, 255, 255, 255), 1.2f);
            e.Graphics.DrawPath(borderPen, p);

            // Draw Windows 4-Square Logo
            DrawWindowsLogo(e.Graphics, new Rectangle(16, 16, 22, 22));
        };
        settingsCard.Controls.Add(windowsIconBox);

        var startupTitle = new Label
        {
            Text = "Start with Windows",
            Font = new Font("Segoe UI", 12.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(88, 118),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        settingsCard.Controls.Add(startupTitle);

        var startupSubtitle = new Label
        {
            Text = "Run automatically in background on startup",
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(160, 165, 185),
            Location = new Point(89, 145),
            Size = new Size(330, 22),
            AutoEllipsis = true,
            BackColor = Color.Transparent
        };
        settingsCard.Controls.Add(startupSubtitle);

        _startupToggle = new GlassToggle
        {
            Location = new Point(settingsCard.Width - 84, 126),
            Checked = TrayApplicationContext.IsStartupEnabled()
        };
        _startupToggle.CheckedChanged += (s, e) =>
        {
            TrayApplicationContext.SetStartupEnabled(_startupToggle.Checked);
        };
        settingsCard.Controls.Add(_startupToggle);

        Controls.Add(settingsCard);

        // --- Bottom Button Bar (Width = 514) ---
        var buttonPanel = new Panel
        {
            Location = new Point(28, 310),
            Size = new Size(Width - 56, 50),
            BackColor = Color.Transparent
        };

        var openAppBtn = new GlassButton
        {
            Text = "Apple Music",
            Kind = GlassButtonKind.AppleMusic,
            Location = new Point(0, 0),
            Size = new Size(150, 48)
        };
        openAppBtn.Click += (s, e) => TrayApplicationContext.OpenAppleMusic();
        buttonPanel.Controls.Add(openAppBtn);

        var exitBtn = new GlassButton
        {
            Text = "Exit App",
            Kind = GlassButtonKind.ExitApp,
            Location = new Point(162, 0),
            Size = new Size(140, 48)
        };
        exitBtn.Click += (s, e) => _context.ExitApplication();
        buttonPanel.Controls.Add(exitBtn);

        var hideBtn = new GlassButton
        {
            Text = "Close to Tray",
            Kind = GlassButtonKind.CloseToTray,
            Location = new Point(314, 0),
            Size = new Size(200, 48)
        };
        hideBtn.Click += (s, e) => HideToTray();
        buttonPanel.Controls.Add(hideBtn);

        Controls.Add(buttonPanel);

        // Footer Caption
        var footer = new Label
        {
            Text = "Runs silently in your notification tray",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(125, 130, 150),
            Location = new Point(0, 395),
            Size = new Size(Width, 22),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        footer.MouseDown += OnWindowDrag;
        Controls.Add(footer);
    }

    private void ApplyRoundedCorners(int radius)
    {
        try
        {
            int preference = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }
        catch { }

        try
        {
            IntPtr hRgn = CreateRoundRectRgn(0, 0, Width + 1, Height + 1, radius, radius);
            SetWindowRgn(Handle, hRgn, true);
        }
        catch { }
    }

    private static void DrawDiscordIcon(Graphics g, Rectangle r)
    {
        using var brush = new SolidBrush(Color.White);
        int x = r.X, y = r.Y, w = r.Width, h = r.Height;
        using var path = new GraphicsPath();
        path.AddArc(x, y + 2, w, h - 4, 0, 360);
        g.FillPath(brush, path);

        using var cutBrush = new SolidBrush(Color.FromArgb(30, 28, 44));
        g.FillEllipse(cutBrush, x + 6, y + 8, 4, 5);
        g.FillEllipse(cutBrush, x + w - 10, y + 8, 4, 5);
    }

    private static void DrawWindowsLogo(Graphics g, Rectangle r)
    {
        using var brush = new SolidBrush(Color.White);
        int halfW = (r.Width - 3) / 2;
        int halfH = (r.Height - 3) / 2;

        g.FillRectangle(brush, r.X, r.Y, halfW, halfH);
        g.FillRectangle(brush, r.X + halfW + 3, r.Y, halfW, halfH);
        g.FillRectangle(brush, r.X, r.Y + halfH + 3, halfW, halfH);
        g.FillRectangle(brush, r.X + halfW + 3, r.Y + halfH + 3, halfW, halfH);
    }

    private void OnWindowDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }
    }

    public void UpdateTrackDisplay(TrackInfo? track, TrackMetadata? meta)
    {
        // Minimal settings mode
    }

    public void SyncToggles()
    {
        _rpcToggle.Checked = _context.IsRpcEnabled;
        _startupToggle.Checked = TrayApplicationContext.IsStartupEnabled();
    }

    private void HideToTray()
    {
        Hide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
        }
        else
        {
            base.OnFormClosing(e);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = CreateRoundedRectangle(rect, 26);

        // Smooth, continuous dark frosted slate-violet glass background (no demarcation lines)
        using (var brush = new LinearGradientBrush(
            rect,
            Color.FromArgb(36, 30, 52), // Rich ambient violet at the top
            Color.FromArgb(16, 14, 25), // Deep dark slate at the bottom
            90f))
        {
            e.Graphics.FillPath(brush, path);
        }

        // Frosted glass outer border stroke
        using var borderPen = new Pen(Color.FromArgb(55, 255, 255, 255), 1.5f);
        e.Graphics.DrawPath(borderPen, path);
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        var arc = new Rectangle(rect.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
