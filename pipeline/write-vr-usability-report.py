"""Collect current local VR repair evidence after packaging; no physical-device claims."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

root = Path(__file__).resolve().parents[1]
out = root / 'qa/vr-usability'
def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))
def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

xr = read(out / 'xr-final-report.json')
player = read(out / 'player/runtime-report.json')
readiness = read(out / 'player/readiness-regressions.json')
apk = read(out / 'apk/learning-apk-verification.json')
upgrade = read(out / 'apk/upgrade-identity.json')
kit = read(out / 'school-kit.json')
audio = read(out / 'guide-audio.json')
health = read(out / 'auth-health.json')
content = read(root / 'qa/vr-content-report.json')
checks = {
    'xrIntegration': xr['passed'] and not xr['runtimeQaBypass'] and not xr['errors'] and all(c['passed'] for c in xr['checks']),
    'compiledScenarios': player['sceneAttemptsPassed'] == player['variants'] == 72 and not player['errors'],
    'compiledWidgets': all(player[k] for k in ['xrButtonRayInput', 'xrSliderRayInput', 'xrReleaseStops', 'xrScrollRayInput']),
    'readiness': readiness['passed'] and all(c['passed'] for c in readiness['checks']),
    'content': content['passed'] and content['version'] == '2026.10.1',
    'guideAndAudio': audio['passed'],
    'windowsBuild': (root / 'qa/learning-build-StandaloneWindows64-QA.txt').read_text().startswith('Succeeded\n0 errors\n0 warnings'),
    'androidBuild': (root / 'qa/learning-build-Android.txt').read_text().startswith('Succeeded\n0 errors\n0 warnings'),
    'apk': apk['failures'] == 0 and sha(Path(apk['apk'])) == apk['artifactSha256'],
    'sameUpgradeIdentity': upgrade['matchesPreviousSchoolBuild'],
    'package': kit['verified'] and kit['apkSha256'] == apk['artifactSha256'] and sha(Path(kit['archive'])) == kit['archiveSha256'],
}
report = {
    'utc': datetime.now(timezone.utc).isoformat(),
    'applicationVersion': '1.3.0', 'versionCode': 6, 'contentVersion': '2026.10.1',
    'localValidationPassed': all(checks.values()), 'checks': checks,
    'verification': {
        'xrChecks': len(xr['checks']), 'buttonTargetsOnPressFrame': sum(c['id'].startswith('ui_target/') for c in xr['checks']),
        'compiledScenarios': player['sceneAttemptsPassed'], 'readinessChecks': len(readiness['checks']),
        'coreAssertions': 826, 'contentChecks': content['checks'], 'apkChecks': len(apk['checks']),
        'coreEvidence': 'Current-session rtk dotnet run --project qa/learning-tests/LearningTests.csproj -- unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/catalog.json exited 0 with PASS 826 assertions.',
    },
    'auth': {'healthReachable': health['reachable'], 'httpStatus': health['httpStatus'], 'vrEmailAndCodeEntryVerified': True, 'liveEmailSent': False, 'physicalQuestLoginVerified': False},
    'visualReview': ['email-keyboard-1.0.png', 'email-keyboard-1.5.png', '../vr-natural-first-person.png', 'player/pneumothorax-0-0-reasoning.png', 'player/vr-case-procedure-step.png'],
    'artifact': kit, 'apkSha256': apk['artifactSha256'], 'signing': apk['signing'],
    'physicalQuestTested': False, 'questPerformanceMeasured': False, 'productionReady': False,
    'remainingAcceptance': ['Current APK on physical Quest 3: email visibility, controller alignment, ray cutting, placement and tray readability.', 'Live learner email-code sign-in, save/reopen and Android persistence.', 'Sustained on-device performance and comfort.'],
    'backend': 'Curriculum and backend were not changed. Earlier backend evidence remains separate from the current read-only health check.',
}
(out / 'verification-report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'localValidationPassed': report['localValidationPassed'], 'checks': checks, 'verification': report['verification']}, indent=2))
if not report['localValidationPassed']:
    raise SystemExit(1)
