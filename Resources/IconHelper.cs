using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace AppleMusicDiscordRPC.Resources;

public static class IconHelper
{
    private static Icon? _cachedIcon;
    private static Image? _cachedLogoImage;

    public static Image GetLogoImage()
    {
        if (_cachedLogoImage != null) return _cachedLogoImage;

        var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app_logo.jpg");
        if (!File.Exists(localPath))
        {
            localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_logo.jpg");
        }

        if (File.Exists(localPath))
        {
            try
            {
                var bytes = File.ReadAllBytes(localPath);
                using var ms = new MemoryStream(bytes);
                using var temp = Image.FromStream(ms);
                _cachedLogoImage = new Bitmap(temp);
                return _cachedLogoImage;
            }
            catch { }
        }

        _cachedLogoImage = CreateVectorLogo(256);
        return _cachedLogoImage;
    }

    public static Icon GetAppIcon()
    {
        if (_cachedIcon != null) return _cachedIcon;

        // Try loading generated .ico first
        var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app_icon.ico");
        if (!File.Exists(icoPath))
        {
            icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
        }

        if (File.Exists(icoPath))
        {
            try
            {
                _cachedIcon = new Icon(icoPath, 64, 64);
                return _cachedIcon;
            }
            catch { }
        }

        try
        {
            var logo = GetLogoImage();
            using var resized = new Bitmap(logo, new Size(64, 64));
            IntPtr hIcon = resized.GetHicon();
            _cachedIcon = (Icon)Icon.FromHandle(hIcon).Clone();
            return _cachedIcon;
        }
        catch
        {
            using var bmp = CreateVectorLogo(64);
            IntPtr hIcon = bmp.GetHicon();
            _cachedIcon = (Icon)Icon.FromHandle(hIcon).Clone();
            return _cachedIcon;
        }
    }

    public static void EnsureIcoGenerated(string baseDir)
    {
        try
        {
            var logo = GetLogoImage();
            var targetIco = Path.Combine(baseDir, "app_icon.ico");
            var resIco = Path.Combine(baseDir, "Resources", "app_icon.ico");

            CreateIcoFromImage(logo, targetIco);
            if (Directory.Exists(Path.Combine(baseDir, "Resources")))
            {
                CreateIcoFromImage(logo, resIco);
            }
        }
        catch { }
    }

    public static void CreateIcoFromImage(Image sourceImage, string outputPath)
    {
        int[] sizes = new[] { 256, 128, 64, 48, 32, 16 };
        var pngStreams = new List<byte[]>();

        foreach (var size in sizes)
        {
            using var resized = new Bitmap(size, size);
            using (var g = Graphics.FromImage(resized))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(sourceImage, 0, 0, size, size);
            }
            using var ms = new MemoryStream();
            resized.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            pngStreams.Add(ms.ToArray());
        }

        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // ICONDIR
        bw.Write((short)0); // Reserved
        bw.Write((short)1); // Type 1 = ICO
        bw.Write((short)sizes.Length); // Image count

        int offset = 6 + (16 * sizes.Length);
        for (int i = 0; i < sizes.Length; i++)
        {
            int size = sizes[i];
            byte[] pngData = pngStreams[i];

            bw.Write((byte)(size == 256 ? 0 : size)); // Width
            bw.Write((byte)(size == 256 ? 0 : size)); // Height
            bw.Write((byte)0); // Color count
            bw.Write((byte)0); // Reserved
            bw.Write((short)1); // Color planes
            bw.Write((short)32); // Bits per pixel
            bw.Write((int)pngData.Length); // Size of image data
            bw.Write((int)offset); // Offset of image data
            offset += pngData.Length;
        }

        foreach (var pngData in pngStreams)
        {
            bw.Write(pngData);
        }
    }

    private static Bitmap CreateVectorLogo(int size)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(2, 2, size - 4, size - 4);
        using (var brush = new LinearGradientBrush(
            rect,
            Color.FromArgb(245, 30, 85),
            Color.FromArgb(135, 30, 220),
            45.0f))
        {
            g.FillEllipse(brush, rect);
        }

        using var whiteBrush = new SolidBrush(Color.White);
        using var notePen = new Pen(Color.White, size * 0.08f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        float x1 = size * 0.40f;
        float x2 = size * 0.62f;
        float y1 = size * 0.30f;
        float y2 = size * 0.24f;
        float yBottom = size * 0.60f;

        g.DrawLine(notePen, x1, yBottom, x1, y1);
        g.DrawLine(notePen, x2, yBottom - (size * 0.08f), x2, y2);
        g.DrawLine(notePen, x1, y1, x2, y2);

        g.FillEllipse(whiteBrush, x1 - (size * 0.14f), yBottom - (size * 0.06f), size * 0.18f, size * 0.14f);
        g.FillEllipse(whiteBrush, x2 - (size * 0.14f), yBottom - (size * 0.14f), size * 0.18f, size * 0.14f);

        return bmp;
    }
}
