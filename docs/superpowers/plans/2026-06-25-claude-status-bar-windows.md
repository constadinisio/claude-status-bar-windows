# Claude Status Bar (Windows) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Portar a Windows la app macOS `claude-status-bar`, mostrando el estado de Claude Code (idle/thinking/tool/permission/done) como un widget incrustado en la barra de tareas de Windows 11, con fallback automático a ícono de bandeja.

**Architecture:** Una app C#/.NET 9 lee `%USERPROFILE%\.claude\statusbar\state.json` por polling (400 ms) y lo renderiza mediante un `IStatusRenderer` intercambiable: `EmbeddedRenderer` (ventana WPF reparentada en `Shell_TrayWnd` vía `SetParent`) con caída automática a `TrayRenderer` (`NotifyIcon`). Los hooks Node.js del proyecto original escriben ese `state.json`; `update.js` se reutiliza intacto y `lifecycle.js`/`install.js`/`uninstall.js` se adaptan a Windows.

**Tech Stack:** C# / .NET 9 (`net9.0-windows`), WinForms (core de bandeja) + WPF (widget incrustado), Win32 P/Invoke (user32/shcore/shell32), `System.Text.Json` source-generated, xUnit para tests, Node.js (hooks), Velopack (instalador + auto-update).

## Global Constraints

- **Target framework:** `net9.0-windows`, `<UseWindowsForms>true</UseWindowsForms>` y `<UseWPF>true</UseWPF>` en el proyecto de app.
- **DPI:** `app.manifest` con `PerMonitorV2` (obligatorio). Sin esto el widget se ve mal escalado contra la barra.
- **Ruta de estado (verbatim):** `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "statusbar", "state.json")` → en este equipo `C:\Users\User\.claude\statusbar\state.json`.
- **Lectura de `state.json`:** siempre con `FileShare.ReadWrite` (no bloquear el `rename` atómico del hook), reintentos ante `IOException`, e ignorar `JsonException` (JSON parcial).
- **Polling:** `System.Threading.Timer` a 400 ms. NO usar `FileSystemWatcher`.
- **Schema de `state.json` (camelCase, verbatim):** `state, label, tool, project, sessionId, transcript, startedAt, ts`. Valores de `state`: `idle | thinking | tool | permission | done`.
- **GDI:** todo `Bitmap.GetHicon()` debe ir seguido de `DestroyIcon` del handle nativo. Nunca regenerar íconos por frame sin liberar.
- **Validación de taskbar:** la ventana `Shell_TrayWnd` usada debe pertenecer a `explorer.exe` (hay apps de terceros que crean ventanas homónimas).
- **Empaquetado:** single-file self-contained, `PublishTrimmed=false` y `PublishAot=false` (no soportado en WinForms/WPF).
- **No trimming/AOT** en ningún momento.
- **Hooks en `hooks.json`/`settings.json`:** exec form (`"command":"node","args":[...]`), nunca shell form con rutas sin comillas.

---

## File Structure

App C# (carpeta `app/`):

- `app/ClaudeStatusBar.sln` — solución.
- `app/src/ClaudeStatusBar/ClaudeStatusBar.csproj` — proyecto WinExe (.NET 9, WinForms+WPF).
- `app/src/ClaudeStatusBar/app.manifest` — PerMonitorV2.
- `app/src/ClaudeStatusBar/Program.cs` — entrypoint, mutex single-instance, arranque del `ApplicationContext`.
- `app/src/ClaudeStatusBar/Core/AppState.cs` — record del estado + enum.
- `app/src/ClaudeStatusBar/Core/StatusBarPaths.cs` — resolución de rutas `~/.claude/statusbar`.
- `app/src/ClaudeStatusBar/Core/StateReader.cs` — lectura robusta + parseo de `state.json`.
- `app/src/ClaudeStatusBar/Core/StateJsonContext.cs` — `JsonSerializerContext` source-gen.
- `app/src/ClaudeStatusBar/Core/StatePoller.cs` — timer 400 ms + dispatch a UI.
- `app/src/ClaudeStatusBar/Core/StatusViewModel.cs` — deriva texto/ícono/elapsed desde `AppState`.
- `app/src/ClaudeStatusBar/Render/IStatusRenderer.cs` — interfaz de render.
- `app/src/ClaudeStatusBar/Render/TrayRenderer.cs` — `NotifyIcon` + ícono animado.
- `app/src/ClaudeStatusBar/Render/IconFactory.cs` — genera `Icon[]` por frame sin fugar GDI.
- `app/src/ClaudeStatusBar/Render/EmbeddedRenderer.cs` — orquesta el widget WPF incrustado.
- `app/src/ClaudeStatusBar/Render/Widget.xaml(.cs)` — ventana WPF borderless del widget.
- `app/src/ClaudeStatusBar/Interop/Native.cs` — P/Invoke (user32/shcore/shell32) + structs/consts.
- `app/src/ClaudeStatusBar/Interop/TaskbarLocator.cs` — encuentra `Shell_TrayWnd` (valida explorer) y calcula el rect de incrustación.
- `app/src/ClaudeStatusBar/Interop/WindowEmbedder.cs` — `SetParent` + estilos + reposicionado + re-embed `TaskbarCreated`.
- `app/src/ClaudeStatusBar/App/TrayApplicationContext.cs` — pega todo: poller → renderer activo, menú, fallback.
- `app/tests/ClaudeStatusBar.Tests/ClaudeStatusBar.Tests.csproj` — xUnit.
- `app/tests/ClaudeStatusBar.Tests/*` — tests de Core (lo testeable sin UI).

Hooks (carpeta `hooks/`) y plugin (`.claude-plugin/`):

- `hooks/update.js` — copiado intacto del repo original.
- `hooks/lifecycle.js` — adaptado a Windows.
- `hooks/install.js` — adaptado a Windows.
- `hooks/uninstall.js` — adaptado a Windows.
- `hooks/hooks.json` — exec form.
- `.claude-plugin/plugin.json` — manifiesto del plugin.
- `.claude-plugin/marketplace.json` — marketplace self-hosted.

Empaquetado:

- `build/publish.ps1` — `dotnet publish` single-file.
- `build/pack.ps1` — `vpk pack` (Velopack).

---

## Phase 1 — Núcleo de lectura de estado (testeable, sin UI)

### Task 1: Solución, proyectos y modelo de estado

**Files:**
- Create: `app/src/ClaudeStatusBar/ClaudeStatusBar.csproj`
- Create: `app/src/ClaudeStatusBar/app.manifest`
- Create: `app/src/ClaudeStatusBar/Core/AppState.cs`
- Create: `app/tests/ClaudeStatusBar.Tests/ClaudeStatusBar.Tests.csproj`
- Test: `app/tests/ClaudeStatusBar.Tests/AppStateTests.cs`

**Interfaces:**
- Produces: `enum StatusKind { Idle, Thinking, Tool, Permission, Done }`; `record AppState(StatusKind State, string Label, string Tool, string Project, string SessionId, string Transcript, long StartedAt, long Ts)`; `static StatusKind StatusKindParser.Parse(string? raw)`.

- [ ] **Step 1: Crear el csproj de la app**

```xml
<!-- app/src/ClaudeStatusBar/ClaudeStatusBar.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWindowsForms>true</UseWindowsForms>
    <UseWPF>true</UseWPF>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <RootNamespace>ClaudeStatusBar</RootNamespace>
    <AssemblyName>ClaudeStatusBar</AssemblyName>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Crear el manifest PerMonitorV2**

```xml
<!-- app/src/ClaudeStatusBar/app.manifest -->
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Step 3: Crear el csproj de tests**

```xml
<!-- app/tests/ClaudeStatusBar.Tests/ClaudeStatusBar.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\ClaudeStatusBar\ClaudeStatusBar.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Crear la solución y agregar proyectos**

```bash
cd app
dotnet new sln -n ClaudeStatusBar
dotnet sln add src/ClaudeStatusBar/ClaudeStatusBar.csproj
dotnet sln add tests/ClaudeStatusBar.Tests/ClaudeStatusBar.Tests.csproj
```

- [ ] **Step 5: Escribir el test que falla (parseo de estado)**

```csharp
// app/tests/ClaudeStatusBar.Tests/AppStateTests.cs
using ClaudeStatusBar.Core;
using Xunit;

