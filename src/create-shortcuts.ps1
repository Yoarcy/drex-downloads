$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$shell=New-Object -ComObject WScript.Shell
foreach($folder in @([Environment]::GetFolderPath('Desktop'),$root)){
 $path=Join-Path $folder 'Drex Downloads.lnk'
 if(Test-Path $path){Copy-Item $path ($path+'.bak') -Force}
 $link=$shell.CreateShortcut($path)
 $link.TargetPath=Join-Path $root 'DrexDownloads.exe'
 $link.WorkingDirectory=$root
 $link.Description='Gestor de descargas con recuperacion por bloques'
 $link.Save()
 Write-Output $path
}
