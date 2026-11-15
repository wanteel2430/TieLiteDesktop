@echo off
setlocal
cd /d "%~dp0"
set DOTNET_CLI_TELEMETRY_OPTOUT=1
if exist "dist\TieLiteDesktop.exe" (
  start "" "dist\TieLiteDesktop.exe" %*
) else if exist "bin\Release\net7.0-windows\TieLiteDesktop.exe" (
  start "" "bin\Release\net7.0-windows\TieLiteDesktop.exe" %*
) else if exist "bin\Debug\net7.0-windows\TieLiteDesktop.exe" (
  start "" "bin\Debug\net7.0-windows\TieLiteDesktop.exe" %*
) else (
  echo TieLiteDesktop.exe not found, running build first...
  call build.bat
  start "" "bin\Release\net7.0-windows\TieLiteDesktop.exe" %*
)