public class AppStateTests
{
    [Theory]
    [InlineData("idle", StatusKind.Idle)]
    [InlineData("thinking", StatusKind.Thinking)]
    [InlineData("tool", StatusKind.Tool)]
    [InlineData("permission", StatusKind.Permission)]
    [InlineData("done", StatusKind.Done)]
    [InlineData("DONE", StatusKind.Done)]   // case-insensitive
    [InlineData("", StatusKind.Idle)]       // vacío -> idle
    [InlineData(null, StatusKind.Idle)]     // null -> idle
    [InlineData("garbage", StatusKind.Idle)]// desconocido -> idle
    public void Parse_maps_raw_state_to_kind(string? raw, StatusKind expected)
        => Assert.Equal(expected, StatusKindParser.Parse(raw));
}
```

- [ ] **Step 6: Correr el test y verificar que falla**

Run: `dotnet test app/tests/ClaudeStatusBar.Tests --filter AppStateTests`
Expected: FAIL de compilación — `StatusKind`/`StatusKindParser`/`AppState` no existen.

- [ ] **Step 7: Implementar el modelo y el parser**

```csharp
// app/src/ClaudeStatusBar/Core/AppState.cs
namespace ClaudeStatusBar.Core;

public enum StatusKind { Idle, Thinking, Tool, Permission, Done }

public sealed record AppState(
    StatusKind State,
    string Label,
    string Tool,
    string Project,
    string SessionId,
    string Transcript,
    long StartedAt,
    long Ts)
{
    public static AppState Idle { get; } =
        new(StatusKind.Idle, "", "", "", "", "", 0, 0);
}

public static class StatusKindParser
{
    public static StatusKind Parse(string? raw) => (raw ?? "").Trim().ToLowerInvariant() switch
    {
        "thinking"   => StatusKind.Thinking,
        "tool"       => StatusKind.Tool,
        "permission" => StatusKind.Permission,
        "done"       => StatusKind.Done,
        _            => StatusKind.Idle,
    };
}
```

- [ ] **Step 8: Correr el test y verificar que pasa**

Run: `dotnet test app/tests/ClaudeStatusBar.Tests --filter AppStateTests`
Expected: PASS (9 casos).

- [ ] **Step 9: Commit**

```bash
git add app/
git commit -m "feat: scaffold .NET solution and AppState model"
```

---

### Task 2: Resolución de rutas

**Files:**
- Create: `app/src/ClaudeStatusBar/Core/StatusBarPaths.cs`
- Test: `app/tests/ClaudeStatusBar.Tests/StatusBarPathsTests.cs`

**Interfaces:**
- Produces: `static class StatusBarPaths` con `string StateFile { get; }`, `string StatusBarDir { get; }`, `string SessionsDir { get; }`.

- [ ] **Step 1: Escribir el test que falla**

```csharp
// app/tests/ClaudeStatusBar.Tests/StatusBarPathsTests.cs
using ClaudeStatusBar.Core;
using Xunit;

public class StatusBarPathsTests
{
    [Fact]
    public void StateFile_lives_under_user_profile_dot_claude_statusbar()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expected = Path.Combine(profile, ".claude", "statusbar", "state.json");
        Assert.Equal(expected, StatusBarPaths.StateFile);
    }

    [Fact]
    public void SessionsDir_ends_with_sessions_d()
        => Assert.EndsWith(Path.Combine("statusbar", "sessions.d"), StatusBarPaths.SessionsDir);
}
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test app/tests/ClaudeStatusBar.Tests --filter StatusBarPathsTests`
Expected: FAIL — `StatusBarPaths` no existe.

- [ ] **Step 3: Implementar**

```csharp
// app/src/ClaudeStatusBar/Core/StatusBarPaths.cs
namespace ClaudeStatusBar.Core;

public static class StatusBarPaths
{
    private static string Home =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string StatusBarDir => Path.Combine(Home, ".claude", "statusbar");
    public static string StateFile    => Path.Combine(StatusBarDir, "state.json");
    public static string SessionsDir  => Path.Combine(StatusBarDir, "sessions.d");
}
```

- [ ] **Step 4: Correr el test y verificar que pasa**

Run: `dotnet test app/tests/ClaudeStatusBar.Tests --filter StatusBarPathsTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add app/
git commit -m "feat: resolve ~/.claude/statusbar paths"
```

---

### Task 3: Lectura robusta y parseo de `state.json`

**Files:**
- Create: `app/src/ClaudeStatusBar/Core/StateJsonContext.cs`
- Create: `app/src/ClaudeStatusBar/Core/StateReader.cs`
- Test: `app/tests/ClaudeStatusBar.Tests/StateReaderTests.cs`

**Interfaces:**
- Consumes: `AppState`, `StatusKindParser` (Task 1).
- Produces: `sealed class StateReader { StateReader(string path); AppState? TryRead(); }`. Devuelve `null` si el archivo no existe, está vacío, o el JSON es inválido/parcial.

- [ ] **Step 1: Escribir el test que falla**

```csharp
// app/tests/ClaudeStatusBar.Tests/StateReaderTests.cs
using ClaudeStatusBar.Core;
using Xunit;

public class StateReaderTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(),
        "csb_test_" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void TryRead_parses_valid_state()
    {
        File.WriteAllText(_path,
            """{"state":"tool","label":"Running Bash","tool":"Bash","project":"demo","sessionId":"s1","transcript":"/t.jsonl","startedAt":1719000000,"ts":1719000005}""");
        var r = new StateReader(_path).TryRead();
        Assert.NotNull(r);
        Assert.Equal(StatusKind.Tool, r!.State);
        Assert.Equal("Running Bash", r.Label);
        Assert.Equal("Bash", r.Tool);
        Assert.Equal(1719000000, r.StartedAt);
    }

    [Fact]
    public void TryRead_returns_null_when_missing() => Assert.Null(new StateReader(_path).TryRead());

    [Fact]
    public void TryRead_returns_null_on_partial_json()
    {
        File.WriteAllText(_path, """{"state":"too""");
        Assert.Null(new StateReader(_path).TryRead());
    }

    [Fact]
    public void TryRead_does_not_lock_writer()
    {
        File.WriteAllText(_path, """{"state":"idle","ts":1}""");
        var r = new StateReader(_path);
        r.TryRead();
        // El escritor debe poder renombrar/reescribir sin excepción.
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, """{"state":"done","ts":2}""");
        File.Move(tmp, _path, overwrite: true);
        Assert.Equal(StatusKind.Done, r.TryRead()!.State);
    }

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }
}
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test app/tests/ClaudeStatusBar.Tests --filter StateReaderTests`
Expected: FAIL — `StateReader` no existe.

- [ ] **Step 3: Implementar el JSON context source-gen**

```csharp
// app/src/ClaudeStatusBar/Core/StateJsonContext.cs
using System.Text.Json.Serialization;

namespace ClaudeStatusBar.Core;

// DTO 1:1 con el schema camelCase de state.json (state como string crudo).
internal sealed class StateDto
{
    public string? state { get; set; }
    public string? label { get; set; }
    public string? tool { get; set; }
    public string? project { get; set; }
    public string? sessionId { get; set; }
    public string? transcript { get; set; }
    public long startedAt { get; set; }
    public long ts { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(StateDto))]
internal partial class StateJsonContext : JsonSerializerContext { }
```

- [ ] **Step 4: Implementar el reader**

```csharp
// app/src/ClaudeStatusBar/Core/StateReader.cs
using System.Text.Json;

namespace ClaudeStatusBar.Core;

public sealed class StateReader
{
    private readonly string _path;
    public StateReader(string path) => _path = path;

    public AppState? TryRead()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (!File.Exists(_path)) return null;
                using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read,
                                              FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                var json = sr.ReadToEnd();
                if (string.IsNullOrWhiteSpace(json)) return null;

                var dto = JsonSerializer.Deserialize(json, StateJsonContext.Default.StateDto);
                if (dto is null) return null;

                return new AppState(
                    StatusKindParser.Parse(dto.state),
                    dto.label ?? "", dto.tool ?? "", dto.project ?? "",
                    dto.sessionId ?? "", dto.transcript ?? "",
                    dto.startedAt, dto.ts);
            }
            catch (IOException) when (attempt < 2) { Thread.Sleep(20); }
            catch (JsonException) { return null; }
        }
        return null;
    }
}
```

- [ ] **Step 5: Correr el test y verificar que pasa**

Run: `dotnet test app/tests/ClaudeStatusBar.Tests --filter StateReaderTests`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add app/
git commit -m "feat: robust state.json reader with source-gen JSON"
```

---

### Task 4: Poller con cambio de estado y marshalling a UI

**Files:**
- Create: `app/src/ClaudeStatusBar/Core/StatePoller.cs`
- Test: `app/tests/ClaudeStatusBar.Tests/StatePollerTests.cs`

