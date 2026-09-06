param([ValidateSet('Install','Uninstall','Status')][string]$Mode='Status', [string]$SourceDirectory='')
$ErrorActionPreference='Stop'
$owner='PhoneUsbCamera.v3'
$camera='{87493A16-2CF8-4781-9A51-8B0674F10010}'
$properties='{87493A16-2CF8-4781-9A51-8B0674F10011}'
$category='{860BB310-5D01-11D0-BD3B-00A0C911CE86}'
$keys=@("Software\Classes\CLSID\$camera", "Software\Classes\CLSID\$properties", "Software\Classes\CLSID\$category\Instance\$camera")
$base=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,[Microsoft.Win32.RegistryView]::Registry64)
try {
    foreach($keyPath in $keys) {
        $key=$base.OpenSubKey($keyPath)
        if($key) {
            try { if($key.GetValue('PhoneUsbCamera.Owner') -ne $owner) { throw "Refusing to change unowned registration: $keyPath" } }
            finally { $key.Dispose() }
        }
    }
    if($Mode -eq 'Status') {
        $key=$base.OpenSubKey($keys[0]+'\InprocServer32')
        if($key) { try { Write-Output ('REGISTERED_DLL='+$key.GetValue('')) } finally { $key.Dispose() } }
        else { Write-Output 'NOT_REGISTERED' }
        return
    }
    if($Mode -eq 'Uninstall') {
        # Only three exact project-owned keys; keep DLL files recoverable on disk.
        foreach($keyPath in $keys) { $base.DeleteSubKeyTree($keyPath,$false) }
        Write-Output 'Unregistered Phone USB Camera for this user. Component files were retained; OBS and other cameras were not changed.'
        return
    }
    if (-not $SourceDirectory) {
        $SourceDirectory = if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'PhoneUsbCameraFilter.dll')) { '.' } else { 'native-dist' }
    }
    $source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot $SourceDirectory)).TrimEnd('\')
    if($source -ne $PSScriptRoot.TrimEnd('\') -and -not $source.StartsWith($PSScriptRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Source must be inside this project.' }
    $dll=Join-Path $source 'PhoneUsbCameraFilter.dll'
    $hash=(Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant()
    $componentRoot=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'PhoneUsbCamera\VirtualCamera'
    $installPath=Join-Path $componentRoot $hash.Substring(0,16)
    New-Item -ItemType Directory -Path $installPath -Force | Out-Null
    $installedDll=Join-Path $installPath 'PhoneUsbCameraFilter.dll'
    if(-not (Test-Path -LiteralPath $installedDll)) { Copy-Item -LiteralPath $dll -Destination $installedDll }
    if((Get-FileHash -LiteralPath $installedDll -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw 'Installed component hash mismatch.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') -Destination $installPath -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'third_party\UnityCapture\UnityCaptureFilter.cpp') -Destination (Join-Path $installPath 'Filter-source-and-license.cpp') -Force
    $snapshot=@{}
    function Read-RegistryTree($path) {
        $key=$base.OpenSubKey($path)
        if(-not $key) { return $null }
        try {
            $values=@{}; foreach($name in $key.GetValueNames()) { $values[$name]=@{ value=$key.GetValue($name); kind=[int]$key.GetValueKind($name) } }
            $children=@{}; foreach($child in $key.GetSubKeyNames()) { $children[$child]=Read-RegistryTree ($path+'\'+$child) }
            return @{ values=$values; children=$children }
        } finally { $key.Dispose() }
    }
    function Restore-RegistryTree($path,$tree) {
        $base.DeleteSubKeyTree($path,$false)
        if(-not $tree) { return }
        $key=$base.CreateSubKey($path)
        try { foreach($entry in $tree.values.GetEnumerator()) { $key.SetValue($entry.Key,$entry.Value.value,[Microsoft.Win32.RegistryValueKind]$entry.Value.kind) } }
        finally { $key.Dispose() }
        foreach($child in $tree.children.GetEnumerator()) { Restore-RegistryTree ($path+'\'+$child.Key) $child.Value }
    }
    foreach($keyPath in $keys) { $snapshot[$keyPath]=Read-RegistryTree $keyPath }
    try {
        foreach($keyPath in $keys) {
            $key=$base.CreateSubKey($keyPath)
            try { $key.SetValue('PhoneUsbCamera.Owner',$owner) } finally { $key.Dispose() }
        }
        foreach($keyPath in $keys[0..1]) {
            $key=$base.CreateSubKey($keyPath+'\InprocServer32')
            try { $key.SetValue('',$installedDll); $key.SetValue('ThreadingModel','Both') } finally { $key.Dispose() }
        }
        $key=$base.CreateSubKey($keys[2])
        try { $key.SetValue('CLSID',$camera); $key.SetValue('FriendlyName','Phone USB Camera'); $key.SetValue('DevicePath','phone-usb-camera:v3') }
        finally { $key.Dispose() }
    } catch {
        foreach($keyPath in $keys) { Restore-RegistryTree $keyPath $snapshot[$keyPath] }
        throw
    }
    Write-Output ('REGISTERED_DLL='+$installedDll)
    Write-Output ('SHA256='+$hash)
    Write-Output 'SCOPE=CurrentUser / 64-bit DirectShow. No OBS changes. Rollback: install-camera.ps1 -Mode Uninstall'
} finally { $base.Dispose() }
