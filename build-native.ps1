param([string]$OutputDirectory = 'native-dist')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$outputRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
if (-not $outputRoot.StartsWith($projectRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be inside the project.' }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'Visual Studio C++ Build Tools are required.' }
$vc = Get-ChildItem (Join-Path $vs 'VC\Tools\MSVC') -Directory | Sort-Object Name -Descending | Select-Object -First 1
$kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10'
$sdk = Get-ChildItem (Join-Path $kits 'Include') -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'um\mfapi.h') } | Sort-Object Name -Descending | Select-Object -First 1
$compiler = Join-Path $vc.FullName 'bin\Hostx64\x64\cl.exe'
$oldInclude = $env:INCLUDE
$oldLib = $env:LIB
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
try {
    $env:INCLUDE = @((Join-Path $vc.FullName 'include'), (Join-Path $sdk.FullName 'ucrt'), (Join-Path $sdk.FullName 'shared'), (Join-Path $sdk.FullName 'um'), (Join-Path $sdk.FullName 'winrt')) -join ';'
    $env:LIB = @((Join-Path $vc.FullName 'lib\x64'), (Join-Path $kits ('Lib\' + $sdk.Name + '\ucrt\x64')), (Join-Path $kits ('Lib\' + $sdk.Name + '\um\x64'))) -join ';'
    Push-Location $outputRoot
    try {
        $definition = Join-Path $projectRoot 'third_party\UnityCapture\UnityCaptureFilter.def'
        & $compiler /nologo /O2 /MT /LD /std:c++17 /EHsc /W3 /utf-8 /DNDEBUG /D_CRT_SECURE_NO_WARNINGS /D_HAS_EXCEPTIONS=0 `
            (Join-Path $projectRoot 'third_party\UnityCapture\UnityCaptureFilter.cpp') `
            (Join-Path $projectRoot 'third_party\UnityCapture\streams.cpp') `
            /link /OUT:PhoneUsbCameraFilter.dll "/DEF:$definition" `
            ole32.lib oleaut32.lib advapi32.lib user32.lib gdi32.lib uuid.lib strmiids.lib
        if ($LASTEXITCODE -ne 0) { throw 'Camera component build failed.' }
        & $compiler /nologo /O2 /MT /LD /std:c++17 /EHsc /W3 /utf-8 `
            (Join-Path $projectRoot 'native\PhoneCameraNative.cpp') `
            /link /OUT:PhoneCameraNative.dll mfplat.lib mf.lib mfuuid.lib wmcodecdspuuid.lib ole32.lib oleaut32.lib
        if ($LASTEXITCODE -ne 0) { throw 'Native decoder build failed.' }
        $csharp = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
        & $csharp /nologo /target:exe /platform:x64 /out:DirectUsbProbe.exe (Join-Path $projectRoot 'native\DirectUsbProbe.cs')
        if ($LASTEXITCODE -ne 0) { throw 'USB probe build failed.' }
        & $compiler /nologo /O2 /MT /std:c++17 /EHsc /W3 /utf-8 `
            (Join-Path $projectRoot 'native\FilterProbe.cpp') `
            /link /OUT:FilterProbe.exe ole32.lib oleaut32.lib strmiids.lib
        if ($LASTEXITCODE -ne 0) { throw 'DirectShow probe build failed.' }
        & $compiler /nologo /O2 /MT /std:c++17 /EHsc /W3 /utf-8 `
            (Join-Path $projectRoot 'native\NativeUnitTests.cpp') /link /OUT:NativeUnitTests.exe
        if ($LASTEXITCODE -ne 0) { throw 'Native unit test build failed.' }
        & .\NativeUnitTests.exe
        if ($LASTEXITCODE -ne 0) { throw 'Native unit tests failed.' }
    } finally { Pop-Location }
} finally { $env:INCLUDE=$oldInclude; $env:LIB=$oldLib }
Write-Host "Native prototype built (not installed): $outputRoot"
