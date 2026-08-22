param([string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent))

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repo = (Resolve-Path $RepositoryRoot).Path
$themeRoot = Join-Path $repo "assets\windows-themes\whitesur"
$sourceDir = Join-Path $themeRoot "Sources"
$wallpaperDir = Join-Path $themeRoot "DesktopBackground"
$previewDir = Join-Path $themeRoot "Preview"
$cursorDir = Join-Path $themeRoot "Cursors"
$soundDir = Join-Path $themeRoot "Sounds"
$iconDir = Join-Path $themeRoot "Icons"
$dist = Join-Path $repo "dist\themes"
foreach ($directory in @($themeRoot,$sourceDir,$wallpaperDir,$previewDir,$cursorDir,$soundDir,$iconDir,$dist)) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }

$liddy = [Drawing.Color]::FromArgb(100,124,100)
$seafoam = [Drawing.Color]::FromArgb(0,183,195)
$aqua = [Drawing.Color]::FromArgb(113,225,217)
$deep = [Drawing.Color]::FromArgb(19,58,71)
$white = [Drawing.Color]::FromArgb(248,255,253)

function New-Canvas {
    $bitmap = New-Object Drawing.Bitmap 64,64
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.Color]::Transparent)
    return @($bitmap,$graphics)
}

function Add-MarineArrow([Drawing.Graphics]$graphics,[double]$scale=1.0) {
    $points = [Drawing.PointF[]]@(
        [Drawing.PointF]::new([float](6*$scale),[float](3*$scale)),[Drawing.PointF]::new([float](8*$scale),[float](47*$scale)),
        [Drawing.PointF]::new([float](19*$scale),[float](36*$scale)),[Drawing.PointF]::new([float](27*$scale),[float](58*$scale)),
        [Drawing.PointF]::new([float](37*$scale),[float](54*$scale)),[Drawing.PointF]::new([float](28*$scale),[float](33*$scale)),
        [Drawing.PointF]::new([float](45*$scale),[float](31*$scale)))
    $outlinePen = New-Object Drawing.Pen $deep,4
    $foamPen = New-Object Drawing.Pen $white,2
    $fill = New-Object Drawing.Drawing2D.LinearGradientBrush ([Drawing.PointF]::new(6,3)),([Drawing.PointF]::new(40,55)),$seafoam,$aqua
    $crest = [Drawing.PointF[]]$points[0..3]
    $graphics.FillPolygon($fill,$points); $graphics.DrawPolygon($outlinePen,$points); $graphics.DrawLines($foamPen,$crest)
    $fill.Dispose(); $foamPen.Dispose(); $outlinePen.Dispose()
}

function Convert-ToCursorBytes([Drawing.Bitmap]$bitmap,[int]$hotX,[int]$hotY) {
    $pngStream = New-Object IO.MemoryStream; $bitmap.Save($pngStream,[Drawing.Imaging.ImageFormat]::Png); $png=$pngStream.ToArray(); $pngStream.Dispose()
    $stream=New-Object IO.MemoryStream; $writer=New-Object IO.BinaryWriter $stream
    $writer.Write([UInt16]0);$writer.Write([UInt16]2);$writer.Write([UInt16]1);$writer.Write([byte]64);$writer.Write([byte]64);$writer.Write([byte]0);$writer.Write([byte]0)
    $writer.Write([UInt16]$hotX);$writer.Write([UInt16]$hotY);$writer.Write([UInt32]$png.Length);$writer.Write([UInt32]22);$writer.Write($png);$writer.Flush()
    $bytes=$stream.ToArray();$writer.Dispose();$stream.Dispose();return ,$bytes
}

