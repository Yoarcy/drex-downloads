$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'build.ps1')
$dist=Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$stage=Join-Path $dist ('stage-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $stage | Out-Null
$files=@('DrexDownloads.exe','DrexDownloads.exe.config','ABRIR_DREX_DOWNLOADS.bat','LEEME.txt','README.md','CHANGELOG.md','assets/drex-downloads.ico','assets/drex-downloads.png','assets/icon-prompt.txt','docs/BUILD.md','docs/NETWORK.md','docs/ARQUITECTURA.md')
foreach($name in $files){$target=Join-Path $stage $name;New-Item -ItemType Directory -Force (Split-Path $target -Parent)|Out-Null;Copy-Item (Join-Path $root $name) $target}
$zip=Join-Path $dist 'DrexDownloads-1.0.7-portable.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
if(Test-Path $zip){Remove-Item $zip}
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip)
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$setup=Join-Path $dist 'DrexDownloads-Setup-1.0.7.exe'
& $csc /nologo /target:winexe /optimize+ /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:Microsoft.CSharp.dll "/win32icon:$root\assets\drex-downloads.ico" "/resource:$zip,payload.zip" "/out:$setup" "$PSScriptRoot\Installer.cs"
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
@($zip,$setup)|ForEach-Object { $h=Get-FileHash $_ -Algorithm SHA256; $h.Hash.ToLower()+'  '+[IO.Path]::GetFileName($_) }|Set-Content (Join-Path $dist 'SHA256SUMS.txt') -Encoding ASCII
Write-Output 'Distribution ready in dist/'
