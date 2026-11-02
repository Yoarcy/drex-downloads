$ErrorActionPreference='Stop'
if(!$DrexRoot){throw 'This supervisor must be launched by Drex Downloads'}
$root=$DrexRoot
$runtime=Join-Path $root 'network'
[IO.Directory]::CreateDirectory($runtime)|Out-Null
$state=Join-Path $runtime 'snapshot.json'
$lease=Join-Path $runtime 'lease.json'
$status=Join-Path $runtime 'status.txt'
$group='DrexDownloads.Exclusive.v1'
# Migrate only the legacy task belonging to this installation, while it is idle.
$legacy=Get-ScheduledTask -TaskName 'Drex Downloads - Network Guard' -ErrorAction SilentlyContinue
if($legacy){
 $expected=Join-Path $runtime 'guard.ps1'
 if(@($legacy.Actions | Where-Object {$_.Arguments -like ('*'+$expected+'*')}).Count -eq 0){throw 'Another installation owns the legacy supervisor'}
 if(Test-Path $state){throw 'Disable the legacy exclusive mode before upgrading'}
 Stop-ScheduledTask -TaskName $legacy.TaskName
 Unregister-ScheduledTask -TaskName $legacy.TaskName -Confirm:$false
 Start-Sleep -Milliseconds 500
}
# Automatic reboot recovery uses an embedded helper in protected Program Files.
# No external script is installed or executed by the recovery task.
if(!$DrexRecoveryOnly){
 $safeDir=Join-Path $env:ProgramFiles 'DrexDownloads-Recovery'
 [IO.Directory]::CreateDirectory($safeDir)|Out-Null
 if((Get-Item $safeDir).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Unsafe recovery directory'}
 $safeExe=Join-Path $safeDir 'DrexDownloadsRecovery.exe'
 if((Test-Path $safeExe) -and ((Get-Item $safeExe).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Unsafe recovery executable'}
 $source=Join-Path $root 'DrexDownloads.exe'
 if(!(Test-Path $safeExe) -or (Get-FileHash $source).Hash -ne (Get-FileHash $safeExe).Hash){Copy-Item -LiteralPath $source -Destination $safeExe -Force}
 $encodedRoot=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($root))
 $action=New-ScheduledTaskAction -Execute $safeExe -Argument ('--network-recover '+$encodedRoot)
 $settings=New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 3) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
 Register-ScheduledTask -TaskName 'Drex Downloads - Integrated Recovery' -Action $action -Settings $settings -Trigger (New-ScheduledTaskTrigger -AtStartup) -User 'SYSTEM' -RunLevel Highest -Force | Out-Null
}
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
function Allow($name,$program,$service,$protocol=256,$ports='',$direction=2,$remote=''){
 $r=New-Object -ComObject HNetCfg.FWRule
 $r.Name='Drex Exclusive - '+$name;$r.Grouping=$group;$r.Direction=$direction;$r.Action=1;$r.Enabled=$true;$r.Profiles=7
 if($program){$r.ApplicationName=$program};if($service){$r.ServiceName=$service}
 if($remote){$r.RemoteAddresses=$remote};$r.Protocol=$protocol;if($ports){$r.RemotePorts=$ports};$policy.Rules.Add($r)
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
 # LAN remains available to every program, including new processes and RDP.
 Allow 'LAN local subnet' '' '' 256 '' 2 'LocalSubnet'
 Allow 'LAN private IPv4' '' '' 256 '' 2 '10.0.0.0/8,172.16.0.0/12,192.168.0.0/16,169.254.0.0/16'
 Allow 'LAN private IPv6' '' '' 256 '' 2 'fc00::/7,fe80::/10'
 Allow 'LAN discovery' '' '' 256 '' 2 '224.0.0.0/24,ff02::/16'
 Allow 'Downloader' (Join-Path $root 'DrexDownloads.exe') ''
 foreach($optional in @(OptionalPrograms)){Allow $optional.Name $optional.Path ''}
 Allow 'DNS UDP' "$env:WINDIR\System32\svchost.exe" 'Dnscache' 17 '53'
 Allow 'DNS TCP' "$env:WINDIR\System32\svchost.exe" 'Dnscache' 6 '53'
 Allow 'DHCP' "$env:WINDIR\System32\svchost.exe" 'Dhcp' 17 '67,68,546,547'
 # Preserve previously disabled inbound filtering; leave pre-existing block rules intact.
 foreach($p in $profiles){if(!$p.enabled){$r=New-Object -ComObject HNetCfg.FWRule;$r.Name='Drex Exclusive - Preserve inbound '+$p.id;$r.Grouping=$group;$r.Direction=1;$r.Action=1;$r.Enabled=$true;$r.Profiles=$p.id;$policy.Rules.Add($r)}}
 foreach($r in @($policy.Rules)){if($r.Enabled -and $r.Direction -eq 2 -and $r.Action -eq 1 -and $r.Grouping -ne $group){$r.Enabled=$false}}
 foreach($id in 1,2,4){$policy.DefaultOutboundAction($id)=0;$policy.FirewallEnabled($id)=$true}
 Status 'Exclusivo: Internet limitado; LAN libre'
}
try {
 Restore
 if($DrexRecoveryOnly){exit}
 [IO.File]::WriteAllText((Join-Path $runtime 'integrated.version'),'1.0.6')
 while($true){
  try {
   $active=$false
   if(!(Test-Path $lease) -or ((Get-Date).ToUniversalTime()-(Get-Item $lease).LastWriteTimeUtc).TotalSeconds -gt 20){Restore;break}
   if(Test-Path $lease){$info=Get-Item $lease;if(((Get-Date).ToUniversalTime()-$info.LastWriteTimeUtc).TotalSeconds -lt 15){$j=Get-Content -Raw $lease|ConvertFrom-Json;$active=$j.enabled -and $j.active}}
   if($active){if(!(Test-Path $state)){Activate}}elseif(Test-Path $state){Restore}
  }catch{$err=$_;try{Restore}catch{};[IO.File]::WriteAllText((Join-Path $runtime 'error.txt'),$err.ToString()+' '+$err.InvocationInfo.PositionMessage);Status ('Error: '+$err.Exception.Message);Start-Sleep -Seconds 10}
  if(Test-Path $status){[IO.File]::SetLastWriteTimeUtc($status,[DateTime]::UtcNow)}
  Start-Sleep -Seconds 2
 }
}finally{try{Restore}catch{};Remove-Item (Join-Path $runtime 'integrated.version') -ErrorAction SilentlyContinue;$mutex.ReleaseMutex();$mutex.Dispose()}
