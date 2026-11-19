#!/usr/bin/env bash
# Runs on a booted emulator: installs the sample and screenshots each sheet. README images go to screenshots/,
# checks (presentations from inside a sheet must appear above it) to checks/.
set -euo pipefail
pkg=com.sebfrie.plugin.maui.sheet.sample
apk=$(find samples -name "$pkg-Signed.apk" | head -1)
mkdir -p screenshots checks

adb install -r "$apk"
# A clean status bar: fixed clock, no notification icons.
adb shell settings put global sysui_demo_allowed 1
adb shell am broadcast -a com.android.systemui.demo -e command enter
adb shell am broadcast -a com.android.systemui.demo -e command clock -e hhmm 0941
adb shell am broadcast -a com.android.systemui.demo -e command notifications -e visible false

launch() {
	adb shell am force-stop "$pkg"
	adb shell am start -W -n "$pkg/$pkg.MainActivity" --es SAMPLE_SHEET "$1"
	sleep 15
}

# Taps the element with the given content description or text.
tap() {
	adb shell uiautomator dump /sdcard/ui.xml > /dev/null
	adb exec-out cat /sdcard/ui.xml | python3 -c '
import re, sys
for node in re.findall(r"<node [^>]*>", sys.stdin.read()):
    if f"content-desc=\"{sys.argv[1]}\"" in node or f"text=\"{sys.argv[1]}\"" in node:
        x1, y1, x2, y2 = map(int, re.search(r"bounds=\"\[(\d+),(\d+)\]\[(\d+),(\d+)\]\"", node).groups())
        print((x1 + x2) // 2, (y1 + y2) // 2)
        break
else:
    sys.exit(f"{sys.argv[1]} not found")' "$1" | xargs adb shell input tap
	sleep 3
}

for sheet in form list passthrough nav nav-pushed nav-picked; do
	launch "$sheet"
	adb exec-out screencap -p > "screenshots/android-$sheet.png"
done

launch nav
tap "More options"
adb exec-out screencap -p > screenshots/android-nav-overflow.png

for check in nav-alert nav-modal nav-date; do
	launch "$check"
	adb exec-out screencap -p > "checks/android-$check.png"
done
