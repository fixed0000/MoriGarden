@echo off
setlocal
cd /d "%~dp0"
set "MORI_REVIEW_ENGINE=.tools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe"
if not exist "%MORI_REVIEW_ENGINE%" (
    echo Run Play.cmd once to install the engine, then close the game and try again.
    pause
    exit /b 1
)
dotnet build Mori.csproj --nologo
if errorlevel 1 (
    pause
    exit /b 1
)
start "Chao Animation Review" "%MORI_REVIEW_ENGINE%" --path "%~dp0." ChaoAnimationReview.tscn
