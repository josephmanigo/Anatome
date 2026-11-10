import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

ROOT = Path(__file__).resolve().parents[1]
QA = ROOT / 'qa/school-signed-release'
BASE = ROOT / 'qa/production-school-20261016'
APK = ROOT / 'builds/AnatomeSchool/Anatome-1.4.9-Production.apk'
ARCHIVE = ROOT / 'builds/Anatome-1.4.9-Production-Classroom.zip'

def sha(path):
    return hashlib.file_digest(path.open('rb'), 'sha256').hexdigest()

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def save(path, value):
    path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')

def record(path):
    return {'path': path.relative_to(ROOT).as_posix(), 'sha256': sha(path), 'bytes': path.stat().st_size}

assert not ARCHIVE.exists(), 'Never replace an issued classroom ZIP.'
baseline = read(BASE / 'verification-summary.json')
strict = read(QA / 'learning-apk-verification.json')
payload = read(QA / 'payload-equivalence.json')
lineage = read(QA / 'signing-lineage.json')
receipt = read(QA / 'signing-receipt.json')
assert len(strict['checks']) == 30 and strict['failures'] == 0
assert all(c['passed'] for c in strict['checks']) and strict['nonDebugSigner']
assert payload['passed'] and payload['entries'] == 1346 and not payload['changed']
assert sha(APK) == payload['signedSha256'] == receipt['apkSha256'] == strict['artifactSha256']
assert payload['sourceSha256'] == baseline['apk']['sha256']
assert lineage['passed'] and lineage['installedDataCapability'] and not lineage['oldKeyRollbackAllowed']
assert read(QA / 'signing-checks.json')['passed']
assert read(QA / 'signing-storage-verification.json')['passed']
for item in baseline['evidence']:
    assert sha(ROOT / item['path']) == item['sha256'], item['path']

acceptance = {
    'recordedUtc': datetime.now(timezone.utc).isoformat(),
    'source': 'Direct user messages in this task; user-reported, not independently observed by the agent',
    'version': '1.4.9', 'versionCode': 17,
    'userConfirmedVersion': True, 'versionCodeInferredFromNamedRelease': True,
    'headsetConfirmationQuote': 'Both Quest 2 and Quest 3 — version 1.4.9',
    'functionalConfirmationQuote': 'i test it already and there is no problem',
    'loginConfirmationQuote': 'the login is working also',
    'quest2FunctionalPassed': True, 'quest3FunctionalPassed': True, 'loginPassed': True,
    'associatedCandidateSha256': payload['sourceSha256'],
    'hashAssociation': 'Project artifact for the version named by the user; the user did not independently supply an APK hash.',
    'productionPayloadUnchanged': True,
    'signedInstallationTransitionTested': False,
    'measuredHeadsetFpsSupplied': False, 'sustainedThermalMeasurementsSupplied': False,
    'exhaustiveOfflineAndCrossDeviceRecoveryClaimed': False,
}
save(QA / 'user-acceptance.json', acceptance)