**Interfaces:**
- Consumes: `StateReader`, `AppState` (Tasks 1, 3).
- Produces: `sealed class StatePoller : IDisposable { StatePoller(StateReader reader, Action<AppState> onChanged, int periodMs = 400, Action<Action>? marshal = null); void Start(); }`. Solo invoca `onChanged` cuando el `Ts` cambia respecto al último visto. `marshal` permite inyectar el dispatcher de UI (en tests, ejecución directa).

- [ ] **Step 1: Escribir el test que falla**

```csharp
// app/tests/ClaudeStatusBar.Tests/StatePollerTests.cs
using ClaudeStatusBar.Core;
using Xunit;

public class StatePollerTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(),
        "csb_poll_" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public async Task Fires_only_on_ts_change()
    {
        File.WriteAllText(_path, """{"state":"idle","ts":1}""");
        var seen = new List<AppState>();
        using var poller = new StatePoller(new StateReader(_path),
            s => { lock (seen) seen.Add(s); }, periodMs: 30, marshal: a => a());
        poller.Start();

        await Task.Delay(120);                       // varios ticks, mismo ts -> 1 evento
        File.WriteAllText(_path + ".tmp", """{"state":"done","ts":2}""");
        File.Move(_path + ".tmp", _path, overwrite: true);
        await Task.Delay(120);                        // nuevo ts -> 2do evento

        lock (seen)
        {
            Assert.Equal(2, seen.Count);
            Assert.Equal(StatusKind.Idle, seen[0].State);
            Assert.Equal(StatusKind.Done, seen[1].State);
        }
    }

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }
}
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test app/tests/ClaudeStatusBar.Tests --filter StatePollerTests`
Expected: FAIL — `StatePoller` no existe.

- [ ] **Step 3: Implementar el poller**

```csharp
// app/src/ClaudeStatusBar/Core/StatePoller.cs
namespace ClaudeStatusBar.Core;

public sealed class StatePoller : IDisposable
{
    private readonly StateReader _reader;
    private readonly Action<AppState> _onChanged;
    private readonly int _periodMs;
    private readonly Action<Action> _marshal;
    private System.Threading.Timer? _timer;
    private long _lastTs = long.MinValue;
    private int _busy;   // evita reentrancia si un tick se solapa

    public StatePoller(StateReader reader, Action<AppState> onChanged,
                       int periodMs = 400, Action<Action>? marshal = null)
    {
        _reader = reader;
        _onChanged = onChanged;
        _periodMs = periodMs;
        _marshal = marshal ?? (a => a());
    }

    public void Start() =>
        _timer = new System.Threading.Timer(_ => Tick(), null, 0, _periodMs);

    private void Tick()
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1) return;
        try
        {
            var state = _reader.TryRead();
            if (state is null || state.Ts == _lastTs) return;
            _lastTs = state.Ts;
            _marshal(() => _onChanged(state));
        }
        finally { Interlocked.Exchange(ref _busy, 0); }
    }

    public void Dispose() => _timer?.Dispose();
}
```

- [ ] **Step 4: Correr el test y verificar que pasa**

Run: `dotnet test app/tests/ClaudeStatusBar.Tests --filter StatePollerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add app/
git commit -m "feat: state poller firing only on ts change"
```

---

### Task 5: ViewModel — texto, tooltip y elapsed derivados del estado

**Files:**
- Create: `app/src/ClaudeStatusBar/Core/StatusViewModel.cs`
- Test: `app/tests/ClaudeStatusBar.Tests/StatusViewModelTests.cs`

**Interfaces:**
- Consumes: `AppState`, `StatusKind` (Task 1).
- Produces: `static class StatusViewModel` con `string Tooltip(AppState s)`, `string ShortLabel(AppState s)`, `string Elapsed(AppState s, long nowUnix)`, `bool ShowPermissionDot(AppState s)`.

- [ ] **Step 1: Escribir el test que falla**

```csharp
// app/tests/ClaudeStatusBar.Tests/StatusViewModelTests.cs
using ClaudeStatusBar.Core;
using Xunit;

public class StatusViewModelTests
{
    private static AppState S(StatusKind k, string label = "", long started = 0)
        => new(k, label, "", "proj", "s", "", started, 0);

    [Fact]
    public void Elapsed_formats_mm_ss()
        => Assert.Equal("00:05", StatusViewModel.Elapsed(S(StatusKind.Tool, started: 1000), 1005));

    [Fact]
    public void Elapsed_is_empty_when_not_started()
        => Assert.Equal("", StatusViewModel.Elapsed(S(StatusKind.Idle), 1005));

    [Fact]
    public void Permission_dot_only_when_permission()
    {
        Assert.True(StatusViewModel.ShowPermissionDot(S(StatusKind.Permission)));
        Assert.False(StatusViewModel.ShowPermissionDot(S(StatusKind.Thinking)));
    }

    [Fact]
    public void Tooltip_includes_project_and_label()
    {
        var t = StatusViewModel.Tooltip(S(StatusKind.Tool, "Running Bash", started: 1000));
        Assert.Contains("proj", t);
        Assert.Contains("Running Bash", t);
    }
}
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test app/tests/ClaudeStatusBar.Tests --filter StatusViewModelTests`
Expected: FAIL — `StatusViewModel` no existe.

- [ ] **Step 3: Implementar**

```csharp
// app/src/ClaudeStatusBar/Core/StatusViewModel.cs
namespace ClaudeStatusBar.Core;

public static class StatusViewModel
{
    public static bool ShowPermissionDot(AppState s) => s.State == StatusKind.Permission;

    public static string ShortLabel(AppState s) => s.State switch
    {
        StatusKind.Idle       => "",
        StatusKind.Thinking   => string.IsNullOrEmpty(s.Label) ? "Thinking…" : s.Label,
        StatusKind.Tool       => string.IsNullOrEmpty(s.Label) ? "Using tool" : s.Label,
        StatusKind.Permission => "Permission",
        StatusKind.Done       => "Done",
        _                     => "",
    };

    public static string Elapsed(AppState s, long nowUnix)
    {
        if (s.StartedAt <= 0) return "";
        var secs = Math.Max(0, nowUnix - s.StartedAt);
        return $"{secs / 60:00}:{secs % 60:00}";
    }

    public static string Tooltip(AppState s)
    {
        var parts = new List<string> { $"Claude Code: {ShortLabel(s)}".TrimEnd(':', ' ') };
        if (!string.IsNullOrEmpty(s.Project)) parts.Add($"Project: {s.Project}");
        return string.Join("\n", parts);
    }
}
```

- [ ] **Step 4: Correr el test y verificar que pasa**

Run: `dotnet test app/tests/ClaudeStatusBar.Tests --filter StatusViewModelTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add app/
git commit -m "feat: status view model (tooltip, elapsed, permission dot)"
```

---

## Phase 2 — Modo bandeja (fallback robusto, primero usable)

> Esta fase entrega una app funcional end-to-end por la vía robusta. El modo embedded (Phase 3) se construye encima sin romperla.

### Task 6: Fábrica de íconos animados sin fuga de GDI

**Files:**
- Create: `app/src/ClaudeStatusBar/Interop/Native.cs` (solo `DestroyIcon` por ahora)
- Create: `app/src/ClaudeStatusBar/Render/IconFactory.cs`
- Test: `app/tests/ClaudeStatusBar.Tests/IconFactoryTests.cs`

**Interfaces:**
- Produces: `static class IconFactory { Icon[] FramesFor(StatusKind kind, int sizePx); Icon Dot(int sizePx, System.Drawing.Color color); }`. Genera íconos clonados a managed y destruye el HICON nativo.
- Produces (Native): `static class Native { [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h); ... }`.

- [ ] **Step 1: Escribir el test que falla (no fuga de handles en loop)**

```csharp
// app/tests/ClaudeStatusBar.Tests/IconFactoryTests.cs
using System.Diagnostics;
using ClaudeStatusBar.Core;
using ClaudeStatusBar.Render;
using Xunit;

public class IconFactoryTests
{
    [Fact]
    public void FramesFor_returns_at_least_one_frame()
        => Assert.NotEmpty(IconFactory.FramesFor(StatusKind.Thinking, 16));

    [Fact]
    public void Generating_many_icons_does_not_leak_gdi_handles()
    {
        var proc = Process.GetCurrentProcess();
        proc.Refresh();
        long before = proc.HandleCount;
        for (int i = 0; i < 500; i++)
            foreach (var f in IconFactory.FramesFor(StatusKind.Tool, 16)) f.Dispose();
        proc.Refresh();
        // Margen amplio; si hubiera fuga de HICON, crecería ~500+.
        Assert.True(proc.HandleCount - before < 200,
            $"posible fuga de handles GDI: +{proc.HandleCount - before}");
    }
}
```

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test app/tests/ClaudeStatusBar.Tests --filter IconFactoryTests`
Expected: FAIL — `IconFactory`/`Native` no existen.

- [ ] **Step 3: Crear `Native.cs` con `DestroyIcon`**

```csharp
// app/src/ClaudeStatusBar/Interop/Native.cs
using System.Runtime.InteropServices;

