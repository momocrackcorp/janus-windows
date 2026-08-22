$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$themeRoot = Join-Path $repo "assets\windows-themes\crux"
$cursorDir = Join-Path $themeRoot "Cursors"
$soundDir = Join-Path $themeRoot "Sounds"
$iconDir = Join-Path $themeRoot "Icons"
$previewDir = Join-Path $themeRoot "Preview"
$wallpaperDir = Join-Path $themeRoot "DesktopBackground"
$dist = Join-Path $repo "dist"

foreach ($directory in @($cursorDir, $soundDir, $iconDir, $previewDir, $dist)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$navy = [System.Drawing.Color]::FromArgb(31, 67, 108)
$blue = [System.Drawing.Color]::FromArgb(49, 95, 155)
$gold = [System.Drawing.Color]::FromArgb(232, 120, 23)
$cream = [System.Drawing.Color]::FromArgb(224, 226, 220)
$outline = [System.Drawing.Color]::FromArgb(12, 25, 42)

function New-Canvas {
    $bitmap = New-Object System.Drawing.Bitmap 64, 64, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    return @($bitmap, $graphics)
}

function Add-ArrowShape([System.Drawing.Graphics]$graphics, [int]$offsetX = 0, [int]$offsetY = 0, [double]$scale = 1.0) {
    $raw = @(@(7, 4), @(7, 47), @(18, 37), @(28, 58), @(38, 53), @(28, 33), @(45, 33))
    $points = foreach ($point in $raw) {
        New-Object System.Drawing.PointF (($point[0] * $scale) + $offsetX), (($point[1] * $scale) + $offsetY)
    }
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddPolygon([System.Drawing.PointF[]]$points)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.Point]::new(5, 4)), ([System.Drawing.Point]::new(40, 56)), $blue, $navy
    $pen = New-Object System.Drawing.Pen $outline, 3
    $graphics.FillPath($brush, $path)
    $graphics.DrawPath($pen, $path)
    $goldPen = New-Object System.Drawing.Pen $gold, 1.8
    $graphics.DrawLine($goldPen, [float](9 * $scale + $offsetX), [float](8 * $scale + $offsetY), [float](9 * $scale + $offsetX), [float](41 * $scale + $offsetY))
    $goldPen.Dispose(); $pen.Dispose(); $brush.Dispose(); $path.Dispose()
}

function Add-Spinner([System.Drawing.Graphics]$graphics, [int]$centerX, [int]$centerY, [int]$frame) {
    for ($index = 0; $index -lt 8; $index++) {
        $angle = (($index * 45) - 90) * [Math]::PI / 180
        $x = $centerX + [Math]::Cos($angle) * 15
        $y = $centerY + [Math]::Sin($angle) * 15
        $distance = ($index - $frame + 8) % 8
        $alpha = [Math]::Max(55, 255 - ($distance * 28))
        $color = if (($index % 2) -eq 0) { [System.Drawing.Color]::FromArgb($alpha, $blue) } else { [System.Drawing.Color]::FromArgb($alpha, $gold) }
        $brush = New-Object System.Drawing.SolidBrush $color
        $size = if ($index -eq $frame) { 8 } else { 6 }
        $graphics.FillEllipse($brush, [float]($x - $size / 2), [float]($y - $size / 2), $size, $size)
        $brush.Dispose()
    }
}

function Convert-BitmapToCursorBytes([System.Drawing.Bitmap]$bitmap, [int]$hotX, [int]$hotY) {
    $pngStream = New-Object System.IO.MemoryStream
    $bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $png = $pngStream.ToArray()
    $pngStream.Dispose()
    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $stream
    $writer.Write([UInt16]0); $writer.Write([UInt16]2); $writer.Write([UInt16]1)
    $writer.Write([byte]64); $writer.Write([byte]64); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([UInt16]$hotX); $writer.Write([UInt16]$hotY)
    $writer.Write([UInt32]$png.Length); $writer.Write([UInt32]22); $writer.Write($png)
    $writer.Flush(); $result = $stream.ToArray(); $writer.Dispose(); $stream.Dispose()
    return ,$result
}

