@echo off
title StudyRoom - Herramientas de pantalla
if exist "%~dp0StudyRoom.Tools.exe" (
  start "" "%~dp0StudyRoom.Tools.exe"
  exit /b
)
powershell.exe -NoProfile -File "%~dp0scripts\desktop\start.ps1"
if errorlevel 1 pause
