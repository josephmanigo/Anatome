$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$destination=Join-Path $root 'native\toolchain'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach($package in @('microsoft.windows.sdk.cpp','microsoft.windows.sdk.cpp.x64')){
 $version='10.0.26100.1'
 $archive=Join-Path $destination "$package.zip"
 if(!(Test-Path -LiteralPath $archive)){
   & curl.exe -L --fail --retry 3 --output $archive "https://api.nuget.org/v3-flatcontainer/$package/$version/$package.$version.nupkg"
   if($LASTEXITCODE){throw "Download failed: $package"}
 }
 Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $destination $package) -Force
 Get-FileHash -LiteralPath $archive -Algorithm SHA256
}
