[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $vswhere='C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
    $vs=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if(!$vs){throw 'Visual Studio C++ tools are required'}
    & (Join-Path $vs 'Common7/Tools/Launch-VsDevShell.ps1') -Arch amd64 -HostArch amd64 -SkipAutomaticLocation | Out-Null
    $env:VSLANG='1033'
    $cmake=Join-Path $vs 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
    $ninja=Join-Path $vs 'Common7/IDE/CommonExtensions/Microsoft/CMake/Ninja/ninja.exe'
    $env:PKG_CONFIG_PATH=Join-Path $root '.build/deps/vcpkg/installed/x64-windows/lib/pkgconfig'
    $pkgconf=Join-Path $root '.build/deps/vcpkg/installed/x64-windows/tools/pkgconf/pkgconf.exe'
    function Invoke-CMake([string[]]$Arguments) { & $cmake @Arguments; if($LASTEXITCODE -ne 0){throw "CMake failed: $Arguments"} }
    Invoke-CMake -Arguments @('-S','native/radio','-B','.build/radio-ninja','-G','Ninja',"-DCMAKE_MAKE_PROGRAM=$ninja",'-DCMAKE_BUILD_TYPE=Release',"-DPKG_CONFIG_EXECUTABLE=$pkgconf",'-DPKG_CONFIG_ARGN=','-DCMAKE_C_COMPILER=cl','-DCMAKE_CXX_COMPILER=cl')
    Invoke-CMake -Arguments @('--build','.build/radio-ninja','--target','AirDrop.Radio','--clean-first','--parallel','8')
    Invoke-CMake -Arguments @('-S','native/stack','-B','.build/stack-ninja','-G','Ninja',"-DCMAKE_MAKE_PROGRAM=$ninja",'-DCMAKE_BUILD_TYPE=Release','-DCMAKE_C_COMPILER=cl','-DCMAKE_CXX_COMPILER=cl')
    Invoke-CMake -Arguments @('--build','.build/stack-ninja','--clean-first','--parallel','8')
    $llvm=Join-Path $root '.build/deps/llvm-mingw-20261104-ucrt-x86_64/bin'
    $env:Path=$llvm+';'+$env:Path
    Invoke-CMake -Arguments @('-S','native/awdl','-B','.build/awdl','-G','Ninja',"-DCMAKE_MAKE_PROGRAM=$ninja",'-DCMAKE_BUILD_TYPE=Release',"-DCMAKE_C_COMPILER=$llvm/x86_64-w64-mingw32-clang.exe")
    Invoke-CMake -Arguments @('--build','.build/awdl','--parallel','8')
    # This pinned upstream defines executables but does not register CTest cases.
    foreach($name in @('frame_fixup','host_frame','redact','service_bridge','tcp_merge')) {
        & (Join-Path $root ".build/awdl/bin/${name}_test.exe")
        if($LASTEXITCODE -ne 0){throw "AWDL test failed: $name"}
        Write-Output "Passed: $name"
    }
    & (Join-Path $PSScriptRoot 'Package-Engine.ps1')
} finally { Pop-Location }
