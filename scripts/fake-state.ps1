# scripts/fake-state.ps1  — escribe state.json atómicamente, como los hooks.
param([string]$State = "thinking", [string]$Label = "Thinking…", [string]$Tool = "")
$dir = Join-Path $env:USERPROFILE ".claude\statusbar"
New-Item -ItemType Directory -Force -Path $dir | Out-Null
# UTC epoch (igual que los hooks Node con Date.now()). NO usar `Get-Date -UFormat %s`:
# en Windows PowerShell devuelve el epoch en hora LOCAL, lo que mete un desfase igual
# al timezone (p.ej. 180:00 = 3 h en GMT-3) en el cronómetro del widget.
$now = [int][DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$obj = @{ state=$State; label=$Label; tool=$Tool; project="demo";
          sessionId="s1"; transcript=""; startedAt=$now; ts=$now }
$tmp = Join-Path $dir "state.json.tmp"
($obj | ConvertTo-Json -Compress) | Out-File -FilePath $tmp -Encoding utf8 -NoNewline
Move-Item -Force $tmp (Join-Path $dir "state.json")
Write-Host "state.json -> $State / $Label"
