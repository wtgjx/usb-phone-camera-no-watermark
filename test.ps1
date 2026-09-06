$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$testOutput = Join-Path $projectRoot 'test-output'
New-Item -ItemType Directory -Path $testOutput -Force | Out-Null
$testExe = Join-Path $testOutput 'UiSmokeTests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testManifest = Join-Path $projectRoot 'app.manifest'
& (Join-Path $projectRoot 'build-brand.ps1')
& $compiler /nologo /target:exe /platform:x64 /main:PhoneUsbCamera.UiSmokeTests `
    /out:$testExe /win32manifest:$testManifest `
    /resource:$(Join-Path $projectRoot 'assets\ucam.ico'),UCam.Icon.ico `
    /resource:$(Join-Path $projectRoot 'assets\ucam.png'),UCam.Logo.png `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
    /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll `
    (Join-Path $projectRoot 'PhoneUsbCamera.cs') `
    (Join-Path $projectRoot 'CameraStudioForm.cs') `
    (Join-Path $projectRoot 'BrandUI.cs') `
    (Join-Path $projectRoot 'PreviewWindowBridge.cs') `
    (Join-Path $projectRoot 'NativeCameraBridge.cs') `
    (Join-Path $projectRoot 'native\DirectUsbProbe.cs') `
    (Join-Path $projectRoot 'tests\UiSmokeTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'UI layout smoke tests failed.' }
