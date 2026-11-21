param([int]$Width = 842, [int]$Height = 1500)
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Win2 { [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool r); [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }
"@
[Win2]::SetProcessDPIAware() | Out-Null
$p = Get-Process DTDTrainer | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
[Win2]::MoveWindow($p.MainWindowHandle, 0, 0, $Width, $Height, $true) | Out-Null
