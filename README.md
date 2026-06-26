# Claude Status Bar for Windows

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
![Platform: Windows 11](https://img.shields.io/badge/Platform-Windows%2011-0078D6.svg)
![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4.svg)

> Bring [Claude Code](https://claude.com/claude-code)'s live status into your Windows 11 taskbar — an animated icon and label that reflect what Claude is doing in real time.

🇪🇸 **[Leé esta documentación en español →](README.es.md)**

<!-- Replace this note with a real screenshot once captured (see docs/assets/.gitkeep). -->
> 📸 _Screenshot coming soon — the widget lives inside the taskbar, next to the clock._

This is a Windows port of the macOS [claude-status-bar](https://github.com/m1ckc3s/claude-status-bar) concept. Instead of a menu-bar item, it embeds a small widget directly into the Windows 11 taskbar (`Shell_TrayWnd`), with automatic fallback to a notification-area (tray) icon when embedding isn't possible.

## Features

- **Live status** — reflects Claude Code's current activity: idle, thinking, running a tool, or waiting for your permission.
- **Descriptive labels** — shows what's happening (`Running command`, `Editing`, `Reading`, `Searching`, `Browsing web`, …).
- **Animated icons** — a subtle pulse animation while Claude is busy.
- **Taskbar-embedded** — sits inside the Windows 11 taskbar, not a floating window.
- **Graceful fallback** — drops to a classic tray icon if the taskbar embed fails (older Windows, shell replacements, Explorer restarts).
- **Self-managing** — launches when a Claude session starts and quits itself when all sessions end.
- **Completion chime** — optional sound when a turn longer than ~1 minute finishes (off by default; toggle from the menu).
- **Auto-update** — ships with [Velopack](https://velopack.io); updates install silently in the background.
- **Start with Windows** — optional autostart toggle.

## How it works

```
Claude Code hooks  ──►  ~/.claude/statusbar/state.json  ──►  ClaudeStatusBar.exe
   (Node.js)              (atomic writes)                      (polls every 400 ms)
                                                                      │
                                                       ┌──────────────┴───────────────┐
                                                       ▼                               ▼
                                            EmbeddedRenderer                     TrayRenderer
                                       (WPF widget reparented into        (NotifyIcon fallback,
                                        Shell_TrayWnd via SetParent)       with right-click menu)
```

Claude Code [hooks](https://docs.claude.com/en/docs/claude-code/hooks) fire on every session/tool/prompt event. The Node.js hook scripts translate each event into a small `state.json` file. The app polls that file four times a second and renders the current state into the taskbar.

## Requirements

| Requirement | Notes |
|---|---|
| **Windows 11** | The embedded widget targets the Windows 11 taskbar. On Windows 10 (or if embedding fails) it automatically falls back to a tray icon. |
| **Node.js** | The hooks run via `node` (tested on v24; any recent LTS works). Must be on your `PATH`. |
| **Claude Code** | The thing whose status you're showing. |
| ~~.NET runtime~~ | **Not required** — the app is published as a self-contained single-file executable. |

## Installation

Installation is two steps: install the **app**, then wire up the **hooks**.

### Step 1 — Install the app

Download `ClaudeStatusBar-win-Setup.exe` from the [latest release](https://github.com/constadinisio/claude-status-bar-windows/releases/latest) and run it.

It installs to `%LOCALAPPDATA%\ClaudeStatusBar`, enables silent auto-updates, and adds an entry to **Apps & Features** for clean uninstall.

> **No release yet?** Until the first release is published you can build the app from source — see [CONTRIBUTING.md](CONTRIBUTING.md#build-the-app).

### Step 2 — Install the hooks

The hooks tell Claude Code to update the status file. Pick one:

#### Option A — As a Claude Code plugin (recommended)

From inside Claude Code:

```
/plugin marketplace add constadinisio/claude-status-bar-windows
/plugin install claude-status-bar-windows@claude-status-bar-windows
```

#### Option B — Manual hook install

```bash
git clone https://github.com/constadinisio/claude-status-bar-windows
cd claude-status-bar-windows
node hooks/install.js
```

This merges the status-bar hooks into `~/.claude/settings.json` (your existing hooks are preserved), copies the scripts to `~/.claude/statusbar/`, and saves a one-time backup at `~/.claude/settings.json.bak-statusbar`. It is safe to re-run — it never duplicates entries.

That's it. Start a Claude Code session and the widget appears.

## Usage

Once installed, the widget runs automatically:

- It **launches** when a Claude Code session starts and **quits itself** when all sessions end — no manual management needed.
- The icon and label update live as Claude works.

**Tray menu (fallback mode):** when running as a tray icon, right-click it for:

- **Iniciar con Windows** — toggle launching at login (writes to the `HKCU\…\Run` registry key).
- **Play Completion Sound** — toggle the completion chime (≥1 min turns).
- **Salir** — quit the app.

**Embedded widget:** right-click the widget in the taskbar for a small menu with **Play Completion Sound**. It has no quit button by design — the app self-quits when no Claude session is active. The full menu (autostart, quit) lives on the tray icon in fallback mode.

## States

| State | Meaning |
|---|---|
| **idle** | No active turn. |
| **thinking** | Claude is reasoning. |
| **tool** | Claude is running a tool (the label says which: `Running command`, `Editing`, `Reading`, …). |
| **permission** | Claude is waiting for you to approve an action. |
| **done** | A turn just finished. |

## Configuration & debugging

- **State file:** `~/.claude/statusbar/state.json`
- **Active sessions:** one file per session under `~/.claude/statusbar/sessions.d/`
- **Hook debug log:** set the environment variable `CLAUDE_STATUSBAR_DEBUG=1` to log every hook invocation to `~/.claude/statusbar/hooks.log`.

## Uninstall

1. **Remove the hooks:**
   - Plugin install: `/plugin uninstall claude-status-bar-windows@claude-status-bar-windows`
   - Manual install: `node hooks/uninstall.js` (restores `settings.json`; leaves your other hooks intact and kills the running app).
2. **Remove the app:** Windows **Settings → Apps → Installed apps → ClaudeStatusBar → Uninstall**.

## Troubleshooting

| Symptom | Likely cause & fix |
|---|---|
| No widget in the taskbar, but a tray icon appears | The embed fell back to tray mode (non-Win11 taskbar, a shell replacement, or an Explorer restart). This is expected behavior, not a crash. |
| Nothing appears at all | (1) The app isn't installed at `%LOCALAPPDATA%\ClaudeStatusBar` — re-run the installer. (2) `node` isn't on your `PATH` — the hooks can't run. (3) No Claude session is active — the app only runs while sessions exist. |
| Status never changes | The hooks aren't firing. Reinstall them (Step 2), then set `CLAUDE_STATUSBAR_DEBUG=1` and check `~/.claude/statusbar/hooks.log`. |
| Animation looks "stuck" | A session was force-closed. It self-clears on the next session start/end. |
| The app won't start at login after toggling | Some shells restrict `HKCU\…\Run`. Re-toggle **Iniciar con Windows** from the tray menu. |

## Contributing

Contributions are welcome — see [CONTRIBUTING.md](CONTRIBUTING.md) for build, test, and hook-development instructions.

## Trademarks

This is an unofficial, open-source project. **It is not affiliated with, endorsed by, or sponsored by Anthropic.** "Claude", the Claude spark logo, and the Clawd crab character are trademarks of Anthropic, used here nominatively. This project is MIT licensed, but that covers the source code only and conveys no rights to Anthropic's trademarks or brand. The icon art is ported from the upstream [m1ckc3s/claude-status-bar](https://github.com/m1ckc3s/claude-status-bar) project.

## License

[MIT](LICENSE) © 2026 Constantino Di Nisio
