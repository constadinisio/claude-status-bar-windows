# Contributing

Thanks for your interest in improving **Claude Status Bar for Windows**! This guide covers how to build, test, and develop the app and its hooks.

> Documentation is bilingual (English + Spanish), but contributions, issues, and PRs are handled in English to keep things accessible to the wider Claude Code community.

## Prerequisites

| Tool | Version | Used for |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.0+ | Building and testing the C# app |
| [Node.js](https://nodejs.org) | recent LTS (tested on v24) | Running and developing the hooks |
| Windows | 11 recommended | Taskbar-embed code paths only exercise fully on Win11 |
| [`vpk`](https://velopack.io) | latest | Packaging releases (installed automatically by `build/pack.ps1`) |

## Project layout

```
app/
  src/ClaudeStatusBar/        WinForms + WPF app (.NET 9, net9.0-windows)
    Core/                     State model, polling, JSON, paths
    Interop/                  Win32 P/Invoke: taskbar locate + window embed
    Render/                   IStatusRenderer + Embedded/Tray renderers, icons
    App/                      Tray context, autostart (HKCU Run key)
    Program.cs                Entry point, single-instance mutex, Velopack/update
  tests/ClaudeStatusBar.Tests/  xUnit unit tests
hooks/                        Node.js hooks (state writers + lifecycle + install)
  update.js                   Maps hook events → state.json (reused from upstream)
  lifecycle.js                SessionStart/End: launch app, track sessions
  install.js / uninstall.js   Merge/remove hooks in ~/.claude/settings.json
  hooks.json                  Plugin hook manifest (uses CLAUDE_PLUGIN_ROOT)
build/                        publish.ps1 (single-file exe) + pack.ps1 (Velopack)
.claude-plugin/               plugin.json + marketplace.json
docs/                         specs, plans, and UI assets
```

## Build the app

Self-contained single-file build (no .NET runtime needed to run the output):

```powershell
powershell -File build/publish.ps1
```

Output lands in `build/publish/ClaudeStatusBar.exe`. Run it directly to test locally.

For a full Velopack installer + update packages:

```powershell
powershell -File build/pack.ps1
```

Artifacts land in `build/Releases/` (`…-Setup.exe`, `…-Portable.zip`, `…-full.nupkg`).

## Run the tests

```powershell
dotnet test app/ClaudeStatusBar.sln
```

Please keep tests green and add coverage for new behavior (the suite uses xUnit and lives in `app/tests/ClaudeStatusBar.Tests`).

## Develop the hooks

The hooks are plain Node.js scripts. The state writer (`update.js`) reads a hook JSON payload on stdin and writes `~/.claude/statusbar/state.json`. You can exercise it without Claude Code by piping a synthetic payload:

```bash
echo '{"session_id":"s1","cwd":"C:/demo","hook_event_name":"PreToolUse","tool_name":"Bash","tool_input":{}}' | node hooks/update.js pre
```

Then inspect `~/.claude/statusbar/state.json`. Set `CLAUDE_STATUSBAR_DEBUG=1` to log every invocation to `~/.claude/statusbar/hooks.log`.

To test the full install/uninstall flow against your real `~/.claude/settings.json`:

```bash
node hooks/install.js     # idempotent — safe to run repeatedly
node hooks/uninstall.js   # restores settings.json; leaves other hooks intact
```

A one-time backup is written to `~/.claude/settings.json.bak-statusbar` on first install.

### Hook conventions

- **Always use exec form** for hook commands (`{"type":"command","command":"node","args":[script, event]}`), never shell strings — this avoids the unquoted-path bug when `node.exe` or script paths contain spaces (e.g. `C:\Program Files\nodejs\node.exe`).
- **Writes must be atomic** — write to a temp file then `rename` so a crash mid-write can't corrupt `state.json` or `settings.json`.
- **Never clobber user data** — merge into `settings.json`, strip only our own entries (matched by the `~/.claude/statusbar` marker path).

## Coding conventions

- C#: nullable enabled, implicit usings, small focused files. Follow the patterns already in `Core/`, `Interop/`, and `Render/`.
- Prefer immutable records for state (`AppState` is a `record`).
- Keep Win32 interop isolated in `Interop/`.

## Commit & PR process

- **Conventional commits:** `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `chore:`, `perf:`, `ci:`.
- Keep PRs focused; describe the change and how you tested it.
- Run `dotnet test` before pushing.

## Cutting a release

1. Bump the version in `.claude-plugin/plugin.json`, `.claude-plugin/marketplace.json`, and `build/pack.ps1` (`--packVersion`).
2. `powershell -File build/pack.ps1`
3. Publish the artifacts in `build/Releases/` to a GitHub Release whose tag matches the version, e.g.:
   ```bash
   gh release create v0.1.0 build/Releases/* --title "v0.1.0" --notes "…"
   ```
   Velopack's `GithubSource` auto-update points at this repo's releases, so the tag and assets must be published for updates to flow.
