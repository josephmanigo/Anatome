$ErrorActionPreference='Stop'
$taskSource=Join-Path $PSScriptRoot '../../pipeline/sign-classroom-apk.ps1'
$taskTokens=$null;$taskErrors=$null
$taskAst=[Management.Automation.Language.Parser]::ParseFile((Resolve-Path $taskSource),[ref]$taskTokens,[ref]$taskErrors)
if($taskErrors.Count){throw 'Signing script has parse errors.'}
$taskFunction=$taskAst.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Test-SigningDirectoryAccess'},$true)
. ([scriptblock]::Create($taskFunction.Extent.Text))
$taskUser=[Security.Principal.WindowsIdentity]::GetCurrent().User
$taskSystem=[Security.Principal.SecurityIdentifier]::new('S-1-5-18')
function New-TestAcl {
    $acl=[Security.AccessControl.DirectorySecurity]::new()
    $acl.SetOwner($taskUser);$acl.SetAccessRuleProtection($true,$false)
    foreach($principal in @($taskSystem,$taskUser)){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($principal,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))}
    return $acl
}
$taskExpected=New-TestAcl
$taskChecks=0
function Assert-Access($actual,$expectedResult,$label){
    if((Test-SigningDirectoryAccess $actual $taskExpected) -ne $expectedResult){throw "Signing ACL check failed: $label"}
    $script:taskChecks++
}
Assert-Access (New-TestAcl) $true 'required permissions'
Assert-Access (Get-Acl -LiteralPath (Join-Path $env:LOCALAPPDATA 'Anatome/Signing')) $true 'existing private directory'
$taskCase=New-TestAcl;$taskCase.SetAccessRuleProtection($false,$true);Assert-Access $taskCase $false 'inheritance enabled'
$taskCase=New-TestAcl;$taskCase.SetOwner($taskSystem);Assert-Access $taskCase $false 'wrong owner'
$taskCase=New-TestAcl;$taskCase.PurgeAccessRules($taskSystem);Assert-Access $taskCase $false 'missing SYSTEM'
$taskCase=New-TestAcl;$taskCase.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new('Everyone','Read','ContainerInherit,ObjectInherit','None','Allow'));Assert-Access $taskCase $false 'extra principal'
$taskCase=New-TestAcl;$taskCase.PurgeAccessRules($taskUser);$taskCase.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($taskUser,'Read','ContainerInherit,ObjectInherit','None','Allow'));Assert-Access $taskCase $false 'insufficient owner rights'
$taskCase=New-TestAcl;$taskCase.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($taskUser,'Write','ContainerInherit,ObjectInherit','None','Deny'));Assert-Access $taskCase $false 'extra deny rule'
Write-Output "Signing ACL: $taskChecks assertions passed; no filesystem permissions changed."
