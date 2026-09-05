param([string]$Method='', [string]$Log='unity-compile', [switch]$Graphics, [string]$Target='', [string]$Project='ThoraxAssetDemo')
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskTemp=Join-Path $taskRoot '.tmp'
New-Item -ItemType Directory -Path $taskTemp -Force | Out-Null
# JDK NIO pipe paths on Windows must fit the Unix-domain socket path limit.
# Keep this override local to the launched process tree, not the user's settings.
$env:JAVA_TOOL_OPTIONS='-Djdk.net.unixdomain.tmpdir="'+$taskTemp+'"'
$env:TEMP=$taskTemp
$env:TMP=$taskTemp
$taskArgs=@('-batchmode','-quit','-projectPath',('"'+$taskRoot+'\unity\'+$Project+'"'),'-logFile',('"'+$taskRoot+'\qa\'+$Log+'.log"'))
if (!$Graphics) {$taskArgs+='-nographics'}
if ($Method) {$taskArgs+=@('-executeMethod',$Method)}
if ($Target) {$taskArgs+=@('-buildTarget',$Target)}
$taskProcess=Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe' -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
Write-Output "Unity PID $($taskProcess.Id), log qa/$Log.log"
