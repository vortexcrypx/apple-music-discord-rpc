using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppleMusicDiscordRPC.UI;

public enum GlassButtonKind
{
    AppleMusic,
    ExitApp,
    CloseToTray
}

public class GlassButton : Control
{
    private bool _isHovered;
    private bool _isPressed;

    public GlassButtonKind Kind { get; set; } = GlassButtonKind.AppleMusic;
    public int CornerRadius { get; set; } = 14;

    public GlassButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        BackColor = Color.Transparent;
        Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
        Cursor = Cursors.Hand;
        Size = new Size(150, 48);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovered = false;
        _isPressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        if (mevent.Button == MouseButtons.Left)
        {
            _isPressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        _isPressed = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        pevent.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        pevent.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = CreateRoundedRectangle(rect, CornerRadius);

        if (Kind == GlassButtonKind.CloseToTray)
        {
            // Primary vibrant Apple neon red/pink button
            Color c1 = _isPressed ? Color.FromArgb(220, 30, 65) : (_isHovered ? Color.FromArgb(255, 60, 95) : Color.FromArgb(255, 45, 85));
            Color c2 = _isPressed ? Color.FromArgb(200, 20, 80) : (_isHovered ? Color.FromArgb(245, 45, 110) : Color.FromArgb(240, 30, 90));

            using (var brush = new LinearGradientBrush(rect, c1, c2, 45f))
            {
                pevent.Graphics.FillPath(brush, path);
            }

            using (var borderPen = new Pen(Color.FromArgb(120, 255, 255, 255), 1.2f))
            {
                pevent.Graphics.DrawPath(borderPen, path);
            }

            // Draw white '✕' icon
            int iconX = 26;
            int iconY = Height / 2;
            using (var xPen = new Pen(Color.White, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                pevent.Graphics.DrawLine(xPen, iconX - 6, iconY - 6, iconX + 6, iconY + 6);
                pevent.Graphics.DrawLine(xPen, iconX + 6, iconY - 6, iconX - 6, iconY + 6);
            }

            // Draw single clear centered text
            var textRect = new Rectangle(40, 0, Width - 46, Height);
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(pevent.Graphics, Text, Font, textRect, Color.White, flags);
        }
        else
        {
            // Frosted dark glass button
            int alpha = _isPressed ? 55 : (_isHovered ? 45 : 25);
            using (var brush = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
            {
                pevent.Graphics.FillPath(brush, path);
            }

            int borderAlpha = _isHovered ? 100 : 50;
            using (var borderPen = new Pen(Color.FromArgb(borderAlpha, 255, 255, 255), 1.2f))
            {
                pevent.Graphics.DrawPath(borderPen, path);
            }

            int iconCenterX = 26;
            int iconCenterY = Height / 2;

            if (Kind == GlassButtonKind.AppleMusic)
            {
                // Draw neon pink musical note icon
                using var noteBrush = new SolidBrush(Color.FromArgb(255, 55, 95));
                using var notePen = new Pen(Color.FromArgb(255, 55, 95), 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };

                pevent.Graphics.DrawLine(notePen, iconCenterX - 2, iconCenterY + 4, iconCenterX - 2, iconCenterY - 7);
                pevent.Graphics.DrawLine(notePen, iconCenterX + 6, iconCenterY + 2, iconCenterX + 6, iconCenterY - 9);
                pevent.Graphics.DrawLine(notePen, iconCenterX - 2, iconCenterY - 7, iconCenterX + 6, iconCenterY - 9);

                pevent.Graphics.FillEllipse(noteBrush, iconCenterX - 7, iconCenterY + 1, 7, 6);
                pevent.Graphics.FillEllipse(noteBrush, iconCenterX + 1, iconCenterY - 1, 7, 6);
            }
            else if (Kind == GlassButtonKind.ExitApp)
            {
                // Draw settings gear icon
                using var gearPen = new Pen(Color.White, 2f);
                pevent.Graphics.DrawEllipse(gearPen, iconCenterX - 6, iconCenterY - 6, 12, 12);
                using var dotBrush = new SolidBrush(Color.White);
                pevent.Graphics.FillEllipse(dotBrush, iconCenterX - 2, iconCenterY - 2, 4, 4);

                // Gear teeth
                pevent.Graphics.DrawLine(gearPen, iconCenterX, iconCenterY - 8, iconCenterX, iconCenterY - 6);
                pevent.Graphics.DrawLine(gearPen, iconCenterX, iconCenterY + 6, iconCenterX, iconCenterY + 8);
                pevent.Graphics.DrawLine(gearPen, iconCenterX - 8, iconCenterY, iconCenterX - 6, iconCenterY);
                pevent.Graphics.DrawLine(gearPen, iconCenterX + 6, iconCenterY, iconCenterX + 8, iconCenterY);
            }

            var textRect = new Rectangle(40, 0, Width - 46, Height);
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(pevent.Graphics, Text, Font, textRect, Color.White, flags);
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
