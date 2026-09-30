Add-Type -AssemblyName System.Drawing
$srcPath = "Resources\app_logo.jpg"
if (!(Test-Path $srcPath)) {
    Write-Error "Logo not found"
    exit 1
}

$img = [System.Drawing.Image]::FromFile($srcPath)
$sizes = @(256, 128, 64, 48, 32, 16)
$pngStreams = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($img, 0, 0, $s, $s)
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngStreams += ,$ms.ToArray()
    $g.Dispose()
    $bmp.Dispose()
}
$img.Dispose()

$targets = @('app_icon.ico', 'Resources\app_icon.ico')
if (Test-Path 'publish') {
    $targets += 'publish\app_icon.ico'
    if (Test-Path 'publish\Resources') {
        $targets += 'publish\Resources\app_icon.ico'
    }
}

foreach ($outPath in $targets) {
    $fs = [System.IO.File]::Create($outPath)
    $bw = New-Object System.IO.BinaryWriter($fs)
    $bw.Write([int16]0)
    $bw.Write([int16]1)
    $bw.Write([int16]$sizes.Count)

    $offset = 6 + (16 * $sizes.Count)
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]
        $data = $pngStreams[$i]
        $w = if ($s -eq 256) { 0 } else { $s }
        $bw.Write([byte]$w)
        $bw.Write([byte]$w)
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([int16]1)
        $bw.Write([int16]32)
        $bw.Write([int]$data.Length)
        $bw.Write([int]$offset)
        $offset += $data.Length
    }

    foreach ($data in $pngStreams) {
        $bw.Write($data)
    }
    $bw.Close()
    $fs.Close()
}

Write-Output "Generated all ICO files successfully."
