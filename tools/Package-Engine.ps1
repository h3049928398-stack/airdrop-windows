[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runtime = if($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $root 'dist\AirDrop' }
$output = Join-Path $runtime 'backend'
New-Item -ItemType Directory -Force $output,(Join-Path $output 'protocol'),(Join-Path $output 'licenses') | Out-Null
$artifacts = @{
    '.build\radio-ninja\AirDrop.Radio.exe' = 'AirDrop.Radio.exe'
    '.build\awdl\bin\AirDrop.Awdl.exe' = 'AirDrop.Awdl.exe'
    '.build\stack-ninja\AirDrop.Network.exe' = 'AirDrop.Network.exe'
    '.build\deps\vcpkg\installed\x64-windows\bin\libusb-1.0.dll' = 'libusb-1.0.dll'
    'backend\receiver_worker.py' = 'receiver_worker.py'
    'backend\beacon.ps1' = 'beacon.ps1'
}
foreach ($source in $artifacts.Keys) {
    Copy-Item -LiteralPath (Join-Path $root $source) -Destination (Join-Path $output $artifacts[$source]) -Force
}
foreach ($name in @('receiver.py','transfer.py','cpio.py','identity.py','mdns.py','netif.py','redact.py')) {
    Copy-Item -LiteralPath (Join-Path $root "upstream\winawdl\airdrop\$name") -Destination (Join-Path $output 'protocol') -Force
}
Copy-Item -LiteralPath (Join-Path $root '.build\python') -Destination $output -Recurse -Force
$licenses = @{
    'upstream\devourer\LICENSE' = 'devourer-GPL-2.0.txt'
    'upstream\winawdl\LICENSE' = 'winawdl-MIT.txt'
    'upstream\winawdl\airdrop\LICENSE' = 'receiver-MIT.txt'
    'upstream\winawdl\awdl\LICENSE' = 'winawdl-GPL-3.0.txt'
    'upstream\winawdl\upstream\owl\COPYING' = 'owl-GPL-3.0.txt'
    'upstream\winawdl\upstream\owl\radiotap\COPYING' = 'radiotap.txt'
    'upstream\lwip\COPYING' = 'lwip-BSD.txt'
    '.build\deps\vcpkg\installed\x64-windows\share\libusb\copyright' = 'libusb.txt'
    '.build\python\LICENSE.txt' = 'python.txt'
    '.build\deps\llvm-mingw-20261104-ucrt-x86_64\LICENSE.TXT' = 'llvm-mingw.txt'
}
Copy-Item -LiteralPath (Join-Path $root 'docs/native.lock.json') -Destination (Join-Path $output 'licenses') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs/THIRD_PARTY.md') -Destination (Join-Path $output 'licenses') -Force
foreach ($source in $licenses.Keys) {
    Copy-Item -LiteralPath (Join-Path $root $source) -Destination (Join-Path $output ('licenses\'+$licenses[$source])) -Force
}
$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$vs) { throw 'Visual Studio C++ runtime redistributables are required' }
$runtime = Join-Path $vs 'VC\Redist\MSVC'
$version = Get-ChildItem $runtime -Directory | Where-Object Name -match '^\d+\.' | Sort-Object Name -Descending | Select-Object -First 1
foreach ($dll in @('msvcp140.dll','vcruntime140.dll','vcruntime140_1.dll')) {
    $crt=Get-ChildItem (Join-Path $version.FullName 'x64') -Directory -Filter '*.CRT' | Select-Object -First 1
    Copy-Item -LiteralPath (Join-Path $crt.FullName $dll) -Destination $output -Force
}
Write-Output "Packaged local engine: $output"
