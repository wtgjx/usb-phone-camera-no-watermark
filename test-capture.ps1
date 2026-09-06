param([string]$RuntimeDirectory='dist')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$testOutput = Join-Path $projectRoot 'test-output'
New-Item -ItemType Directory -Path $testOutput -Force | Out-Null
$testExe = Join-Path $testOutput 'native-capture-smoke-test.exe'
$dependencyDirectory = [IO.Path]::GetFullPath((Join-Path $projectRoot $RuntimeDirectory))
if (-not $dependencyDirectory.StartsWith($projectRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Runtime must be inside the project.' }
$probe = Join-Path $projectRoot 'native-dist\FilterProbe.exe'
if (-not (Test-Path -LiteralPath $probe)) { throw 'Run build-native.ps1 first.' }
Copy-Item -LiteralPath (Join-Path $dependencyDirectory 'PhoneCameraNative.dll') -Destination $testOutput -Force
$testManifest = Join-Path $projectRoot 'app.manifest'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /main:PhoneUsbCamera.CaptureSmokeTests `
    /out:$testExe /win32manifest:$testManifest `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
    /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll `
    (Join-Path $projectRoot 'PhoneUsbCamera.cs') `
    (Join-Path $projectRoot 'CameraStudioForm.cs') `
    (Join-Path $projectRoot 'BrandUI.cs') `
    (Join-Path $projectRoot 'PreviewWindowBridge.cs') `
    (Join-Path $projectRoot 'NativeCameraBridge.cs') `
    (Join-Path $projectRoot 'native\DirectUsbProbe.cs') `
    (Join-Path $projectRoot 'tests\CaptureSmokeTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Capture test compilation failed.' }
& $testExe $dependencyDirectory $probe
if ($LASTEXITCODE -ne 0) { throw 'Integrated capture hardware test failed.' }
