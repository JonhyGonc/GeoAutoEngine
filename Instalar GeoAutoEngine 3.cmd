@echo off
setlocal
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\install_geoautoengine.ps1" -Configuration Release
if errorlevel 1 (
  echo.
  echo A instalação não foi concluída.
  pause
  exit /b 1
)
echo.
pause
