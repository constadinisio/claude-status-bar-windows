# Claude Status Bar para Windows

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
![Platform: Windows 11](https://img.shields.io/badge/Platform-Windows%2011-0078D6.svg)
![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4.svg)

> Llevá el estado en vivo de [Claude Code](https://claude.com/claude-code) a la barra de tareas de Windows 11 — un ícono animado y una etiqueta que reflejan qué está haciendo Claude en tiempo real.

🇬🇧 **[Read this documentation in English →](README.md)**

<!-- Reemplazá esta nota con una captura real cuando la tengas (ver docs/assets/.gitkeep). -->
> 📸 _Captura próximamente — el widget vive dentro de la barra de tareas, al lado del reloj._

Este es un port a Windows del concepto macOS [claude-status-bar](https://github.com/m1ckc3s/claude-status-bar). En lugar de un ítem en la barra de menú, incrusta un pequeño widget directamente en la barra de tareas de Windows 11 (`Shell_TrayWnd`), con caída automática a un ícono en el área de notificación (bandeja) cuando la incrustación no es posible.

## Características

- **Estado en vivo** — refleja la actividad actual de Claude Code: inactivo, pensando, ejecutando una herramienta o esperando tu permiso.
- **Etiquetas descriptivas** — muestra qué está pasando (`Running command`, `Editing`, `Reading`, `Searching`, `Browsing web`, …).
- **Íconos animados** — una sutil animación de pulso mientras Claude trabaja.
- **Incrustado en la barra** — vive dentro de la barra de tareas de Windows 11, no es una ventana flotante.
- **Fallback elegante** — cae a un ícono de bandeja clásico si la incrustación falla (Windows más viejo, reemplazos del shell, reinicios de Explorer).
- **Autogestionado** — arranca cuando empieza una sesión de Claude y se cierra solo cuando terminan todas.
- **Sonido de completación** — sonido opcional cuando termina un turno de más de ~1 minuto (apagado por defecto; se activa desde el menú).
- **Auto-actualización** — incluye [Velopack](https://velopack.io); las actualizaciones se instalan en silencio en segundo plano.
- **Iniciar con Windows** — toggle opcional de autostart.

## Cómo funciona

```
Hooks de Claude Code  ──►  ~/.claude/statusbar/state.json  ──►  ClaudeStatusBar.exe
   (Node.js)                (escrituras atómicas)                (polling cada 400 ms)
                                                                        │
                                                       ┌────────────────┴───────────────┐
                                                       ▼                                 ▼
                                            EmbeddedRenderer                       TrayRenderer
                                     (widget WPF reparentado en               (fallback NotifyIcon,
                                      Shell_TrayWnd vía SetParent)             con menú de clic derecho)
```

Los [hooks](https://docs.claude.com/en/docs/claude-code/hooks) de Claude Code se disparan en cada evento de sesión/herramienta/prompt. Los scripts de hook en Node.js traducen cada evento a un pequeño archivo `state.json`. La app lee ese archivo cuatro veces por segundo y renderiza el estado actual en la barra de tareas.

## Requisitos

| Requisito | Notas |
|---|---|
| **Windows 11** | El widget incrustado apunta a la barra de tareas de Windows 11. En Windows 10 (o si la incrustación falla) cae automáticamente a un ícono de bandeja. |
| **Node.js** | Los hooks corren con `node` (probado en v24; cualquier LTS reciente funciona). Tiene que estar en el `PATH`. |
| **Claude Code** | Aquello cuyo estado estás mostrando. |
| ~~Runtime de .NET~~ | **No hace falta** — la app se publica como ejecutable self-contained de archivo único. |

## Instalación

La instalación tiene dos pasos: instalar la **app** y luego conectar los **hooks**.

### Paso 1 — Instalar la app

Descargá `ClaudeStatusBar-win-Setup.exe` desde el [último release](https://github.com/constadinisio/claude-status-bar-windows/releases/latest) y ejecutalo.

Se instala en `%LOCALAPPDATA%\ClaudeStatusBar`, habilita auto-actualizaciones silenciosas y agrega una entrada en **Aplicaciones** para una desinstalación limpia.

> **¿Todavía no hay release?** Hasta que se publique el primer release, podés compilar la app desde el código fuente — ver [CONTRIBUTING.md](CONTRIBUTING.md#build-the-app).

### Paso 2 — Instalar los hooks

Los hooks le dicen a Claude Code que actualice el archivo de estado. Elegí una opción:

#### Opción A — Como plugin de Claude Code (recomendado)

Desde adentro de Claude Code:

```
/plugin marketplace add constadinisio/claude-status-bar-windows
/plugin install claude-status-bar-windows@claude-status-bar-windows
```

#### Opción B — Instalación manual de hooks

```bash
git clone https://github.com/constadinisio/claude-status-bar-windows
cd claude-status-bar-windows
node hooks/install.js
```

Esto fusiona los hooks de la status bar en `~/.claude/settings.json` (tus hooks existentes se conservan), copia los scripts a `~/.claude/statusbar/` y guarda un backup único en `~/.claude/settings.json.bak-statusbar`. Es seguro re-ejecutarlo — nunca duplica entradas.

Listo. Iniciá una sesión de Claude Code y el widget aparece.

## Uso

Una vez instalado, el widget corre automáticamente:

- **Arranca** cuando empieza una sesión de Claude Code y **se cierra solo** cuando terminan todas — no hace falta gestionarlo a mano.
- El ícono y la etiqueta se actualizan en vivo a medida que Claude trabaja.

**Menú de bandeja (modo fallback):** cuando corre como ícono de bandeja, hacé clic derecho para:

- **Iniciar con Windows** — alterna el inicio al loguearte (escribe en la clave de registro `HKCU\…\Run`).
- **Play Completion Sound** — alterna el sonido de completación (turnos ≥1 min).
- **Salir** — cierra la app.

**Widget incrustado:** hacé clic derecho sobre el widget en la barra de tareas para un menú chico con **Play Completion Sound**. No tiene botón de salir a propósito — la app se autocierra cuando no hay sesión de Claude activa. El menú completo (autostart, salir) vive en el ícono de bandeja en modo fallback.

## Estados

| Estado | Significado |
|---|---|
| **idle** | Sin turno activo. |
| **thinking** | Claude está razonando. |
| **tool** | Claude está ejecutando una herramienta (la etiqueta dice cuál: `Running command`, `Editing`, `Reading`, …). |
| **permission** | Claude está esperando que apruebes una acción. |
| **done** | Un turno acaba de terminar. |

## Configuración y depuración

- **Archivo de estado:** `~/.claude/statusbar/state.json`
- **Sesiones activas:** un archivo por sesión bajo `~/.claude/statusbar/sessions.d/`
- **Log de depuración de hooks:** definí la variable de entorno `CLAUDE_STATUSBAR_DEBUG=1` para registrar cada invocación de hook en `~/.claude/statusbar/hooks.log`.

## Desinstalar

1. **Quitar los hooks:**
   - Instalación por plugin: `/plugin uninstall claude-status-bar-windows@claude-status-bar-windows`
   - Instalación manual: `node hooks/uninstall.js` (restaura `settings.json`; deja intactos tus otros hooks y cierra la app en ejecución).
2. **Quitar la app:** **Configuración → Aplicaciones → Aplicaciones instaladas → ClaudeStatusBar → Desinstalar**.

## Solución de problemas

| Síntoma | Causa probable y solución |
|---|---|
| No hay widget en la barra, pero aparece un ícono de bandeja | La incrustación cayó a modo bandeja (barra no-Win11, un reemplazo del shell o un reinicio de Explorer). Es comportamiento esperado, no un crash. |
| No aparece nada | (1) La app no está instalada en `%LOCALAPPDATA%\ClaudeStatusBar` — reejecutá el instalador. (2) `node` no está en el `PATH` — los hooks no pueden correr. (3) No hay sesión de Claude activa — la app solo corre mientras hay sesiones. |
| El estado nunca cambia | Los hooks no se disparan. Reinstalalos (Paso 2), después poné `CLAUDE_STATUSBAR_DEBUG=1` y revisá `~/.claude/statusbar/hooks.log`. |
| La animación parece "trabada" | Una sesión se cerró a la fuerza. Se autolimpia en el siguiente inicio/fin de sesión. |
| La app no arranca al loguearte tras activar el toggle | Algunos shells restringen `HKCU\…\Run`. Volvé a activar **Iniciar con Windows** desde el menú de bandeja. |

## Contribuir

Las contribuciones son bienvenidas — ver [CONTRIBUTING.md](CONTRIBUTING.md) para instrucciones de build, tests y desarrollo de hooks.

## Marcas registradas

Este es un proyecto no oficial y de código abierto. **No está afiliado, avalado ni patrocinado por Anthropic.** "Claude", el logo spark de Claude y el personaje Clawd (el cangrejo) son marcas registradas de Anthropic, usadas aquí de forma nominativa. Este proyecto tiene licencia MIT, pero eso cubre solo el código fuente y no otorga ningún derecho sobre las marcas o la identidad de Anthropic. El arte de los íconos está portado del proyecto original [m1ckc3s/claude-status-bar](https://github.com/m1ckc3s/claude-status-bar).

## Licencia

[MIT](LICENSE) © 2026 Constantino Di Nisio
