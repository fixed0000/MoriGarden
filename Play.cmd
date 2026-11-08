@echo off
setlocal
cd /d "%~dp0"
dotnet build Mori.csproj --nologo
if errorlevel 1 goto failure
start "Mori" ".tools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe" --path "%~dp0."
exit /b 0
:failure
echo Build failed. Please share this window's output.
pause
