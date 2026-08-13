$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$source = Join-Path $root "assets\janus-icons"
$dist = Join-Path $root "dist"
$target = Join-Path $dist "Tema-Iconos-JANUS-v2.2.zip"

New-Item -ItemType Directory -Path $dist -Force | Out-Null
if (Test-Path $target) { Remove-Item -LiteralPath $target -Force }

$files = Get-ChildItem -LiteralPath $source -File | Where-Object {
  $_.Extension -in ".png", ".ico", ".txt"
}
Compress-Archive -LiteralPath $files.FullName -DestinationPath $target -CompressionLevel Optimal
Write-Host "Paquete creado: $target"
