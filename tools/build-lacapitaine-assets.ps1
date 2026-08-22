param([string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent))

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repo = (Resolve-Path $RepositoryRoot).Path
$themeRoot = Join-Path $repo "assets\windows-themes\lacapitaine"
$sourceDir = Join-Path $themeRoot "Sources"
$wallpaperDir = Join-Path $themeRoot "DesktopBackground"
$previewDir = Join-Path $themeRoot "Preview"
$cursorDir = Join-Path $themeRoot "Cursors"
$soundDir = Join-Path $themeRoot "Sounds"
$iconDir = Join-Path $themeRoot "Icons"
$dist = Join-Path $repo "dist\themes"
foreach ($directory in @($themeRoot,$sourceDir,$wallpaperDir,$previewDir,$cursorDir,$soundDir,$iconDir,$dist)) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }

$granite = [Drawing.Color]::FromArgb(89,101,107)
$fjord = [Drawing.Color]::FromArgb(47,111,137)
$alpineRed = [Drawing.Color]::FromArgb(214,76,63)
$snow = [Drawing.Color]::FromArgb(246,249,250)
$charcoal = [Drawing.Color]::FromArgb(27,36,41)

function New-Canvas {
    $bitmap = New-Object Drawing.Bitmap 64,64
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.Color]::Transparent)
    return @($bitmap,$graphics)
}

function Add-AlpineArrow([Drawing.Graphics]$graphics,[double]$scale=1.0) {
    $points = [Drawing.PointF[]]@(
        [Drawing.PointF]::new([float](6*$scale),[float](3*$scale)),[Drawing.PointF]::new([float](8*$scale),[float](48*$scale)),
        [Drawing.PointF]::new([float](19*$scale),[float](37*$scale)),[Drawing.PointF]::new([float](28*$scale),[float](59*$scale)),
        [Drawing.PointF]::new([float](38*$scale),[float](55*$scale)),[Drawing.PointF]::new([float](29*$scale),[float](34*$scale)),
        [Drawing.PointF]::new([float](46*$scale),[float](31*$scale)))
    $outline = New-Object Drawing.Pen $charcoal,4
    $rope = New-Object Drawing.Pen $alpineRed,3
    $fill = New-Object Drawing.Drawing2D.LinearGradientBrush ([Drawing.PointF]::new(6,3)),([Drawing.PointF]::new(42,57)),$snow,$fjord
    $graphics.FillPolygon($fill,$points);$graphics.DrawPolygon($outline,$points);$graphics.DrawLine($rope,[float](10*$scale),[float](12*$scale),[float](24*$scale),[float](45*$scale))
    $fill.Dispose();$rope.Dispose();$outline.Dispose()
}

function Convert-ToCursorBytes([Drawing.Bitmap]$bitmap,[int]$hotX,[int]$hotY) {
    $pngStream=New-Object IO.MemoryStream;$bitmap.Save($pngStream,[Drawing.Imaging.ImageFormat]::Png);$png=$pngStream.ToArray();$pngStream.Dispose()
    $stream=New-Object IO.MemoryStream;$writer=New-Object IO.BinaryWriter $stream
    $writer.Write([UInt16]0);$writer.Write([UInt16]2);$writer.Write([UInt16]1);$writer.Write([byte]64);$writer.Write([byte]64);$writer.Write([byte]0);$writer.Write([byte]0)
    $writer.Write([UInt16]$hotX);$writer.Write([UInt16]$hotY);$writer.Write([UInt32]$png.Length);$writer.Write([UInt32]22);$writer.Write($png);$writer.Flush()
    $bytes=$stream.ToArray();$writer.Dispose();$stream.Dispose();return ,$bytes
}

