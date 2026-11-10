# Android classroom signing

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

Private storage on this computer is `%LOCALAPPDATA%\Anatome\Signing` (outside the repository). It contains the encrypted `anatome-release.p12`, a Windows CurrentUser DPAPI-protected password, public certificate, identity record and rotation lineage. Folder permissions allow only the current Windows user and SYSTEM. Reopening the encrypted key using the stored DPAPI secret was verified against the issued certificate. Passwords are passed through process environment variables and cleared/restored afterward.

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
.\pipeline\build-school-release.ps1 -Version <next-version> -VersionCode <higher-code> -CertificateSha256 36b13699d72d8f159e19881ea5fa368f593677a2b218d0082ba1a871dd23e910 -Python <python-executable>
```

The wrapper pins the existing private identity, refuses to overwrite an issued APK, builds the application, then signs it with the stored permanent key and existing lineage. It verifies version/package/privacy policy, source payload equivalence, the exact certificate, rotation proof and 16 KB ZIP alignment. Release credentials never need to be entered into Unity. Keep a copy of each release's `qa/school-signed-release` evidence before issuing a later version. Device acceptance applies to each new application payload.

For a previously built and tested APK, `pipeline/sign-classroom-apk.ps1` accepts its path and pinned source SHA-256, plus version/code. It signs without rebuilding. This is the path used for the accepted 1.4.9 candidate. Do not rerun it over the issued output. The older direct Unity `AndroidRelease` method does not add this rotation lineage; use the wrapper for classroom releases.

Independent verification of the issued APK:

```powershell
python pipeline/verify-learning-apk.py --apk builds/AnatomeSchool/Anatome-1.4.9-Production.apk --expected-version 1.4.9 --expected-version-code 17 --expected-package org.thoraxlab.learning --expected-certificate-sha256 36b13699d72d8f159e19881ea5fa368f593677a2b218d0082ba1a871dd23e910 --require-release-signing --require-school-policy --output-dir qa/school-signed-release
```
