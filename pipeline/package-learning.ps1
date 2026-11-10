param([int]$BenchmarkProcess=0)
$taskRoot=Split-Path $PSScriptRoot -Parent
if($BenchmarkProcess){Wait-Process -Id $BenchmarkProcess -ErrorAction Stop}
$taskPlayer=Join-Path $taskRoot 'builds/LearningWindows'
$taskApk=Join-Path $taskRoot 'builds/LearningQuest/ThoraxLab.apk'
$taskPerformance=Join-Path $taskRoot 'qa/learning-performance/windows-15-minute-benchmark.json'
if(!(Test-Path -LiteralPath $taskPerformance)){throw 'Benchmark report is missing; evidence package was not produced.'}
$taskOutput=Join-Path $taskRoot 'handoff'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskArchive=Join-Path $taskOutput ('ThoraxLab-Learning-Windows-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.zip')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskZip=[System.IO.Compression.ZipFile]::Open($taskArchive,[System.IO.Compression.ZipArchiveMode]::Create)
try {
  $taskDebug=Join-Path $taskPlayer 'Thorax Lab_BurstDebugInformation_DoNotShip'
  foreach($taskFile in (Get-ChildItem -LiteralPath $taskPlayer -Recurse -File)) {
    if($taskFile.FullName.StartsWith($taskDebug+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){continue}
    $taskRelative=[IO.Path]::GetRelativePath($taskPlayer,$taskFile.FullName).Replace('\','/')
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskZip,$taskFile.FullName,'ThoraxLab-LearningWindows/'+$taskRelative,[System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
} finally {$taskZip.Dispose()}
$taskCheck=[System.IO.Compression.ZipFile]::OpenRead($taskArchive)
try {foreach($taskRequired in @('ThoraxLab.exe','UnityPlayer.dll','ThoraxLab_Data/globalgamemanagers')){if(!$taskCheck.GetEntry('ThoraxLab-LearningWindows/'+$taskRequired)){throw ('Missing archive entry: '+$taskRequired)}}} finally {$taskCheck.Dispose()}
$taskSettings=((Get-Content -LiteralPath (Join-Path $taskRoot 'qa/learning-performance/LearningQA-data/settings.save') -Raw) -split "`n",3)[2] | ConvertFrom-Json
$taskProfile=$taskSettings.profiles | Where-Object id -eq $taskSettings.selectedId | Select-Object -First 1
$taskFiles=@($taskArchive,$taskApk) | ForEach-Object { $taskFile=Get-Item -LiteralPath $_; [ordered]@{path=$taskFile.FullName;bytes=$taskFile.Length;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()} }
$taskManifest=[ordered]@{
  utc=[DateTime]::UtcNow.ToString('o');status='Working UI review build; full release not accepted';unity='6000.3.19f1';
  artifacts=$taskFiles;performance=(Get-Content -LiteralPath $taskPerformance -Raw | ConvertFrom-Json);
  benchmarkConfiguration=[ordered]@{width=$taskProfile.width;height=$taskProfile.height;renderScale=$taskProfile.renderScale;frameCap=$taskProfile.frameCap;vsync=$taskProfile.vsync;msaa=$taskProfile.msaa;textureMipLimit=$taskProfile.textureLimit;shadows=$taskProfile.shadows;narrationVolume=$taskProfile.narrationVolume};
  runtime=(Get-Content -LiteralPath (Join-Path $taskRoot '.impeccable/review/runtime-report.json') -Raw | ConvertFrom-Json);
  backend=(Get-Content -LiteralPath (Join-Path $taskRoot 'qa/learning-backend-report.json') -Raw | ConvertFrom-Json);
  liveApi=(Get-Content -LiteralPath (Join-Path $taskRoot 'qa/learning-live-api-report.json') -Raw | ConvertFrom-Json);
  liveSetup=(Get-Content -LiteralPath (Join-Path $taskRoot 'qa/learning-live-setup.json') -Raw | ConvertFrom-Json);
  apkVerification=(Get-Content -LiteralPath (Join-Path $taskRoot 'qa/learning-apk-verification.json') -Raw | ConvertFrom-Json);
  benchmarkArtifact=(Get-Content -LiteralPath (Join-Path $taskRoot 'qa/learning-performance/benchmark-artifact.json') -Raw | ConvertFrom-Json);
  limitations=@('SMTP test failed: saved provider hostname does not resolve; real login pending','Real-account and concurrent-device tests pending','Detailed clinical procedures incomplete','Physical Quest 3 and Iris Xe-class laptop not tested','User visual acceptance pending','Quest APK uses a debug certificate for sideload review','15-minute benchmark predates the objective-reward correction; graphics code and preset values are unchanged')
}
$taskManifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $taskRoot 'qa/learning-delivery-manifest.json') -Encoding utf8
Write-Output ('Packaged '+$taskArchive)
