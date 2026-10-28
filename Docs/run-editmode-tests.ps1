#!/usr/bin/env powershell
# Apogee Test Suite Runner
# Usage: powershell -File Docs/run-editmode-tests.ps1

$root = Split-Path -Parent $MyInvocation.MyCommand.Path | Split-Path -Parent
$runnerProj = Join-Path $root "Docs/TestRunner/ApogeeLocalTestRunner.csproj"

dotnet run --project $runnerProj --nologo
exit $LASTEXITCODE
