@echo off
setlocal
cd /d "%~dp0"
set DOTNET_CLI_TELEMETRY_OPTOUT=1
echo Building TieLiteDesktop (Release)...
dotnet build TieLiteDesktop.csproj -c Release
if errorlevel 1 (
  echo Build failed.
  pause
  exit /b 1
)
echo.
echo Output: bin\Release\net7.0-windows\TieLiteDesktop.exe
pause
