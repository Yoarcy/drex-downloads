$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$state=Join-Path $PSScriptRoot 'snapshot.json'
$lease=Join-Path $PSScriptRoot 'lease.json'
$status=Join-Path $PSScriptRoot 'status.txt'
$group='DrexDownloads.Exclusive.v1'
$mutex=New-Object Threading.Mutex($false,'Global\DrexDownloadsFirewallGuard')
if(!$mutex.WaitOne(0)){exit}
$policy=New-Object -ComObject HNetCfg.FwPolicy2
function Status($s){[IO.File]::WriteAllText($status,$s)}
function Restore {
 if(Test-Path $state){
  $s=Get-Content -Raw $state | ConvertFrom-Json
  foreach($p in $s.profiles){$policy.DefaultOutboundAction([int]$p.id)=[int]$p.outbound;$policy.FirewallEnabled([int]$p.id)=[bool]$p.enabled}
  foreach($r in @($policy.Rules)){if($r.Name -in $s.allowed){$r.Enabled=$true}}
  if($s.netProfiles){foreach($p in $s.netProfiles){Set-NetFirewallProfile -Name $p.Name -DefaultOutboundAction $p.DefaultOutboundAction}}
  foreach($r in @($policy.Rules)){if($r.Grouping -eq $group){$policy.Rules.Remove($r.Name)}}
  Remove-Item $state
 }
 Status 'Normal'
}
function Allow($name,$program,$service,$protocol=256,$ports='',$direction=2){
 $r=New-Object -ComObject HNetCfg.FWRule
 $r.Name='Drex Exclusive - '+$name;$r.Grouping=$group;$r.Direction=$direction;$r.Action=1;$r.Enabled=$true;$r.Profiles=7
 if($program){$r.ApplicationName=$program};if($service){$r.ServiceName=$service}
 $r.Protocol=$protocol;if($ports){$r.RemotePorts=$ports};$policy.Rules.Add($r)
}
function OptionalPrograms {
 # Missing optional software is normal on a portable installation.
 $paths=@(
  (Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe')
 )
 if(${env:ProgramFiles(x86)}){$paths+=(Join-Path ${env:ProgramFiles(x86)} 'Google\Chrome\Application\chrome.exe')}
 foreach($path in $paths | Select-Object -Unique){if(Test-Path -LiteralPath $path -PathType Leaf){[pscustomobject]@{Name='Chrome '+$path;Path=$path}}}
 # Do not grant the System transport exception on machines without active WSL.
 $wslRunning=@(Get-Process -Name 'wslhost','wslrelay','vmmemWSL' -ErrorAction SilentlyContinue).Count -gt 0
 if($wslRunning){
  foreach($path in @((Join-Path $env:ProgramFiles 'WSL\wslhost.exe'),(Join-Path $env:ProgramFiles 'WSL\wslrelay.exe'),(Join-Path $env:WINDIR 'System32\wsl.exe'),(Join-Path $env:WINDIR 'System32\wslhost.exe'))){
   if(Test-Path -LiteralPath $path -PathType Leaf){[pscustomobject]@{Name='WSL '+$path;Path=$path}}
  }
  [pscustomobject]@{Name='WSL System transport';Path='System'}
 }
}
function Activate {
 $profiles=@();foreach($id in 1,2,4){$profiles+=@{id=$id;enabled=$policy.FirewallEnabled($id);outbound=$policy.DefaultOutboundAction($id)}}
 $allowed=@($policy.Rules | Where-Object {$_.Enabled -and $_.Direction -eq 2 -and $_.Action -eq 1 -and $_.Grouping -ne $group} | ForEach-Object {$_.Name})
 $netProfiles=@(Get-NetFirewallProfile | Select-Object Name,@{n='DefaultOutboundAction';e={$_.DefaultOutboundAction.ToString()}})
 $json=@{profiles=$profiles;allowed=$allowed;netProfiles=$netProfiles}|ConvertTo-Json -Depth 5
 $bytes=[Text.Encoding]::UTF8.GetBytes($json);$f=[IO.File]::Open($state,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read);try{$f.Write($bytes,0,$bytes.Length);$f.Flush($true)}finally{$f.Dispose()}
 Allow 'Downloader' (Join-Path $root 'DrexDownloads.exe') ''
 foreach($optional in @(OptionalPrograms)){Allow $optional.Name $optional.Path ''}
 Allow 'DNS UDP' "$env:WINDIR\System32\svchost.exe" 'Dnscache' 17 '53'
 Allow 'DNS TCP' "$env:WINDIR\System32\svchost.exe" 'Dnscache' 6 '53'
 Allow 'DHCP' "$env:WINDIR\System32\svchost.exe" 'Dhcp' 17 '67,68,546,547'
 # Preserve previously disabled inbound filtering; leave pre-existing block rules intact.
 foreach($p in $profiles){if(!$p.enabled){$r=New-Object -ComObject HNetCfg.FWRule;$r.Name='Drex Exclusive - Preserve inbound '+$p.id;$r.Grouping=$group;$r.Direction=1;$r.Action=1;$r.Enabled=$true;$r.Profiles=$p.id;$policy.Rules.Add($r)}}
 foreach($r in @($policy.Rules)){if($r.Enabled -and $r.Direction -eq 2 -and $r.Action -eq 1 -and $r.Grouping -ne $group){$r.Enabled=$false}}
 foreach($id in 1,2,4){$policy.DefaultOutboundAction($id)=0;$policy.FirewallEnabled($id)=$true}
 Status 'Exclusivo: descargador + excepciones disponibles'
}
try {
 Restore
 while($true){
  try {
   $active=$false
   if(Test-Path $lease){$info=Get-Item $lease;if(((Get-Date).ToUniversalTime()-$info.LastWriteTimeUtc).TotalSeconds -lt 15){$j=Get-Content -Raw $lease|ConvertFrom-Json;$active=$j.enabled -and $j.active}}
   if($active){if(!(Test-Path $state)){Activate}}elseif(Test-Path $state){Restore}
  }catch{$err=$_;try{Restore}catch{};[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'error.txt'),$err.ToString()+' '+$err.InvocationInfo.PositionMessage);Status ('Error: '+$err.Exception.Message);Start-Sleep -Seconds 10}
  if(Test-Path $status){[IO.File]::SetLastWriteTimeUtc($status,[DateTime]::UtcNow)}
  Start-Sleep -Seconds 2
 }
}finally{try{Restore}catch{};$mutex.ReleaseMutex();$mutex.Dispose()}
