@echo off
if not exist "%~dp0StudyRoom.Tools.exe" (
  echo Ejecuta este archivo desde la carpeta original del ZIP extraido.
  pause
  exit /b 1
)
"%~dp0StudyRoom.Tools.exe" --uninstall
