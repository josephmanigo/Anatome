param([switch]$Android)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$sdk='C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Data\PlaybackEngines\AndroidPlayer'
$cmake=Join-Path $sdk 'SDK\cmake\3.22.1\bin\cmake.exe'
$ninja=Join-Path $sdk 'SDK\cmake\3.22.1\bin\ninja.exe'
if($Android){
 & $cmake -S "$root\native" -B "$root\native\build-android" -G Ninja "-DCMAKE_MAKE_PROGRAM=$ninja" "-DCMAKE_TOOLCHAIN_FILE=$sdk\NDK\build\cmake\android.toolchain.cmake" -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-29 -DCMAKE_BUILD_TYPE=Release
 if($LASTEXITCODE){throw 'Android configure failed'}
 & $cmake --build "$root\native\build-android" --parallel 6
 if($LASTEXITCODE){throw 'Android native build failed'}
 Copy-Item -LiteralPath "$root\native\build-android\libthoraxcut.so" -Destination "$root\unity\ThoraxLab\Assets\Plugins\Android\arm64-v8a\libthoraxcut.so"
} else {
 $vs='C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat'
 $body=@"
@echo off
call "$vs" -arch=x64 >nul
set "PATH=$root\native\toolchain\microsoft.windows.sdk.cpp\c\bin\10.0.26100.0\x64;%PATH%"
set "INCLUDE=$root\native\toolchain\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0\ucrt;$root\native\toolchain\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0\shared;$root\native\toolchain\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0\um;%INCLUDE%"
set "LIB=$root\native\toolchain\microsoft.windows.sdk.cpp.x64\c\ucrt\x64;$root\native\toolchain\microsoft.windows.sdk.cpp.x64\c\um\x64;%LIB%"
"$cmake" -S "$root\native" -B "$root\native\build-windows" -G Ninja "-DCMAKE_MAKE_PROGRAM=$ninja" -DCMAKE_BUILD_TYPE=Release
if errorlevel 1 exit /b 1
"$cmake" --build "$root\native\build-windows" --parallel 6
if errorlevel 1 exit /b 1
"$root\native\build-windows\thoraxcut_tests.exe"
exit /b %errorlevel%
"@
 $runner=Join-Path $root 'native\build-windows.cmd'
 [IO.File]::WriteAllText($runner,$body)
 & $env:ComSpec /d /c $runner
 if($LASTEXITCODE){throw 'Windows native build/tests failed'}
 Copy-Item -LiteralPath "$root\native\build-windows\thoraxcut.dll" -Destination "$root\unity\ThoraxLab\Assets\Plugins\x86_64\thoraxcut.dll"
}
