@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0pipeline\build-current-apk.ps1"
set "ANATOME_BUILD_EXIT=%ERRORLEVEL%"
echo.
if not "%ANATOME_BUILD_EXIT%"=="0" echo The build did not finish. Read the error above before closing this window.
pause
exit /b %ANATOME_BUILD_EXIT%
