param([int]$X, [int]$Y, [string]$ProcessName = "DTDTrainer")
Add-Type @"
using System; using System.Text; using System.Runtime.InteropServices;
public static class WinClick {
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern IntPtr ChildWindowFromPointEx(IntPtr parent, POINT p, uint flags);
  [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr h, ref POINT p);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[WinClick]::SetProcessDPIAware() | Out-Null

$foundHwnd = [IntPtr]::Zero
$timeout = 10
$sw = [System.Diagnostics.Stopwatch]::StartNew()
while ($sw.Elapsed.TotalSeconds -lt $timeout -and $foundHwnd -eq [IntPtr]::Zero) {
    [WinClick]::EnumWindows({
        param($h, $lp)
        if ([WinClick]::IsWindowVisible($h)) {
            $sb = New-Object System.Text.StringBuilder 256
            [WinClick]::GetWindowText($h, $sb, 256) | Out-Null
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

$top = $foundHwnd
$r = New-Object WinClick+RECT
[WinClick]::GetWindowRect($top, [ref]$r) | Out-Null
$screen = New-Object WinClick+POINT
$screen.X = $r.L + $X
$screen.Y = $r.T + $Y
$h = $top
while ($true) {
  $pt = $screen; [WinClick]::ScreenToClient($h, [ref]$pt) | Out-Null
  $c = [WinClick]::ChildWindowFromPointEx($h, $pt, 1)  # CWP_SKIPINVISIBLE
  if ($c -eq [IntPtr]::Zero -or $c -eq $h) { break }
  $h = $c
}
$pt = $screen; [WinClick]::ScreenToClient($h, [ref]$pt) | Out-Null
$l = [IntPtr](($pt.Y -shl 16) -bor ($pt.X -band 0xFFFF))
[WinClick]::PostMessage($h, 0x0201, [IntPtr]1, $l) | Out-Null
Start-Sleep -Milliseconds 60
[WinClick]::PostMessage($h, 0x0202, [IntPtr]0, $l) | Out-Null
"clicked hwnd=$h at client $($pt.X),$($pt.Y)"
