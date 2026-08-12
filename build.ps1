$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$venv = Join-Path $root ".venv"
if (-not (Get-Command py -ErrorAction SilentlyContinue)) {
    throw "Instala Python 3.11 o superior desde python.org y vuelve a ejecutar este archivo."
}
py -3 -m venv $venv
& "$venv\Scripts\python.exe" -m pip install --upgrade pip pyinstaller
& "$venv\Scripts\pyinstaller.exe" --noconfirm --clean --onefile --windowed --name "MigradorSeguro" --paths $root "$root\run.py"
Write-Host "Ejecutable creado en: $root\dist\MigradorSeguro.exe"
