$global:isResolving = $false

[System.AppDomain]::CurrentDomain.add_AssemblyResolve({
    param($sender, $eventArgs)
    if ($global:isResolving -or -not $eventArgs -or -not $eventArgs.Name) { return $null }
    $global:isResolving = $true
    try {
        $name = $eventArgs.Name.Split(',')[0]
        $known = @{
            "netstandard" = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\MonoBleedingEdge\lib\mono\4.7.1-api\Facades\netstandard.dll"
            "Newtonsoft.Json" = "C:\Users\bmiko\.nuget\packages\newtonsoft.json\13.0.4\lib\net45\Newtonsoft.Json.dll"
            "nunit.framework" = "Library\PackageCache\com.unity.ext.nunit@1.0.6\net35\unity-custom\nunit.framework.dll"
            "UnityEngine" = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\Managed\UnityEngine\UnityEngine.dll"
            "Apogee.Core" = "Temp\bin\Debug\Apogee.Core.dll"
            "Apogee.Design" = "Temp\bin\Debug\Apogee.Design.dll"
            "Apogee.Data" = "Temp\bin\Debug\Apogee.Data.dll"
        }

        if ($known.ContainsKey($name)) {
            $p = $known[$name]
            if (Test-Path $p) {
                return [System.Reflection.Assembly]::LoadFrom((Resolve-Path $p).Path)
            }
        }
    }
    finally {
        $global:isResolving = $false
    }
    return $null
})

# Preload
$preload = @(
    "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\MonoBleedingEdge\lib\mono\4.7.1-api\Facades\netstandard.dll",
    "C:\Users\bmiko\.nuget\packages\newtonsoft.json\13.0.4\lib\net45\Newtonsoft.Json.dll",
    "Library\PackageCache\com.unity.ext.nunit@1.0.6\net35\unity-custom\nunit.framework.dll",
    "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data\Managed\UnityEngine\UnityEngine.dll",
    "Temp\bin\Debug\Apogee.Core.dll",
    "Temp\bin\Debug\Apogee.Design.dll",
    "Temp\bin\Debug\Apogee.Data.dll"
)

foreach ($dll in $preload) {
    if (Test-Path $dll) {
        [System.Reflection.Assembly]::LoadFrom((Resolve-Path $dll).Path) | Out-Null
    }
}

$editModeDll = (Resolve-Path "Temp\bin\Debug\Apogee.Tests.EditMode.dll").Path
$asm = [System.Reflection.Assembly]::LoadFrom($editModeDll)

$passCount = 0
$failCount = 0
$failedTests = @()

$types = $asm.GetTypes() | Where-Object { $_.IsClass -and -not $_.IsAbstract }

foreach ($type in $types) {
    $testMethods = $type.GetMethods() | Where-Object { 
        $_.GetCustomAttributes($false) | Where-Object { $_.GetType().Name -eq "TestAttribute" }
    }

    if ($testMethods.Count -gt 0) {
        Write-Host "`n=== $($type.Name) ($($testMethods.Count) tests) ===" -ForegroundColor Cyan
        $instance = [Activator]::CreateInstance($type)

        $setupMethods = $type.GetMethods() | Where-Object { 
            $_.GetCustomAttributes($false) | Where-Object { $_.GetType().Name -eq "SetUpAttribute" }
        }

        foreach ($m in $testMethods) {
            try {
                foreach ($s in $setupMethods) { $s.Invoke($instance, @()) }
                $m.Invoke($instance, @())
                $passCount++
                Write-Host "  [PASS] $($m.Name)" -ForegroundColor Green
            }
            catch {
                $failCount++
                $inner = $_.Exception.InnerException
                $msg = if ($inner) { "$($inner.GetType().Name): $($inner.Message)" } else { $_.Exception.Message }
                $failedTests += "$($type.Name).$($m.Name): $msg"
                Write-Host "  [FAIL] $($m.Name): $msg" -ForegroundColor Red
            }
        }
    }
}

Write-Host "`n----------------------------------------" -ForegroundColor White
Write-Host "TEST RESULTS: Passed: $passCount, Failed: $failCount" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })

if ($failCount -gt 0) {
    Write-Host "`nFailures:" -ForegroundColor Red
    $failedTests | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
} else {
    exit 0
}
