import hashlib, json, re, zipfile
from datetime import datetime, timezone
from pathlib import Path

root = Path(__file__).resolve().parents[1]
qa = root / 'qa/production-school-20261016'
apk = root / 'builds/AnatomeQuest/Anatome-1.4.9-Classroom-Candidate.apk'
kit = root / 'builds/Anatome-1.4.9-Classroom-Candidate.zip'
def read(name): return json.loads((qa / name).read_text(encoding='utf-8-sig'))
def sha(path):
    with path.open('rb') as stream: return hashlib.file_digest(stream, 'sha256').hexdigest()
def record(path): return {'path': path.relative_to(root).as_posix(), 'sha256': sha(path), 'bytes': path.stat().st_size}
def all_checks(report): return all(item['passed'] for item in report['checks'])

xr, ui = read('xr-full.json'), read('xr-ui-audit.json')
runtime, readiness = read('player/runtime-report.json'), read('player/readiness-regressions.json')
policy, backend, content, live = [read(name + '.json') for name in ('build-policy','backend','content','live-api')]
native, gate, android_gate = read('native-player.json'), read('windows-release-gate.json'), read('android-compiler-gate.json')
package, strict = read('apk/learning-apk-verification.json'), read('strict-candidate/learning-apk-verification.json')
assert xr['passed'] and all_checks(xr) and len(xr['checks']) == 2411 and not xr['errors']
assert ui['passed'] and all_checks(ui) and len(ui['checks']) == 1048
assert runtime['sceneAttemptsPassed'] == runtime['variants'] == 72 and not runtime['errors']
assert all_checks(readiness) and len(readiness['checks']) == 22
assert policy['passed'] and len(policy['checks']) == 16 and not policy['errors']
assert backend['result'] == 'pass' and backend['checks'] == 263
assert content['passed'] and content['checks'] == 1761 and content['version'] == '2026.10.2'
assert live['passed'] and all_checks(live) and live['emailsSent'] == 0 and not live['authenticatedUserTests']
assert native['failures'] == 0 and len(native['checks']) == 89 and all_checks(native)
assert gate['passed'] and android_gate['passed']
assert 'PASS 896 assertions' in (qa / 'core-tests.txt').read_text(encoding='utf-8-sig')
assert 'RESULT 105/105' in (qa / 'native-tests.txt').read_text(encoding='utf-8-sig')
assert package['failures'] == 0 and all_checks(package)
assert strict['schoolPolicyRequired'] and strict['releaseSigningRequired']
assert [item['name'] for item in strict['checks'] if not item['passed']] == ['Non-debug release signing certificate']
assert strict['certificateSha256'] == package['certificateSha256'] == 'b340d254f5849ccea16bd38c92cb630f2093366a24e877b42c892412aa9e9aae'
assert package['artifactSha256'] == strict['artifactSha256'] == sha(apk)
for name in ('windows-qa-build.txt','windows-release-build.txt','android-build.txt'):
    text = (qa / name).read_text(encoding='utf-8-sig')
    assert text.startswith('Succeeded') and '0 errors' in text, name

source = sorted(set((root / 'unity/ThoraxLab/Assets/ThoraxLab').rglob('*.cs')) | {
    root / p for p in (
        'unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json',
        'unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/supabase-config.json',
        'unity/ThoraxLab/ProjectSettings/ProjectSettings.asset',
        'unity/ThoraxLab/ProjectSettings/GraphicsSettings.asset',
        'unity/ThoraxLab/Packages/manifest.json', 'unity/ThoraxLab/Packages/packages-lock.json',
        'unity/ThoraxLab/Assets/Plugins/Android/arm64-v8a/libthoraxcut.so',
        'unity/ThoraxLab/Assets/Plugins/x86_64/thoraxcut.dll',
        'pipeline/verify-learning-apk.py','pipeline/verify-vr-release-gate.ps1',
        'pipeline/build-school-release.ps1','pipeline/run-unity.ps1')})
(qa / 'source-hashes.json').write_text(json.dumps({'scope':'Workspace code/configuration snapshot after final build; not a reproducible-build claim', 'files':[record(p) for p in source]}, indent=2))
evidence_names = ['core-tests.txt','content.json','backend.json','live-api.json','native-tests.txt','native-player.json',
    'native-plugin-hashes.json','build-policy.json','xr-full.json','xr-ui-audit.json',
    'windows-release-gate.json','android-compiler-gate.json','player/runtime-report.json','player/readiness-regressions.json',
    'windows-qa-build.txt','windows-release-build.txt','android-build.txt','android-graphics.txt',
    'apk/learning-apk-verification.json','apk/learning-apk-signature.txt','strict-candidate/learning-apk-verification.json',
    'source-hashes.json','signing-missing-credentials.txt','signing-invalid-fingerprint.txt','signing-missing-fingerprint.txt']
