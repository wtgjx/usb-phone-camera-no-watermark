param(
    [string]$Version = '3.0.1',
    [string]$InstallerDirectory = 'release-dist\installer'
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$installerPath = Join-Path ([IO.Path]::GetFullPath((Join-Path $projectRoot $InstallerDirectory))) 'UCam-Setup-win64.exe'
$checksumPath = $installerPath + '.sha256'

if (-not (Test-Path -LiteralPath $installerPath)) { throw "Missing installer: $installerPath" }
if (-not (Test-Path -LiteralPath $checksumPath)) { throw "Missing checksum: $checksumPath" }

$installer = Get-Item -LiteralPath $installerPath
if ($installer.Length -lt 10MB) { throw 'Installer is unexpectedly small.' }
if ($installer.VersionInfo.ProductName.Trim() -ne 'UCam') { throw 'Unexpected installer product name.' }
if ($installer.VersionInfo.FileVersion.Trim() -ne ($Version + '.0')) { throw 'Unexpected installer file version.' }

$actualHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumLine = (Get-Content -LiteralPath $checksumPath -Raw).Trim()
if ($checksumLine -ne ($actualHash + '  ' + [IO.Path]::GetFileName($installerPath))) {
    throw 'SHA-256 checksum file does not match the installer.'
}

$issPath = Join-Path $projectRoot 'installer\UCam.iss'
$iss = [IO.File]::ReadAllText($issPath, [Text.UTF8Encoding]::new($false))
foreach ($requiredMarker in @(
    'PrivilegesRequired=lowest',
    'Root: HKCU64',
    'PhoneUsbCamera.Owner',
    'Flags: uninsdeletekey',
    'Name: "{autodesktop}',
    'Filename: "{app}\U'
)) {
    if (-not $iss.Contains($requiredMarker)) { throw "Installer definition is missing: $requiredMarker" }
}
if ($iss.Contains('Source: "{#SourceRoot}\install-camera.ps1"')) {
    throw 'The end-user installer must not ship the manual PowerShell registration script.'
}

$signature = Get-AuthenticodeSignature -LiteralPath $installerPath
Write-Host "PASS: installer metadata, payload size, checksum, desktop shortcut and owned HKCU64 registration definition."
Write-Host "INSTALLER=$installerPath"
Write-Host "SHA256=$actualHash"
Write-Host "SIGNATURE=$($signature.Status)"
