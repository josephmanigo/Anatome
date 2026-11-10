param([string]$AssemblyPath='builds/AnatomeWindows/Anatome_Data/Managed/Assembly-CSharp.dll',[string]$OutputPath='qa/vr-release-gate-report.json')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskAssembly=Join-Path $taskRoot $AssemblyPath
$taskCecil=Get-ChildItem -LiteralPath (Join-Path $taskRoot 'unity/ThoraxLab/Library/PackageCache') -Directory -Filter 'com.unity.nuget.mono-cecil@*' | ForEach-Object {Join-Path $_.FullName 'Mono.Cecil.dll'} | Where-Object {Test-Path -LiteralPath $_} | Select-Object -First 1
Add-Type -Path $taskCecil
$taskDefinition=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($taskAssembly)
try {
  $taskRuntime=$taskDefinition.MainModule.Types | Where-Object FullName -EQ 'ThoraxLab.Learning.LabRuntime'
  $taskInput=$taskDefinition.MainModule.Types | Where-Object FullName -EQ 'ThoraxLab.Learning.LearningXRInput'
  function Read-ConstantGate($taskType,$taskName){
    $taskMethod=$taskType.Methods | Where-Object Name -EQ $taskName
    if(!$taskMethod -or !$taskMethod.HasBody){throw ('Missing compiled gate '+$taskName)}
    $taskCode=@($taskMethod.Body.Instructions | ForEach-Object {$_.ToString()})
    [ordered]@{method=$taskMethod.FullName;unconditionallyFalse=($taskCode.Count -eq 2 -and $taskMethod.Body.Instructions[0].OpCode.Name -eq 'ldc.i4.0' -and $taskMethod.Body.Instructions[1].OpCode.Name -eq 'ret');instructions=$taskCode}
  }
  $taskVerification=Read-ConstantGate $taskRuntime 'get_VerificationRunning'
  $taskSimulation=Read-ConstantGate $taskInput 'get_SimulationAllowed'
  $taskReferences=@(($taskRuntime.Methods | Where-Object Name -EQ 'get_CanInteract').Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference]} | ForEach-Object {$_.Operand.FullName})
  $taskEditorAbsent=!(Test-Path -LiteralPath (Join-Path (Split-Path $taskAssembly) 'Assembly-CSharp-Editor.dll'))
  $taskAccount=$taskDefinition.MainModule.Types | Where-Object FullName -EQ 'ThoraxLab.Learning.AccountService'
  $taskTransportHookAbsent=$null -ne $taskAccount -and @($taskAccount.Fields | Where-Object Name -EQ 'VerificationRequestStarted').Count -eq 0
  $taskPassed=$taskVerification.unconditionallyFalse -and $taskSimulation.unconditionallyFalse -and $taskEditorAbsent -and $taskTransportHookAbsent -and ($taskReferences -match 'get_XRReady').Count -gt 0
  [ordered]@{schemaVersion=2;verifiedUtc=[DateTime]::UtcNow.ToString('o');method='Read-only managed IL inspection using Unity-bundled Mono.Cecil; no Unity or player launch.';assembly=$taskAssembly;assemblySha256=(Get-FileHash -LiteralPath $taskAssembly -Algorithm SHA256).Hash.ToLowerInvariant();assemblyWrittenUtc=(Get-Item -LiteralPath $taskAssembly).LastWriteTimeUtc.ToString('o');passed=$taskPassed;verificationRunning=$taskVerification;editorSimulation=$taskSimulation;canInteractReferences=$taskReferences;editorAssemblyAbsent=$taskEditorAbsent;editorTransportHookAbsent=$taskTransportHookAbsent;conclusion='The inspected release assembly retains the XR tracking gate, compiles simulator and runtime verification bypasses to false, and excludes the Editor-only network interruption hook.'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskRoot $OutputPath) -Encoding utf8
  if(!$taskPassed){throw 'Release tracking gate inspection failed.'}
  Write-Output 'Release tracking gate inspection passed.'
} finally {$taskDefinition.Dispose()}