function New-MarineCursor([string]$name,[string]$kind,[int]$hotX,[int]$hotY) {
    $canvas=New-Canvas;$bitmap=$canvas[0];$graphics=$canvas[1]
    $deepPen=New-Object Drawing.Pen $deep,4;$foamPen=New-Object Drawing.Pen $white,2;$aquaPen=New-Object Drawing.Pen $aqua,5
    $seaBrush=New-Object Drawing.SolidBrush $seafoam;$aquaBrush=New-Object Drawing.SolidBrush $aqua;$foamBrush=New-Object Drawing.SolidBrush $white
    switch($kind) {
        "arrow" { Add-MarineArrow $graphics }
        "help" { Add-MarineArrow $graphics; $graphics.FillEllipse($seaBrush,36,34,24,24);$graphics.DrawEllipse($deepPen,36,34,24,24);$font=New-Object Drawing.Font "Segoe UI",13,([Drawing.FontStyle]::Bold);$graphics.DrawString("?",$font,$foamBrush,42,35);$font.Dispose() }
        "hand" { $graphics.FillEllipse($aquaBrush,12,10,40,40);$graphics.DrawEllipse($deepPen,12,10,40,40);for($i=0;$i -lt 5;$i++){$angle=(-90+$i*72)*[Math]::PI/180;$graphics.DrawLine($foamPen,32,30,[float](32+18*[Math]::Cos($angle)),[float](30+18*[Math]::Sin($angle)))}$graphics.FillEllipse($seaBrush,25,23,14,14) }
        "ibeam" { $graphics.DrawLine($deepPen,20,7,44,7);$graphics.DrawLine($aquaPen,32,8,32,56);$graphics.DrawLine($deepPen,20,57,44,57) }
        "cross" { $graphics.DrawEllipse($aquaPen,13,13,38,38);$graphics.DrawLine($deepPen,32,3,32,61);$graphics.DrawLine($deepPen,3,32,61,32);$graphics.FillEllipse($foamBrush,28,28,8,8) }
        "move" { $graphics.DrawLine($deepPen,32,7,32,57);$graphics.DrawLine($deepPen,7,32,57,32);$graphics.FillPolygon($seaBrush,[Drawing.Point[]]@([Drawing.Point]::new(32,2),[Drawing.Point]::new(23,15),[Drawing.Point]::new(41,15)));$graphics.FillPolygon($seaBrush,[Drawing.Point[]]@([Drawing.Point]::new(32,62),[Drawing.Point]::new(23,49),[Drawing.Point]::new(41,49)));$graphics.FillPolygon($aquaBrush,[Drawing.Point[]]@([Drawing.Point]::new(2,32),[Drawing.Point]::new(15,23),[Drawing.Point]::new(15,41)));$graphics.FillPolygon($aquaBrush,[Drawing.Point[]]@([Drawing.Point]::new(62,32),[Drawing.Point]::new(49,23),[Drawing.Point]::new(49,41))) }
        "ns" { $graphics.DrawLine($deepPen,32,9,32,55);$graphics.FillPolygon($seaBrush,[Drawing.Point[]]@([Drawing.Point]::new(32,2),[Drawing.Point]::new(21,17),[Drawing.Point]::new(43,17)));$graphics.FillPolygon($aquaBrush,[Drawing.Point[]]@([Drawing.Point]::new(32,62),[Drawing.Point]::new(21,47),[Drawing.Point]::new(43,47))) }
        "we" { $graphics.DrawLine($deepPen,9,32,55,32);$graphics.FillPolygon($seaBrush,[Drawing.Point[]]@([Drawing.Point]::new(2,32),[Drawing.Point]::new(17,21),[Drawing.Point]::new(17,43)));$graphics.FillPolygon($aquaBrush,[Drawing.Point[]]@([Drawing.Point]::new(62,32),[Drawing.Point]::new(47,21),[Drawing.Point]::new(47,43))) }
        "nwse" { $graphics.DrawLine($deepPen,11,11,53,53);$graphics.DrawLine($foamPen,14,14,50,50);$graphics.FillPolygon($seaBrush,[Drawing.Point[]]@([Drawing.Point]::new(5,5),[Drawing.Point]::new(21,8),[Drawing.Point]::new(8,21)));$graphics.FillPolygon($aquaBrush,[Drawing.Point[]]@([Drawing.Point]::new(59,59),[Drawing.Point]::new(43,56),[Drawing.Point]::new(56,43))) }
        "nesw" { $graphics.DrawLine($deepPen,53,11,11,53);$graphics.DrawLine($foamPen,50,14,14,50);$graphics.FillPolygon($seaBrush,[Drawing.Point[]]@([Drawing.Point]::new(59,5),[Drawing.Point]::new(43,8),[Drawing.Point]::new(56,21)));$graphics.FillPolygon($aquaBrush,[Drawing.Point[]]@([Drawing.Point]::new(5,59),[Drawing.Point]::new(21,56),[Drawing.Point]::new(8,43))) }
        "up" { $graphics.DrawLine($deepPen,32,14,32,58);$graphics.FillPolygon($seaBrush,[Drawing.Point[]]@([Drawing.Point]::new(32,2),[Drawing.Point]::new(17,21),[Drawing.Point]::new(47,21)));$graphics.DrawArc($aquaPen,17,36,30,18,10,160) }
        "no" { Add-MarineArrow $graphics 0.72;$graphics.DrawEllipse($deepPen,25,25,34,34);$graphics.DrawEllipse($aquaPen,28,28,28,28);$graphics.DrawLine($aquaPen,33,33,51,51) }
        "pen" { $graphics.DrawLine($deepPen,10,54,49,15);$graphics.DrawLine($aquaPen,13,51,47,17);$graphics.FillPolygon($seaBrush,[Drawing.Point[]]@([Drawing.Point]::new(49,9),[Drawing.Point]::new(56,16),[Drawing.Point]::new(48,23),[Drawing.Point]::new(41,16)));$graphics.FillPolygon($foamBrush,[Drawing.Point[]]@([Drawing.Point]::new(7,58),[Drawing.Point]::new(13,44),[Drawing.Point]::new(21,52))) }
    }
    [IO.File]::WriteAllBytes((Join-Path $cursorDir $name),(Convert-ToCursorBytes $bitmap $hotX $hotY))
    foreach($item in @($foamBrush,$aquaBrush,$seaBrush,$aquaPen,$foamPen,$deepPen,$graphics,$bitmap)){$item.Dispose()}
}

