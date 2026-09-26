@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
set "DOTNET_ROOT=%~dp0.tools\dotnet"
set "DOTNET_CLI_HOME=%~dp0.tools"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
if exist "%~dp0bin\Release\net10.0\TradeCompany.dll" goto run
"%DOTNET_ROOT%\dotnet.exe" restore --configfile "%~dp0NuGet.Config"
if errorlevel 1 goto end
"%DOTNET_ROOT%\dotnet.exe" build --no-restore "%~dp0TradeCompany.csproj" --configuration Release
if errorlevel 1 goto end
:run
"%DOTNET_ROOT%\dotnet.exe" "%~dp0bin\Release\net10.0\TradeCompany.dll"
exit /b %errorlevel%
:end
pause