(ROOT / 'docs/CLASSROOM-RELEASE.md').write_text('''# Anatome classroom release — 1.4.9

**Production-signed release for private school/classroom distribution.** Package **org.thoraxlab.learning**, version **1.4.9**, Android version code **17**, curriculum **2026.10.2**.

- APK: `builds/AnatomeSchool/Anatome-1.4.9-Production.apk`
- Classroom ZIP: `builds/Anatome-1.4.9-Production-Classroom.zip`
- Current release record: `qa/school-signed-release/verification-summary.json`
- APK SHA-256: `bb3d3bf8a82a5e1b150ffc4d9429fe0de4ebf0720697dd6bee269f1e95e47dbc`

## Acceptance and release scope

On 16 October 2026 (Asia/Shanghai), the user confirmed **“Both Quest 2 and Quest 3 — version 1.4.9”**, reported no problems, and confirmed login works. These are accepted user-reported functional results. The APK hash is associated with the project artifact for that version; the user did not independently provide a file hash.

The production APK contains the **same 1,346 application entries** as the tested classroom candidate, including its manifest, code, native libraries and resources. Only signing metadata changed. All **30 strict APK checks pass**, including the permanent non-debug signer and Android school/privacy policy. The underlying core, content, database, native, simulator, scene and release-build evidence remains in `qa/production-school-20261016`; its old pending-device/signing summary is preserved as history and superseded by the current release record.

The APK uses a permanent release certificate and a two-certificate Android V3 rotation proof from the previous debug certificate. Android's verifier accepts it for API 29–36 with installed-data capability enabled and old-key rollback disabled. This is the supported update mechanism for the app's minimum Android API 29. No headset was connected for an actual installation of this newly signed artifact. **Confirm one in-place update and retained progress before installing it across the classroom.** Your completed functional tests do not need to be repeated merely because the APK was signed.

General functional acceptance does not claim a measured FPS minimum, a timed thermal run or every offline/account recovery path. This release is prepared for private classroom distribution; no public store submission or clinical certification is claimed.

## Included improvements

- Refined case head, skin, vessel and tray presentation from 1.4.8 is retained, along with the four learning modes and source attribution.
- Automatic headset refresh requests the highest rate the runtime reports. **There is no fixed 72 FPS cap.** Actual frame rate depends on headset load and the selected graphics settings.
- Privacy and shared-headset guidance is available before login and in Account & saves.
- Android backup and device transfer exclude app data, including device-bound credentials and local saves. Account sync and explicit learner records remain available. Cleartext traffic is disabled.
- Build checks reject private/service-role keys and invalid or non-HTTPS account configuration. Release verification pins the signing certificate and validates the packaged policy.
- The signing pipeline preserves application contents, verifies its rotation chain, and refuses to overwrite an issued production APK.

## Install and update

1. Finish or pause the lesson, connect online and use **Sync now**. Confirm success. Export progress if a separate readable record is needed; exports are not automatic restore files.
2. Use Meta Quest Developer Hub to **update the existing app** with `Anatome-1.4.9-Production.apk`. It retains the package name and carries the verified signing rotation proof. Do not uninstall or clear app data.
3. Open the app and confirm version 1.4.9, the existing account and expected progress. Record the headset, OS, date and APK hash for this installation check, then deploy to the remaining headsets.
4. If Android refuses the update, stop and retain the installed app and its data. Preserve the installation error for diagnosis. Do not bypass it with uninstall, data reset or forced downgrade.

Keep this APK and ZIP with the school's release record. The old debug-signed candidate remains an archive, not a rollback installer after key rotation. A corrective release should have a higher version code and use the same permanent key and lineage.

## Shared headset routine

- Guest practice is session-only and requires no email. It does not create a persistent learning record.
- For saved learning, use a school-approved learner email. Enable trusted offline PIN access only where the school permits that email to appear on the headset's sign-in page.
- Finish/pause, connect online, **Sync now**, then sign out before handing the headset to another learner. Remove trusted offline access for accounts that should no longer appear.
- Offline PIN access retains local work. Reconnect and sync before switching devices; unsynced work cannot be recovered from cloud storage.
- Replace a forgotten PIN through fresh online email-code sign-in. Do not delete an account to solve a network issue.
- Account deletion affects cloud data and the current device. Other offline devices and previously exported files need separate cleanup. The school controls account eligibility and retention of exported records.

## Future releases

Use [VR-RELEASE-SIGNING.md](VR-RELEASE-SIGNING.md) for the permanent key, backup procedure and canonical build command. Keep private signing materials outside the project and classroom ZIP. Increase both the app version and Android version code before issuing a new application payload; retain its evidence separately.

The prior automated suite covered 896 core checks, 1,761 content checks, 263 local database checks, 105 native checks, 89 native-player checks, 2,411 full XR simulator checks, 1,048 isolated UI checks, 72 scene scenarios, 22 readiness regressions and 16 build-policy checks. Five live API probes were anonymous and sent no emails. The user confirmation supplies current login evidence; the anonymous probes are not relabeled as authenticated testing.
''', encoding='utf-8')

