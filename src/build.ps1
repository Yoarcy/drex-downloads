$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /utf8output "/win32icon:$root\assets\drex-downloads.ico" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll /reference:System.Security.dll "/out:$root\DrexDownloads.exe" "$PSScriptRoot\Core.cs" "$PSScriptRoot\App.cs" "$PSScriptRoot\NetworkControl.cs" "$PSScriptRoot\RestartControl.cs" "/resource:$root\network\guard.ps1,Drex.NetworkGuard"
if ($LASTEXITCODE -ne 0) { throw 'La compilación falló' }
