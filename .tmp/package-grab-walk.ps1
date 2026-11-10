$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskQa=Join-Path $taskRoot 'qa/grab-walk'
$taskKit=Join-Path $taskRoot 'handoff/Anatome-Grab-Walk-Quest-Test-Kit'
$taskXr=Get-Content (Join-Path $taskQa 'xr-final-report.json') -Raw | ConvertFrom-Json
$taskPlayer=Get-Content (Join-Path $taskQa 'player/runtime-report.json') -Raw | ConvertFrom-Json
$taskReadiness=Get-Content (Join-Path $taskQa 'player/readiness-regressions.json') -Raw | ConvertFrom-Json
$taskApkReport=Get-Content (Join-Path $taskQa 'apk/learning-apk-verification.json') -Raw | ConvertFrom-Json
if(!$taskXr.passed -or $taskXr.runtimeQaBypass -or $taskXr.errors.Count -ne 0){throw 'Current XR verification must pass without a runtime bypass.'}
foreach($taskMode in @('IdentifyEnumerate','AnatomyCases','GuidedDissection','AssembleAnatomy')){
  if(@($taskXr.checks | Where-Object { $_.id -eq ('all_mode_grab/acquired/'+$taskMode) -and $_.passed }).Count -ne 1){throw ('Missing controller grab evidence: '+$taskMode)}
  if(@($taskXr.checks | Where-Object { $_.id -like ('all_mode_walk/direction/'+$taskMode+'/*') -and $_.passed }).Count -ne 4){throw ('Missing walking directions: '+$taskMode)}
}
if($taskPlayer.variants -ne 72 -or $taskPlayer.sceneAttemptsPassed -ne 72 -or $taskPlayer.errors.Count -ne 0 -or !$taskReadiness.passed){throw 'All compiled activity and readiness checks must pass.'}
if($taskApkReport.failures -ne 0){throw 'APK verification failed.'}
foreach($taskExpected in @(@('Application version','1.4.3'),@('Application version code','11'))){
  if(@($taskApkReport.checks | Where-Object {$_.name -eq $taskExpected[0] -and $_.passed -and [string]$_.detail -eq $taskExpected[1]}).Count -ne 1){throw 'Wrong APK version.'}
}
$taskBuilds=[ordered]@{}
foreach($taskTarget in @('StandaloneWindows64-QA','StandaloneWindows64','Android')){
  $taskBuild=Get-Content (Join-Path $taskQa ('build-'+$taskTarget+'.txt')) -Raw
  if($taskBuild -notmatch '^Succeeded\s+0 errors\s+0 warnings'){throw ('Build did not pass: '+$taskTarget)}
  $taskBuilds[$taskTarget]=$taskBuild.Trim()
}
foreach($taskCheck in @(@('height-after.txt','PASS 63 '),@('core-tests.txt','PASS 889 '),@('gesture-tests.txt','PASS 31 '))){
  if((Get-Content (Join-Path $taskQa $taskCheck[0]) -Raw) -notmatch $taskCheck[1]){throw ('Missing checks: '+$taskCheck[0])}
}
$taskApk=Join-Path $taskRoot 'builds/AnatomeQuest/Anatome.apk'
$taskHash=(Get-FileHash -LiteralPath $taskApk -Algorithm SHA256).Hash.ToLowerInvariant()
if($taskHash -cne $taskApkReport.artifactSha256){throw 'APK differs from the verified artifact.'}
$taskPreviousSigner=(Select-String -LiteralPath (Join-Path $taskRoot 'qa/chest-height/apk/learning-apk-signature.txt') -Pattern '^Signer #1 certificate SHA-256 digest:').Line
$taskCurrentSigner=(Select-String -LiteralPath (Join-Path $taskQa 'apk/learning-apk-signature.txt') -Pattern '^Signer #1 certificate SHA-256 digest:').Line
if(!$taskPreviousSigner -or $taskPreviousSigner -cne $taskCurrentSigner){throw 'APK update signer does not match the previous test kit.'}
$taskSummary=[ordered]@{
  utc=[DateTime]::UtcNow.ToString('o');status='READY_FOR_GRAB_WALK_RETEST_PHYSICAL_QUEST_PENDING'
  applicationVersion='1.4.3';versionCode=11;contentVersion='2026.10.2';package='org.thoraxlab.learning';apkSha256=$taskHash
  verified=[ordered]@{xrChecks=$taskXr.checks.Count;xrErrors=$taskXr.errors.Count;runtimeQaBypass=$taskXr.runtimeQaBypass;cameraHeightAndLocomotionChecks=63;gestureAndInputSwitchChecks=31;learningCoreAssertions=889;sceneVariants=72;sceneVariantsPassed=$taskPlayer.sceneAttemptsPassed;readinessChecks=$taskReadiness.checks.Count;apkChecks=$taskApkReport.checks.Count;apkFailures=$taskApkReport.failures;updateSignerMatchesPrevious=$true;builds=$taskBuilds}
  changes=@('Inspection grabbing in all four learning modes with pose restoration and no assessment credit','Current assembly placement and cut-chunk removal retain their validated evidence paths','Joystick walking default and one-time legacy settings upgrade; saved teleport remains available','Deadzone, bounded diagonal speed, room-scale startup alignment and floor-aligned collision clearance','Pause, tracking loss, carrying while walking, handedness and maximum height boost checked through controller input')
  compatibility='Settings schema remains 1 for the existing cloud RPC; movementRevision records the one-time walking upgrade. Live authenticated synchronization was not retested.'
  audit=[ordered]@{scope='Shared anatomy grab and controller movement paths across four learning modes';unresolvedLocalCodeFindings=@();reproducedFailures=@('IdentifyEnumerate controller grip rejected by CanManipulate','Joystick input below the deadzone moved the rig','Raised capsule hit the ceiling during walking','Floor-aligned capsule overlapped equipment at the old start','Repeated floor collision recovery changed calibrated height');initialFailuresRetained=$true}
  evidence=@('qa/grab-walk/xr-before.json','qa/grab-walk/height-before.txt','qa/grab-walk/xr-final-report.json','qa/grab-walk/player/runtime-report.json','qa/grab-walk/player/readiness-regressions.json','qa/grab-walk/apk/learning-apk-verification.json','qa/grab-walk/source-hashes.json')
  physicalQuestTested=$false;authenticatedLearnerCurrentBuildTested=$false;vrPerformanceMeasured=$false;productionRelease=$false;signing=$taskApkReport.signing
}
$taskSummary | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $taskQa 'verification-summary.json') -Encoding utf8
New-Item -ItemType Directory -Force $taskKit | Out-Null
Copy-Item -LiteralPath $taskApk -Destination (Join-Path $taskKit 'Anatome.apk')
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/VR-GRAB-WALK.md') -Destination (Join-Path $taskKit 'GRAB-WALK-RETEST.md')
Copy-Item -LiteralPath (Join-Path $taskQa 'verification-summary.json') -Destination (Join-Path $taskKit 'VERIFICATION.json')
Copy-Item -LiteralPath (Join-Path $taskQa 'apk/learning-apk-verification.json') -Destination (Join-Path $taskKit 'APK-VERIFICATION.json')
[ordered]@{applicationVersion='1.4.3';versionCode=11;package='org.thoraxlab.learning';apk='Anatome.apk';sha256=$taskHash;bytes=(Get-Item $taskApk).Length;signing=$taskApkReport.signing;physicalQuestTested=$false} | ConvertTo-Json | Set-Content (Join-Path $taskKit 'BUILD-INFO.json') -Encoding utf8
@'
ANATOME 1.4.3 — QUEST GRAB AND WALK RETEST
Install Anatome.apk as an update (package org.thoraxlab.learning, version code 11).
Keep the earlier kit until this build has passed your headset test. Do not uninstall to update.
Grip visible anatomy to lift and turn it in any of the four modes. Inspection returns it on release.
Left stick walks; right stick turns. Left dominant hand swaps sticks. Aim away from the panel to walk.
Use Settings > VR comfort > Movement to select Teleport if preferred, then Apply.
Read GRAB-WALK-RETEST.md for the five physical checks. Match the APK hash in BUILD-INFO.json.
Local software verification passed. Physical Quest 2/3 testing and sustained comfort/performance remain pending.
Android Debug signing is for controlled sideload review, not a production/store release.
'@ | Set-Content (Join-Path $taskKit 'README.txt') -Encoding utf8
$taskStamp=[TimeZoneInfo]::ConvertTimeFromUtc([DateTime]::UtcNow,[TimeZoneInfo]::FindSystemTimeZoneById('China Standard Time')).ToString('yyyyMMdd-HHmmss')
$taskZip=Join-Path $taskRoot ('handoff/Anatome-Grab-Walk-Quest-Test-Kit-'+$taskStamp+'.zip')
Compress-Archive -LiteralPath $taskKit -DestinationPath $taskZip -CompressionLevel Optimal
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskArchive=[IO.Compression.ZipFile]::OpenRead($taskZip)
try{
  $taskEntry=$taskArchive.GetEntry('Anatome-Grab-Walk-Quest-Test-Kit/Anatome.apk');if(!$taskEntry){throw 'APK missing from ZIP.'}
  $taskStream=$taskEntry.Open();try{$taskSha=[Security.Cryptography.SHA256]::Create();try{$taskArchivedHash=[Convert]::ToHexString($taskSha.ComputeHash($taskStream)).ToLowerInvariant()}finally{$taskSha.Dispose()}}finally{$taskStream.Dispose()}
  if($taskArchivedHash -cne $taskHash){throw 'Archived APK hash mismatch.'}
}finally{$taskArchive.Dispose()}
[ordered]@{utc=[DateTime]::UtcNow.ToString('o');archive=$taskZip;archiveSha256=(Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash.ToLowerInvariant();apkSha256=$taskHash;verified=$true} | ConvertTo-Json | Set-Content (Join-Path $taskQa 'school-kit.json') -Encoding utf8
Write-Output $taskZip
