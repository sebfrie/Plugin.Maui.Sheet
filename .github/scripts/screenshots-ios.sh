#!/usr/bin/env bash
# Boots an iPhone simulator, installs the sample and screenshots each sheet. README images go to screenshots/,
# checks (presentations from inside a sheet must appear above it) to checks/.
set -euo pipefail
bundle=com.sebfrie.plugin.maui.sheet.sample
app=$(find samples -path "*iossimulator-arm64/Plugin.Maui.Sheet.Sample.app" -type d | head -1)
mkdir -p screenshots checks

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

# simctl occasionally aborts inside CoreSimulator; a retry gets the screenshot.
shot() {
	for attempt in 1 2 3; do
		xcrun simctl io "$udid" screenshot "$1" && return
		sleep 5
	done
	return 1
}

launch() {
	xcrun simctl terminate "$udid" "$bundle" || true
	SIMCTL_CHILD_SAMPLE_SHEET="$1" xcrun simctl launch "$udid" "$bundle"
	sleep 15
}

for sheet in form list passthrough nav nav-pushed nav-picked; do
	launch "$sheet"
	shot "screenshots/ios-$sheet.png"
done

for check in nav-alert nav-modal nav-date; do
	launch "$check"
	shot "checks/ios-$check.png"
done

# simctl cannot tap; the overflow menu needs idb (installed best-effort by the workflow). idb finds the button
# through accessibility when it can, else taps where it sits on an iPhone 18 Pro (points, top right of the sheet).
if command -v idb > /dev/null; then
	launch nav
	idb connect "$udid" || true
	point=$(idb ui describe-all --udid "$udid" --json 2> /dev/null | python3 -c '
import json, sys
for e in json.load(sys.stdin):
    if e.get("AXUniqueId") == "NativeSheetOverflow" or e.get("AXLabel") == "More":
        f = e["frame"]
        print(f["x"] + f["width"] / 2, f["y"] + f["height"] / 2)
        break
else:
    sys.exit(1)' 2> /dev/null) || point="362 100"
	if idb ui tap --udid "$udid" $point; then
		sleep 3
		shot screenshots/ios-nav-overflow.png
	else
		echo "Skipped the overflow screenshot."
	fi
fi
