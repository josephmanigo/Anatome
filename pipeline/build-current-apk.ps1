param(
    [string]$Python = '',
    [switch]$PreflightOnly,
    [switch]$NoExplorer
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskVersion = '1.6.9'
$taskCode = 29
$taskCertificate = '36b13699d72d8f159e19881ea5fa368f593677a2b218d0082ba1a871dd23e910'
$taskUnity = 'C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor'
$taskOutput = Join-Path $taskRoot "builds/AnatomeSchool/Anatome-$taskVersion-Production.apk"
$taskEvidence = Join-Path $taskRoot 'qa/current-apk'

function Require-File([string]$Path, [string]$Help) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw $Help }
}

function Find-Python {
    if ($Python) { $taskCandidates = @($Python) }
    else {
        $taskCandidates = @('python.exe', 'py.exe', 'python3.exe' | ForEach-Object {
            $taskCommand = Get-Command $_ -CommandType Application -ErrorAction SilentlyContinue
            if ($taskCommand -and $taskCommand.Source -notmatch '\\Microsoft\\WindowsApps\\') { $taskCommand.Source }
        })
    }
    foreach ($taskCandidate in $taskCandidates) {
        try {
            & $taskCandidate -c 'import sys; sys.exit(0 if sys.version_info >= (3, 9) else 1)' 2>$null
            if ($LASTEXITCODE -eq 0) { return $taskCandidate }
        } catch { }
    }
    throw 'Python 3.9 or newer is required. Install Python or run this script with -Python followed by its full executable path.'
}

