# Phase 4 mockups (real UI kit and font over the real scene) -> docs\fase4\mockups\*.png
#   powershell -ExecutionPolicy Bypass -File tools\mockups.ps1
$ErrorActionPreference = 'Continue'
. (Join-Path $PSScriptRoot 'godot.ps1')
$out = Join-Path $Root 'docs\fase4\mockups'
New-Item -ItemType Directory -Force $out | Out-Null
dotnet build (Join-Path $Game 'Umbra.Game.csproj') -nologo -v q | Select-String ' error ' | ForEach-Object { $_.Line }
& $Godot --headless --path $Game --import 2>&1 | Out-Null
foreach ($m in 'menu', 'config', 'controles', 'inventario', 'arvore') {
    & $Godot --path $Game -- --preset medio --ticks 330 --freeze --mockup $m --shot (Join-Path $out "$m.png") --frames 30 2>&1 | Where-Object { $_ -match 'captura|ERROR|SCRIPT' }
}

# Interface scale: 1080p (1.5x: the text is re-rasterised at 33 px = 3x the font, still crisp) and 1440p (2x)
& $Godot --path $Game -- --preset baixo --ticks 330 --freeze --mockup config --size 1920x1080 --shot (Join-Path $out 'escala_1080p_fracionaria.png') --frames 30 2>&1 | Where-Object { $_ -match 'captura|ERROR' }
& $Godot --path $Game -- --preset baixo --ticks 330 --freeze --mockup config --size 2560x1440 --shot (Join-Path $out 'escala_1440p.png') --frames 30 2>&1 | Where-Object { $_ -match 'captura|ERROR' }
