$ErrorActionPreference='Stop'
$taskRoot=Resolve-Path (Join-Path $PSScriptRoot '../..')
$taskProject=Join-Path $taskRoot 'unity/ThoraxLab'
$taskUnity='C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Data'
$taskChecks=@()
Push-Location $taskProject
try {
    foreach($taskConfig in @(@{name='editor';dag='1900b0aE.dag'},@{name='android';dag='1300b0aP.dag'},@{name='player';dag='1900b0aPDevDbg.dag'})) {
        $taskOutput=Join-Path $PSScriptRoot ('compile/'+$taskConfig.name)
        New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
        foreach($taskAssembly in @('ThoraxLab.Learning.Core','Assembly-CSharp','Assembly-CSharp-Editor')) {
            if($taskConfig.name -ne 'editor' -and $taskAssembly -eq 'Assembly-CSharp-Editor'){continue}
            $taskRsp=Get-Content -LiteralPath ('Library/Bee/artifacts/'+$taskConfig.dag+'/'+$taskAssembly+'.rsp') -Raw
            $taskRsp=[regex]::Replace($taskRsp,'(?m)^-out:.*$','-out:"'+(Join-Path $taskOutput ($taskAssembly+'.dll'))+'"')
            $taskRsp=[regex]::Replace($taskRsp,'(?m)^-refout:.*$','-refout:"'+(Join-Path $taskOutput ($taskAssembly+'.ref.dll'))+'"')
            foreach($taskReference in @('ThoraxLab.Learning.Core','Assembly-CSharp')) {
                if($taskReference -eq $taskAssembly){continue}
                $taskPattern='(?m)^-r:"[^"]*/'+[regex]::Escape($taskReference)+'(?:\.ref)?\.dll"\r?$'
                $taskRsp=[regex]::Replace($taskRsp,$taskPattern,'-r:"'+(Join-Path $taskOutput ($taskReference+'.ref.dll'))+'"')
            }
            $taskNewSource=if($taskAssembly -eq 'Assembly-CSharp'){'Assets/ThoraxLab/Learning/GuidedInteractionCoach.Dissection.cs'}elseif($taskAssembly -eq 'Assembly-CSharp-Editor'){'Assets/ThoraxLab/Editor/LearningVRSimulatorVerification.FovGuide.cs'}else{$null}
            if($taskNewSource -and !$taskRsp.Contains($taskNewSource)){$taskRsp+=[Environment]::NewLine+'"'+$taskNewSource+'"'+[Environment]::NewLine}
            if($taskConfig.name -eq 'player' -and $taskAssembly -eq 'Assembly-CSharp'){
                # Reuse the existing development fixture entry point in the isolated test assembly only.
                $taskFixture='Assets/ThoraxLab/Learning/LearningRuntimeVerification.cs'
                $taskFixtureText=Get-Content -LiteralPath $taskFixture -Raw
                if(!$taskFixtureText.Contains('LearningGlassUIVerification.Run(app,output)')){throw 'Development fixture hook changed.'}
                $taskFixtureCopy=Join-Path $taskOutput 'LearningRuntimeVerification.cs'
                $taskFixtureText.Replace('LearningGlassUIVerification.Run(app,output)','FovGuideRuntime.Run(app,output)') | Set-Content -LiteralPath $taskFixtureCopy -Encoding utf8
                if(!$taskRsp.Contains($taskFixture)){throw 'Cached response file does not include the development fixture.'}
                $taskRsp=$taskRsp.Replace($taskFixture,$taskFixtureCopy)
                $taskRsp+=[Environment]::NewLine+'"'+(Join-Path $PSScriptRoot 'FovGuideRuntime.cs')+'"'+[Environment]::NewLine
            }
            $taskRspPath=Join-Path $taskOutput ($taskAssembly+'.rsp')
            $taskLog=Join-Path $taskOutput ($taskAssembly+'.txt')
            Set-Content -LiteralPath $taskRspPath -Value $taskRsp -Encoding utf8
            & (Join-Path $taskUnity 'NetCoreRuntime/dotnet.exe') (Join-Path $taskUnity 'DotNetSdkRoslyn/csc.dll') ('@'+$taskRspPath) > $taskLog 2>&1
            $taskExit=$LASTEXITCODE
            Get-Content -LiteralPath $taskLog
            $taskChecks+=@{name=$taskConfig.name+'/'+$taskAssembly;exitCode=$taskExit;log=$taskLog}
            if($taskExit -ne 0){throw ('Compilation failed: '+$taskAssembly)}
        }
    }
} finally {
    Pop-Location
    @{utc=[DateTime]::UtcNow.ToString('o');passed=($taskChecks.Count -eq 7 -and @($taskChecks|Where-Object {$_.exitCode -ne 0}).Count -eq 0);checks=$taskChecks;method='Unity Roslyn with installed cached references; player assembly uses isolated QA fixture';editorSceneRan=$false;apkBuilt=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'compilation.json') -Encoding utf8
}
