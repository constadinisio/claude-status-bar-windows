#!/usr/bin/env node
// Installs the status-bar hooks into ~/.claude/settings.json (merging, never
// clobbering existing hooks) and copies update.js/lifecycle.js to ~/.claude/statusbar/.
// Re-runnable: existing status-bar hooks are stripped before re-adding.
//
// Windows port: LaunchAgent/launchctl/process.getuid() removed entirely.
// Emits exec-form hook commands to avoid the unquoted-path bug when node.exe
// or script paths contain spaces (e.g. C:\Program Files\nodejs\node.exe).

const fs   = require("fs");
const os   = require("os");
const path = require("path");

const home          = os.homedir();
const sbDir         = path.join(home, ".claude", "statusbar");
const MARKER        = sbDir; // every hook arg we add points inside this dir
const updateDest    = path.join(sbDir, "update.js");
const lifecycleDest = path.join(sbDir, "lifecycle.js");
const settingsPath  = path.join(home, ".claude", "settings.json");
const node          = process.execPath;

fs.mkdirSync(sbDir, { recursive: true });
fs.copyFileSync(path.join(__dirname, "update.js"),    updateDest);
fs.copyFileSync(path.join(__dirname, "lifecycle.js"), lifecycleDest);

// Exec form: { type:"command", command:<node>, args:[<script>, <event>] }
// This survives paths with spaces on Windows (no shell quoting needed).
const mkHook = (script, arg) => ({ type: "command", command: node, args: [script, arg] });
const cmd    = (evt) => mkHook(updateDest,    evt);
const life   = (evt) => mkHook(lifecycleDest, evt);

let settings = {};
if (fs.existsSync(settingsPath)) {
  settings = JSON.parse(fs.readFileSync(settingsPath, "utf8"));
  const bak = settingsPath + ".bak-statusbar";
  if (!fs.existsSync(bak)) fs.copyFileSync(settingsPath, bak);
}
settings.hooks = settings.hooks || {};

// Strip our hooks by checking both .command and .args for the marker path.
// (exec form puts the script path in args, not command.)
const stripOurs = (arr) =>
  (arr || [])
    .map((entry) => ({
      ...entry,
      hooks: (entry.hooks || []).filter((h) => {
        const inCmd  = (h.command || "").includes(MARKER);
        const inArgs = (h.args   || []).some((a) => String(a).includes(MARKER));
        return !inCmd && !inArgs;
      }),
    }))
    .filter((entry) => (entry.hooks || []).length > 0);

const addUnmatched = (evt, hook) => {
  settings.hooks[evt] = stripOurs(settings.hooks[evt]);
  settings.hooks[evt].push({ hooks: [hook] });
};
const addMatched = (evt, hook) => {
  settings.hooks[evt] = stripOurs(settings.hooks[evt]);
  settings.hooks[evt].push({ matcher: "*", hooks: [hook] });
};

// Status hooks (drive the animation / label)
addUnmatched("UserPromptSubmit", cmd("prompt"));
addMatched("PreToolUse",         cmd("pre"));
addMatched("PostToolUse",        cmd("post"));
addUnmatched("Notification",     cmd("notify"));
addMatched("PermissionRequest",  cmd("permreq"));
addUnmatched("Stop",             cmd("stop"));
// Lifecycle hooks (launch the app; it quits itself when no longer needed)
addUnmatched("SessionStart", life("start"));
addUnmatched("SessionEnd",   life("end"));

fs.writeFileSync(settingsPath, JSON.stringify(settings, null, 2) + "\n");
console.log("Installed status-bar hooks into", settingsPath);
console.log("Scripts:", updateDest, "and", lifecycleDest);
console.log("Backup (first run only):", settingsPath + ".bak-statusbar");
