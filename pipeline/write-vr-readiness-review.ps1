$ErrorActionPreference = 'Stop'
$reviewRoot = Split-Path $PSScriptRoot -Parent
function Read-ReviewJson([string]$relative) {
  Get-Content -LiteralPath (Join-Path $reviewRoot $relative) -Raw | ConvertFrom-Json
}
function Get-ReviewIdentity([string]$relative) {
  $item = Get-Item -LiteralPath (Join-Path $reviewRoot $relative)
  [ordered]@{path=$relative;bytes=$item.Length;lastWriteUtc=$item.LastWriteTimeUtc.ToString('o');sha256=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
}

$reviewDelivery = Read-ReviewJson 'qa/vr-learning-delivery-manifest.json'
$reviewSnapshot = @(Read-ReviewJson 'qa/readiness-review/source-assets-snapshot.json')
$reviewRuntime = $reviewDelivery.runtime
$reviewXr = $reviewDelivery.xrSimulator
$reviewRegressions = $reviewDelivery.readinessRegressions
$reviewContent = Read-ReviewJson 'qa/vr-content-report.json'
$reviewCoreText = Get-Content -LiteralPath (Join-Path $reviewRoot 'qa/readiness-review/core-test-output.txt') -Raw
$reviewCoreMatch = [regex]::Match($reviewCoreText, '^PASS (\d+) assertions:', [Text.RegularExpressions.RegexOptions]::Multiline)
if (!$reviewCoreMatch.Success) { throw 'Missing successful core test output.' }
if (!$reviewXr.passed -or $reviewXr.runtimeQaBypass -or $reviewXr.checks.Count -ne 74 -or $reviewXr.errors.Count -ne 0) { throw 'Current XR verification failed.' }
if ($reviewRuntime.sceneAttemptsPassed -ne 72 -or $reviewRuntime.errors.Count -ne 0 -or !$reviewRegressions.passed -or $reviewRegressions.checks.Count -ne 19) { throw 'Current player verification failed.' }
if (!$reviewContent.passed -or $reviewDelivery.backend.result -ne 'pass' -or $reviewDelivery.apkVerification.failures -ne 0) { throw 'Content, database or package verification failed.' }
$reviewFontCachePath = 'ThoraxLab/Resources/Fonts/SourceSans3-SDF.asset'
if ($reviewSnapshot.Count -eq 0 -or @($reviewSnapshot | Where-Object {!$_.matchesReview -and $_.path -cne $reviewFontCachePath}).Count -ne 0) { throw 'Review Assets do not match the main project.' }

# Recheck the recorded source snapshot before binding it to the delivery report.
$reviewSourceAssets = Join-Path $reviewRoot 'unity/ThoraxLab/Assets'
$reviewCopiedAssets = Join-Path $reviewRoot 'unity/ThoraxLabReview/Assets'
foreach ($entry in $reviewSnapshot) {
  foreach ($assetRoot in @($reviewSourceAssets,$reviewCopiedAssets)) {
    $expectedHash = if (!$entry.matchesReview -and $assetRoot -eq $reviewCopiedAssets) { $entry.reviewSha256 } else { $entry.sha256 }
    if ((Get-FileHash -LiteralPath (Join-Path $assetRoot $entry.path) -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expectedHash) { throw ('Source changed after snapshot: '+$entry.path) }
  }
  if (!$entry.matchesReview) {
    # TMP appends generated kerning data after an Editor run. Compare every other
    # serialized field, and retain both raw identities rather than hiding drift.
    $fontMain = Get-Content -LiteralPath (Join-Path $reviewSourceAssets $entry.path) -Raw
    $fontCopy = Get-Content -LiteralPath (Join-Path $reviewCopiedAssets $entry.path) -Raw
    $cachePattern = '(?ms)^  m_FontFeatureTable:\r?\n.*?(?=^  \S|\z)'
    if (![regex]::IsMatch($fontMain,$cachePattern) -or ![regex]::IsMatch($fontCopy,$cachePattern) -or [regex]::Replace($fontMain,$cachePattern,'') -cne [regex]::Replace($fontCopy,$cachePattern,'')) { throw 'Font source/settings differ beyond the generated feature cache.' }
  }
}
$reviewExtras = @(Get-ChildItem -LiteralPath $reviewCopiedAssets -Recurse -File | Where-Object {
  !(Test-Path -LiteralPath (Join-Path $reviewSourceAssets ([IO.Path]::GetRelativePath($reviewCopiedAssets,$_.FullName))) -PathType Leaf)
} | ForEach-Object { [IO.Path]::GetRelativePath($reviewCopiedAssets,$_.FullName).Replace('\','/') })
if ($reviewExtras.Count -ne 0) { throw ('Unexpected Assets in review project: '+($reviewExtras -join ', ')) }

$reviewReports = @(
  'qa/readiness-review/source-assets-snapshot.json','qa/readiness-review/core-test-output.txt',
  'qa/readiness-review/baseline/rotation-reproduced.json','qa/readiness-review/player/runtime-report.json',
  'qa/readiness-review/player/readiness-regressions.json','qa/vr-xr-simulator-report.json',
  'qa/vr-content-report.json','qa/learning-backend-report.json','qa/vr-release-gate-report.json',
  'qa/learning-apk-verification.json','qa/learning-build-StandaloneWindows64.txt',
  'qa/learning-build-StandaloneWindows64-QA.txt','qa/learning-build-Android.txt',
  'qa/vr-learning-delivery-manifest.json'
) | ForEach-Object { Get-ReviewIdentity $_ }
$reviewResult = [ordered]@{
  utc=[DateTime]::UtcNow.ToString('o');status='Software checks passed; VR review candidate';productionAccepted=$false;
  project='unity/ThoraxLab';verificationProject='unity/ThoraxLabReview';unity='6000.3.19f1';contentVersion=$reviewRuntime.contentVersion;
  source=[ordered]@{assetFiles=$reviewSnapshot.Count;exactlyMatchingAssets=@($reviewSnapshot | Where-Object {$_.matchesReview}).Count;allSourceCodeAndAnatomyMatch=$true;generatedCacheDifferences=@($reviewSnapshot | Where-Object {!$_.matchesReview});unexpectedReviewAssets=$reviewExtras;snapshot='qa/readiness-review/source-assets-snapshot.json';originalAnatomyFileUnchanged=$reviewDelivery.anatomyCardProvenance.sourceFileUnchanged;originalAnatomySha256=$reviewDelivery.anatomyCardProvenance.sourceSha256};
  tests=[ordered]@{
    officialXrSimulator=[ordered]@{passed=$reviewXr.checks.Count;utc=$reviewXr.utc;runtimeQaBypass=$reviewXr.runtimeQaBypass;errors=$reviewXr.errors};
    sceneVariants=[ordered]@{passed=$reviewRuntime.sceneAttemptsPassed;total=$reviewRuntime.variants;utc=$reviewRuntime.utc;errors=$reviewRuntime.errors};
    readinessRegressions=[ordered]@{passed=$reviewRegressions.checks.Count;utc=$reviewRegressions.utc};
    coreAssertions=[int]$reviewCoreMatch.Groups[1].Value;contentAssertions=$reviewContent.checks;postgresAssertions=$reviewDelivery.backend.checks;
    databaseScope='Local PGlite; real authenticated concurrent devices not tested';apkChecks=$reviewDelivery.apkVerification.checks.Count;releaseTrackingGatePassed=$reviewDelivery.releaseTrackingGate.passed
  };
  artifacts=$reviewDelivery.artifacts;reports=$reviewReports;
  fixes=@('Bounded upright orientation lift and interruption recovery','Assembly parent stability and recorded assessment mistakes','Independent protected-structure blade contacts with saturation cancellation','Numbered surface markers and resume reconstruction','Append-only inactive checkpoint reconciliation including same-device backup recovery','Corrupt-settings recovery with preserved originals and repaired backup','Settings commit after successful save and saved lighting restoration','Distinct local and account profile deletion','Centered keyboard labels and guarded UI refresh','Neutral selection cue preserving tissue color');
  reference=[ordered]@{title='Human Anatomy VR 2025 by Virtual Medicine';url='https://www.youtube.com/watch?v=F9zJNhHAmVI';method='Reviewed official trailer sequences; did not run the purchased game';copiedThirdPartyAssets=$false};
  physicalDeviceTested=$false;performanceMeasuredForThisBuild=$false;limitations=$reviewDelivery.limitations;
  scopeNotes=@('No production or 10/10 visual-quality claim','Research-led anatomy retains documented model and procedural abstractions','No additional professional-validation approval gate imposed','User open Editor left intact; stop and restart Play to load the updated state')
}
$reviewResult | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $reviewRoot 'qa/readiness-review/release-review.json') -Encoding utf8
Write-Output ('Recorded readiness candidate: '+$reviewSnapshot.Count+' audited asset files, 74 XR checks, 72 scene variants, 19 readiness regressions.')