function New-Cursor([string]$name, [string]$kind, [int]$hotX, [int]$hotY) {
    $canvas = New-Canvas; $bitmap = $canvas[0]; $graphics = $canvas[1]
    $darkPen = New-Object System.Drawing.Pen $outline, 4
    $bluePen = New-Object System.Drawing.Pen $blue, 4
    $goldPen = New-Object System.Drawing.Pen $gold, 3
    $blueBrush = New-Object System.Drawing.SolidBrush $blue
    $goldBrush = New-Object System.Drawing.SolidBrush $gold
    $creamBrush = New-Object System.Drawing.SolidBrush $cream
    switch ($kind) {
        "arrow" { Add-ArrowShape $graphics }
        "help" { Add-ArrowShape $graphics; $graphics.FillEllipse($goldBrush, 37, 34, 23, 23); $graphics.DrawEllipse($darkPen, 37, 34, 23, 23); $font = New-Object System.Drawing.Font "Segoe UI", 12, ([System.Drawing.FontStyle]::Bold); $graphics.DrawString("?", $font, $creamBrush, 42, 35); $font.Dispose() }
        "hand" { $graphics.FillEllipse($blueBrush, 11, 8, 38, 38); $graphics.DrawEllipse($darkPen, 11, 8, 38, 38); $graphics.DrawLine($goldPen, 30, 17, 30, 48); $graphics.DrawLine($goldPen, 30, 29, 45, 29); $graphics.DrawLine($goldPen, 30, 48, 20, 36) }
        "ibeam" { $graphics.DrawLine($darkPen, 20, 8, 44, 8); $graphics.DrawLine($darkPen, 32, 8, 32, 56); $graphics.DrawLine($darkPen, 20, 56, 44, 56); $graphics.DrawLine($goldPen, 32, 11, 32, 53) }
        "cross" { $graphics.DrawEllipse($darkPen, 10, 10, 44, 44); $graphics.DrawEllipse($goldPen, 14, 14, 36, 36); $graphics.DrawLine($bluePen, 32, 3, 32, 61); $graphics.DrawLine($bluePen, 3, 32, 61, 32) }
        "move" { $graphics.DrawLine($darkPen, 32, 5, 32, 59); $graphics.DrawLine($darkPen, 5, 32, 59, 32); $graphics.FillPolygon($blueBrush, [System.Drawing.Point[]]@([System.Drawing.Point]::new(32, 2), [System.Drawing.Point]::new(24, 13), [System.Drawing.Point]::new(40, 13))); $graphics.FillPolygon($blueBrush, [System.Drawing.Point[]]@([System.Drawing.Point]::new(32, 62), [System.Drawing.Point]::new(24, 51), [System.Drawing.Point]::new(40, 51))); $graphics.FillPolygon($goldBrush, [System.Drawing.Point[]]@([System.Drawing.Point]::new(2, 32), [System.Drawing.Point]::new(13, 24), [System.Drawing.Point]::new(13, 40))); $graphics.FillPolygon($goldBrush, [System.Drawing.Point[]]@([System.Drawing.Point]::new(62, 32), [System.Drawing.Point]::new(51, 24), [System.Drawing.Point]::new(51, 40))) }
        "ns" { $graphics.DrawLine($darkPen, 32, 8, 32, 56); $graphics.FillPolygon($blueBrush, [System.Drawing.Point[]]@([System.Drawing.Point]::new(32, 2), [System.Drawing.Point]::new(22, 16), [System.Drawing.Point]::new(42, 16))); $graphics.FillPolygon($goldBrush, [System.Drawing.Point[]]@([System.Drawing.Point]::new(32, 62), [System.Drawing.Point]::new(22, 48), [System.Drawing.Point]::new(42, 48))) }
        "we" { $graphics.DrawLine($darkPen, 8, 32, 56, 32); $graphics.FillPolygon($blueBrush, [System.Drawing.Point[]]@([System.Drawing.Point]::new(2, 32), [System.Drawing.Point]::new(16, 22), [System.Drawing.Point]::new(16, 42))); $graphics.FillPolygon($goldBrush, [System.Drawing.Point[]]@([System.Drawing.Point]::new(62, 32), [System.Drawing.Point]::new(48, 22), [System.Drawing.Point]::new(48, 42))) }
        "nwse" { $graphics.DrawLine($darkPen, 11, 11, 53, 53); $graphics.DrawLine($goldPen, 14, 14, 50, 50); $graphics.FillPolygon($blueBrush, [System.Drawing.Point[]]@([System.Drawing.Point]::new(5, 5), [System.Drawing.Point]::new(21, 8), [System.Drawing.Point]::new(8, 21))); $graphics.FillPolygon($goldBrush, [System.Drawing.Point[]]@([System.Drawing.Point]::new(59, 59), [System.Drawing.Point]::new(43, 56), [System.Drawing.Point]::new(56, 43))) }
        "nesw" { $graphics.DrawLine($darkPen, 53, 11, 11, 53); $graphics.DrawLine($goldPen, 50, 14, 14, 50); $graphics.FillPolygon($blueBrush, [System.Drawing.Point[]]@([System.Drawing.Point]::new(59, 5), [System.Drawing.Point]::new(43, 8), [System.Drawing.Point]::new(56, 21))); $graphics.FillPolygon($goldBrush, [System.Drawing.Point[]]@([System.Drawing.Point]::new(5, 59), [System.Drawing.Point]::new(21, 56), [System.Drawing.Point]::new(8, 43))) }
        "up" { $graphics.DrawLine($darkPen, 32, 14, 32, 58); $graphics.DrawLine($goldPen, 32, 15, 32, 56); $graphics.FillPolygon($blueBrush, [System.Drawing.Point[]]@([System.Drawing.Point]::new(32, 2), [System.Drawing.Point]::new(17, 20), [System.Drawing.Point]::new(47, 20))) }
        "no" { Add-ArrowShape $graphics 0 0 0.72; $graphics.DrawEllipse($darkPen, 25, 25, 34, 34); $graphics.DrawEllipse($goldPen, 28, 28, 28, 28); $graphics.DrawLine($goldPen, 32, 32, 52, 52) }
        "pen" { $graphics.DrawLine($darkPen, 11, 53, 49, 15); $graphics.DrawLine($bluePen, 13, 51, 47, 17); $graphics.FillPolygon($goldBrush, [System.Drawing.Point[]]@([System.Drawing.Point]::new(49, 10), [System.Drawing.Point]::new(55, 16), [System.Drawing.Point]::new(48, 22), [System.Drawing.Point]::new(42, 16))); $graphics.FillPolygon($creamBrush, [System.Drawing.Point[]]@([System.Drawing.Point]::new(8, 57), [System.Drawing.Point]::new(13, 44), [System.Drawing.Point]::new(21, 52))) }
    }
    $bytes = Convert-BitmapToCursorBytes $bitmap $hotX $hotY
    [System.IO.File]::WriteAllBytes((Join-Path $cursorDir $name), $bytes)
    $creamBrush.Dispose(); $goldBrush.Dispose(); $blueBrush.Dispose(); $goldPen.Dispose(); $bluePen.Dispose(); $darkPen.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}

