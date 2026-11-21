param([string]$ProcessName = "DTDTrainer", [string]$Out)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Text; using System.Runtime.InteropServices;
public static class Win {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[Win]::SetProcessDPIAware() | Out-Null

$foundHwnd = [IntPtr]::Zero
$timeout = 10
$sw = [System.Diagnostics.Stopwatch]::StartNew()
while ($sw.Elapsed.TotalSeconds -lt $timeout -and $foundHwnd -eq [IntPtr]::Zero) {
    [Win]::EnumWindows({
        param($h, $lp)
        if ([Win]::IsWindowVisible($h)) {
            $sb = New-Object System.Text.StringBuilder 256
            [Win]::GetWindowText($h, $sb, 256) | Out-Null
            if ($sb.ToString().Contains("DAVE THE DIVER")) {
                $script:foundHwnd = $h
                return $false
            }
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null
    if ($foundHwnd -ne [IntPtr]::Zero) { break }
    Start-Sleep -Milliseconds 200
}

if ($foundHwnd -eq [IntPtr]::Zero) { throw "Could not find DAVE THE DIVER window" }

$r = New-Object Win+RECT
[Win]::GetWindowRect($foundHwnd, [ref]$r) | Out-Null
$w = $r.R - $r.L; $h = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[Win]::PrintWindow($foundHwnd, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc); $g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
"saved $w x $h to $Out"
