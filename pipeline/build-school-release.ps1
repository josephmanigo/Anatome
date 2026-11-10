param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$CertificateSha256,
    [string]$Python='python',
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='1.4.9',
    [int]$VersionCode=17
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskIdentity=Join-Path $env:LOCALAPPDATA 'Anatome/Signing/identity.json'
if (!(Test-Path -LiteralPath $taskIdentity)) {throw 'Restore the existing classroom signing identity before building; do not generate a replacement key.'}
$taskRecord=Get-Content -LiteralPath $taskIdentity -Raw | ConvertFrom-Json
if ($taskRecord.certificateSha256 -ne $CertificateSha256) {throw 'Expected certificate differs from the stored classroom identity.'}
$taskOutput=Join-Path $taskRoot "builds/AnatomeSchool/Anatome-$Version-Production.apk"
if (Test-Path -LiteralPath $taskOutput) {throw 'Production output already exists. Use a new version/code for a new release.'}
$taskApk=Join-Path $taskRoot 'builds/AnatomeQuest/Anatome.apk'
$taskStarted=Get-Date
& "$PSScriptRoot/run-unity.ps1" -Project ThoraxLab -Method ThoraxLab.Editor.LearningBuild.Android -Target Android -Graphics -Wait -Log school-release-build
if (!(Test-Path -LiteralPath $taskApk) -or (Get-Item -LiteralPath $taskApk).LastWriteTime -lt $taskStarted) {
    throw 'No fresh release APK was produced for the requested version.'
}
& "$PSScriptRoot/sign-classroom-apk.ps1" -SourceApk $taskApk -ExpectedSourceSha256 (Get-FileHash -LiteralPath $taskApk -Algorithm SHA256).Hash -Python $Python -Version $Version -VersionCode $VersionCode
Write-Output "Production APK verified: $taskOutput. Record device acceptance for each new application payload before classroom rollout."
