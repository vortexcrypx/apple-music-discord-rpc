using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppleMusicDiscordRPC.UI;

public class GlassToggle : Control
{
    private bool _checked = true;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked != value)
            {
                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? CheckedChanged;

    public GlassToggle()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        BackColor = Color.Transparent;
        Size = new Size(58, 32);
        Cursor = Cursors.Hand;
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Bounding rect with padding for outer neon glow
        var rect = new Rectangle(3, 3, Width - 7, Height - 7);
        int radius = rect.Height / 2;

        if (_checked)
        {
            // Outer neon bloom glow (matching the reference screenshot)
            var glowRect = new Rectangle(rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6);
            using (var glowPath = CreateRoundedRectangle(glowRect, glowRect.Height / 2))
            using (var glowBrush = new SolidBrush(Color.FromArgb(45, 255, 45, 85)))
            {
                e.Graphics.FillPath(glowBrush, glowPath);
            }

            // Track background - Vibrant Apple Music neon red/pink
            using (var path = CreateRoundedRectangle(rect, radius))
            using (var brush = new LinearGradientBrush(
                rect,
                Color.FromArgb(255, 45, 85),
                Color.FromArgb(245, 30, 80),
                0f))
            {
                e.Graphics.FillPath(brush, path);
            }
        }
        else
        {
            // Inactive dark frosted state
            using (var path = CreateRoundedRectangle(rect, radius))
            using (var brush = new SolidBrush(Color.FromArgb(42, 45, 60)))
            {
                e.Graphics.FillPath(brush, path);
            }
        }

        // Thumb knob (Pure white circle)
        int knobDiameter = rect.Height - 4;
        int knobX = _checked ? (rect.Right - knobDiameter - 2) : (rect.Left + 2);
        int knobY = rect.Y + 2;
        var knobRect = new Rectangle(knobX, knobY, knobDiameter, knobDiameter);

        // Knob shadow
        using (var shadowBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
        {
            e.Graphics.FillEllipse(shadowBrush, knobRect.X, knobRect.Y + 1, knobRect.Width, knobRect.Height);
        }

        // Knob
        using (var knobBrush = new SolidBrush(Color.White))
        {
            e.Graphics.FillEllipse(knobBrush, knobRect);
        }
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