function New-AnimatedCursor([string]$name, [bool]$includeArrow) {
    $frames = @()
    for ($frame = 0; $frame -lt 8; $frame++) {
        $canvas = New-Canvas; $bitmap = $canvas[0]; $graphics = $canvas[1]
        if ($includeArrow) { Add-ArrowShape $graphics 0 0 0.70; Add-Spinner $graphics 46 45 $frame } else { Add-Spinner $graphics 32 32 $frame }
        $frames += ,(Convert-BitmapToCursorBytes $bitmap $(if ($includeArrow) { 5 } else { 32 }) $(if ($includeArrow) { 4 } else { 32 }))
        $graphics.Dispose(); $bitmap.Dispose()
    }
    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $stream
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("RIFF")); $writer.Write([UInt32]0); $writer.Write([System.Text.Encoding]::ASCII.GetBytes("ACON"))
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("anih")); $writer.Write([UInt32]36)
    foreach ($value in @([UInt32]36, [UInt32]8, [UInt32]8, [UInt32]64, [UInt32]64, [UInt32]32, [UInt32]1, [UInt32]6, [UInt32]3)) { $writer.Write($value) }
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("rate")); $writer.Write([UInt32]32); 0..7 | ForEach-Object { $writer.Write([UInt32]6) }
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("seq ")); $writer.Write([UInt32]32); 0..7 | ForEach-Object { $writer.Write([UInt32]$_) }
    $listStream = New-Object System.IO.MemoryStream
    $listWriter = New-Object System.IO.BinaryWriter $listStream
    $listWriter.Write([System.Text.Encoding]::ASCII.GetBytes("fram"))
    foreach ($cursor in $frames) { $listWriter.Write([System.Text.Encoding]::ASCII.GetBytes("icon")); $listWriter.Write([UInt32]$cursor.Length); $listWriter.Write($cursor); if (($cursor.Length % 2) -ne 0) { $listWriter.Write([byte]0) } }
    $listWriter.Flush(); $list = $listStream.ToArray(); $listWriter.Dispose(); $listStream.Dispose()
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("LIST")); $writer.Write([UInt32]$list.Length); $writer.Write($list); if (($list.Length % 2) -ne 0) { $writer.Write([byte]0) }
    $writer.Flush(); $length = $stream.Length; $stream.Position = 4; $writer.Write([UInt32]($length - 8)); $writer.Flush()
    [System.IO.File]::WriteAllBytes((Join-Path $cursorDir $name), $stream.ToArray())
    $writer.Dispose(); $stream.Dispose()
}

