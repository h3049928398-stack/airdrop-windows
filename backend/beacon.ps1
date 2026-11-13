# Windows PowerShell 5.1. Independent BLE helper; -ValidateOnly never advertises.
param([switch]$ValidateOnly)
$ErrorActionPreference='Stop'
try {
    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    [void][Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementPublisher,Windows.Devices.Bluetooth.Advertisement,ContentType=WindowsRuntime]
    [void][Windows.Devices.Bluetooth.Advertisement.BluetoothLEManufacturerData,Windows.Devices.Bluetooth.Advertisement,ContentType=WindowsRuntime]
    $parameters=New-Object CodeDom.Compiler.CompilerParameters
    $parameters.GenerateInMemory=$true
    [void]$parameters.ReferencedAssemblies.Add([Reflection.Assembly]::Load('System.Runtime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a').Location)
    [void]$parameters.ReferencedAssemblies.Add("$env:windir\System32\WinMetadata\Windows.Devices.winmd")
    [void]$parameters.ReferencedAssemblies.Add("$env:windir\System32\WinMetadata\Windows.Foundation.winmd")
    Add-Type -CompilerParameters $parameters -TypeDefinition @'
using Windows.Devices.Bluetooth.Advertisement;
public static class AirDropBlePayload {
    public static void Attach(BluetoothLEAdvertisementPublisher publisher,BluetoothLEManufacturerData section) {
        publisher.Advertisement.ManufacturerData.Add(section);
    }
}
'@
    $hashes=New-Object byte[] 8
    $random=[Security.Cryptography.RandomNumberGenerator]::Create()
    $random.GetBytes($hashes); $random.Dispose()
    [byte[]]$bytes=@(5,18,0,0,0,0,0,0,0,0,1)+$hashes+@(0)
    $section=New-Object Windows.Devices.Bluetooth.Advertisement.BluetoothLEManufacturerData
    $section.CompanyId=76
    $section.Data=[Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions]::AsBuffer($bytes)
    $publisher=New-Object Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementPublisher
    [AirDropBlePayload]::Attach($publisher,$section)
    if($ValidateOnly) { Write-Output 'BLE payload constructed; no advertisement started.'; exit 0 }
    $publisher.Start()
    for($attempt=0;$attempt -lt 150 -and "$($publisher.Status)" -notin @('Started','Aborted');$attempt++) { Start-Sleep -Milliseconds 100 }
    Write-Output "BLE publisher: $($publisher.Status)"
    if("$($publisher.Status)" -ne 'Started') { exit 1 }
    try { while($true) { Start-Sleep -Seconds 1 } } finally { $publisher.Stop() }
} catch { Write-Error $_; exit 1 }
