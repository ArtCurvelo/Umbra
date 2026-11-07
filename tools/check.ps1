# Full verification of the slice: domain tests (xUnit), game build, asset import, headless integration self-test.
#   powershell -ExecutionPolicy Bypass -File tools\check.ps1
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'godot.ps1')
Set-Location $Root

Write-Host '== 1/4 testes do domínio (xUnit)'
dotnet test (Join-Path $Root 'tests\Umbra.Domain.Tests\Umbra.Domain.Tests.csproj') -nologo
if ($LASTEXITCODE -ne 0) { throw 'testes do domínio falharam' }

Write-Host '== 2/4 build do jogo'
dotnet build (Join-Path $Game 'Umbra.Game.csproj') -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'build do jogo falhou' }

Write-Host '== 3/4 importação de recursos'
& $Godot --headless --path $Game --import 2>&1 | Out-Null

Write-Host '== 4/4 autoteste de integração (sem janela)'
& $Godot --headless --path $Game -- --selftest 2>&1 | Where-Object { $_ -match '^(ok|FALHA|SELFTEST|ERROR|SCRIPT ERROR)' } | ForEach-Object { Write-Host $_ }
if ($LASTEXITCODE -ne 0) { throw "autoteste falhou (código $LASTEXITCODE)" }
Write-Host '== tudo aprovado'
