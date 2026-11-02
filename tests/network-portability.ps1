$ErrorActionPreference='Stop'
$source=Join-Path (Split-Path $PSScriptRoot -Parent) 'network\guard.ps1'
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($source,[ref]$tokens,[ref]$errors)
if($errors.Count){throw ($errors|Out-String)}
$fn=$ast.Find({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'OptionalPrograms'},$true)
Invoke-Expression $fn.Extent.Text
$script:present=@();$script:wsl=$false
function Test-Path {param($LiteralPath,$PathType) return $LiteralPath -in $script:present}
function Get-Process {param($Name,$ErrorAction) if($script:wsl){[pscustomobject]@{Name='wslhost'}}}
function Assert($condition,$label){if(!$condition){throw $label};Write-Output "PASS: $label"}
$p=@(OptionalPrograms)
Assert ($p.Count -eq 0) 'No Chrome/WSL: no optional exceptions, no error'
$chrome=Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe'
$script:present=@($chrome)
$p=@(OptionalPrograms)
Assert ($p.Count -eq 1 -and $p[0].Path -eq $chrome) 'Chrome only: no System transport exemption'
$script:present=@();$script:wsl=$true
$p=@(OptionalPrograms)
Assert ($p.Count -eq 1 -and $p[0].Path -eq 'System') 'Active WSL: transport preserved even if optional executable paths absent'
$hostPath=Join-Path $env:ProgramFiles 'WSL\wslhost.exe'
$script:present=@($chrome,$hostPath)
$p=@(OptionalPrograms)
Assert ($p.Count -eq 3) 'Chrome plus active WSL: only present paths and transport allowed'
$script:wsl=$false
$p=@(OptionalPrograms)
Assert ($p.Count -eq 1) 'Inactive WSL installation: transport exception omitted'
Write-Output '5/5 portability cases passed; no firewall changes performed'
