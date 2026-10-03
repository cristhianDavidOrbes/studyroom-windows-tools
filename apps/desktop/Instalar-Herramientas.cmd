@echo off
if not exist "%~dp0StudyRoom.Tools.exe" (
  echo Extrae todo el ZIP antes de instalar.
  pause
  exit /b 1
)
"%~dp0StudyRoom.Tools.exe" --install
