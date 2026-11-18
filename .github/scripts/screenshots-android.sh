#!/usr/bin/env bash
# Runs on a booted emulator: installs the sample and screenshots each sheet.
set -euo pipefail
pkg=com.sebfrie.plugin.maui.sheet.sample
apk=$(find samples -name "$pkg-Signed.apk" | head -1)
mkdir -p screenshots

adb install -r "$apk"
# A clean status bar: fixed clock, no notification icons.
adb shell settings put global sysui_demo_allowed 1
adb shell am broadcast -a com.android.systemui.demo -e command enter
adb shell am broadcast -a com.android.systemui.demo -e command clock -e hhmm 0941
adb shell am broadcast -a com.android.systemui.demo -e command notifications -e visible false

for sheet in form list passthrough; do
	adb shell am force-stop "$pkg"
	adb shell am start -W -n "$pkg/$pkg.MainActivity" --es SAMPLE_SHEET "$sheet"
	sleep 15
	adb exec-out screencap -p > "screenshots/android-$sheet.png"
done