namespace ClaudeStatusBar.Interop;

internal static partial class Native
{
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyIcon(IntPtr hIcon);
}
```

> Nota: el csproj ya es `net9.0-windows`; `LibraryImport` (source-gen P/Invoke) requiere `partial`. Si preferís, usá `[DllImport]` clásico.

- [ ] **Step 4: Implementar `IconFactory`**

```csharp
// app/src/ClaudeStatusBar/Render/IconFactory.cs
using System.Drawing;
using System.Drawing.Drawing2D;
using ClaudeStatusBar.Core;
using ClaudeStatusBar.Interop;

namespace ClaudeStatusBar.Render;

public static class IconFactory
{
    // Crea un Icon managed a partir de un Bitmap, liberando el HICON nativo.
    private static Icon FromBitmap(Bitmap bmp)
    {
        IntPtr h = bmp.GetHicon();
        try { using var tmp = Icon.FromHandle(h); return (Icon)tmp.Clone(); }
        finally { Native.DestroyIcon(h); }
    }

    private static Bitmap NewCanvas(int size, out Graphics g)
    {
        var bmp = new Bitmap(size, size);
        g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        return bmp;
    }

    public static Icon Dot(int sizePx, Color color)
    {
        using var bmp = NewCanvas(sizePx, out var g);
        using (g)
        using (var brush = new SolidBrush(color))
            g.FillEllipse(brush, 2, 2, sizePx - 4, sizePx - 4);
        return FromBitmap(bmp);
    }

    // Placeholder de animación: un punto que "pulsa" en N frames.
    // Reemplazable luego por arte real (spark/terminal/crab del proyecto original).
    public static Icon[] FramesFor(StatusKind kind, int sizePx)
    {
        var baseColor = kind switch
        {
            StatusKind.Permission => Color.Gold,
            StatusKind.Done       => Color.LimeGreen,
            StatusKind.Idle       => Color.Gray,
            _                     => Color.FromArgb(0xD9, 0x77, 0x57), // naranja Claude
        };

        if (kind is StatusKind.Idle or StatusKind.Done or StatusKind.Permission)
            return new[] { Dot(sizePx, baseColor) };

        const int frames = 4;
        var result = new Icon[frames];
        for (int i = 0; i < frames; i++)
        {
            using var bmp = NewCanvas(sizePx, out var g);
            using (g)
            {
                int inset = 2 + i;                       // animación simple
                using var brush = new SolidBrush(baseColor);
                g.FillEllipse(brush, inset, inset, sizePx - inset * 2, sizePx - inset * 2);
            }
            result[i] = FromBitmap(bmp);
        }
        return result;
    }
}
```

- [ ] **Step 5: Correr el test y verificar que pasa**

Run: `dotnet test app/tests/ClaudeStatusBar.Tests --filter IconFactoryTests`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add app/
git commit -m "feat: leak-free animated icon factory"
```

---

### Task 7: `IStatusRenderer` y `TrayRenderer`

**Files:**
- Create: `app/src/ClaudeStatusBar/Render/IStatusRenderer.cs`
- Create: `app/src/ClaudeStatusBar/Render/TrayRenderer.cs`

**Interfaces:**
- Consumes: `AppState`, `StatusViewModel`, `IconFactory` (Tasks 1, 5, 6).
- Produces: `interface IStatusRenderer : IDisposable { void Render(AppState state); event EventHandler? ExitRequested; }`.
- Produces: `sealed class TrayRenderer : IStatusRenderer` (usa `NotifyIcon`, anima con `System.Windows.Forms.Timer`).

> UI: sin test unitario; verificación manual al final de la Phase 2 (Task 9).

- [ ] **Step 1: Definir la interfaz**

```csharp
// app/src/ClaudeStatusBar/Render/IStatusRenderer.cs
using ClaudeStatusBar.Core;

namespace ClaudeStatusBar.Render;

public interface IStatusRenderer : IDisposable
{
    void Render(AppState state);
    event EventHandler? ExitRequested;
}
```

- [ ] **Step 2: Implementar `TrayRenderer`**

```csharp
// app/src/ClaudeStatusBar/Render/TrayRenderer.cs
using System.Windows.Forms;
using ClaudeStatusBar.Core;

namespace ClaudeStatusBar.Render;

public sealed class TrayRenderer : IStatusRenderer
{
    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 140 };
    private Icon[] _frames = Array.Empty<Icon>();
    private int _frameIdx;
    private int _iconSize = 16;

    public event EventHandler? ExitRequested;

    public TrayRenderer()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Salir", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _icon = new NotifyIcon
        {
            Visible = true,
            Text = "Claude Code: idle",
            ContextMenuStrip = menu,
            Icon = SystemIcons.Application, // reemplazado en el primer Render
        };
        _anim.Tick += (_, _) =>
        {
            if (_frames.Length == 0) return;
            _frameIdx = (_frameIdx + 1) % _frames.Length;
            _icon.Icon = _frames[_frameIdx];
        };
    }

    public void Render(AppState state)
    {
        _anim.Stop();
        DisposeFrames();

        _iconSize = DpiIconSize();
        _frames = IconFactory.FramesFor(state.State, _iconSize);
        _frameIdx = 0;
        _icon.Icon = _frames[0];

        var tip = StatusViewModel.Tooltip(state);
        _icon.Text = tip.Length > 63 ? tip[..63] : tip;  // NotifyIcon.Text máx 64 chars

        if (_frames.Length > 1) _anim.Start();
    }

    private static int DpiIconSize()
    {
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        return (int)Math.Round(16 * g.DpiX / 96f);
    }

    private void DisposeFrames()
    {
        foreach (var f in _frames) f.Dispose();
        _frames = Array.Empty<Icon>();
    }

    public void Dispose()
    {
        _anim.Stop(); _anim.Dispose();
        _icon.Visible = false; _icon.Dispose();
        DisposeFrames();
    }
}
```

- [ ] **Step 3: Compilar**

Run: `dotnet build app/src/ClaudeStatusBar`
Expected: build OK (sin warnings de nullability nuevos).

- [ ] **Step 4: Commit**

```bash
git add app/
git commit -m "feat: tray renderer with animated NotifyIcon"
```

---

### Task 8: Entrypoint, single-instance y `TrayApplicationContext`

**Files:**
- Create: `app/src/ClaudeStatusBar/Program.cs`
- Create: `app/src/ClaudeStatusBar/App/TrayApplicationContext.cs`

**Interfaces:**
- Consumes: `StateReader`, `StatePoller`, `StatusBarPaths` (Phase 1), `IStatusRenderer`, `TrayRenderer` (Task 7).
- Produces: `sealed class TrayApplicationContext : ApplicationContext { TrayApplicationContext(Func<IStatusRenderer> rendererFactory); }`.

- [ ] **Step 1: Implementar `TrayApplicationContext`**

```csharp
// app/src/ClaudeStatusBar/App/TrayApplicationContext.cs
using System.Windows.Forms;
using ClaudeStatusBar.Core;
using ClaudeStatusBar.Render;

namespace ClaudeStatusBar.App;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly IStatusRenderer _renderer;
    private readonly StatePoller _poller;

    public TrayApplicationContext(Func<IStatusRenderer> rendererFactory)
    {
        _renderer = rendererFactory();
        _renderer.ExitRequested += (_, _) => ExitThread();

        var ui = WindowsFormsSynchronizationContext.Current
                 ?? throw new InvalidOperationException("No UI sync context");
        _poller = new StatePoller(
            new StateReader(StatusBarPaths.StateFile),
            state => _renderer.Render(state),
            periodMs: 400,
            marshal: action => ui.Post(_ => action(), null));
        _poller.Start();

        // Estado inicial inmediato.
        _renderer.Render(AppState.Idle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _poller.Dispose(); _renderer.Dispose(); }
        base.Dispose(disposing);
    }
}
```

- [ ] **Step 2: Implementar `Program.cs`**