summary = {
    'utc':datetime.now(timezone.utc).isoformat(), 'status':'CLASSROOM_CANDIDATE_LOCALLY_VERIFIED_SIGNING_TRANSITION_AND_DEVICE_ACCEPTANCE_PENDING',
    'productionReady':False, 'version':'1.4.9', 'versionCode':17, 'package':'org.thoraxlab.learning', 'curriculum':'2026.10.2',
    'apk':record(apk), 'certificateSha256':package['certificateSha256'],
    'signing':'Existing Android Debug identity preserved by owner request. Permanent publisher signing remains open.',
    'checks': {'core':896,'content':1761,'databaseLocal':263,'anonymousLiveApi':5,'nativeUnit':105,'nativePlayerIntegration':89,
        'fullXrSimulator':2411,'isolatedUiAudit':1048,'learningScenarios':72,'readinessRegressions':22,'buildPolicy':16,
        'apk':len(package['checks']),'strictCandidatePassed':len(strict['checks'])-1,'strictCandidateExpectedFailures':1},
    'releaseGates': {'windowsCompiledGate':True,'androidCompilerInputGate':True,'physicalQuest2CurrentApk':False,
        'physicalQuest3CurrentApk':False,'freshAuthenticatedLearnerFlow':False,'permanentPublisherSigning':False},
    'deviceFeedback':'User reported earlier Quest 2 build without bugs; exact version not explicitly reconfirmed. Not acceptance of 1.4.9 or Quest 3.',
    'historicalBackendEvidence':record(root / 'qa/dissection-overhaul/live-migration-report.json'),
    'observations':[
        'Initial full XR run missed progress/Guide once. Isolated UI audit (1048) and instrumented full rerun (2411) passed without changing production pointer behavior; initial report retained as xr-first-failure.json.',
        'Legacy native capacity fixture recut empty chunk cavities. Updated fixture cuts distinct tissue and verifies every fragment increment and the 33rd-cut rejection; 89 checks pass. Initial report retained as native-player-old-fixture.json.',
        'Android manifest merge conflict was fixed with a tools:replace override; final package policy inspected after merge.',
        '42 fresh desktop QA captures; privacy top/bottom and case station visually inspected. These are not stereo headset captures.',
        '72-scenario QA player precedes the selftest-only fixture correction; production learning code is unchanged between it and the final builds.',
        'Final Windows build has one warning for regenerated Unity performance-test metadata; no build errors.',
        'Android compiler gate inspects CIL input to IL2CPP, not native machine-code behavior.',
        'Anonymous live probes sent no emails. Historical content deployment is separate from fresh authenticated acceptance.'
    ],
    'artifacts':[record(root / 'builds/AnatomeWindows/Anatome_Data/Managed/Assembly-CSharp.dll'),record(root / 'builds/AnatomeVRTest/Anatome_Data/Managed/Assembly-CSharp.dll')],
    'evidence':[record(qa / p) for p in evidence_names]
}
(qa / 'verification-summary.json').write_text(json.dumps(summary,indent=2))
readme = '''ANATOME 1.4.9 — CLASSROOM CANDIDATE (version code 17)

Install Anatome-1.4.9-Classroom-Candidate.apk using the existing-app update flow in Meta Quest Developer Hub.
Package: org.thoraxlab.learning. Existing debug certificate retained by owner request.
Sync learner progress before updating; do not uninstall to work around a signer mismatch.

Read CLASSROOM-RELEASE.md before rollout. Permanent signing, exact-build Quest 2/Quest 3 acceptance,
and a fresh approved learner login/sync/recovery check remain open. Earlier Quest 2 feedback is not
acceptance of this new APK. Automated checks and artifact SHA-256 are in verification-summary.json.

Automatic refresh requests the highest rate supported by the headset; no fixed 72 FPS cap.
Requested refresh does not guarantee achieved FPS. Classroom comfort and sustained timing need headset tests.
'''
with zipfile.ZipFile(kit, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
    archive.writestr('README.txt',readme)
    archive.write(apk,apk.name)
    for name in ('CLASSROOM-RELEASE.md','VR-RELEASE-SIGNING.md','VR-QUICKSTART.md'):
        archive.write(root / 'docs' / name,name)
    archive.write(root / 'ATTRIBUTION.txt','ATTRIBUTION.txt')
    archive.write(qa / 'verification-summary.json','verification-summary.json')
    for name in evidence_names: archive.write(qa / name,'evidence/' + name)
    for name in ('vr-privacy.png','vr-privacy-bottom.png','asthma-0-0-station.png'):
        archive.write(qa / 'player' / name,'preview/' + name)
    archive.writestr('SHA256.txt',sha(apk)+'  '+apk.name+'\n')
with zipfile.ZipFile(kit) as archive:
    assert archive.testzip() is None
    assert hashlib.sha256(archive.read(apk.name)).hexdigest() == sha(apk)
    assert json.loads(archive.read('verification-summary.json'))['apk']['sha256'] == sha(apk)
delivery = {'utc':datetime.now(timezone.utc).isoformat(),'apk':record(apk),'zip':record(kit),'zipIntegrity':True,'packagedApkMatches':True}
(qa / 'delivery.json').write_text(json.dumps(delivery,indent=2))
print(json.dumps(delivery,indent=2))
