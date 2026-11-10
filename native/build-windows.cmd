@echo off
call "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 >nul
set "PATH=C:\Users\My Pc\Downloads\new\native\toolchain\microsoft.windows.sdk.cpp\c\bin\10.0.26100.0\x64;%PATH%"
set "INCLUDE=C:\Users\My Pc\Downloads\new\native\toolchain\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0\ucrt;C:\Users\My Pc\Downloads\new\native\toolchain\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0\shared;C:\Users\My Pc\Downloads\new\native\toolchain\microsoft.windows.sdk.cpp\c\Include\10.0.26100.0\um;%INCLUDE%"
set "LIB=C:\Users\My Pc\Downloads\new\native\toolchain\microsoft.windows.sdk.cpp.x64\c\ucrt\x64;C:\Users\My Pc\Downloads\new\native\toolchain\microsoft.windows.sdk.cpp.x64\c\um\x64;%LIB%"
"C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\3.22.1\bin\cmake.exe" -S "C:\Users\My Pc\Downloads\new\native" -B "C:\Users\My Pc\Downloads\new\native\build-windows" -G Ninja "-DCMAKE_MAKE_PROGRAM=C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\3.22.1\bin\ninja.exe" -DCMAKE_BUILD_TYPE=Release
if errorlevel 1 exit /b 1
"C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\cmake\3.22.1\bin\cmake.exe" --build "C:\Users\My Pc\Downloads\new\native\build-windows" --parallel 6
if errorlevel 1 exit /b 1
"C:\Users\My Pc\Downloads\new\native\build-windows\thoraxcut_tests.exe"
exit /b %errorlevel%