(ROOT / 'docs/VR-RELEASE-SIGNING.md').write_text('''# Android classroom signing

The issued **1.4.9 / code 17** production APK keeps package `org.thoraxlab.learning` and uses the permanent **Anatome Classroom Release** key. The 1.4.9 candidate and its original ZIP remain unchanged as historical artifacts.

| Item | Value |
|---|---|
| Production APK | `builds/AnatomeSchool/Anatome-1.4.9-Production.apk` |
| New certificate SHA-256 | `36b13699d72d8f159e19881ea5fa368f593677a2b218d0082ba1a871dd23e910` |
| Previous debug certificate SHA-256 | `b340d254f5849ccea16bd38c92cb630f2093366a24e877b42c892412aa9e9aae` |
| Alias | `anatome-classroom-release` |
| Key | RSA 3072, SHA256withRSA, valid until 3 March 2054 |
| APK scheme | Android V3, rotation minimum API 28; application minimum API 29 |

## Update transition

An ordinary unrelated certificate cannot replace an installed app. This APK embeds a signed **proof of rotation** from the previous debug key to the permanent release key. Verification with Android's bundled `apksig` checks every API level 29–36, the exact old and new certificates, installed-data permission, and disabled old-key rollback. No debug-signed V1/V2 fallback is included. Public proof and certificates are in `qa/school-signed-release`.

The source candidate's 1,346 application entries are unchanged; only signing metadata differs. The user confirmed the candidate version on Quest 2 and Quest 3 and confirmed login. The newly signed in-place installation itself has not been observed. Follow the [classroom update instructions](CLASSROOM-RELEASE.md): update one headset, confirm retained progress, then roll out. If installation fails, preserve the error and installed data; never uninstall as a workaround.

Android documents this mechanism in [APK signature scheme V3](https://source.android.com/docs/security/features/apksigning/v3) and [`apksigner`](https://developer.android.com/tools/apksigner). Key custody and long-term signing are covered in [Android app signing](https://developer.android.com/studio/publish/app-signing).

## Private key custody

Private storage on this computer is `%LOCALAPPDATA%\\Anatome\\Signing` (outside the repository). It contains the encrypted `anatome-release.p12`, a Windows CurrentUser DPAPI-protected password, public certificate, identity record and rotation lineage. Folder permissions allow only the current Windows user and SYSTEM. Reopening the encrypted key using the stored DPAPI secret was verified against the issued certificate. Passwords are passed through process environment variables and cleared/restored afterward.

**A portable owner-controlled backup is still required.** Copying `password.dpapi` alone does not make its secret usable under another Windows profile. Keep the private key and its password in the school's secure recovery process; losing them prevents future releases from updating this app. Private key/password files must never enter the classroom ZIP, repository, screenshots, logs or chat.

To make a portable copy with a password chosen by the owner, use a private PowerShell terminal in the current Windows account. Create a private destination directory in the school's encrypted backup storage first. The following operation imports the same private key into a new encrypted PKCS12 file. `keytool` prompts for the **new backup password** without putting it in command arguments; retain that password in the school password manager. The original keystore remains untouched. This owner backup procedure has not been run.

```powershell
$privateSigning = Join-Path $env:LOCALAPPDATA 'Anatome/Signing'
$backupPath = Read-Host 'Absolute path for the new private backup .p12 file'
if (![IO.Path]::IsPathFullyQualified($backupPath) -or (Test-Path -LiteralPath $backupPath)) {
    throw 'Choose a new absolute file path in private encrypted storage outside the project.'
}
$securePassword = (Get-Content -LiteralPath "$privateSigning/password.dpapi" -Raw).Trim() | ConvertTo-SecureString
$previousSecret = $env:ANATOME_SIGNING_SECRET
try {
    $env:ANATOME_SIGNING_SECRET = [PSCredential]::new('signing', $securePassword).GetNetworkCredential().Password
    & 'C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool.exe' -importkeystore -srckeystore "$privateSigning/anatome-release.p12" -srcstoretype PKCS12 -srcalias anatome-classroom-release -srcstorepass:env ANATOME_SIGNING_SECRET -srckeypass:env ANATOME_SIGNING_SECRET -destkeystore $backupPath -deststoretype PKCS12
    if ($LASTEXITCODE -ne 0) { throw 'Backup failed; keep the original signing files.' }
} finally {
    $env:ANATOME_SIGNING_SECRET = $previousSecret
    $securePassword.Dispose()
}
```

Back up `identity.json`, `certificate.der` and `debug-to-release.lineage` beside that encrypted copy. Independently open the backup with `keytool -list -v -keystore <backup-path> -alias anatome-classroom-release` and confirm its SHA-256 fingerprint matches the table above; enter the backup password only at the prompt. A restored machine must recreate its local DPAPI password file from the backup password. Preserve the lineage for every future APK; the original debug private key is unnecessary once the lineage exists.

## Future build and verification

Keep the current release immutable. For a new release, update `LearningBuild.Prepare` to the next app version and Android version code, run the relevant tests, and close Unity. From the repository root:

```powershell
.\\pipeline\\build-school-release.ps1 -Version <next-version> -VersionCode <higher-code> -CertificateSha256 36b13699d72d8f159e19881ea5fa368f593677a2b218d0082ba1a871dd23e910 -Python <python-executable>
```

The wrapper pins the existing private identity, refuses to overwrite an issued APK, builds the application, then signs it with the stored permanent key and existing lineage. It verifies version/package/privacy policy, source payload equivalence, the exact certificate, rotation proof and 16 KB ZIP alignment. Release credentials never need to be entered into Unity. Keep a copy of each release's `qa/school-signed-release` evidence before issuing a later version. Device acceptance applies to each new application payload.

For a previously built and tested APK, `pipeline/sign-classroom-apk.ps1` accepts its path and pinned source SHA-256, plus version/code. It signs without rebuilding. This is the path used for the accepted 1.4.9 candidate. Do not rerun it over the issued output. The older direct Unity `AndroidRelease` method does not add this rotation lineage; use the wrapper for classroom releases.

Independent verification of the issued APK:

```powershell
python pipeline/verify-learning-apk.py --apk builds/AnatomeSchool/Anatome-1.4.9-Production.apk --expected-version 1.4.9 --expected-version-code 17 --expected-package org.thoraxlab.learning --expected-certificate-sha256 36b13699d72d8f159e19881ea5fa368f593677a2b218d0082ba1a871dd23e910 --require-release-signing --require-school-policy --output-dir qa/school-signed-release
```
''', encoding='utf-8')