function New-ToneWav([string]$name, [double[]]$frequencies, [int]$millisecondsPerTone) {
    $sampleRate = 44100; $samples = New-Object System.Collections.Generic.List[Int16]
    foreach ($frequency in $frequencies) {
        $count = [int]($sampleRate * $millisecondsPerTone / 1000)
        for ($index = 0; $index -lt $count; $index++) {
            $time = $index / $sampleRate
            $edge = [Math]::Min(1.0, [Math]::Min($index / ($sampleRate * 0.025), ($count - $index - 1) / ($sampleRate * 0.05)))
            $envelope = [Math]::Max(0, $edge) * [Math]::Exp(-2.4 * $index / $count)
            $wave = [Math]::Sin(2 * [Math]::PI * $frequency * $time) + (0.22 * [Math]::Sin(4 * [Math]::PI * $frequency * $time))
            $samples.Add([Int16]([Math]::Max(-32767, [Math]::Min(32767, 5200 * $wave * $envelope))))
        }
        1..882 | ForEach-Object { $samples.Add([Int16]0) }
    }
    $stream = New-Object System.IO.MemoryStream; $writer = New-Object System.IO.BinaryWriter $stream
    $dataLength = $samples.Count * 2
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("RIFF")); $writer.Write([UInt32](36 + $dataLength)); $writer.Write([System.Text.Encoding]::ASCII.GetBytes("WAVE"))
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("fmt ")); $writer.Write([UInt32]16); $writer.Write([UInt16]1); $writer.Write([UInt16]1); $writer.Write([UInt32]$sampleRate); $writer.Write([UInt32]($sampleRate * 2)); $writer.Write([UInt16]2); $writer.Write([UInt16]16)
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes("data")); $writer.Write([UInt32]$dataLength); foreach ($sample in $samples) { $writer.Write($sample) }
    $writer.Flush(); [System.IO.File]::WriteAllBytes((Join-Path $soundDir $name), $stream.ToArray()); $writer.Dispose(); $stream.Dispose()
}

function New-Preview([string]$sourceName, [string]$destinationName) {
    $source = [System.Drawing.Image]::FromFile((Join-Path $wallpaperDir $sourceName))
    $target = New-Object System.Drawing.Bitmap 960, 540
    $graphics = [System.Drawing.Graphics]::FromImage($target)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($source, 0, 0, 960, 540)
    $target.Save((Join-Path $previewDir $destinationName), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $target.Dispose(); $source.Dispose()
}

function New-WallpaperVariants([string]$sourceName, [string]$variant, [ValidateSet("Top", "Center", "Bottom")][string]$ultrawideAlignment) {
    $sourcePath = Join-Path $wallpaperDir $sourceName
    $source = [System.Drawing.Image]::FromFile($sourcePath)
    $standard = New-Object System.Drawing.Bitmap 3840, 2160
    $graphics = [System.Drawing.Graphics]::FromImage($standard)
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.DrawImage($source, 0, 0, 3840, 2160)
    $standard.Save((Join-Path $wallpaperDir ("JANUS-Crux-" + $variant + "-4K.png")), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $standard.Dispose()

    $cropHeight = [int][Math]::Round($source.Width / (5120.0 / 2160.0))
    if ($ultrawideAlignment -eq "Top") { $cropY = 0 }
    elseif ($ultrawideAlignment -eq "Bottom") { $cropY = $source.Height - $cropHeight }
    else { $cropY = [int](($source.Height - $cropHeight) / 2) }
    $ultrawide = New-Object System.Drawing.Bitmap 5120, 2160
    $wideGraphics = [System.Drawing.Graphics]::FromImage($ultrawide)
    $wideGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $wideGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $wideGraphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, 5120, 2160), [System.Drawing.Rectangle]::new(0, $cropY, $source.Width, $cropHeight), [System.Drawing.GraphicsUnit]::Pixel)
    $ultrawide.Save((Join-Path $wallpaperDir ("JANUS-Crux-" + $variant + "-Ultrawide-5K.png")), [System.Drawing.Imaging.ImageFormat]::Png)
    $wideGraphics.Dispose(); $ultrawide.Dispose(); $source.Dispose()
}

