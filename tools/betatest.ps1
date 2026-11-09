# AI beta tester: plays the slice headless with five player profiles and writes docs/betatest/relatorio_<data>.md.
#   powershell -ExecutionPolicy Bypass -File tools\betatest.ps1                 # 40 partidas por perfil
#   powershell -ExecutionPolicy Bypass -File tools\betatest.ps1 -Runs 200 -Perfis veterano,novato
# Exit code 2 = the tester found bugs (exception, stuck hero, endless run or a replay that differs).
param([int]$Runs = 40, [int]$Seed = 1, [string]$Perfis = '')
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
$extra = @('--runs', $Runs, '--seed', $Seed, '--saida', (Join-Path $Root 'docs\betatest'))
if ($Perfis) { $extra += @('--perfis', $Perfis) }
dotnet run --project (Join-Path $Root 'src\Umbra.BetaTester') -c Release -- @extra
exit $LASTEXITCODE