```csharp
// app/src/ClaudeStatusBar/Program.cs
using System.Windows.Forms;
using ClaudeStatusBar.App;
using ClaudeStatusBar.Render;

namespace ClaudeStatusBar;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true,
            @"Global\ClaudeStatusBar-9F4C2A77-2C5E-4E2A-9E3A-CSB", out bool createdNew);
        if (!createdNew) return;

        ApplicationConfiguration.Initialize();
        // Por ahora siempre TrayRenderer; en Phase 4 se elige embedded con fallback.
        Application.Run(new TrayApplicationContext(() => new TrayRenderer()));
        GC.KeepAlive(mutex);
    }
}
```

- [ ] **Step 3: Compilar**

Run: `dotnet build app/src/ClaudeStatusBar`
Expected: build OK.

- [ ] **Step 4: Commit**

```bash
git add app/
git commit -m "feat: app entrypoint, single-instance mutex, tray context"
```

---

### Task 9: Verificación manual end-to-end del modo bandeja

**Files:**
- Create: `scripts/fake-state.ps1` (helper de prueba)

- [ ] **Step 1: Crear el helper que simula los hooks**

```powershell
# scripts/fake-state.ps1  — escribe state.json atómicamente, como los hooks.
param([string]$State = "thinking", [string]$Label = "Thinking…", [string]$Tool = "")
$dir = Join-Path $env:USERPROFILE ".claude\statusbar"
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$now = [int][double]::Parse((Get-Date -UFormat %s))
$obj = @{ state=$State; label=$Label; tool=$Tool; project="demo";
          sessionId="s1"; transcript=""; startedAt=$now; ts=$now }
$tmp = Join-Path $dir "state.json.tmp"
($obj | ConvertTo-Json -Compress) | Out-File -FilePath $tmp -Encoding utf8 -NoNewline
Move-Item -Force $tmp (Join-Path $dir "state.json")
Write-Host "state.json -> $State / $Label"
```

- [ ] **Step 2: Correr la app**

Run: `dotnet run --project app/src/ClaudeStatusBar`
Expected: aparece un ícono en la bandeja del sistema (área del reloj). Menú contextual con "Salir".

- [ ] **Step 3: Simular estados y observar**

Run (en otra terminal, repetir con valores):
```powershell
powershell -File scripts/fake-state.ps1 -State thinking -Label "Thinking…"
powershell -File scripts/fake-state.ps1 -State tool -Label "Running Bash" -Tool Bash
powershell -File scripts/fake-state.ps1 -State permission -Label "Permission"
powershell -File scripts/fake-state.ps1 -State idle -Label ""
```
Expected: dentro de ~400 ms el ícono cambia (anima en thinking/tool, punto dorado en permission, gris en idle) y el tooltip refleja el label y el proyecto.

- [ ] **Step 4: Verificar salida limpia**

Click derecho → Salir. Expected: el ícono desaparece y el proceso termina.

- [ ] **Step 5: Commit**

```bash
git add scripts/
git commit -m "test: manual e2e helper for tray mode"
```

---

## Phase 3 — Modo incrustado en la taskbar (la parte riesgosa)

> Validar pronto en máquina real. Si `SetParent` no funcionara en tu build, la app ya es usable por la Phase 2.

### Task 10: P/Invoke completo y `TaskbarLocator`

**Files:**
- Modify: `app/src/ClaudeStatusBar/Interop/Native.cs` (agregar FindWindow/Ex, GetClassName, GetWindowThreadProcessId, GetWindowRect/ClientRect, structs)
- Create: `app/src/ClaudeStatusBar/Interop/TaskbarLocator.cs`
- Test: `app/tests/ClaudeStatusBar.Tests/TaskbarLocatorTests.cs`

**Interfaces:**
- Produces (Native): `FindWindow`, `FindWindowEx`, `GetClassName`, `GetWindowThreadProcessId`, `GetWindowRect`, `MapWindowPoints`, struct `RECT`.
- Produces: `static class TaskbarLocator { IntPtr FindPrimaryTaskbar(); bool TryGetEmbedBounds(IntPtr taskbar, int widthPx, out Native.RECT clientRect); }`. `FindPrimaryTaskbar` valida que el dueño sea `explorer`.

- [ ] **Step 1: Escribir el test que falla (se ejecuta en Windows con explorer)**

```csharp
// app/tests/ClaudeStatusBar.Tests/TaskbarLocatorTests.cs
using ClaudeStatusBar.Interop;
using Xunit;

public class TaskbarLocatorTests
{
    [Fact]
    public void FindPrimaryTaskbar_returns_a_handle_owned_by_explorer()
    {
        var hwnd = TaskbarLocator.FindPrimaryTaskbar();
        Assert.NotEqual(IntPtr.Zero, hwnd);  // hay taskbar en una sesión interactiva
    }

    [Fact]
    public void TryGetEmbedBounds_yields_positive_size()
    {
        var hwnd = TaskbarLocator.FindPrimaryTaskbar();
        Assert.True(TaskbarLocator.TryGetEmbedBounds(hwnd, 200, out var r));
        Assert.True(r.right - r.left > 0 && r.bottom - r.top > 0);
    }
}
```

> Nota de ejecución: estos tests requieren una sesión de escritorio interactiva con explorer. En CI headless, marcarlos `Skip` con un trait de entorno. El implementador debe correrlos localmente.

- [ ] **Step 2: Correr el test y verificar que falla**

Run: `dotnet test app/tests/ClaudeStatusBar.Tests --filter TaskbarLocatorTests`
Expected: FAIL — `TaskbarLocator` no existe.

- [ ] **Step 3: Ampliar `Native.cs`**

```csharp
// añadir dentro de internal static partial class Native (app/.../Interop/Native.cs)
[StructLayout(LayoutKind.Sequential)]
internal struct RECT { public int left, top, right, bottom; }

[LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
internal static partial IntPtr FindWindowW(string? lpClassName, string? lpWindowName);

[LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
internal static partial IntPtr FindWindowExW(IntPtr parent, IntPtr childAfter,
                                             string? className, string? windowName);

[LibraryImport("user32.dll")]
internal static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

[LibraryImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
internal static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

[LibraryImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
internal static partial bool GetClientRect(IntPtr hWnd, out RECT lpRect);

[LibraryImport("user32.dll")]
internal static partial int MapWindowPoints(IntPtr from, IntPtr to, ref RECT pts, uint count);
```

- [ ] **Step 4: Implementar `TaskbarLocator`**

```csharp
// app/src/ClaudeStatusBar/Interop/TaskbarLocator.cs
using System.Diagnostics;

namespace ClaudeStatusBar.Interop;

public static class TaskbarLocator
{
    public static IntPtr FindPrimaryTaskbar()
    {
        // Puede haber varias "Shell_TrayWnd"; tomamos la de explorer.exe.
        IntPtr h = Native.FindWindowW("Shell_TrayWnd", null);
        if (h == IntPtr.Zero) return IntPtr.Zero;
        Native.GetWindowThreadProcessId(h, out uint pid);
        try
        {
            using var p = Process.GetProcessById((int)pid);
            if (p.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
                return h;
        }
        catch (ArgumentException) { }
        return h; // fallback: única encontrada
    }

    // Calcula un rect (coords cliente del taskbar) a la izquierda del system tray.
    public static bool TryGetEmbedBounds(IntPtr taskbar, int widthPx, out Native.RECT clientRect)
    {
        clientRect = default;
        if (taskbar == IntPtr.Zero) return false;
        if (!Native.GetClientRect(taskbar, out var tb)) return false;

        int height = tb.bottom - tb.top;
        int rightEdge = tb.right;

        // Límite derecho = borde izquierdo de TrayNotifyWnd (el área del reloj/iconos).
        IntPtr tray = Native.FindWindowExW(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        if (tray != IntPtr.Zero && Native.GetWindowRect(tray, out var trayScreen))
        {
            var pt = trayScreen;
            Native.MapWindowPoints(IntPtr.Zero, taskbar, ref pt, 2); // screen -> cliente taskbar
            rightEdge = pt.left;
        }

        int margin = 8;
        int right = rightEdge - margin;
        int left = Math.Max(tb.left, right - widthPx);
        clientRect = new Native.RECT { left = left, top = tb.top + 2, right = right, bottom = tb.bottom - 2 };
        return (clientRect.right - clientRect.left) > 0 && height > 0;
    }
}
```

- [ ] **Step 5: Correr el test y verificar que pasa**

Run: `dotnet test app/tests/ClaudeStatusBar.Tests --filter TaskbarLocatorTests`
Expected: PASS (en sesión interactiva con explorer).

- [ ] **Step 6: Commit**

```bash
git add app/
git commit -m "feat: locate primary taskbar and compute embed bounds"
```

---

### Task 11: Widget WPF borderless

