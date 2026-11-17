# Package only a successfully verified build. Private signing files stay on the PC.
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskEvidence = Join-Path $taskRoot 'qa/current-apk'
$taskResult = Get-Content -LiteralPath (Join-Path $taskEvidence 'build-result.json') -Raw | ConvertFrom-Json
$taskCertificate = '36b13699d72d8f159e19881ea5fa368f593677a2b218d0082ba1a871dd23e910'

if ($taskResult.simulatorChecksPassed -ne $true -or $taskResult.releaseSigningVerified -ne $true) {
    throw 'A successful simulator-checked and release-signed build is required before uploading.'
}
if ([string]$taskResult.version -notmatch '^\d+\.\d+\.\d+\z' -or [int]$taskResult.versionCode -lt 1) {
    throw 'The build result has invalid version information.'
}
$taskName = "Anatome-$($taskResult.version)-Production.apk"
$taskApk = Join-Path $taskRoot "builds/AnatomeSchool/$taskName"
if ([IO.Path]::GetFullPath($taskResult.apk) -ne [IO.Path]::GetFullPath($taskApk)) {
    throw 'The build result refers to a different APK.'
}
$taskHash = (Get-FileHash -LiteralPath $taskApk -Algorithm SHA256).Hash.ToLowerInvariant()
if ($taskHash -ne $taskResult.sha256) { throw 'The APK has changed since build verification.' }

$taskReceipt = Get-Content -LiteralPath (Join-Path $taskRoot 'qa/school-signed-release/signing-receipt.json') -Raw | ConvertFrom-Json
if ($taskReceipt.passed -ne $true -or $taskReceipt.zipAlignment16Kb -ne $true -or
    $taskReceipt.apkSha256 -ne $taskHash -or $taskReceipt.certificateSha256 -ne $taskCertificate) {
    throw 'The signing receipt does not verify this APK with the existing release certificate.'
}
foreach ($taskCheck in @('identification', 'inspection', 'museum')) {
    $taskReport = Get-Content -LiteralPath (Join-Path $taskEvidence "$taskCheck.json") -Raw | ConvertFrom-Json
    if ($taskReport.passed -ne $true) { throw "The $taskCheck simulator check did not pass." }
}

$taskCommit = (& git -C $taskRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $taskCommit -notmatch '^[0-9a-f]{40}\z') { throw 'Could not identify the source commit.' }
if ($env:GITHUB_SHA -and $env:GITHUB_SHA -ne $taskCommit) { throw 'The checkout differs from the requested GitHub commit.' }
$taskOutput = Join-Path $taskRoot 'builds/github-apk'
if (Test-Path -LiteralPath $taskOutput) { throw 'The GitHub package folder already exists. Use a clean build workspace.' }
New-Item -ItemType Directory -Path $taskOutput | Out-Null
Copy-Item -LiteralPath $taskApk -Destination (Join-Path $taskOutput $taskName)
# LF and no BOM keep this file usable with sha256sum on the publishing runner.
[IO.File]::WriteAllText((Join-Path $taskOutput "$taskName.sha256"), "$taskHash  $taskName`n", [Text.Encoding]::ASCII)
[ordered]@{
    version = $taskResult.version; versionCode = $taskResult.versionCode
    packageName = 'org.thoraxlab.learning'; apk = $taskName; sha256 = $taskHash
    sourceCommit = $taskCommit; certificateSha256 = $taskCertificate
    createdUtc = $taskResult.createdUtc; simulatorChecksPassed = $true
    releaseSigningVerified = $true; physicalHeadsetTested = $false
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'build-info.json') -Encoding utf8
Write-Host "Ready for GitHub upload: $taskName"