function Add-BubbleSpinner([Drawing.Graphics]$graphics,[int]$cx,[int]$cy,[int]$frame) {
    for($i=0;$i -lt 8;$i++){$angle=($i*45-90)*[Math]::PI/180;$x=$cx+17*[Math]::Cos($angle);$y=$cy+17*[Math]::Sin($angle);$alpha=55+(($i-$frame+8)%8)*22;$brush=New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb([Math]::Min(240,$alpha),$(if($i -eq $frame){$seafoam}else{$aqua})));$size=if($i -eq $frame){9}else{6};$graphics.FillEllipse($brush,[float]($x-$size/2),[float]($y-$size/2),$size,$size);$brush.Dispose()}
}

function New-MarineAni([string]$name,[bool]$withArrow) {
    $frames=@();for($frame=0;$frame -lt 8;$frame++){$canvas=New-Canvas;$bitmap=$canvas[0];$graphics=$canvas[1];if($withArrow){Add-MarineArrow $graphics 0.70;Add-BubbleSpinner $graphics 47 45 $frame}else{Add-BubbleSpinner $graphics 32 32 $frame};$frames+=,(Convert-ToCursorBytes $bitmap $(if($withArrow){5}else{32}) $(if($withArrow){4}else{32}));$graphics.Dispose();$bitmap.Dispose()}
    $stream=New-Object IO.MemoryStream;$writer=New-Object IO.BinaryWriter $stream;$writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF"));$writer.Write([UInt32]0);$writer.Write([Text.Encoding]::ASCII.GetBytes("ACON"));$writer.Write([Text.Encoding]::ASCII.GetBytes("anih"));$writer.Write([UInt32]36);foreach($value in @([UInt32]36,[UInt32]8,[UInt32]8,[UInt32]64,[UInt32]64,[UInt32]32,[UInt32]1,[UInt32]6,[UInt32]3)){$writer.Write($value)};$writer.Write([Text.Encoding]::ASCII.GetBytes("rate"));$writer.Write([UInt32]32);0..7|ForEach-Object{$writer.Write([UInt32]6)};$writer.Write([Text.Encoding]::ASCII.GetBytes("seq "));$writer.Write([UInt32]32);0..7|ForEach-Object{$writer.Write([UInt32]$_)}
    $listStream=New-Object IO.MemoryStream;$listWriter=New-Object IO.BinaryWriter $listStream;$listWriter.Write([Text.Encoding]::ASCII.GetBytes("fram"));foreach($cursor in $frames){$listWriter.Write([Text.Encoding]::ASCII.GetBytes("icon"));$listWriter.Write([UInt32]$cursor.Length);$listWriter.Write($cursor);if($cursor.Length%2){$listWriter.Write([byte]0)}};$listWriter.Flush();$list=$listStream.ToArray();$listWriter.Dispose();$listStream.Dispose();$writer.Write([Text.Encoding]::ASCII.GetBytes("LIST"));$writer.Write([UInt32]$list.Length);$writer.Write($list);if($list.Length%2){$writer.Write([byte]0)};$writer.Flush();$length=$stream.Length;$stream.Position=4;$writer.Write([UInt32]($length-8));$writer.Flush();[IO.File]::WriteAllBytes((Join-Path $cursorDir $name),$stream.ToArray());$writer.Dispose();$stream.Dispose()
}