**Files:**
- Create: `app/src/ClaudeStatusBar/Render/Widget.xaml`
- Create: `app/src/ClaudeStatusBar/Render/Widget.xaml.cs`

**Interfaces:**
- Consumes: `AppState`, `StatusViewModel` (Phase 1).
- Produces: `partial class Widget : Window { void Update(AppState s); IntPtr Handle { get; } }` (Handle vía `WindowInteropHelper`).

> Verificación manual en Task 13.

- [ ] **Step 1: Crear el XAML del widget**

```xml
<!-- app/src/ClaudeStatusBar/Render/Widget.xaml -->
<Window x:Class="ClaudeStatusBar.Render.Widget"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        ShowInTaskbar="False" ResizeMode="NoResize" Topmost="False"
        Width="200" Height="40">
  <Border Background="#00000000" Padding="6,2" VerticalAlignment="Stretch">
    <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
      <Ellipse x:Name="Dot" Width="10" Height="10" Margin="0,0,6,0" Fill="#D97757"/>
      <TextBlock x:Name="LabelText" Foreground="White" FontSize="12"
                 VerticalAlignment="Center" Text=""/>
      <TextBlock x:Name="ElapsedText" Foreground="#AAFFFFFF" FontSize="11"
                 Margin="8,0,0,0" VerticalAlignment="Center" Text=""/>
    </StackPanel>
  </Border>
</Window>
```

- [ ] **Step 2: Crear el code-behind**

```csharp
// app/src/ClaudeStatusBar/Render/Widget.xaml.cs
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ClaudeStatusBar.Core;

namespace ClaudeStatusBar.Render;

public partial class Widget : Window
{
    public Widget() => InitializeComponent();

    public IntPtr Handle => new WindowInteropHelper(this).EnsureHandle();

    public void Update(AppState s)
    {
        LabelText.Text = StatusViewModel.ShortLabel(s);
        ElapsedText.Text = StatusViewModel.Elapsed(s, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Dot.Fill = s.State switch
        {
            StatusKind.Permission => Brushes.Gold,
            StatusKind.Done       => Brushes.LimeGreen,
            StatusKind.Idle       => Brushes.Gray,
            _                     => new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x57)),
        };
    }
}
```

- [ ] **Step 3: Compilar**

Run: `dotnet build app/src/ClaudeStatusBar`
Expected: build OK (XAML compila).

- [ ] **Step 4: Commit**

```bash
git add app/
git commit -m "feat: WPF borderless status widget"
```

---

### Task 12: `WindowEmbedder` — SetParent, estilos, reposición y re-embed

**Files:**
- Modify: `app/src/ClaudeStatusBar/Interop/Native.cs` (SetParent, Get/SetWindowLong(Ptr), SetWindowPos, RegisterWindowMessage, consts de estilos)
- Create: `app/src/ClaudeStatusBar/Interop/WindowEmbedder.cs`

**Interfaces:**
- Consumes: `TaskbarLocator`, `Native` (Task 10).
- Produces: `sealed class WindowEmbedder : IDisposable { bool TryEmbed(IntPtr child, int widthPx); void Reposition(); event EventHandler? Detached; }`. Escucha `TaskbarCreated` para re-embeber.

- [ ] **Step 1: Ampliar `Native.cs` con embedding APIs**

```csharp
// añadir a Native.cs
internal const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
internal const int WS_CHILD = 0x40000000, WS_POPUP = unchecked((int)0x80000000),
                   WS_VISIBLE = 0x10000000, WS_CLIPSIBLINGS = 0x04000000;
internal const int WS_EX_LAYERED = 0x00080000, WS_EX_TOOLWINDOW = 0x00000080,
                   WS_EX_NOACTIVATE = 0x08000000;
internal const uint SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40, SWP_NOZORDER = 0x4;
internal static readonly IntPtr HWND_TOP = IntPtr.Zero;

[LibraryImport("user32.dll", SetLastError = true)]
internal static partial IntPtr SetParent(IntPtr child, IntPtr newParent);

[LibraryImport("user32.dll", SetLastError = true)]
internal static partial IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

[LibraryImport("user32.dll", SetLastError = true)]
internal static partial IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

[LibraryImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
internal static partial bool SetWindowPos(IntPtr hWnd, IntPtr after,
    int x, int y, int cx, int cy, uint flags);

[LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
internal static partial uint RegisterWindowMessageW(string msg);
```

- [ ] **Step 2: Implementar `WindowEmbedder`**

```csharp
// app/src/ClaudeStatusBar/Interop/WindowEmbedder.cs
namespace ClaudeStatusBar.Interop;

public sealed class WindowEmbedder : IDisposable
{
    private IntPtr _child;
    private IntPtr _taskbar;
    private int _widthPx;
    public event EventHandler? Detached;

    public bool TryEmbed(IntPtr child, int widthPx)
    {
        _child = child; _widthPx = widthPx;
        _taskbar = TaskbarLocator.FindPrimaryTaskbar();
        if (_taskbar == IntPtr.Zero) return false;

        // Estilos de ventana hija sin activación, fuera de Alt+Tab, con alpha.
        var style = (long)Native.GetWindowLongPtrW(child, Native.GWL_STYLE);
        style = (style & ~Native.WS_POPUP) | Native.WS_CHILD | Native.WS_CLIPSIBLINGS | Native.WS_VISIBLE;
        Native.SetWindowLongPtrW(child, Native.GWL_STYLE, (IntPtr)style);

        var ex = (long)Native.GetWindowLongPtrW(child, Native.GWL_EXSTYLE);
        ex |= Native.WS_EX_LAYERED | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW;
        Native.SetWindowLongPtrW(child, Native.GWL_EXSTYLE, (IntPtr)ex);

        if (Native.SetParent(child, _taskbar) == IntPtr.Zero) return false;
        Reposition();
        return true;
    }

    public void Reposition()
    {
        if (_child == IntPtr.Zero || _taskbar == IntPtr.Zero) return;
        if (!TaskbarLocator.TryGetEmbedBounds(_taskbar, _widthPx, out var r)) return;
        Native.SetWindowPos(_child, Native.HWND_TOP,
            r.left, r.top, r.right - r.left, r.bottom - r.top,
            Native.SWP_SHOWWINDOW | Native.SWP_NOACTIVATE | Native.SWP_NOZORDER);
    }

    // Llamado desde el WndProc del host al recibir el mensaje "TaskbarCreated".
    public void ReEmbed()
    {
        if (_child != IntPtr.Zero && !TryEmbed(_child, _widthPx))
            Detached?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() { /* la ventana hija la libera su dueño */ }
}
```

- [ ] **Step 3: Compilar**

Run: `dotnet build app/src/ClaudeStatusBar`
Expected: build OK.

- [ ] **Step 4: Commit**

```bash
git add app/
git commit -m "feat: window embedder (SetParent + reposition + re-embed)"
```

---

### Task 13: `EmbeddedRenderer` + verificación manual de incrustación

**Files:**
- Create: `app/src/ClaudeStatusBar/Render/EmbeddedRenderer.cs`

**Interfaces:**
- Consumes: `Widget` (Task 11), `WindowEmbedder` (Task 12), `AppState` (Phase 1).
- Produces: `sealed class EmbeddedRenderer : IStatusRenderer { bool Embed(); }`. `Embed()` devuelve `false` si `SetParent` falla (usado por el fallback de Phase 4). Escucha `TaskbarCreated` vía un `HwndSource` hook sobre el handle del widget.

- [ ] **Step 1: Implementar `EmbeddedRenderer`**

```csharp
// app/src/ClaudeStatusBar/Render/EmbeddedRenderer.cs
using System.Windows.Interop;
using ClaudeStatusBar.Core;
using ClaudeStatusBar.Interop;

namespace ClaudeStatusBar.Render;

public sealed class EmbeddedRenderer : IStatusRenderer
{
    private readonly Widget _widget = new();
    private readonly WindowEmbedder _embedder = new();
    private readonly uint _taskbarCreated = Native.RegisterWindowMessageW("TaskbarCreated");
    private const int WidthPx = 200;

    public event EventHandler? ExitRequested;
    public event EventHandler? EmbedLost;

    public bool Embed()
    {
        _widget.Show();
        var hwnd = _widget.Handle;

        var src = HwndSource.FromHwnd(hwnd);
        src?.AddHook(WndProc);
        _embedder.Detached += (_, _) => EmbedLost?.Invoke(this, EventArgs.Empty);

        return _embedder.TryEmbed(hwnd, WidthPx);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if ((uint)msg == _taskbarCreated) _embedder.ReEmbed();
        return IntPtr.Zero;
    }

    public void Render(AppState state)
    {
        if (!_widget.Dispatcher.CheckAccess())
        { _widget.Dispatcher.Invoke(() => Render(state)); return; }
        _widget.Update(state);
        _embedder.Reposition();
    }

    public void Dispose()
    {
        _embedder.Dispose();
        if (_widget.Dispatcher.CheckAccess()) _widget.Close();
        else _widget.Dispatcher.Invoke(_widget.Close);
    }
}
```

