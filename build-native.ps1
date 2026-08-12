$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $compiler)) { $compiler = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $compiler)) { throw "No se encontró el compilador de .NET Framework incluido con Windows." }
$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Path $dist -Force | Out-Null
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /win32icon:"$root\assets\migrador-seguro.ico" /out:"$dist\MigradorSeguro.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "$root\src\MigradorSeguro.cs"
if ($LASTEXITCODE -ne 0) { throw "Falló la compilación." }
Write-Host "Ejecutable creado: $dist\MigradorSeguro.exe"