function New-AquaticSound([string]$name,[double[]]$notes,[int]$milliseconds) {
    $sampleRate=44100;$samples=New-Object 'Collections.Generic.List[Int16]';$noteIndex=0
    foreach($frequency in $notes){$count=[int]($sampleRate*$milliseconds/1000);for($i=0;$i -lt $count;$i++){$time=$i/$sampleRate;$progress=$i/[Math]::Max(1,$count-1);$attack=[Math]::Min(1,$i/600);$release=[Math]::Min(1,($count-$i)/1800);$envelope=$attack*$release*[Math]::Exp(-2.7*$progress);$wave=[Math]::Sin(2*[Math]::PI*$frequency*$time);$bubble=0.22*[Math]::Sin(2*[Math]::PI*($frequency*(1.95+0.08*$progress))*$time)*[Math]::Exp(-6*$progress);$chime=0.16*[Math]::Sin(2*[Math]::PI*$frequency*2.5*$time);$foam=0.045*[Math]::Sin(2*[Math]::PI*37*$time)*[Math]::Sin(2*[Math]::PI*($frequency*4.1)*$time);$value=($wave+$bubble+$chime+$foam)*$envelope;$samples.Add([Int16]([Math]::Max(-32767,[Math]::Min(32767,3900*$value))))};1..300|ForEach-Object{$samples.Add([Int16]0)};$noteIndex++}
    $stream=New-Object IO.MemoryStream;$writer=New-Object IO.BinaryWriter $stream;$length=$samples.Count*2;$writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF"));$writer.Write([UInt32](36+$length));$writer.Write([Text.Encoding]::ASCII.GetBytes("WAVE"));$writer.Write([Text.Encoding]::ASCII.GetBytes("fmt "));$writer.Write([UInt32]16);$writer.Write([UInt16]1);$writer.Write([UInt16]1);$writer.Write([UInt32]$sampleRate);$writer.Write([UInt32]($sampleRate*2));$writer.Write([UInt16]2);$writer.Write([UInt16]16);$writer.Write([Text.Encoding]::ASCII.GetBytes("data"));$writer.Write([UInt32]$length);foreach($sample in $samples){$writer.Write($sample)};$writer.Flush();[IO.File]::WriteAllBytes((Join-Path $soundDir $name),$stream.ToArray());$writer.Dispose();$stream.Dispose()
}

function Save-Jpeg([Drawing.Image]$image,[string]$path,[long]$quality=93) {$codec=[Drawing.Imaging.ImageCodecInfo]::GetImageEncoders()|Where-Object{$_.MimeType -eq 'image/jpeg'}|Select-Object -First 1;$parameters=New-Object Drawing.Imaging.EncoderParameters 1;$parameters.Param[0]=New-Object Drawing.Imaging.EncoderParameter ([Drawing.Imaging.Encoder]::Quality),$quality;try{$image.Save($path,$codec,$parameters)}finally{$parameters.Param[0].Dispose();$parameters.Dispose()}}

function New-CoverImage([string]$sourcePath,[int]$width,[int]$height,[string]$destination) {
    $source=[Drawing.Image]::FromFile($sourcePath);try{$targetRatio=$width/[double]$height;$sourceRatio=$source.Width/[double]$source.Height;if($sourceRatio -gt $targetRatio){$cropHeight=$source.Height;$cropWidth=[int]($cropHeight*$targetRatio);$cropX=[int](($source.Width-$cropWidth)/2);$cropY=0}else{$cropWidth=$source.Width;$cropHeight=[int]($cropWidth/$targetRatio);$cropX=0;$cropY=[int](($source.Height-$cropHeight)/2)};$target=New-Object Drawing.Bitmap $width,$height;try{$graphics=[Drawing.Graphics]::FromImage($target);try{$graphics.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality;$graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$graphics.DrawImage($source,[Drawing.Rectangle]::new(0,0,$width,$height),[Drawing.Rectangle]::new($cropX,$cropY,$cropWidth,$cropHeight),[Drawing.GraphicsUnit]::Pixel)}finally{$graphics.Dispose()};if([IO.Path]::GetExtension($destination) -eq ".png"){$target.Save($destination,[Drawing.Imaging.ImageFormat]::Png)}else{Save-Jpeg $target $destination}}finally{$target.Dispose()}}finally{$source.Dispose()}
}

