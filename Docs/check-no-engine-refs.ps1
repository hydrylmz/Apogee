#!/usr/bin/env powershell
# Apogee M0-01 Acceptance: Core/Design/Strategy assemblies must not reference UnityEngine
# Usage: powershell -File Docs/check-no-engine-refs.ps1

$noEngineDirs = @("Assets/_Project/Core", "Assets/_Project/Design", "Assets/_Project/Strategy")
$forbidden = @("using UnityEngine", "using UnityEditor", "UnityEngine\.", "UnityEditor\.")
$root = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
$foundErrors = @()

foreach ($dir in $noEngineDirs) {
    $fullDir = Join-Path $root $dir
    if (-not (Test-Path $fullDir)) { continue }
    $csFiles = Get-ChildItem -Path $fullDir -Filter "*.cs" -Recurse
    foreach ($file in $csFiles) {
        $lines = Get-Content $file.FullName
        for ($i = 0; $i -lt $lines.Count; $i++) {
            foreach ($pattern in $forbidden) {
                if ($lines[$i] -match $pattern) {
                    $foundErrors += "$($file.FullName):$($i+1) - Forbidden reference: '$pattern'"
                }
            }
        }
    }
}

if ($foundErrors.Count -eq 0) {
    Write-Host "[OK] No UnityEngine references found in Core/Design/Strategy." -ForegroundColor Green
    exit 0
} else {
    Write-Host "[ERROR] Forbidden references found:" -ForegroundColor Red
    $foundErrors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}
