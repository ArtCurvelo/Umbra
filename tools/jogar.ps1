# Compila e abre a vertical slice numa janela.
#   powershell -ExecutionPolicy Bypass -File tools\jogar.ps1 [-Preset baixo|medio|alto]
param([string]$Preset = 'baixo')
$ErrorActionPreference = 'Continue'
. (Join-Path $PSScriptRoot 'godot.ps1')
dotnet build (Join-Path $Game 'Umbra.Game.csproj') -nologo -v q | Out-Null
& $Godot --headless --path $Game --import 2>&1 | Out-Null
& $Godot --path $Game -- --preset $Preset
