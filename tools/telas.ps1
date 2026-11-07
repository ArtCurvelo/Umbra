# Screenshots of the real Phase 4 screens over the frozen scene -> docs\fase4\telas\*.png
#   powershell -ExecutionPolicy Bypass -File tools\telas.ps1
$ErrorActionPreference = 'Continue'
. (Join-Path $PSScriptRoot 'godot.ps1')
$out = Join-Path $Root 'docs\fase4\telas'
New-Item -ItemType Directory -Force $out | Out-Null
dotnet build (Join-Path $Game 'Umbra.Game.csproj') -nologo -v q | Select-String ' error ' | ForEach-Object { $_.Line }
& $Godot --headless --path $Game --import 2>&1 | Out-Null
foreach ($t in 'menu', 'pausa', 'config:0', 'config:1', 'config:2', 'config:3', 'config:4', 'inventario', 'arvore') {
    $name = $t.Replace(':', '_')
    & $Godot --path $Game -- --preset medio --ticks 900 --freeze --tela $t --shot (Join-Path $out "$name.png") --frames 30 2>&1 | Where-Object { $_ -match 'captura|ERROR|SCRIPT' }
}
& $Godot --path $Game -- --preset baixo --ticks 900 --freeze --tela inventario --size 1920x1080 --shot (Join-Path $out 'inventario_1080p.png') --frames 30 2>&1 | Where-Object { $_ -match 'captura|ERROR' }