try {
    if ($env:OS -ne 'Windows_NT') { throw 'Run this build on the original Windows PC with Unity and the Anatome signing key.' }
    $taskAndroid = Join-Path $taskUnity 'Data/PlaybackEngines/AndroidPlayer'
    Write-Host "Preparing Anatome $taskVersion (Android build $taskCode)."
    Require-File (Join-Path $taskUnity 'Unity.exe') 'Unity 6000.3.19f1 was not found in its original Unity Hub installation path.'
    foreach ($taskTool in @('OpenJDK/bin/java.exe', 'OpenJDK/bin/keytool.exe', 'NDK/source.properties',
        'SDK/build-tools/36.0.0/aapt.exe', 'SDK/build-tools/36.0.0/zipalign.exe', 'SDK/build-tools/36.0.0/lib/apksigner.jar')) {
        Require-File (Join-Path $taskAndroid $taskTool) "Unity Android build support is incomplete: $taskTool. Add Android Build Support, SDK/NDK and OpenJDK in Unity Hub."
    }
    $taskPython = Find-Python
    if (Test-Path -LiteralPath $taskOutput) { throw "A production APK already exists at $taskOutput. This script will not overwrite it." }

    $taskManifestPath = Join-Path $taskRoot 'build-kit-manifest.json'
    if (Test-Path -LiteralPath $taskManifestPath -PathType Leaf) {
        $taskManifest = Get-Content -LiteralPath $taskManifestPath -Raw | ConvertFrom-Json
        if ($taskManifest.version -ne $taskVersion -or $taskManifest.versionCode -ne $taskCode -or @($taskManifest.files).Count -lt 100) {
            throw 'This build kit has unexpected version information or an incomplete file list.'
        }
        Write-Host 'Checking the extracted source and asset hashes...'
        foreach ($taskFile in $taskManifest.files) {
            $taskPath = [IO.Path]::GetFullPath((Join-Path $taskRoot $taskFile.path))
            if (!$taskPath.StartsWith($taskRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'An invalid path was found in the build-kit manifest.' }
            Require-File $taskPath "An extracted file is missing: $($taskFile.path). Extract the entire ZIP again."
            if ((Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash -ne $taskFile.sha256) {
                throw "An extracted file differs from the build kit: $($taskFile.path). Use a fresh extraction."
            }
        }
    } elseif (Test-Path -LiteralPath (Join-Path $taskRoot '.git')) {
        Write-Host 'Building the current Git checkout. Checking that Git LFS assets are downloaded...'
        foreach ($taskRequired in @('unity/ThoraxLab/Assets/ThoraxLab/Models/Thorax.fbx',
            'unity/ThoraxLab/Assets/Plugins/Android/arm64-v8a/libthoraxcut.so',
            'unity/ThoraxLab/Assets/Plugins/x86_64/thoraxcut.dll',
            'unity/ThoraxLab/ProjectSettings/ProjectVersion.txt')) {
            Require-File (Join-Path $taskRoot $taskRequired) "Missing project file: $taskRequired. Restore the checkout and run git lfs pull."
        }
        foreach ($taskAsset in Get-ChildItem -LiteralPath (Join-Path $taskRoot 'unity/ThoraxLab/Assets') -File -Recurse) {
            $taskStream = [IO.File]::OpenRead($taskAsset.FullName)
            try {
                $taskHeader = New-Object byte[] 64
                $taskRead = $taskStream.Read($taskHeader, 0, $taskHeader.Length)
                if ([Text.Encoding]::ASCII.GetString($taskHeader, 0, $taskRead).StartsWith('version https://git-lfs.github.com/spec/v1')) {
                    throw "An asset is still a Git LFS pointer: $($taskAsset.Name). Run git lfs pull before building."
                }
            } finally { $taskStream.Dispose() }
        }
    } else {
        throw 'Use a complete build-kit extraction or a Git clone with Git LFS assets downloaded.'
    }
    $taskSigning = Join-Path $env:LOCALAPPDATA 'Anatome/Signing'
    foreach ($taskPrivateFile in @('identity.json', 'anatome-release.p12', 'password.dpapi', 'certificate.der', 'debug-to-release.lineage')) {
        Require-File (Join-Path $taskSigning $taskPrivateFile) 'The existing Anatome signing identity is incomplete. Use the same Windows account that built the previous production APK; do not create a replacement key.'
    }
    $taskIdentity = Get-Content -LiteralPath (Join-Path $taskSigning 'identity.json') -Raw | ConvertFrom-Json
    if ($taskIdentity.certificateSha256 -ne $taskCertificate -or $taskIdentity.alias -ne 'anatome-classroom-release' -or
        (Get-FileHash -LiteralPath (Join-Path $taskSigning 'certificate.der') -Algorithm SHA256).Hash -ne $taskCertificate) {
        throw 'The signing identity does not match the previously issued Anatome APK.'
    }
    $taskSecure = $null
    try {
        $taskSecure = (Get-Content -LiteralPath (Join-Path $taskSigning 'password.dpapi') -Raw).Trim() | ConvertTo-SecureString
        if ($taskSecure.Length -eq 0) { throw 'The stored signing credential is empty.' }
    } catch {
        throw 'This Windows account cannot open the stored signing credential. Run the build under the account used for the previous Anatome release.'
    } finally {
        if ($taskSecure) { $taskSecure.Dispose() }
    }
    if ($PreflightOnly) { Write-Host 'Preflight passed. No Unity build or signing operation was run.'; exit 0 }

    New-Item -ItemType Directory -Path $taskEvidence -Force | Out-Null
    Push-Location $taskRoot
    try {
        Write-Host 'Preparing Unity assets. Unity must already be activated for this Windows account.'
        & "$PSScriptRoot/run-unity.ps1" -Project ThoraxLab -Method ThoraxLab.Editor.LearningBuild.Prepare -Graphics -Wait -Log current-apk-prepare
        $taskChecks = @(
            @{ Name = 'identification'; Flag = '--identification-focus-only' },
            @{ Name = 'inspection'; Flag = '--model-inspection-only' },
            @{ Name = 'museum'; Flag = '--thorax-museum-only' }
        )
        foreach ($taskCheck in $taskChecks) {
            Write-Host "Running Unity checks: $($taskCheck.Name)..."
            $taskStarted = Get-Date
            & "$PSScriptRoot/run-unity.ps1" -Project ThoraxLab -Method ThoraxLab.Editor.LearningVRSimulatorVerification.Run -Graphics -NoQuit -Wait -Log ("current-apk-" + $taskCheck.Name) -ExtraArgs @($taskCheck.Flag)
            $taskReport = Join-Path $taskRoot 'qa/vr-xr-simulator-report.json'
            Require-File $taskReport 'Unity did not write its simulator verification report. Check the current-apk Unity logs in qa.'
            if ((Get-Item -LiteralPath $taskReport).LastWriteTime -lt $taskStarted) { throw 'Unity left a stale verification report.' }
            Copy-Item -LiteralPath $taskReport -Destination (Join-Path $taskEvidence ($taskCheck.Name + '.json'))
            $taskResult = Get-Content -LiteralPath $taskReport -Raw | ConvertFrom-Json
            if ($taskResult.passed -ne $true) { throw "Unity verification failed: $($taskCheck.Name). See qa/current-apk and qa/current-apk-*.log." }
        }
        Write-Host 'Building and signing the new Android APK with the existing release identity...'
        & "$PSScriptRoot/build-school-release.ps1" -Version $taskVersion -VersionCode $taskCode -CertificateSha256 $taskCertificate -Python $taskPython
        Require-File $taskOutput 'The build completed without the expected signed APK.'
        $taskHash = (Get-FileHash -LiteralPath $taskOutput -Algorithm SHA256).Hash.ToLowerInvariant()
        [ordered]@{
            version = $taskVersion; versionCode = $taskCode; apk = $taskOutput; sha256 = $taskHash
            createdUtc = [DateTime]::UtcNow.ToString('o'); simulatorChecksPassed = $true
            releaseSigningVerified = $true; physicalHeadsetTested = $false
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskEvidence 'build-result.json') -Encoding utf8
        Write-Host ''
        Write-Host "APK READY: $taskOutput"
        Write-Host "SHA-256: $taskHash"
        Write-Host 'Update the existing headset app with this APK. Physical headset validation remains to be done.'
        if (!$NoExplorer) {
            try { Start-Process explorer.exe -ArgumentList ('/select,"' + $taskOutput + '"') }
            catch { Write-Host 'The APK is ready. Open the output folder manually to copy it.' }
        }
    } finally {
        Pop-Location
    }
} catch {
    Write-Host ''
    Write-Host ('BUILD STOPPED: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'Keep the existing installed app. Build details, when available, are under qa/current-apk and qa/current-apk-*.log.'
    exit 1
}
