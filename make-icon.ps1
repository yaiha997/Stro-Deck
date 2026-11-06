# Builds app.ico (multi-size, PNG encoded) for Stro-Deck.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$outIco = Join-Path $PSScriptRoot 'app.ico'
$outPng = Join-Path $PSScriptRoot 'app-256.png'
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$blobs = New-Object System.Collections.ArrayList

function Add-RoundedRect([System.Drawing.Drawing2D.GraphicsPath]$path, [System.Drawing.RectangleF]$r, [single]$radius) {
    $d = $radius * 2
    $path.AddArc($r.X, $r.Y, $d, $d, 180, 90)
    $path.AddArc($r.Right - $d, $r.Y, $d, $d, 270, 90)
    $path.AddArc($r.Right - $d, $r.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($r.X, $r.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
}

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [Math]::Max(0.5, $s * 0.035)
    $outer = New-Object System.Drawing.RectangleF $pad, $pad, ($s - 2 * $pad), ($s - 2 * $pad)
    $outerPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    Add-RoundedRect $outerPath $outer ([Math]::Max(1.5, $s * 0.20))

    $top = [System.Drawing.Color]::FromArgb(255, 26, 26, 33)
    $bottom = [System.Drawing.Color]::FromArgb(255, 8, 8, 10)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush $outer, $top, $bottom, 65
    $g.FillPath($grad, $outerPath)

    $borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 56, 56, 68)), ([Math]::Max(0.6, $s * 0.025))
    $g.DrawPath($borderPen, $outerPath)

    # Four glass tiles, one accent tile
    $innerPad = $s * 0.20
    $gap = $s * 0.055
    $cell = ($s - 2 * $innerPad - $gap) / 2
    $r = [Math]::Max(1.2, $cell * 0.24)
    $positions = @(
        @($innerPad, $innerPad, [System.Drawing.Color]::FromArgb(240, 244, 244, 250)),
        @(($innerPad + $cell + $gap), $innerPad, [System.Drawing.Color]::FromArgb(165, 236, 236, 246)),
        @($innerPad, ($innerPad + $cell + $gap), [System.Drawing.Color]::FromArgb(165, 236, 236, 246)),
        @(($innerPad + $cell + $gap), ($innerPad + $cell + $gap), [System.Drawing.Color]::FromArgb(255, 57, 255, 20))
    )

    foreach ($p in $positions) {
        $rect = New-Object System.Drawing.RectangleF $p[0], $p[1], $cell, $cell
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        Add-RoundedRect $path $rect $r
        $brush = New-Object System.Drawing.SolidBrush $p[2]
        $g.FillPath($brush, $path)
        $brush.Dispose()
        $path.Dispose()
    }

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    [void]$blobs.Add($bytes)
    if ($s -eq 256) { [System.IO.File]::WriteAllBytes($outPng, $bytes) }

    $ms.Dispose()
    $g.Dispose()
    $bmp.Dispose()
    $grad.Dispose()
    $outerPath.Dispose()
    $borderPen.Dispose()
}

$fs = [System.IO.File]::Create($outIco)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$sizes.Count)

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = $sizes[$i]
    $dimByte = if ($dim -ge 256) { 0 } else { $dim }
    $bw.Write([byte]$dimByte)
    $bw.Write([byte]$dimByte)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $size = $blobs[$i].Length
    $bw.Write([UInt32]$size)
    $bw.Write([UInt32]$offset)
    $offset += $size
}
foreach ($blob in $blobs) { $bw.Write([byte[]]$blob) }
$bw.Flush()
$bw.Close()

Write-Host "Icon written: $outIco"
