import {readFileSync,writeFileSync,statSync,existsSync,readdirSync} from 'node:fs';
import {createHash} from 'node:crypto';
import path from 'node:path';
const root=path.resolve(import.meta.dirname,'..'),out='qa/guide-voice-audit';
const read=p=>JSON.parse(readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,''));
const sha=p=>createHash('sha256').update(readFileSync(path.join(root,p))).digest('hex');
const xr=read(out+'/xr-final-report.json'),player=read(out+'/player/runtime-report.json'),readiness=read(out+'/player/readiness-regressions.json');
const assets=read(out+'/assets-report.json'),apk=read(out+'/apk/learning-apk-verification.json'),release=read(out+'/vr-release-gate-report.json');
const content=read('qa/vr-content-report.json'),backend=read('qa/learning-backend-report.json'),kit=read('qa/branding/school-kit.json');
const deployed=read('qa/clinical-learning/live-migration-report.json');
const previous=read('qa/clinical-learning/source-parity.json').map(r=>r.file);
const additions=['Assets/ThoraxLab/Learning/LabUI.Guide.cs','Assets/ThoraxLab/Learning/LabUI.Guide.cs.meta','Assets/ThoraxLab/Resources/Learning/how-to-play.json','Assets/ThoraxLab/Resources/Learning/how-to-play.json.meta',...readdirSync(path.join(root,'unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/Voice')).filter(p=>p.endsWith('.wav')).map(p=>'Assets/ThoraxLab/Resources/Learning/Voice/'+p)];
const parity=[...new Set([...previous,...additions])].sort().map(file=>{const a='unity/ThoraxLab/'+file,b='unity/ThoraxLabReview/'+file;const first=sha(a),second=existsSync(path.join(root,b))?sha(b):null;return {file,sha256:first,reviewSha256:second,match:first===second};});
writeFileSync(path.join(root,out,'source-parity.json'),JSON.stringify(parity,null,2)+'\n');
const required=['inventory_exact_scene_membership','guide_opens_before_sign_in','guide_last_page_bounds','guide_closes_to_original_page','guide_reopens_at_saved_page','guide_menu_shortcut_closes','guide_pauses_lesson_and_interaction','guide_held_grip_not_replayed','guide_preserves_attempt_and_resumes_audio','guide_preserves_manual_audio_pause','guide_does_not_reveal_assessment_answers','guide_audio_pauses_on_focus_loss','guide_audio_resumes_after_focus','guide_manual_pause_survives_focus','guide_replay_restarts_audio'];
const checks={
 assets:assets.passed&&assets.checks.every(c=>c.passed),
 controllerIntegration:xr.passed&&!xr.runtimeQaBypass&&xr.errors.length===0&&required.every(id=>xr.checks.some(c=>c.id===id&&c.passed)),
 mutedAudioStartup:xr.checks.some(c=>c.id==='guide_respects_mute_from_first_sample'&&c.passed),
 allScenarios:player.sceneAttemptsPassed===72&&player.errors.length===0,
 currentPlayer:Date.parse(player.utc)>statSync(path.join(root,'builds/AnatomeVRTest/Anatome_Data/Managed/Assembly-CSharp.dll')).mtimeMs,
 playerWidgets:player.xrButtonRayInput&&player.xrSliderRayInput&&player.xrReleaseStops&&player.xrScrollRayInput,
 readiness:readiness.passed,
 content:content.passed&&content.version==='2026.10.1',
 backend:backend.result==='pass',
 unchangedDeployedRubrics:deployed.passed&&deployed.migrations.every(m=>sha(m.file)===m.sha256),
 releaseGate:release.passed&&sha('builds/AnatomeWindows/Anatome_Data/Managed/Assembly-CSharp.dll')===release.assemblySha256,
 apk:apk.failures===0&&sha('builds/AnatomeQuest/Anatome.apk')===apk.artifactSha256&&apk.checks.some(c=>c.name==='Application version'&&c.detail==='1.2.0'&&c.passed),
 sourceParity:parity.every(r=>r.match),
 zip:kit.verified&&kit.apkSha256===apk.artifactSha256&&sha(path.relative(root,kit.archive))===kit.archiveSha256
};
const report={recordedUtc:new Date().toISOString(),applicationVersion:'1.2.0',contentVersion:'2026.10.1',revision:'guide-female-voice-20261004',localValidationPassed:Object.values(checks).every(Boolean),productionReady:false,checks,
 guide:{pages:10,entry:'Guide in the shared top navigation',exit:'Close guide or controller Menu',illustrated:true,narrated:true,preservesActivity:true},
 narration:{voice:assets.voice.name,gender:assets.voice.gender,clips:assets.voice.clips.length,rate:assets.voice.rate,synthetic:true,headsetListeningReviewed:false},
 anatomy:assets.anatomy,
 verification:{assetChecks:assets.checks.length,xrChecks:xr.checks.length,xrUtc:xr.utc,runtimeScenarios:player.sceneAttemptsPassed,readinessChecks:readiness.checks.length,coreAssertions:826,contentChecks:content.checks,postgresChecks:backend.checks,apkChecks:apk.checks.length,sourceParityFiles:parity.length},
 reviews:{standardsRemaining:0,specInitialP2:1,specResolved:1,specRemaining:0,fix:'Assembly guide now distinguishes outlines in Easy/practice from independent advanced assessments; narration regenerated.'},
 failedEvidence:['xr-fixture-initial-failure.json','xr-muted-startup-failure.json'],
 backend:{curriculumUnchanged:true,deploymentEvidence:'qa/clinical-learning/live-migration-report.json',authenticatedCurrentLearnerValidated:false},
 artifacts:{apkSha256:apk.artifactSha256,signing:apk.signing,kit},
 openGates:['Physical Quest 3 interaction, guide legibility, controller/hand transitions, narrator intelligibility and comfort: headset unavailable per user.','At least 15 minutes of measured on-headset sustained native 72 Hz performance.','Publisher-owned non-debug release signing.','Authenticated current-learner sign-in, save/reopen, offline recovery and cross-platform synchronization.'],
 limitation:'Complete against the selected gross-thorax source inventory and installed catalog, not every anatomical subdivision or a whole human body. Synthetic narration and simulated scenarios do not establish clinical competency.'};
writeFileSync(path.join(root,out,'verification-report.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({localValidationPassed:report.localValidationPassed,productionReady:report.productionReady,checks,verification:report.verification}));
if(!report.localValidationPassed)process.exitCode=1;
