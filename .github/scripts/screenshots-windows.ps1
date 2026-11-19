# Starts the unpackaged sample once per sheet, maximized, and screenshots its window. README images go to screenshots/,
# checks (presentations from inside a sheet) to checks/.
$ErrorActionPreference = 'Stop'
$exe = Get-ChildItem -Recurse samples -Filter Plugin.Maui.Sheet.Sample.exe | Where-Object FullName -like '*net11.0-windows*' | Select-Object -First 1
New-Item -ItemType Directory -Force screenshots, checks | Out-Null

Add-Type -AssemblyName System.Drawing, UIAutomationClient, UIAutomationTypes
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

$shots = @(
	@{ Sheet = 'form' }, @{ Sheet = 'list' }, @{ Sheet = 'passthrough' },
	@{ Sheet = 'nav' }, @{ Sheet = 'nav-pushed' }, @{ Sheet = 'nav-picked' },
	@{ Sheet = 'nav-overflow'; Invoke = 'NativeSheetOverflow' },
	@{ Sheet = 'nav-alert'; Folder = 'checks' }, @{ Sheet = 'nav-modal'; Folder = 'checks' }, @{ Sheet = 'nav-date'; Folder = 'checks' }
)
foreach ($shot in $shots) {
	$env:SAMPLE_SHEET = $shot.Sheet
	$process = Start-Process -FilePath $exe.FullName -PassThru
	for ($i = 0; $i -lt 120 -and $process.MainWindowHandle -eq 0; $i++) { Start-Sleep -Milliseconds 500; $process.Refresh() }
	$window = $process.MainWindowHandle
	[Win32]::ShowWindow($window, 3) | Out-Null # SW_MAXIMIZE
	[Win32]::SetForegroundWindow($window) | Out-Null
	Start-Sleep -Seconds 15

	if ($shot.Invoke) {
		# Clicks a header button through UI Automation, e.g. the overflow button to open its menu.
		$condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $shot.Invoke)
		$button = [System.Windows.Automation.AutomationElement]::FromHandle($window).FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
		if ($button) {
			$button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
			Start-Sleep -Seconds 3
		} else { Write-Warning "$($shot.Invoke) not found" }
	}

	$folder = if ($shot.Folder) { $shot.Folder } else { 'screenshots' }
	$rect = New-Object Win32+RECT
	[Win32]::GetWindowRect($window, [ref]$rect) | Out-Null
	$bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
	$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
	$graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
	$bitmap.Save((Join-Path (Resolve-Path $folder) "windows-$($shot.Sheet).png"), [System.Drawing.Imaging.ImageFormat]::Png)
	$graphics.Dispose(); $bitmap.Dispose()
	Stop-Process -Id $process.Id
}
