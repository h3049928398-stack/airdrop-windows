[CmdletBinding()]
param([switch]$Preview)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.build\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.build\packages'
$project = Join-Path $projectRoot 'src\LiquidGlassWinAirDrop\LiquidGlassWinAirDrop.csproj'
$output = Join-Path $projectRoot 'dist\LiquidGlassWinAirDrop'
& dotnet publish $project -c Release --self-contained false -o $output --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$exe = Join-Path $output 'LiquidGlassWinAirDrop.exe'
$report = Join-Path $projectRoot 'artifacts\self-test.json'
$test = Start-Process -FilePath $exe -ArgumentList @('--self-test', ('"' + $report + '"')) -WindowStyle Hidden -PassThru -Wait
if ($test.ExitCode -ne 0) { throw "Self-test failed. See $report" }
if ($Preview) {
    $png = Join-Path $projectRoot 'artifacts\receive-center.png'
    $render = Start-Process -FilePath $exe -ArgumentList @('--preview', ('"' + $png + '"')) -WindowStyle Hidden -PassThru -Wait
    if ($render.ExitCode -ne 0) { throw "Preview failed. See $png.error.txt" }
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $output -Force
Write-Output "Built and tested: $exe"