evidence_names = ['learning-apk-verification.json', 'learning-apk-signature.txt',
                  'payload-equivalence.json', 'signing-lineage.json', 'signing-receipt.json',
                  'signing-checks.json', 'user-acceptance.json', 'release-certificate.der',
                  'previous-certificate.der', 'debug-to-release.lineage']
summary = {
    'utc': datetime.now(timezone.utc).isoformat(),
    'status': 'PRODUCTION_SIGNED_CLASSROOM_RELEASE_FUNCTIONALLY_ACCEPTED',
    'distribution': 'Private school/classroom APK', 'productionPackageReady': True,
    'fleetInstallationTransitionConfirmed': False,
    'version': '1.4.9', 'versionCode': 17, 'package': 'org.thoraxlab.learning', 'curriculum': '2026.10.2',
    'apk': record(APK), 'certificateSha256': receipt['certificateSha256'],
    'strictReleaseChecks': {'passed': 30, 'failed': 0},
    'payloadEquivalence': payload, 'signingLineage': lineage,
    'userAcceptance': acceptance,
    'automatedApplicationBaseline': {
        **record(BASE / 'verification-summary.json'),
        'counts': baseline['checks'],
        'interpretation': 'Existing application checks, not rerun during signing; application entries are unchanged. Historical pending-signing/device statements and expected candidate debug-signing failure are superseded by this record.'
    },
    'evidence': [record(QA / name) for name in evidence_names],
    'remainingOperations': [
        'Confirm one in-place installation of this production-signed APK and retained progress before fleet rollout; do not uninstall or clear data.',
        'Owner must complete and verify portable signing-key/password backup.'
    ],
    'limits': ['No measured headset FPS or sustained thermal report supplied.',
               'General login success does not establish every offline or cross-device recovery path.',
               'No public-store submission or clinical certification claimed.'],
    'noFixed72FpsCap': True,
}
save(QA / 'verification-summary.json', summary)

