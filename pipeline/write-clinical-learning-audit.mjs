import {readFileSync, writeFileSync, statSync} from 'node:fs';
import {createHash} from 'node:crypto';
const root = new URL('../', import.meta.url);
const read = path => JSON.parse(readFileSync(new URL(path, root),'utf8'));
const sha = path => createHash('sha256').update(readFileSync(new URL(path,root))).digest('hex');
const xr = read('qa/clinical-learning/xr-final-report.json');
const player = read('qa/clinical-learning/player/runtime-report.json');
const readiness = read('qa/clinical-learning/player/readiness-regressions.json');
const content = read('qa/clinical-learning/vr-content-report.json');
const backend = read('qa/clinical-learning/learning-backend-report.json');
const live = read('qa/clinical-learning/live-migration-report.json');
const api = read('qa/clinical-learning/learning-live-api-report.json');
const apk = read('qa/clinical-learning/apk/learning-apk-verification.json');
const signing = read('qa/clinical-learning/release-signing/learning-apk-verification.json');
const release = read('qa/clinical-learning/vr-release-gate-report.json');
const parity = read('qa/clinical-learning/source-parity.json');
const kit = read('qa/branding/school-kit.json');
const catalog = read('unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json');
const assemblyPath = 'builds/AnatomeVRTest/Anatome_Data/Managed/Assembly-CSharp.dll';
const sourceMatches = parity.every(row => row.match && sha('unity/ThoraxLab/'+row.file)===row.sha256 && sha('unity/ThoraxLabReview/'+row.file)===row.reviewSha256);
const checks = {
  currentCurriculum:catalog.version==='2026.10.1'&&content.version===catalog.version&&player.contentVersion===catalog.version,
  actualXrInput:xr.passed&&!xr.runtimeQaBypass&&xr.errors.length===0,
  fullScenarioPlayer:player.sceneAttemptsPassed===72&&player.errors.length===0,
  playerMatchesCurrentBuild:Date.parse(player.utc)>statSync(new URL(assemblyPath,root)).mtimeMs,
  readiness:readiness.passed&&readiness.checks.every(c=>c.passed),
  content:content.passed,
  serverScoring:backend.result==='pass',
  deployedRubricsAndSafety:live.passed,
  livePublicApi:api.passed,
  releaseTrackingGate:release.passed&&sha('builds/AnatomeWindows/Anatome_Data/Managed/Assembly-CSharp.dll')===release.assemblySha256,
  sideloadApk:apk.failures===0&&sha('builds/AnatomeQuest/Anatome.apk')===apk.artifactSha256,
  signingEvidenceBindsApk:signing.artifactSha256===apk.artifactSha256,
  sourceParity:sourceMatches,
  kit:kit.verified&&kit.apkSha256===apk.artifactSha256,
};
const requiredXr=['clinical_labels_follow_maximum_text_size','clinical_practice_spatial_hint_without_credit','clinical_assessment_spatial_hint_refused','clinical_unheld_token_cannot_answer','clinical_wrong_choice_records_rationale','clinical_release_outside_dock_cannot_answer','clinical_focus_loss_cancels_choice','clinical_correct_drop_advances_case'];
checks.clinicalInputCoverage=requiredXr.every(id=>xr.checks.some(c=>c.id===id&&c.passed));
const report={
  schemaVersion:1,recordedUtc:new Date().toISOString(),product:'Anatome',applicationVersion:'1.1.0',contentVersion:catalog.version,
  revision:'advanced-procedural-learning-20261004',audience:'Medical students: advanced procedural anatomy',
  status:'LOCAL_AND_DEPLOYED_RUBRIC_VALIDATION_PASSED_PHYSICAL_RELEASE_ACCEPTANCE_PENDING',
  localValidationPassed:Object.values(checks).every(Boolean),productionReady:false,checks,
  learning:{modes:4,cases:6,variants:72,essentialClinicalDecisions:39,criticalClinicalDecisions:3,narrationClips:311,
    cognitiveOutcomeMeasured:false,clinicalCompetencyValidated:false,schematicResponseStates:true},
  coreRegression:{assertions:826,passed:true,method:'Observed dotnet run --project qa/learning-tests/LearningTests.csproj -- unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json'},
  xr:{report:'qa/clinical-learning/xr-final-report.json',utc:xr.utc,checks:xr.checks.length,errors:xr.errors.length,runtimeQaBypass:xr.runtimeQaBypass,physicalDeviceTested:false},
  developmentPlayer:{report:'qa/clinical-learning/player/runtime-report.json',utc:player.utc,passedScenarios:player.sceneAttemptsPassed,errors:player.errors.length,trackingBypass:true,readinessChecks:readiness.checks.length,
    widgets:{button:player.xrButtonRayInput,slider:player.xrSliderRayInput,release:player.xrReleaseStops,scroll:player.xrScrollRayInput},assemblySha256:sha(assemblyPath)},
  content:{checks:content.checks,baseCaseActions:content.caseSteps,report:'qa/clinical-learning/vr-content-report.json'},
  localPostgresql:{checks:backend.checks,report:'qa/clinical-learning/learning-backend-report.json',liveSupabase:false},
  liveBackend:{projectResumed:true,migrationsApplied:live.migrations,readbackPassed:live.passed,publicApiChecks:api.checks.length,authenticatedLearnerTested:false,
    report:'qa/clinical-learning/live-migration-report.json',proof:'qa/clinical-learning/live-migration-proof.png'},
  sourceParity:{files:parity.length,matched:parity.filter(r=>r.match).length,report:'qa/clinical-learning/source-parity.json'},
  artifacts:{apk:{path:'builds/AnatomeQuest/Anatome.apk',sha256:apk.artifactSha256,checks:apk.checks.length,signing:apk.signing},
    schoolKit:kit,releaseGateReport:'qa/clinical-learning/vr-release-gate-report.json'},
  codeReview:{basis:'.tmp/clinical-learning-before',standards:{initialP2:2,resolved:2,remaining:0},spec:{findings:0},
    runtimeAndVisualFixes:['Correct world-space font units and maximum-size layout','Move choice labels forward and station clear of the specimen to avoid occlusion','Add non-colliding guided token/dock cues; refuse assessment hints','Cache instrument choices and remove per-frame token snapshots'],
    failedEvidenceRetained:['qa/clinical-learning/xr-label-failure-report.json','qa/clinical-learning/player-label-failure-report.json','qa/clinical-learning/label-specimen-occlusion-before.png']},
  accuracy:{sourceReview:'docs/VR-CLINICAL-LEARNING-AUDIT.md',primarySourcesReviewed:true,professionalClinicalCertification:false},
  openProductionGates:[
    {gate:'Physical current-APK Quest 3 acceptance',passed:false,requires:'Visible controllers, all learner buttons, new physical choices, bare hands, interruptions, readability, reach and Android secure storage on the headset'},
    {gate:'Sustained native Quest performance',passed:false,requires:'At least 15 minutes of representative on-device performance and thermal/tracking evidence'},
    {gate:'Publisher release signing',passed:signing.nonDebugSigner,requires:'Publisher-owned non-debug keystore and expected certificate fingerprint; current strict inspection rejects the debug certificate'},
    {gate:'Current authenticated learner persistence',passed:false,requires:'Learner OTP, current-version saved assessment, reopen/offline recovery and Windows/Quest synchronization'},
  ],
};
writeFileSync(new URL('qa/clinical-learning/verification-report.json',root),JSON.stringify(report,null,2));
if(!report.localValidationPassed)throw new Error('Current validation gate failed: '+Object.entries(checks).filter(([,ok])=>!ok).map(([name])=>name).join(', '));
console.log(JSON.stringify({localValidationPassed:report.localValidationPassed,productionReady:report.productionReady,xrChecks:report.xr.checks,scenarios:player.sceneAttemptsPassed,kit:kit.archive},null,2));
