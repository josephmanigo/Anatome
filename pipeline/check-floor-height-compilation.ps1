$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskProject=Join-Path $taskRoot 'unity/ThoraxLabReview'
$taskOutput=Join-Path $taskRoot 'qa/floor-height/compile'
$taskUnity='C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Data'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskChecks=@()
Push-Location $taskProject
try {
  foreach($taskConfig in @(
    @{name='Editor runtime';dag='1900b0aE.dag';assembly='Assembly-CSharp';folder='editor'},
    @{name='Editor verification';dag='1900b0aE.dag';assembly='Assembly-CSharp-Editor';folder='editor'},
    @{name='Android managed scripts';dag='1300b0aP.dag';assembly='Assembly-CSharp';folder='android'}
  )) {
    $taskDirectory=Join-Path $taskOutput $taskConfig.folder
    New-Item -ItemType Directory -Path $taskDirectory -Force | Out-Null
    $taskRsp=Get-Content -LiteralPath ('Library/Bee/artifacts/'+$taskConfig.dag+'/'+$taskConfig.assembly+'.rsp') -Raw
    $taskDll=(Join-Path $taskDirectory ($taskConfig.assembly+'.dll')).Replace('\','/')
    $taskRef=(Join-Path $taskDirectory ($taskConfig.assembly+'.ref.dll')).Replace('\','/')
    $taskRsp=[regex]::Replace($taskRsp,'(?m)^-out:.*$','-out:"'+$taskDll+'"')
    $taskRsp=[regex]::Replace($taskRsp,'(?m)^-refout:.*$','-refout:"'+$taskRef+'"')
    if($taskConfig.assembly -eq 'Assembly-CSharp-Editor'){
      $taskRuntimeRef=(Join-Path $taskDirectory 'Assembly-CSharp.ref.dll').Replace('\','/')
      $taskRsp=[regex]::Replace($taskRsp,'(?m)^-r:"[^"]*/Assembly-CSharp(?:\.ref)?\.dll"\r?$','-r:"'+$taskRuntimeRef+'"')
    }
    $taskRspPath=Join-Path $taskDirectory ($taskConfig.assembly+'.rsp')
    $taskLog=Join-Path $taskDirectory ($taskConfig.assembly+'.txt')
    Set-Content -LiteralPath $taskRspPath -Value $taskRsp -Encoding utf8
    & (Join-Path $taskUnity 'NetCoreRuntime/dotnet.exe') (Join-Path $taskUnity 'DotNetSdkRoslyn/csc.dll') ('@'+$taskRspPath) > $taskLog 2>&1
    $taskExit=$LASTEXITCODE
    Get-Content -LiteralPath $taskLog
    $taskChecks+=@{name=$taskConfig.name;exitCode=$taskExit;log=$taskLog}
    if($taskExit -ne 0){throw ('Compilation failed: '+$taskConfig.name)}
  }
} finally {Pop-Location}
@{utc=[DateTime]::UtcNow.ToString('o');checks=$taskChecks;passed=$true;method='Installed Unity Roslyn compiler and cached Unity build references';unityEditorRan=$false;apkBuilt=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskRoot 'qa/floor-height/compilation.json') -Encoding utf8