function New-AlpineCursor([string]$name,[string]$kind,[int]$hotX,[int]$hotY) {
    $canvas=New-Canvas;$bitmap=$canvas[0];$graphics=$canvas[1]
    $darkPen=New-Object Drawing.Pen $charcoal,4;$snowPen=New-Object Drawing.Pen $snow,2;$fjordPen=New-Object Drawing.Pen $fjord,5;$redPen=New-Object Drawing.Pen $alpineRed,4
    $graniteBrush=New-Object Drawing.SolidBrush $granite;$fjordBrush=New-Object Drawing.SolidBrush $fjord;$redBrush=New-Object Drawing.SolidBrush $alpineRed;$snowBrush=New-Object Drawing.SolidBrush $snow
    switch($kind) {
        "arrow" { Add-AlpineArrow $graphics }
        "help" { Add-AlpineArrow $graphics;$graphics.FillEllipse($redBrush,36,34,24,24);$graphics.DrawEllipse($darkPen,36,34,24,24);$font=New-Object Drawing.Font "Segoe UI",13,([Drawing.FontStyle]::Bold);$graphics.DrawString("?",$font,$snowBrush,42,35);$font.Dispose() }
        "hand" { $graphics.DrawEllipse($darkPen,13,7,37,49);$graphics.DrawEllipse($redPen,17,11,29,41);$graphics.DrawLine($fjordPen,23,18,41,45);$graphics.DrawLine($snowPen,21,44,42,18) }
        "ibeam" { $graphics.DrawLine($darkPen,20,7,44,7);$graphics.DrawLine($fjordPen,32,8,32,56);$graphics.DrawLine($darkPen,20,57,44,57);$graphics.DrawLine($redPen,27,32,37,32) }
        "cross" { $graphics.DrawEllipse($fjordPen,11,11,42,42);$graphics.DrawLine($darkPen,32,3,32,61);$graphics.DrawLine($darkPen,3,32,61,32);$graphics.FillPolygon($redBrush,[Drawing.Point[]]@([Drawing.Point]::new(32,15),[Drawing.Point]::new(27,28),[Drawing.Point]::new(37,28))) }
        "move" { $graphics.DrawLine($darkPen,32,7,32,57);$graphics.DrawLine($darkPen,7,32,57,32);$graphics.FillPolygon($redBrush,[Drawing.Point[]]@([Drawing.Point]::new(32,2),[Drawing.Point]::new(23,15),[Drawing.Point]::new(41,15)));$graphics.FillPolygon($fjordBrush,[Drawing.Point[]]@([Drawing.Point]::new(32,62),[Drawing.Point]::new(23,49),[Drawing.Point]::new(41,49)));$graphics.FillPolygon($graniteBrush,[Drawing.Point[]]@([Drawing.Point]::new(2,32),[Drawing.Point]::new(15,23),[Drawing.Point]::new(15,41)));$graphics.FillPolygon($graniteBrush,[Drawing.Point[]]@([Drawing.Point]::new(62,32),[Drawing.Point]::new(49,23),[Drawing.Point]::new(49,41))) }
        "ns" { $graphics.DrawLine($darkPen,32,9,32,55);$graphics.FillPolygon($redBrush,[Drawing.Point[]]@([Drawing.Point]::new(32,2),[Drawing.Point]::new(21,17),[Drawing.Point]::new(43,17)));$graphics.FillPolygon($fjordBrush,[Drawing.Point[]]@([Drawing.Point]::new(32,62),[Drawing.Point]::new(21,47),[Drawing.Point]::new(43,47))) }
        "we" { $graphics.DrawLine($darkPen,9,32,55,32);$graphics.FillPolygon($redBrush,[Drawing.Point[]]@([Drawing.Point]::new(2,32),[Drawing.Point]::new(17,21),[Drawing.Point]::new(17,43)));$graphics.FillPolygon($fjordBrush,[Drawing.Point[]]@([Drawing.Point]::new(62,32),[Drawing.Point]::new(47,21),[Drawing.Point]::new(47,43))) }
        "nwse" { $graphics.DrawLine($darkPen,11,11,53,53);$graphics.DrawLine($snowPen,14,14,50,50);$graphics.FillPolygon($redBrush,[Drawing.Point[]]@([Drawing.Point]::new(5,5),[Drawing.Point]::new(21,8),[Drawing.Point]::new(8,21)));$graphics.FillPolygon($fjordBrush,[Drawing.Point[]]@([Drawing.Point]::new(59,59),[Drawing.Point]::new(43,56),[Drawing.Point]::new(56,43))) }
        "nesw" { $graphics.DrawLine($darkPen,53,11,11,53);$graphics.DrawLine($snowPen,50,14,14,50);$graphics.FillPolygon($redBrush,[Drawing.Point[]]@([Drawing.Point]::new(59,5),[Drawing.Point]::new(43,8),[Drawing.Point]::new(56,21)));$graphics.FillPolygon($fjordBrush,[Drawing.Point[]]@([Drawing.Point]::new(5,59),[Drawing.Point]::new(21,56),[Drawing.Point]::new(8,43))) }
        "up" { $graphics.DrawLine($darkPen,32,14,32,58);$graphics.FillPolygon($redBrush,[Drawing.Point[]]@([Drawing.Point]::new(32,2),[Drawing.Point]::new(17,21),[Drawing.Point]::new(47,21)));$graphics.DrawArc($fjordPen,17,36,30,18,10,160) }
        "no" { Add-AlpineArrow $graphics 0.72;$graphics.DrawEllipse($darkPen,25,25,34,34);$graphics.DrawEllipse($redPen,28,28,28,28);$graphics.DrawLine($redPen,33,33,51,51) }
        "pen" { $graphics.DrawLine($darkPen,10,54,49,15);$graphics.DrawLine($fjordPen,13,51,47,17);$graphics.FillPolygon($redBrush,[Drawing.Point[]]@([Drawing.Point]::new(49,9),[Drawing.Point]::new(56,16),[Drawing.Point]::new(48,23),[Drawing.Point]::new(41,16)));$graphics.FillPolygon($snowBrush,[Drawing.Point[]]@([Drawing.Point]::new(7,58),[Drawing.Point]::new(13,44),[Drawing.Point]::new(21,52))) }
    }
    [IO.File]::WriteAllBytes((Join-Path $cursorDir $name),(Convert-ToCursorBytes $bitmap $hotX $hotY))
    foreach($item in @($snowBrush,$redBrush,$fjordBrush,$graniteBrush,$redPen,$fjordPen,$snowPen,$darkPen,$graphics,$bitmap)){$item.Dispose()}
}

