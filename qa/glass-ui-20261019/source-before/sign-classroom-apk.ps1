param(
    [Parameter(Mandatory=$true)][string]$SourceApk,
    [Parameter(Mandatory=$true)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$ExpectedSourceSha256,
    [string]$Python='python',
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='1.4.9',
    [int]$VersionCode=17
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskSource=(Resolve-Path -LiteralPath $SourceApk).Path
if ((Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash -ne $ExpectedSourceSha256) {throw 'Source APK does not match the accepted SHA-256. No signing changes made.'}
$taskAndroid='C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Data\PlaybackEngines\AndroidPlayer'
$taskJava=Join-Path $taskAndroid 'OpenJDK/bin/java.exe'
$taskKeytool=Join-Path $taskAndroid 'OpenJDK/bin/keytool.exe'
$taskSigner=Join-Path $taskAndroid 'SDK/build-tools/36.0.0/lib/apksigner.jar'
$taskEvidence=Join-Path $taskRoot 'qa/school-signed-release'
$taskOutput=Join-Path $taskRoot "builds/AnatomeSchool/Anatome-$Version-Production.apk"
if (Test-Path -LiteralPath $taskOutput) {throw 'Production output already exists. Verify and reuse it; do not silently replace an issued artifact.'}
New-Item -ItemType Directory -Path $taskEvidence,(Split-Path $taskOutput) -Force | Out-Null
& $Python "$PSScriptRoot/verify-learning-apk.py" --apk $taskSource --expected-version $Version --expected-version-code $VersionCode --output-dir "$taskEvidence/source" | Set-Content "$taskEvidence/source-verification-console.txt"
if ($LASTEXITCODE -ne 0) {throw 'Accepted source APK failed validation.'}
$taskSigning=Join-Path $env:LOCALAPPDATA 'Anatome/Signing'
$taskSigning=[IO.Path]::GetFullPath($taskSigning)
if ($taskSigning.StartsWith($taskRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)) {throw 'Private signing storage must be outside the workspace.'}
New-Item -ItemType Directory -Path $taskSigning -Force | Out-Null
if ((Get-Item -LiteralPath $taskSigning).Attributes -band [IO.FileAttributes]::ReparsePoint) {throw 'Signing storage must not be a redirected folder.'}
$taskSid=[Security.Principal.WindowsIdentity]::GetCurrent().User
$taskAcl=New-Object Security.AccessControl.DirectorySecurity
$taskAcl.SetOwner($taskSid)
$taskAcl.SetAccessRuleProtection($true,$false)
foreach ($sid in @($taskSid,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))) {
    $taskAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
}
Set-Acl -LiteralPath $taskSigning -AclObject $taskAcl
$taskKeystore=Join-Path $taskSigning 'anatome-release.p12'
$taskSecret=Join-Path $taskSigning 'password.dpapi'
$taskIdentity=Join-Path $taskSigning 'identity.json'
$taskCertificate=Join-Path $taskSigning 'certificate.der'
$taskLineage=Join-Path $taskSigning 'debug-to-release.lineage'
$taskAlias='anatome-classroom-release'
$taskOldFingerprint='b340d254f5849ccea16bd38c92cb630f2093366a24e877b42c892412aa9e9aae'
$taskPreviousSecret=$env:ANATOME_SIGNING_SECRET
$taskPreviousOldSecret=$env:ANATOME_OLD_SIGNING_SECRET
try {
    if (!(Test-Path -LiteralPath $taskIdentity)) {
        if ((Test-Path -LiteralPath $taskKeystore) -or (Test-Path -LiteralPath $taskSecret)) {throw 'Partial signing setup exists. Preserve it and recover it; never generate a replacement over an existing key.'}
        $taskPassword=[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
        $taskSecure=ConvertTo-SecureString $taskPassword -AsPlainText -Force
        $taskSecure | ConvertFrom-SecureString | Set-Content -LiteralPath $taskSecret -Encoding ascii
        $env:ANATOME_SIGNING_SECRET=$taskPassword
        & $taskKeytool -genkeypair -keystore $taskKeystore -storetype PKCS12 -alias $taskAlias -keyalg RSA -keysize 3072 -sigalg SHA256withRSA -validity 10000 -dname 'CN=Anatome Classroom Release' -storepass:env ANATOME_SIGNING_SECRET -keypass:env ANATOME_SIGNING_SECRET -noprompt
        if ($LASTEXITCODE -ne 0) {throw 'Release key generation failed.'}
        & $taskKeytool -exportcert -keystore $taskKeystore -alias $taskAlias -storepass:env ANATOME_SIGNING_SECRET -file $taskCertificate
        if ($LASTEXITCODE -ne 0) {throw 'Public certificate export failed.'}
        $taskFingerprint=(Get-FileHash -LiteralPath $taskCertificate -Algorithm SHA256).Hash.ToLowerInvariant()
        [ordered]@{schemaVersion=1;createdUtc=[DateTime]::UtcNow.ToString('o');alias=$taskAlias;certificateSha256=$taskFingerprint;algorithm='RSA-3072 / SHA256withRSA';passwordProtection='Windows DPAPI CurrentUser';backupRequired='Copy the encrypted keystore and record its password in the owner password manager before replacing this Windows profile.'} | ConvertTo-Json | Set-Content -LiteralPath $taskIdentity -Encoding utf8
    } else {
        $taskRecord=Get-Content -LiteralPath $taskIdentity -Raw | ConvertFrom-Json
        if ($taskRecord.alias -ne $taskAlias) {throw 'Unexpected signing alias; refusing to replace identity.'}
        $taskFingerprint=$taskRecord.certificateSha256
        if ((Get-FileHash -LiteralPath $taskCertificate -Algorithm SHA256).Hash -ne $taskFingerprint) {throw 'Stored public certificate differs from signing identity.'}
        $taskSecure=(Get-Content -LiteralPath $taskSecret -Raw).Trim() | ConvertTo-SecureString
        $env:ANATOME_SIGNING_SECRET=[PSCredential]::new('signing',$taskSecure).GetNetworkCredential().Password
    }
    if (!(Test-Path -LiteralPath $taskLineage)) {
        $taskOldKeystore=Join-Path $env:USERPROFILE '.android/debug.keystore'
        $env:ANATOME_OLD_SIGNING_SECRET='android'
        & $taskKeytool -exportcert -keystore $taskOldKeystore -alias androiddebugkey -storepass:env ANATOME_OLD_SIGNING_SECRET -file "$taskEvidence/previous-certificate.der"
        if ($LASTEXITCODE -ne 0 -or (Get-FileHash "$taskEvidence/previous-certificate.der" -Algorithm SHA256).Hash -ne $taskOldFingerprint) {throw 'Previous keystore does not match the installed-app certificate; cannot authorize key rotation.'}
        & $taskJava -jar $taskSigner rotate --out $taskLineage --old-signer --ks $taskOldKeystore --ks-key-alias androiddebugkey --ks-pass env:ANATOME_OLD_SIGNING_SECRET --key-pass env:ANATOME_OLD_SIGNING_SECRET --set-installed-data true --set-shared-uid false --set-permission false --set-rollback false --set-auth false --new-signer --ks $taskKeystore --ks-key-alias $taskAlias --ks-pass env:ANATOME_SIGNING_SECRET --key-pass env:ANATOME_SIGNING_SECRET
        if ($LASTEXITCODE -ne 0) {throw 'Signing rotation proof could not be created.'}
    }
    # Every supported headset is API 29+, so V3 can use the new key without a debug-signed V2 fallback.
    & $taskJava -jar $taskSigner sign --ks $taskKeystore --ks-key-alias $taskAlias --ks-pass env:ANATOME_SIGNING_SECRET --key-pass env:ANATOME_SIGNING_SECRET --lineage $taskLineage --rotation-min-sdk-version 28 --v1-signing-enabled false --v2-signing-enabled false --v3-signing-enabled true --v4-signing-enabled false --out $taskOutput $taskSource
    if ($LASTEXITCODE -ne 0) {throw 'APK signing failed.'}
    & $Python "$PSScriptRoot/verify-learning-apk.py" --apk $taskOutput --expected-version $Version --expected-version-code $VersionCode --expected-certificate-sha256 $taskFingerprint --require-release-signing --require-school-policy --output-dir $taskEvidence | Set-Content "$taskEvidence/verification-console.txt"
    if ($LASTEXITCODE -ne 0) {throw 'Signed APK failed strict release verification.'}
    & $Python "$PSScriptRoot/verify-apk-payload.py" --source $taskSource --signed $taskOutput --output "$taskEvidence/payload-equivalence.json"
    if ($LASTEXITCODE -ne 0) {throw 'Signing changed application contents.'}
    & $taskJava -cp $taskSigner "$PSScriptRoot/VerifySigningLineage.java" $taskOutput $taskOldFingerprint $taskFingerprint | Set-Content "$taskEvidence/signing-lineage.json"
    if ($LASTEXITCODE -ne 0) {throw 'APK rotation proof verification failed.'}
    & (Join-Path $taskAndroid 'SDK/build-tools/36.0.0/zipalign.exe') -c -P 16 4 $taskOutput
    if ($LASTEXITCODE -ne 0) {throw 'Signed APK alignment verification failed.'}
    Copy-Item -LiteralPath $taskCertificate -Destination "$taskEvidence/release-certificate.der"
    Copy-Item -LiteralPath $taskLineage -Destination "$taskEvidence/debug-to-release.lineage"
    [ordered]@{utc=[DateTime]::UtcNow.ToString('o');passed=$true;sourceSha256=$ExpectedSourceSha256.ToLowerInvariant();apk=$taskOutput;apkSha256=(Get-FileHash $taskOutput -Algorithm SHA256).Hash.ToLowerInvariant();certificateSha256=$taskFingerprint;previousCertificateSha256=$taskOldFingerprint;privateKeyOutsideWorkspace=$true;privateDirectoryRestrictedToCurrentUserAndSystem=$true;passwordProtectedWithCurrentUserDpapi=$true;zipAlignment16Kb=$true;physicalInstallationTested=$false} | ConvertTo-Json | Set-Content "$taskEvidence/signing-receipt.json"
    Write-Output "Verified production APK: $taskOutput"
    Write-Output "Private signing identity: $taskSigning (never include this folder in the classroom ZIP)."
} finally {
    $env:ANATOME_SIGNING_SECRET=$taskPreviousSecret
    $env:ANATOME_OLD_SIGNING_SECRET=$taskPreviousOldSecret
    $taskPassword=$null
    if ($taskSecure) {$taskSecure.Dispose()}
}
