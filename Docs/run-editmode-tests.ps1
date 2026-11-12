#!/usr/bin/env powershell
# Apogee Test Suite Runner
# Usage: powershell -ExecutionPolicy Bypass -File Docs/run-editmode-tests.ps1

$root = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
$runtimeDir = "C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.11"
$csc = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\DotNetSdkRoslyn\csc.dll"

$binDir = Join-Path $root "Docs\TestRunner\bin\Debug\net10.0"
New-Item -ItemType Directory -Force -Path $binDir | Out-Null

$runtimeConfig = @"
{
  "runtimeOptions": {
    "tfm": "net10.0",
    "framework": {
      "name": "Microsoft.NETCore.App",
      "version": "10.0.11"
    }
  }
}
"@
[System.IO.File]::WriteAllText((Join-Path $binDir "ApogeeLocalTestRunner.runtimeconfig.json"), $runtimeConfig)

$globalUsings = @"
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
"@
$globalUsingsPath = Join-Path $binDir "GlobalUsings.g.cs"
[System.IO.File]::WriteAllText($globalUsingsPath, $globalUsings)

# Copy unity and third party dlls if needed
$newtonsoftSrc = Join-Path $root "Library\PackageCache\com.unity.nuget.newtonsoft-json@3.2.1\Runtime\Newtonsoft.Json.dll"
Copy-Item -Path $newtonsoftSrc -Destination $binDir -Force -ErrorAction SilentlyContinue
Copy-Item -Path "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\Managed\UnityEngine\UnityEngine*.dll" -Destination $binDir -Force -ErrorAction SilentlyContinue

$sources = @()
$sources += Get-ChildItem -Path (Join-Path $root "Assets\_Project\Core\*.cs"), (Join-Path $root "Assets\_Project\Design\*.cs"), (Join-Path $root "Assets\_Project\Strategy\*.cs"), (Join-Path $root "Assets\_Project\Data\*.cs"), (Join-Path $root "Assets\_Project\Presentation\*.cs") -Recurse | Select-Object -ExpandProperty FullName
$sources += Get-ChildItem -Path (Join-Path $root "Docs\TestRunner\*.cs") | Select-Object -ExpandProperty FullName
$sources += $globalUsingsPath

$refs = @()
foreach ($file in (Get-ChildItem -Path "$runtimeDir\*.dll")) {
    try {
        [void][System.Reflection.AssemblyName]::GetAssemblyName($file.FullName)
        $refs += $file.FullName
    } catch {}
}
$refs += (Join-Path $binDir "Newtonsoft.Json.dll")
$refs += (Join-Path $binDir "UnityEngine.CoreModule.dll")
$refs += (Join-Path $binDir "UnityEngine.UIElementsModule.dll")
$refs += (Join-Path $binDir "UnityEngine.TextRenderingModule.dll")
$refs += (Join-Path $binDir "UnityEngine.AudioModule.dll")

$rsp = Join-Path $binDir "testrunner.rsp"
$outDll = Join-Path $binDir "ApogeeLocalTestRunner.dll"
$lines = @(
  "-target:exe",
  "-out:`"$outDll`"",
  "-langversion:latest",
  "-nullable:disable"
)
foreach ($r in $refs) { $lines += "-r:`"$r`"" }
foreach ($s in $sources) { $lines += "`"$s`"" }
[System.IO.File]::WriteAllLines($rsp, $lines)

dotnet $csc "@$rsp"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Test runner compilation failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

dotnet $outDll
exit $LASTEXITCODE
