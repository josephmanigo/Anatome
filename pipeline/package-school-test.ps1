$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskKit=Join-Path $taskRoot 'handoff/Anatome-Chest-Height-Quest-Test-Kit'
$taskVerified=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/chest-height/apk/learning-apk-verification.json') -Raw | ConvertFrom-Json
if($taskVerified.failures -ne 0 -or @($taskVerified.checks | Where-Object {$_.name -eq 'Application display name' -and $_.passed -and $_.detail -eq 'Anatome'}).Count -ne 1){throw 'Verified Anatome APK is required.'}
if(@($taskVerified.checks | Where-Object {$_.name -eq 'Application version' -and $_.passed -and $_.detail -eq '1.4.2'}).Count -ne 1){throw 'Chest cutting and height APK version 1.4.2 is required.'}
if(@($taskVerified.checks | Where-Object {$_.name -eq 'Application version code' -and $_.passed -and $_.detail -eq 10}).Count -ne 1){throw 'Android version code 10 is required for the school update.'}
$taskXr=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/chest-height/xr-final-report.json') -Raw | ConvertFrom-Json
$taskPlayer=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/chest-height/player-isolated/runtime-report.json') -Raw | ConvertFrom-Json
$taskReadiness=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/chest-height/player-isolated/readiness-regressions.json') -Raw | ConvertFrom-Json
foreach($taskCheck in @('held_trigger_enters_skin_and_draws','held_trigger_skin_cut_commits','wrong_cut_tool_feedback_visible','cut_feedback_reaches_learner','short_player_raised_to_working_height','height_assistance_moves_hands_with_view','assisted_player_can_crouch','extra_height_preserves_crouch','legacy_settings_enable_height_assistance','late_short_floor_pose_does_not_double_height','height_preferences_round_trip','ray_scalpel_tracking_recovery_requires_release','ray_scalpel_tracking_loss_preserves_mesh')){
  if(!$taskXr.passed -or @($taskXr.checks | Where-Object {$_.id -eq $taskCheck -and $_.passed}).Count -ne 1){throw ('Current chest and height regression is required: '+$taskCheck)}
}
foreach($taskCheck in @('focus_unpause_cannot_steal_browser_input','focus_gain_cannot_override_pause','browser_resumes_same_step','references_preserve_attempt','case_has_live_action_feedback','pointer_contact_needs_no_trigger','xr_pointer_contact_without_trigger','case_lesson_starts_with_briefing','case_begins_from_briefing')){
  if(!$taskXr.passed -or @($taskXr.checks | Where-Object {$_.id -eq $taskCheck -and $_.passed}).Count -ne 1){throw ('Current browser/case verification is required: '+$taskCheck)}
}
$taskLinks=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/case-clarity/source-links-after.json') -Raw | ConvertFrom-Json
if(@($taskLinks.checks | Where-Object {$_.id -eq 'bodyparts' -and $_.passed -and $_.status -eq 200}).Count -ne 1){throw 'The reported source 404 must have a successful live readback.'}
foreach($taskTarget in @('FMA7163','REC_fat','REC_fascia','FMA79979','MAL_RUL','FMA7957','FMA8148')){
  foreach($taskPrefix in @('auto_tool','ray_cut_separates_chunk','one_bounded_chunk','uncut_remainder_anchored','xr_grips_chunk_only','release_without_lift_no_credit','collected_chunk_not_duplicated','single_remaining_source','one_visible_cut_renderer','credited_chunk_cannot_return_on_undo')){
    $taskCheck=$taskPrefix+'/'+$taskTarget
    if(!$taskXr.passed -or @($taskXr.checks | Where-Object {$_.id -eq $taskCheck -and $_.passed}).Count -ne 1){throw ('Current chunk verification is required: '+$taskCheck)}
  }
}
foreach($taskCheck in @('left_region_complete','left_only_not_finished','resume_preserves_actual_opening','repeated_restore_keeps_activity_usable','repeated_restore_preserves_opening')){
  if(@($taskXr.checks | Where-Object {$_.id -eq $taskCheck -and $_.passed}).Count -ne 1){throw ('Current regional verification is required: '+$taskCheck)}
}
$taskMesh=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/dissection-overhaul/mesh-report.json') -Raw | ConvertFrom-Json
$taskDissection=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/dissection-overhaul/content-regressions.json') -Raw | ConvertFrom-Json
if(!$taskMesh.passed -or !$taskDissection.passed){throw 'Actual anatomy mesh and complete-topic regressions must pass.'}
foreach($taskCheck in @('button_responds_on_press','held_button_does_not_repeat','button_release_does_not_repeat','navigation_responds_on_press','navigation_hold_does_not_replay','disabled_button_does_not_activate','button_feedback_has_no_fade','card_feedback_has_no_fade','ui_press_captured_until_release','ui_release_restores_world_input','controller_empty_area_drag_scrolls')){
  if(!$taskXr.passed -or @($taskXr.checks | Where-Object {$_.id -eq $taskCheck -and $_.passed}).Count -ne 1){throw ('Current button response verification is required: '+$taskCheck)}
}
foreach($taskCheck in @('vr_email_opens_from_controller/1','vr_email_preview_is_pinned/1.5','vr_email_cursor_edits_middle/1.5','vr_email_insert_at_caret/1.5','vr_keyboard_back_keeps_email/1.5','vr_email_survives_page_return/1.5','vr_long_email_window_keeps_caret','vr_email_character_limit','vr_empty_email_edit_is_safe','vr_code_uses_numeric_keyboard','vr_code_retains_leading_zero_and_limit','vr_code_back_returns_to_email','case_tray_visible_during_choices','case_tray_visible_after_choice','ray_scalpel_reaches_seventy_cm','ray_scalpel_rotation_draws_stroke','ray_scalpel_occlusion_cancels_stroke','ray_scalpel_range_is_bounded','ray_scalpel_pause_requires_new_press','ray_scalpel_commits_native_cut')){
  if(@($taskXr.checks | Where-Object {$_.id -eq $taskCheck -and $_.passed}).Count -ne 1){throw ('Current VR usability verification is required: '+$taskCheck)}
}
$taskNewestSource=Get-ChildItem -LiteralPath (Join-Path $taskRoot 'unity/ThoraxLab/Assets/ThoraxLab') -Filter '*.cs' -Recurse | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if(([DateTime]$taskXr.utc).ToUniversalTime() -le $taskNewestSource.LastWriteTimeUtc){throw 'XR verification predates the current source.'}
$taskGuide=Get-Content -LiteralPath (Join-Path $taskRoot 'unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/how-to-play.json') -Raw | ConvertFrom-Json
$taskNarration=Get-Content -LiteralPath (Join-Path $taskRoot 'pipeline/learning-narration.json') -Raw | ConvertFrom-Json
foreach($taskPage in $taskGuide.pages){
  $taskClip=@($taskNarration.clips | Where-Object {$_.id -eq $taskPage.id})
  if($taskClip.Count -ne 1 -or $taskClip[0].text -cne ($taskPage.title+'. '+$taskPage.text)){throw ('Guide narration text mismatch: '+$taskPage.id)}
  $taskVoiceHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($taskNarration.voice+"`n"+$taskNarration.rate+"`n"+$taskClip[0].text))).ToLowerInvariant()
  $taskVoicePath=Join-Path $taskRoot ('unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/Voice/'+$taskPage.id+'.wav')
  if((Get-Item -LiteralPath $taskVoicePath).Length -le 44 -or (Get-Content -LiteralPath ($taskVoicePath+'.sha256') -Raw).Trim() -cne $taskVoiceHash){throw ('Guide audio requires regeneration: '+$taskPage.id)}
}
$taskAssets=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/guide-voice-audit/assets-report.json') -Raw | ConvertFrom-Json
if(!$taskAssets.passed -or $taskAssets.voice.gender -ne 'Female' -or $taskAssets.voice.clips.Count -ne 341){throw 'Female narration and anatomy asset audit must pass.'}
foreach($taskCheck in @('guide_respects_mute_from_first_sample','inventory_exact_scene_membership','guide_opens_before_sign_in','guide_last_page_bounds','guide_closes_to_original_page','guide_reopens_at_saved_page','guide_menu_shortcut_closes','guide_pauses_lesson_and_interaction','guide_held_grip_not_replayed','guide_preserves_attempt_and_resumes_audio','guide_preserves_manual_audio_pause','guide_does_not_reveal_assessment_answers')){
  if(!$taskXr.passed -or @($taskXr.checks | Where-Object {$_.id -eq $taskCheck -and $_.passed}).Count -ne 1){throw ('Current guide verification is required: '+$taskCheck)}
}
$taskContent=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/vr-content-report.json') -Raw | ConvertFrom-Json
$taskBackend=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/learning-backend-report.json') -Raw | ConvertFrom-Json
$taskLive=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/dissection-overhaul/live-migration-report.json') -Raw | ConvertFrom-Json
$taskApi=Get-Content -LiteralPath (Join-Path $taskRoot 'qa/dissection-overhaul/learning-live-api-report.json') -Raw | ConvertFrom-Json
if($taskPlayer.contentVersion -ne '2026.10.2' -or $taskContent.version -ne '2026.10.2' -or !$taskContent.passed -or $taskBackend.result -ne 'pass'){throw 'Current immutable clinical content and server scoring tests must pass.'}
if(!$taskLive.passed -or $taskLive.contentVersion -ne '2026.10.2' -or $taskLive.definitions -ne 72 -or !$taskLive.oldDefinitionsUnchanged -or !$taskApi.passed){throw 'Current additive backend deployment and API readback are required.'}
if($taskXr.runtimeQaBypass -or @($taskXr.errors).Count -ne 0){throw 'XR verification must use ordinary tracking gates without errors.'}
foreach($taskCheck in @('clinical_labels_follow_maximum_text_size','clinical_practice_spatial_hint_without_credit','clinical_assessment_spatial_hint_refused','clinical_reasoning_has_physical_choices','clinical_unheld_token_cannot_answer','clinical_wrong_choice_records_rationale','clinical_release_outside_dock_cannot_answer','clinical_focus_loss_cancels_choice','clinical_correct_drop_advances_case')){if(!$taskXr.passed -or @($taskXr.checks | Where-Object {$_.id -eq $taskCheck -and $_.passed}).Count -ne 1){throw ('Current physical reasoning verification is required: '+$taskCheck)}}
if($taskPlayer.variants -ne 72 -or $taskPlayer.sceneAttemptsPassed -ne 72 -or @($taskPlayer.errors).Count -ne 0 -or !$taskPlayer.xrSliderRayInput -or !$taskPlayer.xrReleaseStops -or !$taskPlayer.xrButtonRayInput -or !$taskPlayer.xrScrollRayInput){throw 'The full repaired player scenarios and UI widgets must pass before packaging.'}
if(!$taskReadiness.passed -or @($taskReadiness.checks | Where-Object {!$_.passed}).Count -gt 0){throw 'Current player readiness regressions must pass before packaging.'}
if(([DateTime]$taskPlayer.utc).ToUniversalTime() -le (Get-Item -LiteralPath (Join-Path $taskRoot 'builds/AnatomeVRTest/Anatome_Data/Managed/Assembly-CSharp.dll')).LastWriteTimeUtc){throw 'Player verification predates the current compiled runtime.'}
foreach($taskCheck in @('controller_slider_drag_changes_value','controller_slider_release_stops_drag','controller_thumbstick_scrolls_panel')){if(!$taskXr.passed -or @($taskXr.checks | Where-Object {$_.id -eq $taskCheck -and $_.passed}).Count -ne 1){throw ('Current widget input verification is required: '+$taskCheck)}}
foreach($taskCheck in @('lost_focus_blocks_ui','focus_recovery_requires_fresh_press','focus_recovery_accepts_fresh_click','performance_pause_stops_sampling','performance_pause_persists_measurement','performance_resume_samples_again','left_controller_clicks_ui')){if(!$taskXr.passed -or @($taskXr.checks | Where-Object {$_.id -eq $taskCheck -and $_.passed}).Count -ne 1){throw ('Current input lifecycle verification is required: '+$taskCheck)}}
foreach($taskCheck in @('two_visible_tracked_controllers','controller_button_tolerates_hand_jitter','openxr_touch_grip_and_aim_separate','openxr_touch_trigger_reaches_input','controller_confirmation_callback','controller_keyboard_types','controller_keyboard_done_closes_modal','first_person_camera_remains_head_tracked','measured_floor_eye_height_preserved','startup_origin_change_keeps_head_above_floor','tracking_restores_inputs_without_replay','xr_hand_subsystem_routes_open_hand','bare_three_finger_curl_grabs_without_use','bare_pinch_can_use_while_holding','system_hand_gesture_cancels_game_input','returning_held_controller_requires_release')){
  if(!$taskXr.passed -or @($taskXr.checks | Where-Object {$_.id -eq $taskCheck -and $_.passed}).Count -ne 1){throw ('Current natural-view Unity verification is required: '+$taskCheck)}
}
foreach($taskCheck in @('XR Hands ARM64 provider','Hand tracking permission','Optional hand tracking preserves controller support')){
  if(@($taskVerified.checks | Where-Object {$_.name -eq $taskCheck -and $_.passed}).Count -ne 1){throw ('Current controller and hand-tracking APK verification is required: '+$taskCheck)}
}
New-Item -ItemType Directory -Path $taskKit -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'builds/AnatomeQuest/Anatome.apk') -Destination (Join-Path $taskKit 'Anatome.apk')
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/VR-SCHOOL-TEST.md') -Destination (Join-Path $taskKit 'README.txt')
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/VR-QUICKSTART.md') -Destination (Join-Path $taskKit 'VR-QUICKSTART.md')
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/VR-SCHOOL-TEST-RESULTS.txt') -Destination (Join-Path $taskKit 'TEST-RESULTS.txt')
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/VR-BUTTON-RESPONSE.md') -Destination (Join-Path $taskKit 'BUTTON-RESPONSE.md')
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/VR-USABILITY-RETEST.md') -Destination (Join-Path $taskKit 'VR-USABILITY-RETEST.md')
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/VR-CASE-CLARITY.md') -Destination (Join-Path $taskKit 'CASE-CLARITY.md')
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/VR-DISSECTION-OVERHAUL.md') -Destination (Join-Path $taskKit 'DISSECTION-OVERHAUL.md')
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/VR-CHEST-HEIGHT-FIX.md') -Destination (Join-Path $taskKit 'CHEST-HEIGHT-FIX.md')
Copy-Item -LiteralPath (Join-Path $taskRoot 'qa/dissection-overhaul/live-migration-report.json') -Destination (Join-Path $taskKit 'BACKEND-READBACK.json')
Copy-Item -LiteralPath (Join-Path $taskRoot 'qa/dissection-overhaul/live-migration-proof.jpg') -Destination (Join-Path $taskKit 'BACKEND-READBACK.jpg')
Copy-Item -LiteralPath (Join-Path $taskRoot 'qa/chest-height/verification-summary.json') -Destination (Join-Path $taskKit 'VERIFICATION.json')
$taskApk=Get-Item -LiteralPath (Join-Path $taskKit 'Anatome.apk')
$taskHash=(Get-FileHash -LiteralPath $taskApk.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
if($taskHash -cne $taskVerified.artifactSha256){throw 'School APK does not match verified build.'}
[ordered]@{packagedUtc=[DateTime]::UtcNow.ToString('o');productName='Anatome';applicationVersion='1.4.2';applicationVersionCode=10;contentVersion='2026.10.2';purpose='School Quest 2 and Quest 3 chest cutting and height retest';inputRevision='chest-height-20261008';guidePages=10;narrationClips=341;narrator='Microsoft Zira Desktop (female synthetic voice)';anatomyTissues=243;referenceAnnotations=6;learningModes=@('Identify & Enumerate','Assemble Anatomy','Guided Dissection','Anatomy Cases');caseScenarios=6;variants=72;essentialClinicalDecisions=39;inputMethods=@('Visible tracked controllers with aim rays','Optional Quest bare-hand tracking');viewpoint='Automatic 1.65 m working eye height for shorter players; optional extra height; physical crouching retained';unitySimulatorChecks=$taskXr.checks.Count;handTrackingPhysicalTested=$false;apk='Anatome.apk';bytes=$taskApk.Length;sha256=$taskHash;signing=$taskVerified.signing;productionRelease=$false;backendContentDeployed=$taskLive.passed;backendDefinitionDigest=$taskLive.definitionDigest;backendApiChecksPassed=$taskApi.passed;backendEvidenceReusedFromOctober4=$false;authenticatedLearnerCurrentBuildTested=$false;connectedRelease='All 72 current definitions match the verified curriculum checksum; four earlier versions are preserved. Authenticated learner synchronization and physical persistence still need acceptance';previousBuildPhysicalTest='User reports skin cannot be cut and shorter players cannot reach the work area';physicalQuestCurrentBuildTested=$false;performance='Button callback verified on the input frame locally; physical Quest latency and sustained FPS require retest';clinicalCompetencyValidated=$false;educationalOutcomeMeasured=$false;package='org.thoraxlab.learning';unity='6000.3.19f1'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskKit 'BUILD-INFO.json') -Encoding utf8
$taskZip=Join-Path $taskRoot ('handoff/Anatome-Chest-Height-Quest-Test-Kit-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.zip')
Compress-Archive -LiteralPath $taskKit -DestinationPath $taskZip -CompressionLevel Optimal
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskArchive=[IO.Compression.ZipFile]::OpenRead($taskZip)
try {
  $taskEntry=$taskArchive.GetEntry('Anatome-Chest-Height-Quest-Test-Kit/Anatome.apk')
  if(!$taskEntry){throw 'APK missing from school ZIP.'}
  $taskStream=$taskEntry.Open()
  try {$taskSha=[Security.Cryptography.SHA256]::Create();try {$taskArchivedHash=[Convert]::ToHexString($taskSha.ComputeHash($taskStream)).ToLowerInvariant()}finally{$taskSha.Dispose()}}finally{$taskStream.Dispose()}
  if($taskArchivedHash -cne $taskHash){throw 'Archived APK hash mismatch.'}
} finally {$taskArchive.Dispose()}
[ordered]@{utc=[DateTime]::UtcNow.ToString('o');archive=$taskZip;archiveSha256=(Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash.ToLowerInvariant();apkSha256=$taskHash;verified=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskRoot 'qa/chest-height/school-kit.json') -Encoding utf8
Write-Output $taskZip

