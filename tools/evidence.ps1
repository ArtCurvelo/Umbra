# Evidence for the Game Director: preset comparison screenshots (same frozen instant), both cameras, and the
# benchmark of each preset. Opens a window (real rendering). Output in docs\capturas and docs\benchmark.
#   powershell -ExecutionPolicy Bypass -File tools\evidence.ps1 [-Seconds 20]
param([double]$Seconds = 20, [int]$Ticks = 330)
$ErrorActionPreference = 'Continue'   # Godot writes its ANGLE notice to stderr; that is not a failure
. (Join-Path $PSScriptRoot 'godot.ps1')
$shots = Join-Path $Root 'docs\capturas'; $bench = Join-Path $Root 'docs\benchmark'
New-Item -ItemType Directory -Force $shots, $bench | Out-Null
dotnet build (Join-Path $Game 'Umbra.Game.csproj') -nologo -v q | Out-Null
& $Godot --headless --path $Game --import 2>&1 | Out-Null

foreach ($preset in 'baixo', 'medio', 'alto') {
    & $Godot --path $Game -- --preset $preset --camera iso --ticks $Ticks --freeze --shot (Join-Path $shots "presets_$preset.png") --frames 60 2>&1 |
        Where-Object { $_ -match 'captura|ERROR' }
}
& $Godot --path $Game -- --preset alto --camera top --ticks $Ticks --freeze --shot (Join-Path $shots 'camera_topdown.png') --frames 60 2>&1 | Where-Object { $_ -match 'captura|ERROR' }
& $Godot --path $Game -- --preset alto --camera iso --yaw 2 --ticks $Ticks --freeze --shot (Join-Path $shots 'camera_iso_girada.png') --frames 60 2>&1 | Where-Object { $_ -match 'captura|ERROR' }

foreach ($preset in 'baixo', 'medio', 'alto') {
    & $Godot --path $Game -- --preset $preset --bench (Join-Path $bench "$preset.json") --seconds $Seconds --warmup 3 2>&1 |
        Where-Object { $_ -match 'benchmark|ERROR' }
}
