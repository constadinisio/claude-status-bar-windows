# build/pack.ps1 — produce Velopack installer and update packages
# Usage (from repo root):  powershell -File build/pack.ps1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent

# ── 1. Ensure vpk CLI tool is available ───────────────────────────────────────
# dotnet tool update installs when absent, upgrades when present (idempotent)
Write-Host "Installing / updating vpk tool..."
dotnet tool update -g vpk
if ($LASTEXITCODE -ne 0) { Write-Error "Failed to install vpk tool (exit $LASTEXITCODE)"; exit 1 }

# ── 2. Publish self-contained single-file exe ─────────────────────────────────
Write-Host "Running publish.ps1..."
# publish.ps1 uses repo-relative paths; run from repo root CWD (preserved by &)
& "$PSScriptRoot\publish.ps1"
if ($LASTEXITCODE -ne 0) { Write-Error "publish.ps1 failed (exit $LASTEXITCODE)"; exit 1 }

# ── 3. Pack with Velopack ─────────────────────────────────────────────────────
Write-Host "Running vpk pack..."
vpk pack `
    --packId      ClaudeStatusBar `
    --packVersion 0.1.1 `
    --packDir     "$root\build\publish" `
    --mainExe     ClaudeStatusBar.exe `
    --outputDir   "$root\build\Releases"

if ($LASTEXITCODE -ne 0) { Write-Error "vpk pack failed (exit $LASTEXITCODE)"; exit 1 }

# ── 4. Report artifacts ───────────────────────────────────────────────────────
Write-Host "`nPack complete. Artifacts in build\Releases:"
Get-ChildItem "$root\build\Releases" | Sort-Object Name |
    ForEach-Object { "  $($_.Name)  ($([math]::Round($_.Length / 1MB, 2)) MB)" }
