@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>&1
if errorlevel 1 (
    echo Install the .NET 8 SDK, then double-click Play.cmd again.
    start "" "https://dotnet.microsoft.com/download/dotnet/8.0"
    pause
    exit /b 1
)
set "MORI_ENGINE=.tools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe"
if not exist "%MORI_ENGINE%" (
    echo Downloading Godot 4.5.1 .NET for the first launch...
    if not exist ".tools" mkdir ".tools"
    curl.exe --fail --location --retry 2 --output ".tools\mori-engine.zip" "https://github.com/godotengine/godot-builds/releases/download/4.5.1-stable/Godot_v4.5.1-stable_mono_win64.zip"
    if errorlevel 1 goto failure
    powershell.exe -NoProfile -Command "Expand-Archive -LiteralPath '.tools\mori-engine.zip' -DestinationPath '.tools' -Force"
    if errorlevel 1 goto failure
    if not exist "%MORI_ENGINE%" goto failure
)
dotnet build Mori.csproj --nologo
if errorlevel 1 goto failure
start "Mori" "%MORI_ENGINE%" --path "%~dp0."
exit /b 0
:failure
echo Mori could not start. The error is shown above.
pause
exit /b 1