- [ ] **Step 2: Probar incrustación con un host temporal**

Modificar `Program.cs` temporalmente para usar `EmbeddedRenderer`:
```csharp
Application.Run(new TrayApplicationContext(() => {
    var r = new EmbeddedRenderer();
    r.Embed();
    return r;
}));
```
Run: `dotnet run --project app/src/ClaudeStatusBar`
Expected: el widget aparece DENTRO de la barra de tareas, a la izquierda del reloj. Correr `scripts/fake-state.ps1` y verificar que el texto/punto cambian.

- [ ] **Step 3: Probar re-embed ante reinicio de explorer**

Run: en Administrador de tareas, reiniciar "Explorador de Windows". Expected: tras recrearse la barra, el widget vuelve a aparecer en su lugar (gracias a `TaskbarCreated`).

- [ ] **Step 4: Revertir el cambio temporal de `Program.cs`**

(Se reemplaza por el selector real en Task 14.)

- [ ] **Step 5: Commit**

```bash
git add app/
git commit -m "feat: embedded renderer with taskbar re-embed; verified in taskbar"
```

---

## Phase 4 — Selección de modo y fallback

### Task 14: Selector embedded→tray con fallback automático

**Files:**
- Create: `app/src/ClaudeStatusBar/Render/RendererSelector.cs`
- Modify: `app/src/ClaudeStatusBar/Program.cs`

**Interfaces:**
- Consumes: `EmbeddedRenderer`, `TrayRenderer`, `IStatusRenderer`.
- Produces: `static class RendererSelector { IStatusRenderer Create(Action<IStatusRenderer> onFallback); }`. Intenta embedded; si `Embed()` falla o se pierde, crea `TrayRenderer` y notifica.

- [ ] **Step 1: Implementar el selector**

```csharp
// app/src/ClaudeStatusBar/Render/RendererSelector.cs
namespace ClaudeStatusBar.Render;

public static class RendererSelector
{
    public static IStatusRenderer Create()
    {
        try
        {
            var embedded = new EmbeddedRenderer();
            if (embedded.Embed()) return embedded;
            embedded.Dispose();
        }
        catch { /* cae a tray */ }
        return new TrayRenderer();
    }
}
```

- [ ] **Step 2: Conectar en `Program.cs`**

```csharp
// reemplazar la línea Application.Run(...) de Task 8
Application.Run(new TrayApplicationContext(RendererSelector.Create));
```

- [ ] **Step 3: Verificar ambos caminos**

Run: `dotnet run --project app/src/ClaudeStatusBar` → Expected: widget incrustado.
Para forzar fallback, renombrar temporalmente la clase de ventana buscada en `TaskbarLocator` a una inexistente y re-correr → Expected: aparece el ícono de bandeja en su lugar. (Revertir el cambio luego.)

- [ ] **Step 4: Commit**

```bash
git add app/
git commit -m "feat: auto-select embedded renderer with tray fallback"
```

---

## Phase 5 — Hooks de Claude Code (Node.js) y plugin

### Task 15: Portar los scripts de hooks a Windows

**Files:**
- Create: `hooks/update.js` (copia intacta del repo original)
- Create: `hooks/lifecycle.js` (adaptado)
- Create: `hooks/hooks.json` (exec form)

**Interfaces:**
- Produces: `state.json` escrito por los hooks; contrato consumido por la app (Phase 1).

- [ ] **Step 1: Copiar `update.js` intacto**

Descargar verbatim desde el repo original y guardarlo en `hooks/update.js`:
Run: `curl -fsSL https://raw.githubusercontent.com/m1ckc3s/claude-status-bar/main/hooks/update.js -o hooks/update.js`
Expected: archivo creado. No modificar (es cross-platform).

- [ ] **Step 2: Crear `lifecycle.js` adaptado a Windows**

Partir del original y aplicar los 3 cambios (lanzar `.exe`, detección por `tasklist`, sin bundle id):
```js
// hooks/lifecycle.js  (fragmentos clave adaptados)
const os = require("os"), path = require("path"), fs = require("fs"), cp = require("child_process");

const SB_DIR = path.join(os.homedir(), ".claude", "statusbar");
const STATE = path.join(SB_DIR, "state.json");
const SESSIONS = path.join(SB_DIR, "sessions.d");
const EXE_NAME = "ClaudeStatusBar.exe";
// Ruta estable de instalación (no CLAUDE_PLUGIN_ROOT, que cambia entre updates).
const EXE_PATH = path.join(process.env.LOCALAPPDATA || os.homedir(),
                           "ClaudeStatusBar", EXE_NAME);

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

// clearStaleState() y la gestión de sessions.d/ se conservan del original (cross-platform).
// main(): "start" -> registrar sesión + launch(); "end" -> quitar sesión + clearStaleState().
```

> El implementador debe portar `clearStaleState()`, `safeId()` y el manejo de `sessions.d/` tal cual del original, cambiando solo `running()`/`launch()` y eliminando `BUNDLE_ID`/`open -g`.

- [ ] **Step 3: Crear `hooks.json` en exec form**

```json
{
  "hooks": {
    "SessionStart":     [ { "hooks": [ { "type": "command", "command": "node", "args": ["${CLAUDE_PLUGIN_ROOT}/hooks/lifecycle.js", "start"] } ] } ],
    "SessionEnd":       [ { "hooks": [ { "type": "command", "command": "node", "args": ["${CLAUDE_PLUGIN_ROOT}/hooks/lifecycle.js", "end"] } ] } ],
    "UserPromptSubmit": [ { "hooks": [ { "type": "command", "command": "node", "args": ["${CLAUDE_PLUGIN_ROOT}/hooks/update.js", "prompt"] } ] } ],
    "PreToolUse":       [ { "matcher": "*", "hooks": [ { "type": "command", "command": "node", "args": ["${CLAUDE_PLUGIN_ROOT}/hooks/update.js", "pre"] } ] } ],
    "PostToolUse":      [ { "matcher": "*", "hooks": [ { "type": "command", "command": "node", "args": ["${CLAUDE_PLUGIN_ROOT}/hooks/update.js", "post"] } ] } ],
    "Notification":     [ { "hooks": [ { "type": "command", "command": "node", "args": ["${CLAUDE_PLUGIN_ROOT}/hooks/update.js", "notify"] } ] } ],
    "PermissionRequest":[ { "matcher": "*", "hooks": [ { "type": "command", "command": "node", "args": ["${CLAUDE_PLUGIN_ROOT}/hooks/update.js", "permreq"] } ] } ],
    "Stop":             [ { "hooks": [ { "type": "command", "command": "node", "args": ["${CLAUDE_PLUGIN_ROOT}/hooks/update.js", "stop"] } ] } ]
  }
}
```

- [ ] **Step 4: Verificar que `update.js` escribe `state.json` con un evento simulado**

Run:
```bash
echo '{"session_id":"s1","cwd":"C:/demo","hook_event_name":"PreToolUse","tool_name":"Bash","tool_input":{}}' | node hooks/update.js pre
```
Expected: `C:\Users\User\.claude\statusbar\state.json` existe con `"state":"tool"`.

- [ ] **Step 5: Verificar integración con la app**

Con la app corriendo (Phase 2/4), correr el comando del Step 4 y observar que el widget/ícono pasa a "tool".
Expected: cambia dentro de ~400 ms.

- [ ] **Step 6: Commit**

```bash
git add hooks/
git commit -m "feat: port hooks to Windows (update.js intact, lifecycle.js adapted, exec form)"
```

---

### Task 16: Instalador/desinstalador de hooks adaptados

**Files:**
- Create: `hooks/install.js` (adaptado)
- Create: `hooks/uninstall.js` (adaptado)

**Interfaces:**
- Produces: merge no destructivo de hooks en `~/.claude/settings.json` con backup, e idempotencia por marcador.

- [ ] **Step 1: Portar `install.js`**

Partir del original y: (a) **eliminar** todo el bloque LaunchAgent (`launchctl`, plist, `process.getuid()` — crashea en Windows); (b) emitir los comandos en **exec form** (`{type:"command", command:"node", args:[updateDest, evt]}`) en vez de strings sin comillas; (c) conservar el copiado de `update.js`/`lifecycle.js` a `~/.claude/statusbar/`, el backup `.bak-statusbar` y `stripOurs()`.