$wallpapers=@(
    @("Costa","JANUS-WhiteSur-Costa-source.png"),@("Boracay","WhiteSur-Boracay-source.jpg"),@("KohLipe","WhiteSur-KohLipe-source.jpg"),
    @("Perhentian","WhiteSur-Perhentian-source.jpg"),@("Guam","WhiteSur-Guam-source.jpg"),@("Florida","WhiteSur-Florida-source.jpg"),@("KohPoda","WhiteSur-KohPoda-source.jpg")
)
foreach($wallpaper in $wallpapers){$source=Join-Path $sourceDir $wallpaper[1];if(!(Test-Path -LiteralPath $source)){throw "Falta la fuente $($wallpaper[1])"};New-CoverImage $source 3840 2160 (Join-Path $wallpaperDir ("JANUS-WhiteSur-"+$wallpaper[0]+"-4K.jpg"));New-CoverImage $source 5120 2160 (Join-Path $wallpaperDir ("JANUS-WhiteSur-"+$wallpaper[0]+"-Ultrawide-5K.jpg"))}
foreach($variant in @("Costa","Boracay","KohPoda")){New-CoverImage (Join-Path $wallpaperDir ("JANUS-WhiteSur-"+$variant+"-4K.jpg")) 960 540 (Join-Path $previewDir ("JANUS-WhiteSur-"+$variant+"-preview.png"))}

foreach($cursor in @(@("arrow","arrow",7,5),@("help","help",7,5),@("hand","hand",32,18),@("ibeam","ibeam",32,32),@("cross","cross",32,32),@("move","move",32,32),@("size-ns","ns",32,32),@("size-we","we",32,32),@("size-nwse","nwse",32,32),@("size-nesw","nesw",32,32),@("up","up",32,3),@("no","no",7,5),@("pen","pen",9,56))){New-MarineCursor ("whitesur-"+$cursor[0]+".cur") $cursor[1] $cursor[2] $cursor[3]}
New-MarineAni "whitesur-working.ani" $true;New-MarineAni "whitesur-busy.ani" $false

New-AquaticSound "whitesur-notify.wav" @(659.25,783.99,987.77) 80
New-AquaticSound "whitesur-question.wav" @(523.25,659.25,880.00) 95
New-AquaticSound "whitesur-warning.wav" @(440.00,392.00,349.23) 105
New-AquaticSound "whitesur-error.wav" @(329.63,293.66,261.63) 125
New-AquaticSound "whitesur-complete.wav" @(523.25,659.25,783.99,1046.50) 75
New-AquaticSound "whitesur-start.wav" @(392.00,523.25,659.25,783.99,987.77) 90
New-AquaticSound "whitesur-logon.wav" @(523.25,659.25,783.99) 95
New-AquaticSound "whitesur-logoff.wav" @(783.99,659.25,523.25) 95
New-AquaticSound "whitesur-exit.wav" @(659.25,523.25,440.00,349.23) 100

$baseIcons=Join-Path $repo "assets\theme-packs\whitesur"
foreach($baseName in @("este-equipo","archivos-usuario","red","papelera-vacia","papelera-llena","documentos","descargas","escritorio","imagenes","musica","videos","hdd-ssd","usb","unidad-red")){Copy-Item -LiteralPath (Join-Path $baseIcons ($baseName+".ico")) -Destination (Join-Path $iconDir ($baseName+".ico")) -Force;Copy-Item -LiteralPath (Join-Path $baseIcons ($baseName+".png")) -Destination (Join-Path $iconDir ($baseName+".png")) -Force}
foreach($document in @("TEMA.txt","ATRIBUCION.txt","LICENSE.txt")){if(Test-Path -LiteralPath (Join-Path $baseIcons $document)){Copy-Item -LiteralPath (Join-Path $baseIcons $document) -Destination (Join-Path $iconDir $document) -Force}}

