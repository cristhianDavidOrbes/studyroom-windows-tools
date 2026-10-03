[CmdletBinding()]
param([switch]$Demo)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$executable = Join-Path $projectRoot 'apps/desktop/dist/StudyRoom.Tools.exe'
if (!(Test-Path -LiteralPath $executable)) { & (Join-Path $PSScriptRoot 'build.ps1'); if ($LASTEXITCODE -ne 0) { throw 'No se pudieron preparar las herramientas.' } }
if ($Demo) { Start-Process -FilePath $executable -ArgumentList '--demo' }
else { Start-Process -FilePath $executable }
