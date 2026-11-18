#!/usr/bin/env bash
# Boots an iPhone simulator, installs the sample and screenshots each sheet.
set -euo pipefail
bundle=com.sebfrie.plugin.maui.sheet.sample
app=$(find samples -path "*iossimulator-arm64/Plugin.Maui.Sheet.Sample.app" -type d | head -1)
mkdir -p screenshots

# Newest iOS runtime, preferring a non-Max "Pro" iPhone.
udid=$(xcrun simctl list devices available -j | python3 -c '
import json, sys
devices = json.load(sys.stdin)["devices"]
runtimes = sorted((r for r in devices if ".iOS-" in r), key=lambda r: [int(p) for p in r.rsplit("iOS-", 1)[1].split("-")])
phones = [d for d in devices[runtimes[-1]] if d["name"].startswith("iPhone")]
pro = [d for d in phones if "Pro" in d["name"] and "Max" not in d["name"]]
print((pro or phones)[0]["udid"])')

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
