param(
  [ValidateSet("janus", "crux", "newaita", "papirus", "whitesur")]
  [string]$Theme = "janus"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$themeNames = @{
  janus = "JANUS-v2.2"
  crux = "Crux"
  newaita = "Newaita"
  papirus = "Papirus"
  whitesur = "WhiteSur"
}
$source = if ($Theme -eq "janus") {
  Join-Path $root "assets\janus-icons"
} else {
  Join-Path $root ("assets\theme-packs\" + $Theme)
}
$dist = Join-Path $root "dist"
$target = Join-Path $dist ("Tema-Iconos-" + $themeNames[$Theme] + ".zip")

if (-not (Test-Path -LiteralPath $source)) {
  throw "No existe la carpeta fuente del tema: $source"
}

New-Item -ItemType Directory -Path $dist -Force | Out-Null
if (Test-Path $target) { Remove-Item -LiteralPath $target -Force }

$files = Get-ChildItem -LiteralPath $source -File | Where-Object {
  $_.Extension -in ".png", ".ico", ".txt"
}
if ($files.Count -lt 28) {
  throw "El tema debe contener los 14 iconos en PNG e ICO, además de sus archivos de atribución."
}
Compress-Archive -LiteralPath $files.FullName -DestinationPath $target -CompressionLevel Optimal
Write-Host "Paquete creado: $target"
