Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap(64,64)
$g = [System.Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)
$body = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(85,94,106))
$frame = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(210,215,222),3)
$screen = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(86,151,183))
foreach($p in @(@(4,17,31,23),@(29,8,31,23),@(20,32,36,23))) {
  $g.FillRectangle($body,$p[0],$p[1],$p[2],$p[3])
  $g.DrawRectangle($frame,$p[0],$p[1],$p[2],$p[3])
  $g.FillRectangle($screen,$p[0]+4,$p[1]+4,$p[2]-8,$p[3]-9)
  $g.DrawLine($frame,$p[0]+15,$p[1]+$p[3],$p[0]+15,$p[1]+$p[3]+3)
}
$g.Dispose()
$handle=$bitmap.GetHicon()
try {
  $ico=[System.Drawing.Icon]::FromHandle($handle)
  $stream=[System.IO.File]::Create((Join-Path $PSScriptRoot 'janus-dock.ico'))
  try { $ico.Save($stream) } finally { $stream.Dispose() }
} finally {
  Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public static class IconRelease { [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h); }'
  [IconRelease]::DestroyIcon($handle) | Out-Null
  $bitmap.Dispose()
}
