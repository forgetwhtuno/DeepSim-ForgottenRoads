@echo off
setlocal
cd /d "%~dp0"
echo DeepSims - build and install
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0BUILD_AND_INSTALL.ps1" %*
if errorlevel 1 (
  echo.
  echo One or more mod builds failed. Copy the error text and send it back to me.
  pause
  exit /b 1
)
echo.
echo DeepSims is built and installed.
pause
