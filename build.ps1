param([string]$OutputDirectory = 'dist', [switch]$SkipNativeBuild)
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePath = Join-Path $projectRoot 'PhoneUsbCamera.cs'
$uiSourcePath = Join-Path $projectRoot 'CameraStudioForm.cs'
$previewSourcePath = Join-Path $projectRoot 'PreviewWindowBridge.cs'
$nativeSourcePath = Join-Path $projectRoot 'NativeCameraBridge.cs'
$usbSourcePath = Join-Path $projectRoot 'native\DirectUsbProbe.cs'
$manifestPath = Join-Path $projectRoot 'app.manifest'
$vendorRoot = Join-Path $projectRoot 'vendor'
$vendorPath = Join-Path $projectRoot 'vendor\scrcpy-win64-v4.1'
$distPath = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
if (-not $distPath.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be inside the project directory.'
}
$scrcpyDistPath = Join-Path $distPath 'scrcpy'
$outputPath = Join-Path $distPath 'U镜.exe'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$scrcpyDownloadUri = 'https://github.com/Genymobile/scrcpy/releases/download/v4.1/scrcpy-win64-v4.1.zip'
$scrcpyExpectedHash = '5B12172B3264B2889F4583EE64752CE832E29BC8B1089DCA81093459697165DB'

if (-not (Test-Path -LiteralPath $compilerPath)) {
    throw "未找到 Windows C# 编译器：$compilerPath"
}

& (Join-Path $projectRoot 'build-brand.ps1')
if (-not $SkipNativeBuild) { & (Join-Path $projectRoot 'build-native.ps1') }
foreach ($component in @('PhoneCameraNative.dll','PhoneUsbCameraFilter.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $projectRoot ('native-dist\' + $component)))) {
        throw "Missing $component. Run build-native.ps1 first or omit -SkipNativeBuild."
    }
}

if (-not (Test-Path -LiteralPath (Join-Path $vendorPath 'scrcpy.exe'))) {
    Write-Host '未找到本地 scrcpy 4.1，正在下载官方 Windows 64-bit 发行包…'
    New-Item -ItemType Directory -Path $vendorRoot -Force | Out-Null
    $scrcpyArchivePath = Join-Path ([IO.Path]::GetTempPath()) 'phone-usb-camera-scrcpy-win64-v4.1.zip'
    Invoke-WebRequest -UseBasicParsing -Uri $scrcpyDownloadUri -OutFile $scrcpyArchivePath
    $scrcpyActualHash = (Get-FileHash -LiteralPath $scrcpyArchivePath -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($scrcpyActualHash -ne $scrcpyExpectedHash) {
        throw "scrcpy 下载文件校验失败。期望：$scrcpyExpectedHash，实际：$scrcpyActualHash"
    }
    Expand-Archive -LiteralPath $scrcpyArchivePath -DestinationPath $vendorRoot -Force
    Remove-Item -LiteralPath $scrcpyArchivePath -Force
}

if (-not (Test-Path -LiteralPath (Join-Path $vendorPath 'scrcpy.exe'))) {
    throw "scrcpy 4.1 解压后仍未找到 scrcpy.exe：$vendorPath"
}

New-Item -ItemType Directory -Path $distPath -Force | Out-Null
New-Item -ItemType Directory -Path $scrcpyDistPath -Force | Out-Null

$iconPath = Join-Path $projectRoot 'assets\ucam.ico'
$logoPath = Join-Path $projectRoot 'assets\ucam.png'
$compilerArguments = @(
    '/nologo',
    '/main:PhoneUsbCamera.Program',
    '/target:winexe',
    '/platform:x64',
    '/optimize+',
    "/win32manifest:$manifestPath",
    "/win32icon:$iconPath",
    "/resource:$iconPath,UCam.Icon.ico",
    "/resource:$logoPath,UCam.Logo.png",
    "/out:$outputPath",
    '/reference:System.dll',
    '/reference:System.Core.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Web.Extensions.dll',
    '/reference:System.Windows.Forms.dll',
    $sourcePath,
    (Join-Path $projectRoot 'BrandUI.cs'),
    $uiSourcePath,
    $previewSourcePath,
    $nativeSourcePath,
    $usbSourcePath
)
& $compilerPath $compilerArguments

if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码：$LASTEXITCODE"
}

Copy-Item -Path (Join-Path $vendorPath '*') -Destination $scrcpyDistPath -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'native-dist\PhoneCameraNative.dll') -Destination $distPath -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'native-dist\PhoneUsbCameraFilter.dll') -Destination $distPath -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $distPath 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'NATIVE_CAMERA_PROTOTYPE.md') -Destination $distPath -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'UI_DESIGN.md') -Destination $distPath -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination (Join-Path $distPath 'LICENSE') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $distPath 'THIRD_PARTY_NOTICES.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'install-camera.ps1') -Destination $distPath -Force
$licensePath = Join-Path $distPath 'third_party\UnityCapture'
New-Item -ItemType Directory -Path $licensePath -Force | Out-Null
foreach ($licenseFile in @('UnityCaptureFilter.cpp','streams.cpp','streams.h','UPSTREAM.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot ('third_party\UnityCapture\' + $licenseFile)) -Destination $licensePath -Force
}

Write-Host "构建完成：$outputPath"
Write-Host "scrcpy 运行库：$scrcpyDistPath"
