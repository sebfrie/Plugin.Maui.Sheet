#!/usr/bin/env bash
# Boots an iPhone simulator, installs the sample and screenshots each sheet.
set -euo pipefail
bundle=com.sebfrie.plugin.maui.sheet.sample
app=$(find samples -path "*iossimulator-arm64/Plugin.Maui.Sheet.Sample.app" -type d | head -1)
mkdir -p screenshots

# An iPhone 18 model (e.g. "iPhone 18 Pro"; shortest name wins, so not the Max) on the newest iOS runtime. Runtime ids look like "...SimRuntime.iOS-27-2"; some carry extra
# device-specific suffixes, so only the leading version numbers are read.
udid=$(xcrun simctl list devices available -j | python3 -c '
import json, re, sys
devices = json.load(sys.stdin)["devices"]
def version(runtime):
    m = re.search(r"\.iOS-(\d+)(?:-(\d+))?", runtime)
    return (int(m.group(1)), int(m.group(2) or 0)) if m else None
phones = [(version(r), d) for r, ds in devices.items() if version(r) for d in ds if d["name"].startswith("iPhone 18")]
if not phones:
    sys.exit("No iPhone 18 simulator. Available: " + ", ".join(sorted({d["name"] for ds in devices.values() for d in ds})))
print(max(phones, key=lambda p: (p[0], -len(p[1]["name"])))[1]["udid"])')

xcrun simctl boot "$udid"
xcrun simctl bootstatus "$udid"
xcrun simctl status_bar "$udid" override --time 9:41 --batteryState charged --batteryLevel 100 --cellularBars 4 --wifiBars 3
xcrun simctl install "$udid" "$app"

for sheet in form list passthrough; do
	xcrun simctl terminate "$udid" "$bundle" || true
	SIMCTL_CHILD_SAMPLE_SHEET="$sheet" xcrun simctl launch "$udid" "$bundle"
	sleep 15
	xcrun simctl io "$udid" screenshot "screenshots/ios-$sheet.png"
done
