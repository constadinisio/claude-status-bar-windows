#!/usr/bin/env node
// Shared widget-launch helper used by lifecycle.js (SessionStart) and update.js
// (every user turn). Idempotent: it no-ops when an instance is already running, so
// it's safe to call from any hook; the app's singleton mutex is the final guard
// against duplicates.
//
// Why update.js also calls this: the auto-update flow applies a downloaded update
// when the app exits (restart:false), which can leave a live session with NO widget
// until the next SessionStart. Reviving on the next user turn closes that gap.

const fs = require("fs");
const os = require("os");
const path = require("path");
const cp = require("child_process");

const EXE_NAME = "ClaudeStatusBar.exe";
// Stable install path — does NOT use CLAUDE_PLUGIN_ROOT (changes between updates).
const EXE_PATH = path.join(process.env.LOCALAPPDATA || os.homedir(), "ClaudeStatusBar", EXE_NAME);

function running() {
  try {
    const out = cp.execSync(`tasklist /FI "IMAGENAME eq ${EXE_NAME}" /NH`, { encoding: "utf8" });
    return out.toLowerCase().includes(EXE_NAME.toLowerCase());
  } catch { return false; }
}

function launch() {
  if (running() || !fs.existsSync(EXE_PATH)) return;
  cp.spawn(EXE_PATH, [], { stdio: "ignore", detached: true, windowsHide: false }).unref();
}

module.exports = { EXE_NAME, EXE_PATH, running, launch };
