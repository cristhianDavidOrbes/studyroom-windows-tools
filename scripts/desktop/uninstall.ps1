[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$source=$PSScriptRoot
if(!(Test-Path -LiteralPath (Join-Path $source 'StudyRoom.Tools.exe'))){$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../apps/desktop/dist'))}
if(!(Test-Path -LiteralPath (Join-Path $source 'StudyRoom.Tools.exe'))){throw 'Usa Desinstalar-Herramientas.cmd de la carpeta instalada o del ZIP de respaldo.'}
# The native uninstaller removes only its owned startup, protocol and known files.
& (Join-Path $source 'StudyRoom.Tools.exe') '--uninstall'
if($LASTEXITCODE -ne 0){throw 'No se completó la desinstalación de las herramientas.'}
