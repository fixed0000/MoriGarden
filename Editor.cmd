@echo off
cd /d "%~dp0"
start "Mori Editor" ".tools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe" --editor --path "%~dp0."