function Add-CompassSpinner([Drawing.Graphics]$graphics,[int]$cx,[int]$cy,[int]$frame) {
    $ring=New-Object Drawing.Pen $granite,3;$graphics.DrawEllipse($ring,$cx-19,$cy-19,38,38);$ring.Dispose()
    for($i=0;$i -lt 8;$i++){$angle=($i*45-90)*[Math]::PI/180;$x=$cx+15*[Math]::Cos($angle);$y=$cy+15*[Math]::Sin($angle);$brush=New-Object Drawing.SolidBrush $(if($i -eq $frame){$alpineRed}else{$fjord});$size=if($i -eq $frame){9}else{5};$graphics.FillEllipse($brush,[float]($x-$size/2),[float]($y-$size/2),$size,$size);$brush.Dispose()}
}

function New-AlpineAni([string]$name,[bool]$withArrow) {
    $frames=@();for($frame=0;$frame -lt 8;$frame++){$canvas=New-Canvas;$bitmap=$canvas[0];$graphics=$canvas[1];if($withArrow){Add-AlpineArrow $graphics 0.70;Add-CompassSpinner $graphics 47 45 $frame}else{Add-CompassSpinner $graphics 32 32 $frame};$frames+=,(Convert-ToCursorBytes $bitmap $(if($withArrow){5}else{32}) $(if($withArrow){4}else{32}));$graphics.Dispose();$bitmap.Dispose()}
    $stream=New-Object IO.MemoryStream;$writer=New-Object IO.BinaryWriter $stream;$writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF"));$writer.Write([UInt32]0);$writer.Write([Text.Encoding]::ASCII.GetBytes("ACON"));$writer.Write([Text.Encoding]::ASCII.GetBytes("anih"));$writer.Write([UInt32]36);foreach($value in @([UInt32]36,[UInt32]8,[UInt32]8,[UInt32]64,[UInt32]64,[UInt32]32,[UInt32]1,[UInt32]6,[UInt32]3)){$writer.Write($value)};$writer.Write([Text.Encoding]::ASCII.GetBytes("rate"));$writer.Write([UInt32]32);0..7|ForEach-Object{$writer.Write([UInt32]6)};$writer.Write([Text.Encoding]::ASCII.GetBytes("seq "));$writer.Write([UInt32]32);0..7|ForEach-Object{$writer.Write([UInt32]$_)}
    $listStream=New-Object IO.MemoryStream;$listWriter=New-Object IO.BinaryWriter $listStream;$listWriter.Write([Text.Encoding]::ASCII.GetBytes("fram"));foreach($cursor in $frames){$listWriter.Write([Text.Encoding]::ASCII.GetBytes("icon"));$listWriter.Write([UInt32]$cursor.Length);$listWriter.Write($cursor);if($cursor.Length%2){$listWriter.Write([byte]0)}};$listWriter.Flush();$list=$listStream.ToArray();$listWriter.Dispose();$listStream.Dispose();$writer.Write([Text.Encoding]::ASCII.GetBytes("LIST"));$writer.Write([UInt32]$list.Length);$writer.Write($list);if($list.Length%2){$writer.Write([byte]0)};$writer.Flush();$length=$stream.Length;$stream.Position=4;$writer.Write([UInt32]($length-8));$writer.Flush();[IO.File]::WriteAllBytes((Join-Path $cursorDir $name),$stream.ToArray());$writer.Dispose();$stream.Dispose()
}

