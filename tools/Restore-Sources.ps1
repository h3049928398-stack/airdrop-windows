[CmdletBinding()]
param([switch]$WithBuildDependencies)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$lock=Get-Content (Join-Path $root 'docs/native.lock.json') -Raw | ConvertFrom-Json
foreach($source in $lock.sources) {
    if($source.path -like '.build/*' -and !$WithBuildDependencies){continue}
    $target=Join-Path $root $source.path
    if(!(Test-Path (Join-Path $target '.git'))) {
        New-Item -ItemType Directory -Force (Split-Path $target -Parent) | Out-Null
        & git clone --no-checkout $source.url $target
        if($LASTEXITCODE -ne 0){throw "Clone failed: $($source.path)"}
        & git -C $target checkout --detach $source.commit
        if($LASTEXITCODE -ne 0){throw "Checkout failed: $($source.path)"}
    }
    $actual=& git -C $target rev-parse HEAD
    if($actual -ne $source.commit){throw "Pinned revision mismatch: $target (preserving existing checkout)"}
    $changes=& git -C $target status --porcelain --untracked-files=no
    if($changes){throw "Upstream has changes, preserving it: $target"}
}
$winawdl=Join-Path $root 'upstream/winawdl'
& git -C $winawdl submodule update --init upstream/owl
if($LASTEXITCODE -ne 0){throw 'OWL restore failed'}
& git -C (Join-Path $winawdl 'upstream/owl') submodule update --init radiotap
if($LASTEXITCODE -ne 0){throw 'Radiotap restore failed'}
foreach($item in $lock.submodules.PSObject.Properties) {
    $actual=& git -C (Join-Path $root $item.Name) rev-parse HEAD
    if($actual -ne $item.Value){throw "Submodule revision mismatch: $($item.Name)"}
}
if($WithBuildDependencies) {
    foreach($download in $lock.downloads) {
        $archive=Join-Path $root $download.path
        if(!(Test-Path $archive)) {
            New-Item -ItemType Directory -Force (Split-Path $archive -Parent) | Out-Null
            Invoke-WebRequest -Uri $download.url -OutFile $archive
        }
        if((Get-FileHash $archive -Algorithm SHA256).Hash -ne $download.sha256){throw "Download hash mismatch: $archive"}
        if(!(Test-Path (Join-Path $root $download.directory))) {
            $destination=if($download.directory -eq '.build/python'){Join-Path $root $download.directory}else{Split-Path (Join-Path $root $download.directory) -Parent}
            Expand-Archive -LiteralPath $archive -DestinationPath $destination
        }
    }
    $vcpkg=Join-Path $root '.build/deps/vcpkg'
    if(!(Test-Path (Join-Path $vcpkg 'vcpkg.exe'))) { & (Join-Path $vcpkg 'bootstrap-vcpkg.bat') -disableMetrics; if($LASTEXITCODE -ne 0){throw 'vcpkg bootstrap failed'} }
    & (Join-Path $vcpkg 'vcpkg.exe') install libusb:x64-windows pkgconf:x64-windows --disable-metrics
    if($LASTEXITCODE -ne 0){throw 'Dependency build failed'}
}
Write-Output 'Independent source revisions verified. No driver or RF action performed.'
