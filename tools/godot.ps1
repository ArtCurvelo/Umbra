# Path of the Godot 4.7.2 .NET console executable installed by winget (no PATH alias without admin rights).
$script:Godot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
if (-not (Test-Path $script:Godot)) { throw "Godot 4.7.2 .NET não encontrado em $script:Godot" }
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
$script:Root = Split-Path $PSScriptRoot -Parent
$script:Game = Join-Path $script:Root 'game'