function New-MountainSound([string]$name,[double[]]$notes,[int]$milliseconds) {
    $sampleRate=44100;$samples=New-Object 'Collections.Generic.List[Int16]';$noteNumber=0
    foreach($frequency in $notes){$count=[int]($sampleRate*$milliseconds/1000);for($i=0;$i -lt $count;$i++){$time=$i/$sampleRate;$progress=$i/[Math]::Max(1,$count-1);$attack=[Math]::Min(1,$i/700);$release=[Math]::Min(1,($count-$i)/2100);$envelope=$attack*$release*[Math]::Exp(-2.9*$progress);$wind=0.05*[Math]::Sin(2*[Math]::PI*23*$time)*[Math]::Sin(2*[Math]::PI*($frequency*3.7)*$time);$stone=0.16*[Math]::Sin(2*[Math]::PI*($frequency*0.5)*$time)*[Math]::Exp(-10*$progress);$bell=[Math]::Sin(2*[Math]::PI*$frequency*$time)+0.24*[Math]::Sin(2*[Math]::PI*$frequency*2.01*$time);$rope=0.10*[Math]::Sin(2*[Math]::PI*($frequency*1.49)*$time)*[Math]::Exp(-5*$progress);$value=($bell+$wind+$stone+$rope)*$envelope;$samples.Add([Int16]([Math]::Max(-32767,[Math]::Min(32767,3700*$value))))};1..350|ForEach-Object{$samples.Add([Int16]0)};$noteNumber++}
    $stream=New-Object IO.MemoryStream;$writer=New-Object IO.BinaryWriter $stream;$length=$samples.Count*2;$writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF"));$writer.Write([UInt32](36+$length));$writer.Write([Text.Encoding]::ASCII.GetBytes("WAVE"));$writer.Write([Text.Encoding]::ASCII.GetBytes("fmt "));$writer.Write([UInt32]16);$writer.Write([UInt16]1);$writer.Write([UInt16]1);$writer.Write([UInt32]$sampleRate);$writer.Write([UInt32]($sampleRate*2));$writer.Write([UInt16]2);$writer.Write([UInt16]16);$writer.Write([Text.Encoding]::ASCII.GetBytes("data"));$writer.Write([UInt32]$length);foreach($sample in $samples){$writer.Write($sample)};$writer.Flush();[IO.File]::WriteAllBytes((Join-Path $soundDir $name),$stream.ToArray());$writer.Dispose();$stream.Dispose()
}

function Save-Jpeg([Drawing.Image]$image,[string]$path,[long]$quality=93) {$codec=[Drawing.Imaging.ImageCodecInfo]::GetImageEncoders()|Where-Object{$_.MimeType -eq 'image/jpeg'}|Select-Object -First 1;$parameters=New-Object Drawing.Imaging.EncoderParameters 1;$parameters.Param[0]=New-Object Drawing.Imaging.EncoderParameter ([Drawing.Imaging.Encoder]::Quality),$quality;try{$image.Save($path,$codec,$parameters)}finally{$parameters.Param[0].Dispose();$parameters.Dispose()}}

