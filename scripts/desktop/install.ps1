[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$source = $PSScriptRoot
if (!(Test-Path -LiteralPath (Join-Path $source 'StudyRoom.Tools.exe'))) { $source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../apps/desktop/dist')) }
if (!(Test-Path -LiteralPath (Join-Path $source 'StudyRoom.Tools.exe'))) { throw 'No se encuentra StudyRoom.Tools.exe. Extrae primero todo el ZIP o compila el proyecto.' }
# Keep all ownership checks, startup and integrity verification in the native setup.
& (Join-Path $source 'StudyRoom.Tools.exe') '--install'
if($LASTEXITCODE -ne 0){throw 'No se completó la instalación de las herramientas.'}