New-Cursor "crux-arrow.cur" "arrow" 7 5
New-Cursor "crux-help.cur" "help" 7 5
New-Cursor "crux-hand.cur" "hand" 30 17
New-Cursor "crux-ibeam.cur" "ibeam" 32 32
New-Cursor "crux-cross.cur" "cross" 32 32
New-Cursor "crux-move.cur" "move" 32 32
New-Cursor "crux-size-ns.cur" "ns" 32 32
New-Cursor "crux-size-we.cur" "we" 32 32
New-Cursor "crux-size-nwse.cur" "nwse" 32 32
New-Cursor "crux-size-nesw.cur" "nesw" 32 32
New-Cursor "crux-up.cur" "up" 32 3
New-Cursor "crux-no.cur" "no" 7 5
New-Cursor "crux-pen.cur" "pen" 9 56
New-AnimatedCursor "crux-working.ani" $true
New-AnimatedCursor "crux-busy.ani" $false

New-ToneWav "crux-notify.wav" @(587.33, 783.99) 130
New-ToneWav "crux-question.wav" @(493.88, 659.25) 155
New-ToneWav "crux-warning.wav" @(392.00, 329.63) 175
New-ToneWav "crux-error.wav" @(261.63, 220.00) 190
New-ToneWav "crux-start.wav" @(392.00, 587.33, 783.99) 115
New-ToneWav "crux-complete.wav" @(440.00, 587.33, 880.00) 110

$baseIcons = Join-Path $repo "assets\theme-packs\crux"
foreach ($baseName in @("este-equipo", "archivos-usuario", "red", "papelera-vacia", "papelera-llena", "documentos", "descargas", "escritorio", "imagenes", "musica", "videos", "hdd-ssd", "usb", "unidad-red")) {
    Copy-Item (Join-Path $baseIcons ($baseName + ".ico")) (Join-Path $iconDir ($baseName + ".ico")) -Force
    Copy-Item (Join-Path $baseIcons ($baseName + ".png")) (Join-Path $iconDir ($baseName + ".png")) -Force
}
Set-Content -LiteralPath (Join-Path $iconDir "TEMA.txt") -Value "crux" -Encoding UTF8

New-WallpaperVariants "JANUS-Crux-Claro-source.png" "Claro" "Bottom"
New-WallpaperVariants "JANUS-Crux-Oscuro-source.png" "Oscuro" "Center"
New-Preview "JANUS-Crux-Claro-4K.png" "JANUS-Crux-Claro-preview.png"
New-Preview "JANUS-Crux-Oscuro-4K.png" "JANUS-Crux-Oscuro-preview.png"

$manifest = [ordered]@{
    id = "janus-crux"
    version = 1
    displayName = "JANUS Crux"
    publisher = "Momocrackcorp"
    variants = @("Claro", "Oscuro")
    wallpapers = @("Claro", "Oscuro")
    slideshowMinutes = 30
    preserveWindowsMode = $true
    components = @("wallpapers", "colors", "cursors", "sounds")
    accentColor = "#E87817"
    iconCompanion = "Tema-Iconos-Crux.zip"
    safe = $true
    reversible = $true
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $themeRoot "manifest.json") -Encoding UTF8

$themeZip = Join-Path $dist "JANUS-Crux-v1.zip"
$iconsZip = Join-Path $dist "JANUS-Crux-Iconos-v1.zip"
Remove-Item -LiteralPath $themeZip -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $iconsZip -Force -ErrorAction SilentlyContinue
$archive = [System.IO.Compression.ZipFile]::Open($themeZip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $packageFiles = Get-ChildItem -LiteralPath $themeRoot -File -Recurse | Where-Object {
        $_.FullName -notlike "$iconDir\*" -and $_.Name -notlike "*-source.png"
    }
    foreach ($file in $packageFiles) {
        $relative = $file.FullName.Substring($themeRoot.Length + 1).Replace("\", "/")
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $relative, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $archive.Dispose() }
Compress-Archive -Path (Join-Path $iconDir "*") -DestinationPath $iconsZip -CompressionLevel Optimal

Get-FileHash $themeZip, $iconsZip -Algorithm SHA256 | Select-Object Path, Hash
