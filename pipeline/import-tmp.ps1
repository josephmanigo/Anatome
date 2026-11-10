$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../unity/ThoraxLab'))
$package = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Library/PackageCache') -Directory -Filter 'com.unity.ugui@*' | Select-Object -First 1
$archive = Join-Path $package.FullName 'Package Resources/TMP Essential Resources.unitypackage'
$extractRoot = Join-Path (Split-Path -Parent $PSScriptRoot) '.tmp/tmp-essential-import'
New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
& tar.exe -xzf $archive -C $extractRoot
if ($LASTEXITCODE -ne 0) { throw 'TMP package extraction failed.' }
foreach ($folder in Get-ChildItem -LiteralPath $extractRoot -Directory) {
    $nameFile = Join-Path $folder.FullName 'pathname'
    if (!(Test-Path -LiteralPath $nameFile)) { continue }
    $relative = (Get-Content -LiteralPath $nameFile -Raw).Trim()
    $target = [IO.Path]::GetFullPath((Join-Path $projectRoot $relative))
    if (!$target.StartsWith((Join-Path $projectRoot 'Assets') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected path in TMP package.' }
    $assetFile = Join-Path $folder.FullName 'asset'
    if (Test-Path -LiteralPath $assetFile -PathType Leaf) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
        Copy-Item -LiteralPath $assetFile -Destination $target -Force
    } else { New-Item -ItemType Directory -Force -Path $target | Out-Null }
    $metadata = Join-Path $folder.FullName 'asset.meta'
    if (Test-Path -LiteralPath $metadata) { Copy-Item -LiteralPath $metadata -Destination ($target + '.meta') -Force }
}
Write-Output 'Imported bundled TextMeshPro essential resources with original asset GUIDs.'
