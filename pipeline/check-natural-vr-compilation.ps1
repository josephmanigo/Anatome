$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskProject=Join-Path $taskRoot 'unity/ThoraxLabReview'
$taskOutput=Join-Path $taskRoot 'qa/natural-vr/compile'
$taskPackage=Join-Path $taskRoot '.tmp/xr-hands-1.8.1/package'
$taskUnity='C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Data'
if(!(Test-Path -LiteralPath $taskPackage)){throw 'Download and extract the pinned Unity XR Hands 1.8.1 registry package for independent compilation.'}
$taskChecks=@()
Push-Location $taskProject
try {
 foreach($taskPlatform in @(@{name='editor';dag='1900b0aE.dag'},@{name='android';dag='1300b0aP.dag'})) {
  $taskDir=Join-Path $taskOutput $taskPlatform.name
  New-Item -ItemType Directory -Path $taskDir -Force | Out-Null
  $taskBase=Get-Content -LiteralPath ('Library/Bee/artifacts/'+$taskPlatform.dag+'/Assembly-CSharp.rsp')
  $taskOptions=@($taskBase | Where-Object {$_ -match '^[-/]' -and $_ -notmatch '^[-/](out|refout|additionalfile|analyzer):' -and $_ -notmatch '^[-/]r:.*Unity\.XR\.Hands(?:\.ref)?\.dll'})
  $taskHands=Join-Path $taskDir 'Unity.XR.Hands.dll'
  $taskRsp=@($taskOptions)+@('-out:"'+$taskHands+'"','-unsafe+','-define:UNITY_OPENXR_PACKAGE;UNITY_OPENXR_PACKAGE_1_2;UNITY_OPENXR_PACKAGE_1_6;UNITY_OPENXR_PACKAGE_1_8;BURST_PRESENT')
  $taskRsp+=@(Get-ChildItem -LiteralPath (Join-Path $taskPackage 'Runtime') -Filter '*.cs' -Recurse -File | ForEach-Object {'"'+$_.FullName+'"'})
  $taskJobs=@(@{name='Unity.XR.Hands';args=$taskRsp})
  foreach($taskAssembly in @('ThoraxLab.Learning.Core','Assembly-CSharp','Assembly-CSharp-Editor')) {
   if($taskPlatform.name -eq 'android' -and $taskAssembly -eq 'Assembly-CSharp-Editor'){continue}
   $taskText=Get-Content -LiteralPath ('Library/Bee/artifacts/'+$taskPlatform.dag+'/'+$taskAssembly+'.rsp') -Raw
   $taskText=[regex]::Replace($taskText,'(?m)^[-/]r:.*Unity\.XR\.Hands(?:\.ref)?\.dll[^\n]*\n?','')
   $taskText=[regex]::Replace($taskText,'(?m)^-out:.*$','-out:"'+(Join-Path $taskDir ($taskAssembly+'.dll'))+'"')
   $taskText=[regex]::Replace($taskText,'(?m)^-refout:.*$','-refout:"'+(Join-Path $taskDir ($taskAssembly+'.ref.dll'))+'"')
   $taskText=[regex]::Replace($taskText,'(?m)^-r:"[^"]*/ThoraxLab\.Learning\.Core(?:\.ref)?\.dll"\r?$','-r:"'+(Join-Path $taskDir 'ThoraxLab.Learning.Core.ref.dll')+'"')
   if($taskAssembly -eq 'Assembly-CSharp-Editor'){$taskText=[regex]::Replace($taskText,'(?m)^-r:"[^"]*/Assembly-CSharp(?:\.ref)?\.dll"\r?$','-r:"'+(Join-Path $taskDir 'Assembly-CSharp.ref.dll')+'"')}
   $taskText+=[Environment]::NewLine+'-r:"'+$taskHands+'"'+[Environment]::NewLine
   if($taskAssembly -eq 'ThoraxLab.Learning.Core' -and $taskText -notmatch 'XRInteractionGate\.cs'){$taskText+='"Assets/ThoraxLab/Learning/Core/XRInteractionGate.cs"'+[Environment]::NewLine}
   $taskJobs+=@{name=$taskAssembly;args=$taskText}
  }
  foreach($taskJob in $taskJobs){
   $taskRspPath=Join-Path $taskDir ($taskJob.name+'.rsp');$taskLog=Join-Path $taskDir ($taskJob.name+'.txt')
   Set-Content -LiteralPath $taskRspPath -Value $taskJob.args -Encoding utf8
   & (Join-Path $taskUnity 'NetCoreRuntime/dotnet.exe') (Join-Path $taskUnity 'DotNetSdkRoslyn/csc.dll') ('@'+$taskRspPath) > $taskLog 2>&1
   $taskExit=$LASTEXITCODE;Get-Content -LiteralPath $taskLog
   $taskChecks+=@{name=$taskPlatform.name+'/'+$taskJob.name;exitCode=$taskExit;log=$taskLog}
   if($taskExit -ne 0){throw ('Compilation failed: '+$taskJob.name)}
  }
 }
} finally {Pop-Location}
@{utc=[DateTime]::UtcNow.ToString('o');passed=$true;checks=$taskChecks;method='Unity Roslyn with installed build references and official XR Hands 1.8.1 source';editorSceneRan=$false;apkBuilt=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskRoot 'qa/natural-vr/compilation.json') -Encoding utf8