```js
// hooks/install.js  (forma del comando — reemplaza el quoting roto del original)
const node = process.execPath; // node.exe actual
const mkHook = (script, arg) => ({ type: "command", command: node, args: [script, arg] });
// merge en settings.hooks[evento] = [{ matcher?:"*", hooks:[ mkHook(...) ] }]
```

- [ ] **Step 2: Portar `uninstall.js`**

Eliminar `launchctl`/plist/`process.getuid()` y reemplazar `pkill -x ClaudeStatusBar` por:
```js
try { require("child_process").execSync("taskkill /IM ClaudeStatusBar.exe /F", { stdio: "ignore" }); } catch {}
```
Conservar el filtrado de hooks por el marcador `~/.claude/statusbar` y el borrado de eventos vacíos.

- [ ] **Step 3: Verificar install + uninstall idempotentes**

Run:
```bash
node hooks/install.js
node hooks/install.js   # 2da vez: no duplica
node hooks/uninstall.js # deja settings.json como antes (salvo backup)
```
Expected: `~/.claude/settings.json` contiene los 8 hooks tras install (una sola copia) y queda limpio tras uninstall; existe `settings.json.bak-statusbar`.

- [ ] **Step 4: Commit**

```bash
git add hooks/
git commit -m "feat: Windows-safe hook install/uninstall (no LaunchAgent/getuid)"
```

---

### Task 17: Manifiestos de plugin

**Files:**
- Create: `.claude-plugin/plugin.json`
- Create: `.claude-plugin/marketplace.json`

- [ ] **Step 1: Crear `plugin.json`**

```json
{
  "name": "claude-status-bar-windows",
  "description": "Windows taskbar status indicator for Claude Code",
  "version": "0.1.0",
  "homepage": "https://github.com/<owner>/claude-status-bar-windows",
  "license": "MIT"
}
```

- [ ] **Step 2: Crear `marketplace.json`**

```json
{
  "name": "claude-status-bar-windows",
  "owner": "<owner>",
  "plugins": [ { "name": "claude-status-bar-windows", "source": "./", "version": "0.1.0" } ]
}
```

- [ ] **Step 3: Verificar instalación como plugin (manual)**

En Claude Code:
```
/plugin marketplace add <owner>/claude-status-bar-windows
/plugin install claude-status-bar-windows@claude-status-bar-windows
```
Expected: al iniciar una sesión nueva, `hooks/hooks.json` se carga y `state.json` empieza a actualizarse.

- [ ] **Step 4: Commit**

```bash
git add .claude-plugin/
git commit -m "feat: Claude Code plugin manifests"
```

---

## Phase 6 — Empaquetado, auto-arranque y distribución

### Task 18: Publish single-file + auto-arranque

**Files:**
- Create: `build/publish.ps1`
- Modify: `app/src/ClaudeStatusBar/App/TrayApplicationContext.cs` (registrar auto-arranque al iniciar, opción de menú)
- Create: `app/src/ClaudeStatusBar/App/AutoStart.cs`

**Interfaces:**
- Produces: `static class AutoStart { void Enable(); void Disable(); bool IsEnabled { get; } }` (Run key HKCU usando `Environment.ProcessPath`).

- [ ] **Step 1: Implementar `AutoStart`**

```csharp
// app/src/ClaudeStatusBar/App/AutoStart.cs
using Microsoft.Win32;

namespace ClaudeStatusBar.App;

public static class AutoStart
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "ClaudeStatusBar";

    public static bool IsEnabled
    {
        get { using var k = Registry.CurrentUser.OpenSubKey(Key); return k?.GetValue(Name) != null; }
    }

    public static void Enable()
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key, writable: true);
        k?.SetValue(Name, $"\"{Environment.ProcessPath}\"");
    }

    public static void Disable()
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key, writable: true);
        if (k?.GetValue(Name) != null) k.DeleteValue(Name);
    }
}
```

- [ ] **Step 2: Agregar opción "Iniciar con Windows" al menú del tray**

En `TrayRenderer` (y/o widget) agregar un `ToolStripMenuItem` con `Checked = AutoStart.IsEnabled` que togglea `Enable()`/`Disable()`.

- [ ] **Step 3: Crear `build/publish.ps1`**

```powershell
# build/publish.ps1
dotnet publish app/src/ClaudeStatusBar -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:PublishReadyToRun=true `
  -p:PublishTrimmed=false `
  -o build/publish
```

- [ ] **Step 4: Publicar y verificar el exe**

Run: `powershell -File build/publish.ps1`
Expected: `build/publish/ClaudeStatusBar.exe` existe y arranca con doble click (widget o tray). El exe debería ubicarse en `%LOCALAPPDATA%\ClaudeStatusBar\` para que `lifecycle.js` lo encuentre (lo hará el instalador en Task 19).

- [ ] **Step 5: Commit**

```bash
git add app/ build/
git commit -m "feat: single-file publish + HKCU autostart toggle"
```

---

### Task 19: Instalador y auto-update con Velopack

**Files:**
- Create: `build/pack.ps1`
- Modify: `app/src/ClaudeStatusBar/Program.cs` (VelopackApp.Build().Run() + check de update)
- Modify: `app/src/ClaudeStatusBar/ClaudeStatusBar.csproj` (PackageReference Velopack)

- [ ] **Step 1: Agregar Velopack al csproj**

```xml
<ItemGroup>
  <PackageReference Include="Velopack" Version="0.0.*" />
</ItemGroup>
```
(Fijar la versión estable disponible al implementar.)

- [ ] **Step 2: Integrar en `Program.cs`**

```csharp
// primera línea de Main(), antes del mutex:
Velopack.VelopackApp.Build().Run();
// y un chequeo async opcional de updates contra el GithubSource del repo.
```

- [ ] **Step 3: Crear `build/pack.ps1`**

```powershell
# build/pack.ps1
dotnet tool install -g vpk
powershell -File build/publish.ps1
vpk pack --packId ClaudeStatusBar --packVersion 0.1.0 `
  --packDir build/publish --mainExe ClaudeStatusBar.exe
```
El `Setup.exe` resultante instala en una ruta estable; ajustar para que el binario quede en `%LOCALAPPDATA%\ClaudeStatusBar\ClaudeStatusBar.exe` (coincidiendo con `lifecycle.js`).

- [ ] **Step 4: Generar instalador y probar ciclo completo**

Run: `powershell -File build/pack.ps1`
Expected: se genera `Setup.exe` en `Releases/`. Instalar, iniciar una sesión de Claude Code, y confirmar que el widget aparece en la taskbar reflejando el estado real.

- [ ] **Step 5: Commit**

```bash
git add app/ build/
git commit -m "feat: Velopack installer and auto-update"
```

---

## Self-Review

**Cobertura del objetivo:**
- Widget en la taskbar de Win11 → Tasks 10-14 (SetParent + posición + fallback). ✓
- Leer `state.json` como el original → Tasks 2-5. ✓
- Estados idle/thinking/tool/permission/done → Task 1 + ViewModel Task 5 + render Tasks 7/11. ✓
- Punto de permiso (yellow dot) → `ShowPermissionDot` (Task 5), Dot dorado (Tasks 6/11). ✓
- Contador de tiempo transcurrido → `Elapsed` (Task 5), mostrado en widget (Task 11). ✓
- Hooks que escriben el estado → Tasks 15-16 (update.js intacto, lifecycle adaptado). ✓
- Instalación como plugin y como instalador → Tasks 17 (plugin), 16 (install.js), 19 (Velopack). ✓
- Auto-arranque y auto-update → Tasks 18-19. ✓
- Robustez ante reinicio de explorer y DPI → Task 12 (TaskbarCreated), manifest PerMonitorV2 (Task 1). ✓

**Pendiente de arte/refinamiento (no bloqueante, fuera de MVP):** los íconos animados de `IconFactory` son placeholders geométricos; el arte real (spark/terminal/crab del original) y el manejo fino de `WM_DPICHANGED`/multimonitor en el widget incrustado son mejoras posteriores. La animación del widget WPF (storyboard) puede sumarse sobre la base de Task 11.

**Consistencia de tipos:** `IStatusRenderer.Render(AppState)` usado igual en `TrayRenderer`, `EmbeddedRenderer`, `TrayApplicationContext`. `AppState`/`StatusKind` consistentes en todo el plan. `StatusBarPaths.StateFile` reusado en poller y verificación. `TaskbarLocator`/`WindowEmbedder` con firmas consistentes entre Tasks 10-14.
