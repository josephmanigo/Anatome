param([string]$RuntimeReport='qa/branding/player/runtime-report.json')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskPlayer=Join-Path $taskRoot 'builds/AnatomeWindows'
$taskApk=Join-Path $taskRoot 'builds/AnatomeQuest/Anatome.apk'
$taskRuntimePath=Join-Path $taskRoot $RuntimeReport
$taskRegressionPath=Join-Path (Split-Path $taskRuntimePath -Parent) 'readiness-regressions.json'
$taskRegressions=Get-Content -LiteralPath $taskRegressionPath -Raw | ConvertFrom-Json
$taskXrPath=Join-Path $taskRoot 'qa/vr-xr-simulator-report.json'
$taskCatalogPath=Join-Path $taskRoot 'unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json'
$taskRuntime=Get-Content -LiteralPath $taskRuntimePath -Raw | ConvertFrom-Json
$taskXr=Get-Content -LiteralPath $taskXrPath -Raw | ConvertFrom-Json
$taskCatalog=Get-Content -LiteralPath $taskCatalogPath -Raw | ConvertFrom-Json
$taskInstruments=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/instrument-visual-verification.json') -Raw | ConvertFrom-Json
$taskCards=Get-Content -LiteralPath (Join-Path $taskRoot 'unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/CardArt/card-art-provenance.json') -Raw | ConvertFrom-Json
$taskGlovesRoot=Join-Path $taskRoot 'unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/Hands'
$taskGloves=Get-Content -LiteralPath (Join-Path $taskGlovesRoot 'glove-provenance.json') -Raw | ConvertFrom-Json
$taskGloveAuthoring=Get-Content -LiteralPath (Join-Path $taskRoot 'unity/ThoraxLab/Assets/ThoraxLab/Editor/GlovedHandAssets.cs') -Raw
$taskGloveRevision=[regex]::Match($taskGloveAuthoring,'const string Revision = "([^"]+)";')
$taskReleaseGate=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/vr-release-gate-report.json') -Raw | ConvertFrom-Json
$taskApkCheck=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/learning-apk-verification.json') -Raw | ConvertFrom-Json
$taskLive=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/non-performance-verification/live-account-report.json') -Raw | ConvertFrom-Json
$taskRecovery=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/non-performance-verification/cloud-recovery-report.json') -Raw | ConvertFrom-Json
$taskReleaseAssembly=Join-Path $taskPlayer 'Anatome_Data/Managed/Assembly-CSharp.dll'
$taskQaAssembly=Join-Path $taskRoot 'builds/AnatomeVRTest/Anatome_Data/Managed/Assembly-CSharp.dll'
if($taskRuntime.errors.Count -ne 0 -or $taskRuntime.sceneAttemptsPassed -ne $taskRuntime.variants){throw 'Current VR scene verification has unresolved failures.'}
if(!$taskRegressions.passed -or $taskRegressions.checks.Count -lt 22 -or @($taskRegressions.checks | Where-Object {!$_.passed}).Count -ne 0){throw 'The readiness regression suite has unresolved failures.'}
foreach($taskProfileCheck in @('profile_read_omits_optional_profile','profile_delete_does_not_create_default','profile_save_preserves_explicit_identity')){
  if(@($taskRegressions.checks | Where-Object {$_.id -ceq $taskProfileCheck -and $_.passed}).Count -ne 1){throw ('Missing profile serialization regression: '+$taskProfileCheck)}
}
if($taskLive.status -ne 'passed' -or $taskLive.checks.Count -lt 38 -or $taskLive.errors.Count -ne 0 -or @($taskLive.checks | Where-Object {!$_.passed}).Count -ne 0 -or $taskRecovery.status -ne 'passed' -or $taskRecovery.passed.Count -lt 9 -or $taskRecovery.errors.Count -ne 0){throw 'Live account and recovery verification is incomplete.'}
if(!$taskXr.passed -or $taskXr.runtimeQaBypass){throw 'Actual XR Device Simulator input verification has not passed.'}
if(@($taskXr.checks | Where-Object {!$_.passed}).Count -ne 0){throw 'XR simulator evidence contains failed checks.'}
foreach($taskInteractionCheck in @('orientation_wrist_rotation_keeps_torso_on_table','tracking_loss_restores_held_orientation_skin','pause_restores_held_orientation_skin','held_grip_does_not_reacquire_on_tracking_recovery','orientation_lift_stays_within_comfort_bound','assembly_grip_keeps_specimen_reference','assembly_grip_release_places_current_piece')){
  if(@($taskXr.checks | Where-Object {$_.id -eq $taskInteractionCheck -and $_.passed}).Count -ne 1){throw ('Missing passed interaction check: '+$taskInteractionCheck)}
}
foreach($taskInputCheck in @('two_tracked_inputs_without_avatars','LeftHand_no_avatar_geometry','RightHand_no_avatar_geometry','first_person_camera_remains_head_tracked','controller_trigger_routes_without_avatar','controller_partial_grip_preserved','controller_grip_routes_without_avatar','lost_hmd_tracking_hides_both_pointers','lost_hmd_tracking_blocks_ui_press','lost_hmd_tracking_blocks_panel_drag','tracking_restores_inputs_without_replay','measured_floor_eye_height_preserved','startup_origin_change_keeps_head_above_floor','xr_hand_subsystem_routes_open_hand','bare_three_finger_curl_grabs_without_use','bare_pinch_can_use_while_holding','system_hand_gesture_cancels_game_input','returning_held_controller_requires_release')){
  if(@($taskXr.checks | Where-Object {$_.id -eq $taskInputCheck -and $_.passed}).Count -ne 1){throw ('Missing passed XR input check: '+$taskInputCheck)}
}
foreach($taskInputCheck in @('XR Hands ARM64 provider','Hand tracking permission','Optional hand tracking preserves controller support')){
  if(@($taskApkCheck.checks | Where-Object {$_.name -eq $taskInputCheck -and $_.passed}).Count -ne 1){throw ('Missing APK hand-tracking check: '+$taskInputCheck)}
}
if(!$taskGloveRevision.Success -or $taskGloves.revision -cne $taskGloveRevision.Groups[1].Value -or (Get-Content -LiteralPath (Join-Path $taskGlovesRoot 'glove-version.txt') -Raw).Trim() -cne $taskGloves.revision){throw 'Generated glove assets do not match the current authoring revision.'}
if($taskGloves.hands.Count -ne 2){throw 'Glove provenance must contain both handed source assets.'}
$taskGloveHashes=@{
  Left='828996c1e01f710222a90caa3451a2099ec82f0651242a9b4847091a508ec521'
  Right='215bd9c9782a74c95b47e08e802bd2e37b9bd0e2205428543dbdef9da7990db3'
}
foreach($taskSide in @('Left','Right')){
  $taskHand=@($taskGloves.hands | Where-Object {$_.side -ceq $taskSide})
  $taskHandSource='Assets/ThoraxLab/Resources/Learning/Hands/Source/'+$taskSide+'Hand.fbx'
  if($taskHand.Count -ne 1 -or $taskHand[0].source -cne $taskHandSource -or $taskHand[0].sha256 -cne $taskGloveHashes[$taskSide] -or (Get-FileHash -LiteralPath (Join-Path $taskGlovesRoot ('Source/'+$taskSide+'Hand.fbx')) -Algorithm SHA256).Hash.ToLowerInvariant() -cne $taskGloveHashes[$taskSide]){throw ('Official glove source or provenance changed: '+$taskSide)}
  if(!(Test-Path -LiteralPath (Join-Path $taskGlovesRoot ('Glove'+$taskSide+'.prefab')) -PathType Leaf)){throw ('Generated glove prefab is missing: '+$taskSide)}
}
if($taskRuntime.contentVersion -ne $taskCatalog.version){throw 'Runtime evidence is for an older curriculum.'}
if((Get-Item -LiteralPath $taskQaAssembly).LastWriteTimeUtc -gt ([DateTime]$taskRuntime.utc).ToUniversalTime()){throw 'The QA player was rebuilt after its scene report.'}
if((Get-Item -LiteralPath $taskQaAssembly).LastWriteTimeUtc -gt ([DateTime]$taskRegressions.utc).ToUniversalTime()){throw 'The QA player was rebuilt after its readiness report.'}
if($taskInstruments.status -ne 'passed' -or $taskInstruments.instruments.Count -ne 23 -or !$taskCards.sourceFileUnchanged -or $taskCards.cards.Count -ne 16){throw 'Visual geometry or source-card verification is incomplete.'}
if(!$taskReleaseGate.passed -or !$taskReleaseGate.editorTransportHookAbsent -or $taskReleaseGate.assemblySha256 -ne (Get-FileHash -LiteralPath $taskReleaseAssembly -Algorithm SHA256).Hash.ToLowerInvariant()){throw 'Release tracking-gate evidence does not match this Windows assembly.'}
if($taskApkCheck.failures -ne 0 -or $taskApkCheck.artifactSha256 -ne (Get-FileHash -LiteralPath $taskApk -Algorithm SHA256).Hash.ToLowerInvariant()){throw 'APK verification does not match this Quest artifact.'}
foreach($taskGloveLicense in @('Unity-XR-Hands-License.txt','Unity-XR-Hands-Third-Party-Notices.txt')){
  if(@($taskApkCheck.checks | Where-Object {$_.name -ceq ('Packaged '+$taskGloveLicense) -and $_.passed}).Count -ne 1){throw ('APK verification is missing the glove source notice: '+$taskGloveLicense)}
}
foreach($taskBuild in @('learning-build-StandaloneWindows64.txt','learning-build-Android.txt')){
  if((Get-Content -LiteralPath (Join-Path $taskRoot ('qa/'+$taskBuild)) -TotalCount 1) -ne 'Succeeded'){throw ('Build did not succeed: '+$taskBuild)}
}
$taskOutput=Join-Path $taskRoot 'handoff'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskArchive=Join-Path $taskOutput ('Anatome-VR-Windows-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.zip')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskZip=[System.IO.Compression.ZipFile]::Open($taskArchive,[System.IO.Compression.ZipArchiveMode]::Create)
try {
  foreach($taskFile in (Get-ChildItem -LiteralPath $taskPlayer -Recurse -File)) {
    if($taskFile.FullName -match '_BurstDebugInformation_DoNotShip[\\/]'){continue}
    $taskRelative=[IO.Path]::GetRelativePath($taskPlayer,$taskFile.FullName).Replace('\','/')
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskZip,$taskFile.FullName,'Anatome-VR/'+$taskRelative,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
  foreach($taskDoc in (Get-ChildItem -LiteralPath (Join-Path $taskRoot 'docs') -File | Where-Object {$_.Name -like 'VR-*.md' -or $_.Name -in @('LEARNING-SETUP.md','LEARNING-ACCEPTANCE.md','INSTRUMENT-VISUALS.md')})){
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskZip,$taskDoc.FullName,$taskDoc.Name,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
} finally {$taskZip.Dispose()}
$taskCheck=[System.IO.Compression.ZipFile]::OpenRead($taskArchive)
try {foreach($taskRequired in @('Anatome.exe','UnityPlayer.dll','Anatome_Data/globalgamemanagers')){if(!$taskCheck.GetEntry('Anatome-VR/'+$taskRequired)){throw ('Missing archive entry: '+$taskRequired)}}} finally {$taskCheck.Dispose()}
$taskFiles=@($taskArchive,$taskApk) | ForEach-Object { $taskFile=Get-Item -LiteralPath $_; [ordered]@{path=$taskFile.FullName;bytes=$taskFile.Length;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()} }
$taskManifest=[ordered]@{
  utc=[DateTime]::UtcNow.ToString('o');status='Anatome branding revision for school review';productName='Anatome';unity='6000.3.19f1';contentVersion=$taskCatalog.version;
  platforms=@('Windows PC VR','Quest 3 standalone');editorTestInput='Official Unity XR Device Simulator';
  artifacts=$taskFiles;catalogSha256=(Get-FileHash -LiteralPath $taskCatalogPath -Algorithm SHA256).Hash.ToLowerInvariant();
  runtime=$taskRuntime;readinessRegressions=$taskRegressions;xrSimulator=$taskXr;qaPlayerAssemblySha256=(Get-FileHash -LiteralPath $taskQaAssembly -Algorithm SHA256).Hash.ToLowerInvariant();
  instrumentGeometry=$taskInstruments;anatomyCardProvenance=$taskCards;glovedHandProvenance=$taskGloves;
  releaseTrackingGate=$taskReleaseGate;
  liveAccounts=$taskLive;cloudRecovery=$taskRecovery;
  backend=(Get-Content -LiteralPath (Join-Path $taskRoot 'qa/learning-backend-report.json') -Raw | ConvertFrom-Json);
  liveMigration=(Get-Content -LiteralPath (Join-Path $taskRoot 'qa/vr-learning-live-migration.json') -Raw | ConvertFrom-Json);
  cutMeshAudit=(Get-Content -LiteralPath (Join-Path $taskRoot 'qa/vr-fma9757-derived-mesh-audit.json') -Raw | ConvertFrom-Json);
  apkVerification=$taskApkCheck;
  performance=[ordered]@{status='Excluded from this verification at user request';windowsTarget=60;questTarget=72;historicalReport='qa/learning-performance/windows-15-minute-benchmark.json';historicalReportAppliesToThisBuild=$false};
  limitations=@('Unity OTP login and concurrent authenticated sessions passed; recipient Inbox placement was not observed because codes were read from sender Sent copies','Live sessions ran on one Windows computer; Android Keystore, physical headset interaction and cross-platform recovery remain device checks','A live unredeemed-code ten-minute expiration boundary was not timed','Researched procedural teaching includes documented anatomical and mechanical simplifications','User visual and first-time ease-of-use acceptance pending','Quest APK uses a debug certificate; no publisher release keystore exists yet')
}
$taskManifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $taskRoot 'qa/vr-learning-delivery-manifest.json') -Encoding utf8
Write-Output ('Packaged '+$taskArchive)