function New-CoverImage([string]$sourcePath,[int]$width,[int]$height,[string]$destination) {
    $source=[Drawing.Image]::FromFile($sourcePath);try{$targetRatio=$width/[double]$height;$sourceRatio=$source.Width/[double]$source.Height;if($sourceRatio -gt $targetRatio){$cropHeight=$source.Height;$cropWidth=[int]($cropHeight*$targetRatio);$cropX=[int](($source.Width-$cropWidth)/2);$cropY=0}else{$cropWidth=$source.Width;$cropHeight=[int]($cropWidth/$targetRatio);$cropX=0;$cropY=[int](($source.Height-$cropHeight)/2)};$target=New-Object Drawing.Bitmap $width,$height;try{$graphics=[Drawing.Graphics]::FromImage($target);try{$graphics.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality;$graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$graphics.DrawImage($source,[Drawing.Rectangle]::new(0,0,$width,$height),[Drawing.Rectangle]::new($cropX,$cropY,$cropWidth,$cropHeight),[Drawing.GraphicsUnit]::Pixel)}finally{$graphics.Dispose()};if([IO.Path]::GetExtension($destination) -eq ".png"){$target.Save($destination,[Drawing.Imaging.ImageFormat]::Png)}else{Save-Jpeg $target $destination}}finally{$target.Dispose()}}finally{$source.Dispose()}
}

$wallpapers=@(
    @("Muro","LaCapitaine-Muro-source.jpg"),@("Rio","LaCapitaine-Rio-source.jpg"),@("Panorama","LaCapitaine-Panorama-source.jpg"),
    @("Invierno","LaCapitaine-Invierno-source.jpg"),@("Nubes","LaCapitaine-Nubes-source.jpg")
)
foreach($wallpaper in $wallpapers){$source=Join-Path $sourceDir $wallpaper[1];if(!(Test-Path -LiteralPath $source)){throw "Falta la fuente $($wallpaper[1])"};New-CoverImage $source 3840 2160 (Join-Path $wallpaperDir ("JANUS-LaCapitaine-"+$wallpaper[0]+"-4K.jpg"));New-CoverImage $source 5120 2160 (Join-Path $wallpaperDir ("JANUS-LaCapitaine-"+$wallpaper[0]+"-Ultrawide-5K.jpg"))}
foreach($variant in @("Muro","Rio","Invierno")){New-CoverImage (Join-Path $wallpaperDir ("JANUS-LaCapitaine-"+$variant+"-4K.jpg")) 960 540 (Join-Path $previewDir ("JANUS-LaCapitaine-"+$variant+"-preview.png"))}

foreach($cursor in @(@("arrow","arrow",7,5),@("help","help",7,5),@("hand","hand",31,15),@("ibeam","ibeam",32,32),@("cross","cross",32,32),@("move","move",32,32),@("size-ns","ns",32,32),@("size-we","we",32,32),@("size-nwse","nwse",32,32),@("size-nesw","nesw",32,32),@("up","up",32,3),@("no","no",7,5),@("pen","pen",9,56))){New-AlpineCursor ("lacapitaine-"+$cursor[0]+".cur") $cursor[1] $cursor[2] $cursor[3]}
New-AlpineAni "lacapitaine-working.ani" $true;New-AlpineAni "lacapitaine-busy.ani" $false

New-MountainSound "lacapitaine-notify.wav" @(587.33,739.99,880.00) 85
New-MountainSound "lacapitaine-question.wav" @(440.00,587.33,739.99) 100
New-MountainSound "lacapitaine-warning.wav" @(392.00,349.23,293.66) 110
New-MountainSound "lacapitaine-error.wav" @(293.66,261.63,220.00) 130
New-MountainSound "lacapitaine-complete.wav" @(392.00,523.25,659.25,783.99) 80
New-MountainSound "lacapitaine-start.wav" @(293.66,392.00,523.25,659.25,783.99) 95
New-MountainSound "lacapitaine-logon.wav" @(392.00,523.25,659.25) 100
New-MountainSound "lacapitaine-logoff.wav" @(659.25,523.25,392.00) 100
New-MountainSound "lacapitaine-exit.wav" @(587.33,440.00,349.23,293.66) 105

$baseIcons=Join-Path $repo "assets\theme-packs\lacapitaine"
foreach($baseName in @("este-equipo","archivos-usuario","red","papelera-vacia","papelera-llena","documentos","descargas","escritorio","imagenes","musica","videos","hdd-ssd","usb","unidad-red")){Copy-Item -LiteralPath (Join-Path $baseIcons ($baseName+".ico")) -Destination (Join-Path $iconDir ($baseName+".ico")) -Force;Copy-Item -LiteralPath (Join-Path $baseIcons ($baseName+".png")) -Destination (Join-Path $iconDir ($baseName+".png")) -Force}
foreach($document in @("TEMA.txt","ATRIBUCION.txt","LICENSE.txt")){if(Test-Path -LiteralPath (Join-Path $baseIcons $document)){Copy-Item -LiteralPath (Join-Path $baseIcons $document) -Destination (Join-Path $iconDir $document) -Force}}

