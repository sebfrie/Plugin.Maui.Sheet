# Starts the unpackaged sample once per sheet, maximized, and screenshots its window.
$ErrorActionPreference = 'Stop'
$exe = Get-ChildItem -Recurse samples -Filter Plugin.Maui.Sheet.Sample.exe | Where-Object FullName -like '*net11.0-windows*' | Select-Object -First 1
New-Item -ItemType Directory -Force screenshots | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32 {
	[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
	[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
	[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int command);
	[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
	[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[Win32]::SetProcessDPIAware() | Out-Null

foreach ($sheet in 'form', 'list', 'passthrough') {
	$env:SAMPLE_SHEET = $sheet
	$process = Start-Process -FilePath $exe.FullName -PassThru
	for ($i = 0; $i -lt 120 -and $process.MainWindowHandle -eq 0; $i++) { Start-Sleep -Milliseconds 500; $process.Refresh() }
	$window = $process.MainWindowHandle
	[Win32]::ShowWindow($window, 3) | Out-Null # SW_MAXIMIZE
	[Win32]::SetForegroundWindow($window) | Out-Null
	Start-Sleep -Seconds 15

	$rect = New-Object Win32+RECT
	[Win32]::GetWindowRect($window, [ref]$rect) | Out-Null
	$bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
	$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
	$graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
	$bitmap.Save((Join-Path (Resolve-Path screenshots) "windows-$sheet.png"), [System.Drawing.Imaging.ImageFormat]::Png)
	$graphics.Dispose(); $bitmap.Dispose()
	Stop-Process -Id $process.Id
}
