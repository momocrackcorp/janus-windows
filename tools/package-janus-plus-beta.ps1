param([string]$Version = "4.0.0-beta")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$dist = Join-Path $root "dist"
$baseName = "JANUS-Plus-$Version"
$exeName = "$baseName.exe"
$zipPath = Join-Path $dist "$baseName.zip"
$externalHashPath = Join-Path $dist "$baseName-SHA256.txt"
$stage = Join-Path $dist "package-$baseName"

& (Join-Path $root "build-native.ps1") -OutputName $exeName

$resolvedDist = (Resolve-Path -LiteralPath $dist).Path
$expectedStage = Join-Path $resolvedDist "package-$baseName"
if ($stage -ne $expectedStage) { throw "La carpeta temporal no coincide con la ruta esperada." }
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

$exePath = Join-Path $dist $exeName
Copy-Item -LiteralPath $exePath -Destination $stage
Copy-Item -LiteralPath (Join-Path $root "README.md") -Destination $stage
Copy-Item -LiteralPath (Join-Path $root "RELEASE-NOTES-4.0-BETA.md") -Destination $stage

$hash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText(
    (Join-Path $stage "SHA256SUMS.txt"),
    "$hash  $exeName`r`n",
    [System.Text.UTF8Encoding]::new($false)
)
[System.IO.File]::WriteAllText(
    $externalHashPath,
    "$hash  $exeName`r`n",
    [System.Text.UTF8Encoding]::new($false)
)

if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zipPath -CompressionLevel Optimal
Remove-Item -LiteralPath $stage -Recurse -Force

Write-Host "Ejecutable: $exePath"
Write-Host "Paquete:    $zipPath"
Write-Host "Checksum:   $externalHashPath"
Write-Host "SHA-256:    $hash"
