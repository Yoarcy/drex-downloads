$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:exe /reference:System.dll /reference:System.Core.dll /reference:System.Runtime.Serialization.dll /reference:System.Security.dll "/out:$PSScriptRoot\EngineChecks.exe" "$root\src\Core.cs" "$PSScriptRoot\EngineChecks.cs"
if($LASTEXITCODE -ne 0){throw 'Test harness compilation failed'}
$python=Get-Command python.exe -ErrorAction Stop
& $python.Source "$PSScriptRoot\integration.py"
if($LASTEXITCODE -ne 0){throw 'Some checks failed: see latest-report.json'}