$manifest=[ordered]@{id="janus-lacapitaine";version=1;displayName="JANUS La Capitaine";publisher="Momocrackcorp";variants=@($wallpapers|ForEach-Object{$_[0]});previewVariants=@("Muro","Rio","Invierno");components=@("wallpapers","colors","cursors","sounds");baseColor="#59656B";accentColor="#D64C3F";secondaryAccentColor="#2F6F89";slideshowIntervalMilliseconds=1800000;shuffle=$true;preserveWindowsMode=$true;soundStyle="Original mountain wind, stone, rope and distant bell timbres";cursorStyle="Compass, rope and carabiner inspired";iconCompanion="Tema-Iconos-La-Capitaine.zip";safe=$true;reversible=$true}
$manifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $themeRoot "manifest.json") -Encoding UTF8

$credits=@'
JANUS La Capitaine — créditos y licencias de fondos

Muro — “Trolltindan and Trollveggen above sideroad in Romsdalen…”, Simo Räsänen (Ximonic), CC BY-SA 4.0.
https://commons.wikimedia.org/wiki/File:Trolltindan_and_Trollveggen_above_sideroad_in_Romsdalen,_Rauma,_Møre_og_Romsdal,_Norway,_2025_May.jpg
Río — “Trolltindene behind Rauma river in Rauma…”, Simo Räsänen (Ximonic), CC BY-SA 4.0.
https://commons.wikimedia.org/wiki/File:Trolltindene_behind_Rauma_river_in_Rauma,_Møre_og_Romsdal,_Norway,_2025_May.jpg
Panorama — “View from Litlefjellet at Romsdalen, 2013 June”, Simo Räsänen (Ximonic), CC BY-SA 3.0.
https://commons.wikimedia.org/wiki/File:View_from_Litlefjellet_at_Romsdalen,_2013_June.jpg
Invierno — “Vinter i Trollveggen”, Ernst Vikne, CC BY-SA 2.0.
https://commons.wikimedia.org/wiki/File:Vinter_i_Trollveggen.jpg
Nubes — “Romsdalen and Trolltindene with some clouds…”, Simo Räsänen (Ximonic), CC BY-SA 3.0.
https://commons.wikimedia.org/wiki/File:Romsdalen_and_Trolltindene_with_some_clouds,_Møre_og_Romsdal,_Norway_in_2013_June.jpg

Modificaciones: recorte centrado y redimensionado a 3840×2160 y 5120×2160. Las adaptaciones conservan la licencia ShareAlike correspondiente a cada fotografía.
CC BY-SA 4.0: https://creativecommons.org/licenses/by-sa/4.0/
CC BY-SA 3.0: https://creativecommons.org/licenses/by-sa/3.0/
CC BY-SA 2.0: https://creativecommons.org/licenses/by-sa/2.0/
'@
Set-Content -LiteralPath (Join-Path $themeRoot "CREDITOS-FONDOS.txt") -Value $credits -Encoding UTF8

$themeZip=Join-Path $dist "JANUS-LaCapitaine-v1.zip";$iconsZip=Join-Path $dist "Tema-Iconos-La-Capitaine.zip"
Remove-Item -LiteralPath $themeZip,$iconsZip -Force -ErrorAction SilentlyContinue
$archive=[IO.Compression.ZipFile]::Open($themeZip,[IO.Compression.ZipArchiveMode]::Create)
try{Get-ChildItem -LiteralPath $themeRoot -File -Recurse|Where-Object{$_.FullName -notlike "$sourceDir\*" -and $_.FullName -notlike "$iconDir\*"}|ForEach-Object{$relative=$_.FullName.Substring($themeRoot.Length+1).Replace("\","/");[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}}finally{$archive.Dispose()}
Compress-Archive -Path (Join-Path $iconDir "*") -DestinationPath $iconsZip -CompressionLevel Optimal -Force
Get-FileHash $themeZip,$iconsZip -Algorithm SHA256|Select-Object Path,Hash