files = {
    'Anatome-1.4.9-Production.apk': APK,
    'CLASSROOM-RELEASE.md': ROOT / 'docs/CLASSROOM-RELEASE.md',
    'VR-RELEASE-SIGNING.md': ROOT / 'docs/VR-RELEASE-SIGNING.md',
    'evidence/verification-summary.json': QA / 'verification-summary.json',
    **{f'evidence/{name}': QA / name for name in evidence_names},
    'evidence/baseline/verification-summary.json': BASE / 'verification-summary.json',
}
for item in baseline['evidence']:
    path = ROOT / item['path']
    files['evidence/baseline/' + path.relative_to(BASE).as_posix()] = path
checksums = {name: {'sha256': sha(path), 'bytes': path.stat().st_size} for name, path in files.items()}
readme = '''ANATOME 1.4.9 / Android code 17 — PRIVATE CLASSROOM RELEASE

Install Anatome-1.4.9-Production.apk with Meta Quest Developer Hub as an update.
Read CLASSROOM-RELEASE.md first. Do not uninstall or clear learner data.

User-confirmed: Quest 2, Quest 3, and login on version 1.4.9.
Verified: 30/30 strict APK checks, unchanged tested app payload, permanent key,
Android V3 rotation proof for API 29–36. No fixed 72 FPS cap.

Before fleet rollout: confirm one signed in-place update retains progress.
Owner must separately back up the private signing key and password.
No private keys or passwords are included in this kit. Public .der certificates
and the signed .lineage proof are intentionally included for verification.

evidence/verification-summary.json is current. The baseline folder preserves
earlier checks; its pending-device/signing status is superseded by this release.
SHA256.json records every packaged document and evidence file plus the APK.
'''
checksums['START-HERE.txt'] = {'sha256': hashlib.sha256(readme.encode()).hexdigest(), 'bytes': len(readme.encode())}
with ZipFile(ARCHIVE, 'x', ZIP_DEFLATED, compresslevel=6) as archive:
    for name, path in files.items():
        assert Path(name).suffix.lower() not in {'.p12', '.pfx', '.jks', '.keystore', '.dpapi'}
        assert 'password' not in name.lower()
        archive.write(path, name)
    archive.writestr('START-HERE.txt', readme)
    archive.writestr('SHA256.json', json.dumps(checksums, indent=2) + '\n')
with ZipFile(ARCHIVE) as archive:
    assert archive.testzip() is None
    assert len(archive.namelist()) == len(set(archive.namelist()))
    assert set(archive.namelist()) == set(checksums) | {'SHA256.json'}
    for name, expected in checksums.items():
        data = archive.read(name)
        assert hashlib.sha256(data).hexdigest() == expected['sha256'] and len(data) == expected['bytes'], name
save(QA / 'delivery-manifest.json', {
    'passed': True, 'apk': record(APK), 'zip': record(ARCHIVE), 'entries': len(checksums) + 1,
    'zipCrcPassed': True, 'allEntryHashesVerified': True,
    'explicitFileAllowlist': True, 'privateKeysOrPasswordsIncluded': False,
    'originalCandidateApkUnchanged': sha(ROOT / baseline['apk']['path']) == payload['sourceSha256'],
})
print(json.dumps(read(QA / 'delivery-manifest.json'), indent=2))
