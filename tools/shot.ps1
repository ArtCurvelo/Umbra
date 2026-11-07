# One screenshot of the frozen comparison instant (builds first). Prints Godot errors, if any.
#   powershell -ExecutionPolicy Bypass -File tools\shot.ps1 -Out C:\caminho\x.png [-Preset alto] [-Camera iso] [-Ticks 330] [-Yaw 0]
param([Parameter(Mandatory)][string]$Out, [string]$Preset = 'alto', [string]$Camera = 'iso', [int]$Ticks = 330, [int]$Yaw = 0, [string]$Extra = '')
$ErrorActionPreference = 'Continue'
. (Join-Path $PSScriptRoot 'godot.ps1')
dotnet build (Join-Path $Game 'Umbra.Game.csproj') -nologo -v q | Select-String ' error ' | ForEach-Object { $_.Line }
& $Godot --headless --path $Game --import 2>&1 | Out-Null
$userArgs = @('--preset', $Preset, '--camera', $Camera, '--yaw', $Yaw, '--ticks', $Ticks, '--freeze', '--shot', $Out, '--frames', 60)
if ($Extra) { $userArgs += $Extra.Split(' ') }
& $Godot --path $Game -- @userArgs 2>&1 | Where-Object { $_ -match 'captura|ERROR|SCRIPT|error' } | ForEach-Object { "$_" }
# Example: -Extra '--fx ruptura' captures the Ruptura effect.
