$ErrorActionPreference='Stop'
$taskOutput=Join-Path $PSScriptRoot 'test-output'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskGenerator=Join-Path $taskOutput 'BuildBrandIcon.exe'
& $taskCompiler /nologo /target:exe /out:$taskGenerator /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'tools\BuildBrandIcon.cs')
if ($LASTEXITCODE -ne 0) { throw 'Icon builder compilation failed.' }
& $taskGenerator (Join-Path $PSScriptRoot 'assets\ucam.png') (Join-Path $PSScriptRoot 'assets\ucam.ico')
if ($LASTEXITCODE -ne 0) { throw 'Icon packaging failed.' }
Write-Output 'UCam icon packaged at 16/24/32/48/64/128/256 pixels; original artwork preserved.'
