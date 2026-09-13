[CmdletBinding()]
param(
    [string] $SourcePath = 'src\ERBossTrackerJP\Assets\AppIconSource.png'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$assetDirectory = Join-Path $repositoryRoot 'src\ERBossTrackerJP\Assets'
$sourceAssetPath = Join-Path $assetDirectory 'AppIconSource.png'
$pngPath = Join-Path $assetDirectory 'ERBossTrackerJP.png'
$iconPath = Join-Path $assetDirectory 'ERBossTrackerJP.ico'
$resolvedInputPath = if ([IO.Path]::IsPathRooted($SourcePath)) {
    [IO.Path]::GetFullPath($SourcePath)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot $SourcePath))
}

if (-not (Test-Path -LiteralPath $resolvedInputPath -PathType Leaf)) {
    throw "Icon source was not found: $resolvedInputPath"
}

Add-Type -AssemblyName System.Drawing

function New-ResizedPngBytes {
    param(
        [Parameter(Mandatory)]
        [System.Drawing.Image] $SourceImage,

        [Parameter(Mandatory)]
        [int] $Size
    )

    $bitmap = [Drawing.Bitmap]::new(
        $Size,
        $Size,
        [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $stream = [IO.MemoryStream]::new()

    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.DrawImage(
            $SourceImage,
            [Drawing.Rectangle]::new(0, 0, $Size, $Size),
            0,
            0,
            $SourceImage.Width,
            $SourceImage.Height,
            [Drawing.GraphicsUnit]::Pixel)
        $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        return ,$stream.ToArray()
    }
    finally {
        $stream.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$inputImage = [Drawing.Image]::FromFile($resolvedInputPath)

try {
    if ($inputImage.Width -ne $inputImage.Height -or $inputImage.Width -lt 256) {
        throw 'Icon source must be a square image of at least 256 x 256 pixels.'
    }

    $applicationPng = New-ResizedPngBytes -SourceImage $inputImage -Size 1024
    [IO.File]::WriteAllBytes($pngPath, $applicationPng)

    $iconSizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    $iconFrames = @(
        foreach ($size in $iconSizes) {
            [pscustomobject]@{
                Size = $size
                Bytes = New-ResizedPngBytes -SourceImage $inputImage -Size $size
            }
        }
    )

    $iconStream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($iconStream)

    try {
        $writer.Write([uint16] 0)
        $writer.Write([uint16] 1)
        $writer.Write([uint16] $iconFrames.Count)

        $dataOffset = 6 + (16 * $iconFrames.Count)

        foreach ($frame in $iconFrames) {
            $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
            $writer.Write([byte] $dimension)
            $writer.Write([byte] $dimension)
            $writer.Write([byte] 0)
            $writer.Write([byte] 0)
            $writer.Write([uint16] 1)
            $writer.Write([uint16] 32)
            $writer.Write([uint32] $frame.Bytes.Length)
            $writer.Write([uint32] $dataOffset)
            $dataOffset += $frame.Bytes.Length
        }

        foreach ($frame in $iconFrames) {
            $writer.Write($frame.Bytes)
        }

        $writer.Flush()
        [IO.File]::WriteAllBytes($iconPath, $iconStream.ToArray())
    }
    finally {
        $writer.Dispose()
        $iconStream.Dispose()
    }
}
finally {
    $inputImage.Dispose()
}

if (-not [IO.Path]::GetFullPath($resolvedInputPath).Equals(
        [IO.Path]::GetFullPath($sourceAssetPath),
        [StringComparison]::OrdinalIgnoreCase)) {
    Copy-Item -LiteralPath $resolvedInputPath -Destination $sourceAssetPath -Force
}

Write-Host "Generated application icon assets:"
Write-Host "  $sourceAssetPath"
Write-Host "  $pngPath"
Write-Host "  $iconPath"
