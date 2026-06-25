#!/usr/bin/env node
// Removes the status-bar hooks from ~/.claude/settings.json. Leaves all other hooks intact.
//
// Windows port: LaunchAgent/launchctl/process.getuid()/pkill removed.
// Uses taskkill to terminate the status-bar app (best-effort; ignored if not running).

const fs   = require("fs");
const os   = require("os");
const path = require("path");
const cp   = require("child_process");

const home         = os.homedir();
const MARKER       = path.join(home, ".claude", "statusbar");
const settingsPath = path.join(home, ".claude", "settings.json");

// Kill the status-bar app if running. Fail silently — it may not be running.
try { cp.execSync("taskkill /IM ClaudeStatusBar.exe /F", { stdio: "ignore" }); } catch {}

if (!fs.existsSync(settingsPath)) {
  console.log("No settings.json; nothing to do.");
  process.exit(0);
}

const settings = JSON.parse(fs.readFileSync(settingsPath, "utf8"));
for (const evt of Object.keys(settings.hooks || {})) {
  settings.hooks[evt] = (settings.hooks[evt] || [])
    .map((e) => ({
      ...e,
      hooks: (e.hooks || []).filter((h) => {
        // exec form: script path lives in args, not command — check both.
        const inCmd  = (h.command || "").includes(MARKER);
        const inArgs = (h.args   || []).some((a) => String(a).includes(MARKER));
        return !inCmd && !inArgs;
      }),
    }))
    .filter((e) => (e.hooks || []).length > 0);
  if (settings.hooks[evt].length === 0) delete settings.hooks[evt];
}
fs.writeFileSync(settingsPath, JSON.stringify(settings, null, 2) + "\n");
console.log("Removed status-bar hooks from", settingsPath);
