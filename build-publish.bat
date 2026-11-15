@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~publish.ps1" %*
exit /b %ERRORLEVEL%
