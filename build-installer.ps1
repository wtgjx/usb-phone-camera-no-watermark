param(
    [string]$Version = '3.0.1',
    [string]$OutputDirectory = 'release-dist\installer',
    [string]$AppSourceDirectory = '',
    [string]$IsccPath = '',
    [switch]$SkipAppBuild
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$appDisplayName = 'U' + [char]0x955C
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must use MAJOR.MINOR.PATCH.' }

$outputPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
if (-not $outputPath.StartsWith($projectRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be inside this project.'
}

if ($AppSourceDirectory) {
    $appSourcePath = [IO.Path]::GetFullPath((Join-Path $projectRoot $AppSourceDirectory))
} else {
    $stagingRelative = 'release-dist\installer-staging\app'
    $appSourcePath = Join-Path $projectRoot $stagingRelative
    if (-not $SkipAppBuild) {
        & (Join-Path $projectRoot 'build.ps1') -OutputDirectory $stagingRelative
    }
}

$requiredFiles = @(
    ($appDisplayName + '.exe'),
    'PhoneCameraNative.dll',
    'PhoneUsbCameraFilter.dll',
    'scrcpy\adb.exe',
    'scrcpy\scrcpy.exe',
    'scrcpy\scrcpy-server',
    'README.md',
    'LICENSE',
    'THIRD_PARTY_NOTICES.md',
    'third_party\UnityCapture\UnityCaptureFilter.cpp'
)
foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $appSourcePath $requiredFile))) {
        throw "Installer source is incomplete: $requiredFile"
    }
}

if (-not $IsccPath) {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $projectRoot '.tools\inno-setup\ISCC.exe')
    )
    $IsccPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath)) {
    throw 'Inno Setup 6 compiler was not found. Install Inno Setup 6 or pass -IsccPath.'
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$scriptPath = Join-Path $projectRoot 'installer\UCam.iss'
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$compilerOutput = Join-Path $temporaryRoot ('UCamInstallerBuild-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $compilerOutput -Force | Out-Null
try {
    & $IsccPath "/DMyAppVersion=$Version" "/DSourceRoot=$appSourcePath" "/DOutputDir=$compilerOutput" $scriptPath
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE." }

    $compiledInstaller = Join-Path $compilerOutput 'UCam-Setup-win64.exe'
    if (-not (Test-Path -LiteralPath $compiledInstaller)) { throw 'Installer output was not created.' }
    $installerPath = Join-Path $outputPath 'UCam-Setup-win64.exe'
    Copy-Item -LiteralPath $compiledInstaller -Destination $installerPath -Force
} finally {
    $resolvedCompilerOutput = [IO.Path]::GetFullPath($compilerOutput).TrimEnd('\')
    if ($resolvedCompilerOutput.StartsWith($temporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedCompilerOutput)) {
        Remove-Item -LiteralPath $resolvedCompilerOutput -Recurse -Force
    }
}

$hash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
$hashPath = $installerPath + '.sha256'
[IO.File]::WriteAllText($hashPath, "$hash  $([IO.Path]::GetFileName($installerPath))`r`n", [Text.UTF8Encoding]::new($false))

Write-Host "Installer: $installerPath"
Write-Host "SHA256: $hash"
Write-Host "Checksum file: $hashPath"
