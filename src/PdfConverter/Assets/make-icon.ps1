# Regenerates app.ico and app-icon.png from app.png (both located next to this script).
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\make-icon.ps1
#
# It auto-detects the non-white content area in app.png, crops it to a square,
# removes the outer white background via edge flood-fill, then writes:
#   app-icon.png  - transparent PNG for GUI (256 px)
#   app.ico       - multi-resolution icon (16/24/32/48/64/128/256 px)

Add-Type -AssemblyName System.Drawing

function Remove-OuterWhiteBackground {
    param(
        [System.Drawing.Bitmap]$bmp,
        [int]$threshold = 244
    )

    $w = $bmp.Width
    $h = $bmp.Height
    $queue = New-Object System.Collections.Generic.Queue[object]
    $visited = New-Object 'bool[,]' $w, $h

    $isWhite = {
        param($p)
        $p.A -gt 10 -and $p.R -ge $threshold -and $p.G -ge $threshold -and $p.B -ge $threshold
    }

    $enqueueIfWhite = {
        param($x, $y)
        if ($x -lt 0 -or $x -ge $w -or $y -lt 0 -or $y -ge $h) { return }
        if ($visited[$x, $y]) { return }
        $p = $bmp.GetPixel($x, $y)
        if (-not (& $isWhite $p)) { return }
        $visited[$x, $y] = $true
        $bmp.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
        $queue.Enqueue(@($x, $y))
    }

    for ($x = 0; $x -lt $w; $x++) {
        & $enqueueIfWhite $x 0
        & $enqueueIfWhite $x ($h - 1)
    }
    for ($y = 0; $y -lt $h; $y++) {
        & $enqueueIfWhite 0 $y
        & $enqueueIfWhite ($w - 1) $y
    }

    while ($queue.Count -gt 0) {
        $pt = $queue.Dequeue()
        $x = $pt[0]; $y = $pt[1]
        & $enqueueIfWhite ($x - 1) $y
        & $enqueueIfWhite ($x + 1) $y
        & $enqueueIfWhite $x ($y - 1)
        & $enqueueIfWhite $x ($y + 1)
    }
}

$srcPath = Join-Path $PSScriptRoot 'app.png'
$icoPath = Join-Path $PSScriptRoot 'app.ico'
$pngPath = Join-Path $PSScriptRoot 'app-icon.png'

$src = [System.Drawing.Bitmap]::FromFile($srcPath)

# --- Auto-detect the non-white content bounding box ---
$w = $src.Width
$h = $src.Height
$minX = $w; $minY = $h; $maxX = 0; $maxY = 0
$threshold = 244
for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
        $p = $src.GetPixel($x, $y)
        if ($p.A -gt 10 -and ($p.R -lt $threshold -or $p.G -lt $threshold -or $p.B -lt $threshold)) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}

$boxW = $maxX - $minX + 1
$boxH = $maxY - $minY + 1
$side = [Math]::Max($boxW, $boxH)
# Center the square crop around the detected content
$cx = ($minX + $maxX) / 2.0
$cy = ($minY + $maxY) / 2.0
$cropX = [int][Math]::Round($cx - $side / 2.0)
$cropY = [int][Math]::Round($cy - $side / 2.0)

Write-Host "Content box: x=$minX..$maxX y=$minY..$maxY -> crop ($cropX,$cropY) side=$side"

# Square source bitmap (transparent where outside the original image)
$square = New-Object System.Drawing.Bitmap($side, $side, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($square)
$g.Clear([System.Drawing.Color]::Transparent)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($src, (New-Object System.Drawing.Rectangle(0, 0, $side, $side)), (New-Object System.Drawing.Rectangle($cropX, $cropY, $side, $side)), [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$src.Dispose()

Remove-OuterWhiteBackground $square
Write-Host "Removed outer white background"

# --- Build resized frames ---
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bg = [System.Drawing.Graphics]::FromImage($bmp)
    $bg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $bg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $bg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $bg.Clear([System.Drawing.Color]::Transparent)
    $bg.DrawImage($square, 0, 0, $s, $s)
    $bg.Dispose()
    $frames += ,@($s, $bmp)
}

# Transparent PNG for GUI
$guiSize = 256
$guiBmp = New-Object System.Drawing.Bitmap($guiSize, $guiSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gg = [System.Drawing.Graphics]::FromImage($guiBmp)
$gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gg.Clear([System.Drawing.Color]::Transparent)
$gg.DrawImage($square, 0, 0, $guiSize, $guiSize)
$gg.Dispose()
$guiBmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$guiBmp.Dispose()
Write-Host "Wrote $pngPath"

# --- Write ICO file ---
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

# ICONDIR
$bw.Write([uint16]0)      # reserved
$bw.Write([uint16]1)      # type = icon
$bw.Write([uint16]$frames.Count)

# Pre-encode each frame to bytes
$imageBytes = @()
foreach ($f in $frames) {
    $s = $f[0]; $bmp = $f[1]
    $fms = New-Object System.IO.MemoryStream
    $bmp.Save($fms, [System.Drawing.Imaging.ImageFormat]::Png)
    $imageBytes += ,$fms.ToArray()
}

# ICONDIRENTRY table
$offset = 6 + (16 * $frames.Count)
for ($i = 0; $i -lt $frames.Count; $i++) {
    $s = $frames[$i][0]
    $bytes = $imageBytes[$i]
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))  # width
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))  # height
    $bw.Write([byte]0)        # color count
    $bw.Write([byte]0)        # reserved
    $bw.Write([uint16]1)      # planes
    $bw.Write([uint16]32)     # bit count
    $bw.Write([uint32]$bytes.Length)
    $bw.Write([uint32]$offset)
    $offset += $bytes.Length
}
# image data
foreach ($bytes in $imageBytes) { $bw.Write($bytes) }

$bw.Flush()
[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
Write-Host "Wrote $icoPath ($($ms.Length) bytes)"
