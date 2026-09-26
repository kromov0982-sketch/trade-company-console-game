@echo off
setlocal
cd /d "%~dp0"
set "DOTNET_ROOT=%~dp0.tools\dotnet"
set "DOTNET_CLI_HOME=%~dp0.tools"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"

"%DOTNET_ROOT%\dotnet.exe" publish TradeCompany.csproj -c Release -r win-x64 --self-contained true --source https://api.nuget.org/v3/index.json -o release\portable /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:DebugType=None /p:DebugSymbols=false
if errorlevel 1 goto error
xcopy /e /i /y "%~dp0Packaging" "%~dp0release\portable" >nul
exit /b 0
:error
pause
