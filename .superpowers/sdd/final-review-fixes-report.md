# Final Review Fixes Report

Date: 2026-06-25

## Summary

All four fixes applied, build clean (1 warning: WFO0003 only), all 25 tests pass, auto-quit verified.

---

## Fix #1 — Auto-quit when all Claude sessions end (sessions.d watcher)

**File changed:** `app/src/ClaudeStatusBar/App/TrayApplicationContext.cs`

Added:
- `private readonly System.Threading.Timer _sessionTimer;` (fires every 2000 ms, first at 3000 ms)
- `private volatile bool _seenSession;`
- `SessionTick()` method: counts files in `StatusBarPaths.SessionsDir`; sets `_seenSession = true` when count > 0; posts `ExitThread()` to UI thread when `_seenSession && count == 0`
- `_sessionTimer.Dispose()` in `Dispose(bool disposing)`

```csharp
private void SessionTick()
{
    try
    {
        var dir = StatusBarPaths.SessionsDir;
        int count = Directory.Exists(dir) ? Directory.GetFiles(dir).Length : 0;
        if (count > 0)
        {
            _seenSession = true;
            return;
        }
        if (_seenSession)
            _ui.Post(_ => ExitThread(), null);
    }
    catch { /* ignore transient filesystem errors */ }
}
```

---

## Fix #2 — Surface EmbedLost through IStatusRenderer + runtime fallback to tray

**Files changed:**
- `app/src/ClaudeStatusBar/Render/IStatusRenderer.cs` — added `event EventHandler? EmbedLost;`
- `app/src/ClaudeStatusBar/Render/TrayRenderer.cs` — added `EmbedLost` with CS0067 pragmas
- `app/src/ClaudeStatusBar/App/TrayApplicationContext.cs` — mutable `_renderer`, `_lastState` tracking, `OnEmbedLost` handler, `SwapToTray()` method

`IStatusRenderer.cs` addition:
```csharp
event EventHandler? EmbedLost;
```

`TrayRenderer.cs` addition:
```csharp
#pragma warning disable CS0067
public event EventHandler? EmbedLost;
#pragma warning restore CS0067
```

`TrayApplicationContext.cs` key additions:
```csharp
private IStatusRenderer _renderer;  // mutable: may swap embedded → tray on EmbedLost
private readonly SynchronizationContext _ui;
private AppState _lastState = AppState.Idle;

// In constructor:
_renderer.EmbedLost += OnEmbedLost;
_ui = SynchronizationContext.Current ?? throw new InvalidOperationException("No UI sync context");

// Poller callback:
state =>
{
    _lastState = state;
    _renderer.Render(state);
}

private void OnEmbedLost(object? sender, EventArgs e)
{
    _ui.Post(_ => SwapToTray(), null);
}

private void SwapToTray()
{
    if (_renderer is TrayRenderer) return;
    var old = _renderer;
    var tray = new TrayRenderer();
    tray.ExitRequested += (_, _) => ExitThread();
    tray.EmbedLost += OnEmbedLost;
    _renderer = tray;
    old.Dispose();
    _renderer.Render(_lastState);
}
```

`EmbeddedRenderer.cs` already had `public event EventHandler? EmbedLost;` — confirmed it satisfies the interface. No changes needed.

---

## Fix #3 — Poller dedups on full content, not just `ts`

**Files changed:**
- `app/src/ClaudeStatusBar/Core/StatePoller.cs`
- `app/tests/ClaudeStatusBar.Tests/StatePollerTests.cs`

`StatePoller.cs` change:
```csharp
// Before:
private long _lastTs = long.MinValue;
// ...
if (state is null || state.Ts == _lastTs) return;
_marshal(() => _onChanged(state));
_lastTs = state.Ts;

// After:
private AppState? _last;
// ...
if (state is null || state.Equals(_last)) return;
_marshal(() => _onChanged(state));
_last = state;
```

`AppState` is a `sealed record` so `Equals` provides value equality over all fields. Identical re-writes (same ts) are still skipped; any field change fires.

Test renamed: `Fires_only_on_ts_change` → `Fires_only_on_content_change`; inline comments updated to reflect "contenido" (content) instead of "ts".

---

## Fix #4 — Per-user single-instance mutex

**File changed:** `app/src/ClaudeStatusBar/Program.cs`

```csharp
// Before:
@"Global\ClaudeStatusBar-9F4C2A77-2C5E-4E2A-9E3A-CSB"

// After:
@"Local\ClaudeStatusBar-9F4C2A77-2C5E-4E2A-9E3A-CSB"
```

`Local\` scope allows multiple users / RDP sessions to each run their own instance.

---

## Build Output

```
dotnet build app/src/ClaudeStatusBar --no-incremental

CSC : warning WFO0003: ... (expected, per spec)
Compilación correcta.
    1 Advertencia(s)  ← WFO0003 only
    0 Errores
```

---

## Test Output

```
dotnet test app/ClaudeStatusBar.sln

Correctas! - Con error: 0, Superado: 25, Omitido: 0, Total: 25, Duración: 550 ms
```

---

## AUTO-QUIT Test Transcript (Fix #1 verification)

```
Step 1: Create sessions.d and write test1.session
  → dummy file created: True

Step 2: Kill stale instances; launch ClaudeStatusBar.exe
  → Launched PID: 42624

Step 3: Sleep 4s; check process state
  → After 4s - HasExited: False   ✓ ALIVE (saw session file, _seenSession = true)

Step 4: Delete test1.session
  → Deleted session file. Waiting 5 seconds...

Step 5: Sleep 5s; check process state
  → After 5s - HasExited: True    ✓ EXITED ON ITS OWN

Step 6: Cleanup
  → Files remaining in sessions.d: 0
  → No stale processes

Stderr captured: '' (empty)
```

**Result: Auto-quit CONFIRMED.** The process was alive at 4s (had seen the session), then exited autonomously within 5s of the session file being removed.

---

## Files Changed

| File | Fix |
|------|-----|
| `app/src/ClaudeStatusBar/App/TrayApplicationContext.cs` | #1, #2 |
| `app/src/ClaudeStatusBar/Render/IStatusRenderer.cs` | #2 |
| `app/src/ClaudeStatusBar/Render/TrayRenderer.cs` | #2 |
| `app/src/ClaudeStatusBar/Core/StatePoller.cs` | #3 |
| `app/tests/ClaudeStatusBar.Tests/StatePollerTests.cs` | #3 |
| `app/src/ClaudeStatusBar/Program.cs` | #4 |

---

## What Was NOT Done (as specified)

- `hooks/update.js` was NOT modified (byte-identical upstream preserved).
- Velopack ApplyUpdatesAndRestart was NOT implemented (deferred; placeholder URL active).
- No widget context menu was added to EmbeddedRenderer.
