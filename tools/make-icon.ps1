<#
.SYNOPSIS
    Regenerates src/DelayReporter/Assets/app.ico.

.DESCRIPTION
    Draws the badge used by the application: a dark rounded square with three offset bars,
    standing for the stacked delay codes on one flight. Sizes 16 through 256 are written
    as PNG-compressed icon images, which Windows Vista and later read.

    The icon is committed, so this only needs running when the artwork changes.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/make-icon.ps1
#>
param([string]$Output = 'src/DelayReporter/Assets/app.ico')

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

Add-Type -AssemblyName System.Drawing

$accent = [System.Drawing.Color]::FromArgb(0xFF, 0x1F, 0x38, 0x64)
$light = [System.Drawing.Color]::FromArgb(0xFF, 0xD9, 0xE1, 0xF2)
$sizes = @(16, 32, 48, 64, 128, 256)

function New-Badge([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.Clear([System.Drawing.Color]::Transparent)

        $radius = [int]($size * 0.36)
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $path.AddArc(0, 0, $radius, $radius, 180, 90)
        $path.AddArc($size - $radius - 1, 0, $radius, $radius, 270, 90)
        $path.AddArc($size - $radius - 1, $size - $radius - 1, $radius, $radius, 0, 90)
        $path.AddArc(0, $size - $radius - 1, $radius, $radius, 90, 90)
        $path.CloseFigure()

        $brush = New-Object System.Drawing.SolidBrush($accent)
        $g.FillPath($brush, $path)
        $brush.Dispose()
        $path.Dispose()

        $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
        $dot = New-Object System.Drawing.SolidBrush($light)
        $bars = @(@(0.20, 0.26, 0.56), @(0.20, 0.445, 0.40), @(0.20, 0.63, 0.24))
        foreach ($bar in $bars) {
            $x = $bar[0] * $size; $y = $bar[1] * $size; $w = $bar[2] * $size; $h = 0.11 * $size
            $g.FillRectangle($white, $x, $y, $w, $h)
            $d = 0.11 * $size
            $g.FillEllipse($dot, $x + $w + 0.03 * $size, $y - 0.0 * $size, $d, $d)
        }
        $white.Dispose(); $dot.Dispose()
    }
    finally { $g.Dispose() }
    return $bitmap
}

$streams = @()
foreach ($size in $sizes) {
    $bitmap = New-Badge $size
    $memory = New-Object System.IO.MemoryStream
    $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    $streams += , $memory.ToArray()
    $memory.Dispose()
}

$outputPath = Join-Path $root $Output
$file = [System.IO.File]::Create($outputPath)
$writer = New-Object System.IO.BinaryWriter($file)
try {
    $writer.Write([UInt16]0)                  # reserved
    $writer.Write([UInt16]1)                  # type: icon
    $writer.Write([UInt16]$streams.Count)

    $offset = 6 + 16 * $streams.Count
    for ($i = 0; $i -lt $streams.Count; $i++) {
        $size = $sizes[$i]
        $writer.Write([Byte]$(if ($size -ge 256) { 0 } else { $size }))
        $writer.Write([Byte]$(if ($size -ge 256) { 0 } else { $size }))
        $writer.Write([Byte]0)                # palette
        $writer.Write([Byte]0)                # reserved
        $writer.Write([UInt16]1)              # colour planes
        $writer.Write([UInt16]32)             # bits per pixel
        $writer.Write([UInt32]$streams[$i].Length)
        $writer.Write([UInt32]$offset)
        $offset += $streams[$i].Length
    }
    foreach ($bytes in $streams) { $writer.Write($bytes) }
}
finally {
    $writer.Dispose()
    $file.Dispose()
}

Write-Output "Wrote $outputPath with $($streams.Count) sizes."