$manifest=[ordered]@{id="janus-whitesur";version=1;displayName="JANUS WhiteSur";publisher="Momocrackcorp";variants=@($wallpapers|ForEach-Object{$_[0]});previewVariants=@("Costa","Boracay","KohPoda");components=@("wallpapers","colors","cursors","sounds");baseColor="#647C64";accentColor="#00B7C3";slideshowIntervalMilliseconds=1800000;shuffle=$true;preserveWindowsMode=$true;soundStyle="Original aquatic chimes, bubbles and sea foam";cursorStyle="Marine seafoam";iconCompanion="Tema-Iconos-WhiteSur.zip";safe=$true;reversible=$true}
$manifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $themeRoot "manifest.json") -Encoding UTF8

$emdash=[char]0x2014;$leftQuote=[char]0x201C;$rightQuote=[char]0x201D;$aAcute=[char]0x00E1;$eAcute=[char]0x00E9;$uAcute=[char]0x00FA;$oAcute=[char]0x00F3;$multiply=[char]0x00D7
$credits=@(
"JANUS WhiteSur $emdash cr${eAcute}ditos y licencias de fondos","",
"Costa: obra original creada para JANUS/Momocrackcorp.",
"Boracay $emdash $leftQuote`The long, calm, and shallow waters of White Sand Beach$rightQuote, Kstranger, CC0 1.0.",
"https://commons.wikimedia.org/wiki/File:The_long,_calm,_and_shallow_waters_of_White_Sand_Beach.jpg",
"Koh Lipe $emdash $leftQuote`Koh Lipe (island), Tropical lagoon, Thailand$rightQuote, Vyacheslav Argenberg, CC BY 4.0.",
"https://commons.wikimedia.org/wiki/File:Koh_Lipe_(island),_Tropical_lagoon,_Thailand.jpg",
"Perhentian $emdash $leftQuote`Perhentian Kecil Island, Malaysia, Tropical lagoon$rightQuote, Vyacheslav Argenberg, CC BY 4.0.",
"https://commons.wikimedia.org/wiki/File:Perhentian_Kecil_Island,_Malaysia,_Tropical_lagoon.jpg",
"Guam $emdash $leftQuote`A magnificent white sand beach on Guam$rightQuote, David Burdick/NOAA, dominio p${uAcute}blico de Estados Unidos.",
"https://commons.wikimedia.org/wiki/File:A_magnificent_white_sand_beach_on_Guam_(line378229763).jpg",
"Florida $emdash $leftQuote`Clear blue water$rightQuote, Kurtkaiser, CC0 1.0.",
"https://commons.wikimedia.org/wiki/File:Clear_blue_water_.jpg",
"Koh Poda $emdash $leftQuote`White sand beach, Koh Poda tropical archipelago, Krabi, Thailand$rightQuote, Vyacheslav Argenberg, CC BY 4.0.",
"https://commons.wikimedia.org/wiki/File:White_sand_beach,_Koh_Poda_tropical_archipelago,_Krabi,_Thailand.jpg","",
"Modificaciones: recorte centrado y redimensionado a 3840${multiply}2160 y 5120${multiply}2160; no se alter${oAcute} el contenido mediante generaci${oAcute}n autom${aAcute}tica.",
"Licencia CC BY 4.0: https://creativecommons.org/licenses/by/4.0/",
"CC0 1.0: https://creativecommons.org/publicdomain/zero/1.0/"
) -join [Environment]::NewLine
Set-Content -LiteralPath (Join-Path $themeRoot "CREDITOS-FONDOS.txt") -Value $credits -Encoding UTF8

$themeZip=Join-Path $dist "JANUS-WhiteSur-v1.zip";$iconsZip=Join-Path $dist "Tema-Iconos-WhiteSur.zip"
Remove-Item -LiteralPath $themeZip,$iconsZip -Force -ErrorAction SilentlyContinue
$archive=[IO.Compression.ZipFile]::Open($themeZip,[IO.Compression.ZipArchiveMode]::Create)
try{Get-ChildItem -LiteralPath $themeRoot -File -Recurse|Where-Object{$_.FullName -notlike "$sourceDir\*" -and $_.FullName -notlike "$iconDir\*"}|ForEach-Object{$relative=$_.FullName.Substring($themeRoot.Length+1).Replace("\","/");[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}}finally{$archive.Dispose()}
Compress-Archive -Path (Join-Path $iconDir "*") -DestinationPath $iconsZip -CompressionLevel Optimal -Force
Get-FileHash $themeZip,$iconsZip -Algorithm SHA256|Select-Object Path,Hash
