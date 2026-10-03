[CmdletBinding()]
param([switch]$Test,[switch]$HeadlessTest)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = Join-Path $projectRoot 'apps/desktop'
$output = Join-Path $source 'dist'
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
if (!(Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) { $framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319' }
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Se necesita .NET Framework 4.8 para compilar las herramientas.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Xaml.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$arguments = @('/nologo','/utf8output','/optimize+','/platform:anycpu','/target:winexe',('/out:'+(Join-Path $output 'StudyRoom.Tools.exe')),('/win32manifest:'+(Join-Path $source 'StudyRoom.Tools.exe.manifest'))) + $references + @((Join-Path $source 'Model.cs'),(Join-Path $source 'Native.cs'),(Join-Path $source 'App.cs'),(Join-Path $source 'Setup.cs'),(Join-Path $source 'Bridge.cs'),(Join-Path $source 'BridgeReadiness.cs'),(Join-Path $source 'InstallerPayload.cs'),(Join-Path $source 'AssemblyInfo.cs'))
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de las herramientas.' }
$config = '<?xml version="1.0"?><configuration><startup useLegacyV2RuntimeActivationPolicy="true"><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup><runtime><AppContextSwitchOverrides value="Switch.System.Windows.DoNotScaleForDpiChanges=false" /></runtime></configuration>'
[IO.File]::WriteAllText((Join-Path $output 'StudyRoom.Tools.exe.config'),$config)
Copy-Item -LiteralPath (Join-Path $projectRoot 'Iniciar-Herramientas.cmd') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $source 'LEEME.txt') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $source 'Instalar-Herramientas.cmd') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $source 'Desinstalar-Herramientas.cmd') -Destination $output -Force
$payloadNames=@('StudyRoom.Tools.exe','StudyRoom.Tools.exe.config','Iniciar-Herramientas.cmd','Instalar-Herramientas.cmd','Desinstalar-Herramientas.cmd','LEEME.txt')
$hashes=@($payloadNames | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $output $_) -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$_ })
[IO.File]::WriteAllLines((Join-Path $output 'SHA256SUMS.txt'),$hashes,[Text.UTF8Encoding]::new($false))
$resources=@(($payloadNames+@('SHA256SUMS.txt')) | ForEach-Object { '/resource:'+(Join-Path $output $_)+',StudyRoom.Payload.'+$_ })
$installerArguments=@('/nologo','/utf8output','/optimize+','/platform:anycpu','/target:winexe',('/out:'+(Join-Path $output 'Instalar-StudyRoom.exe')),('/win32manifest:'+(Join-Path $source 'StudyRoom.Tools.exe.manifest'))) + $references + $resources + @((Join-Path $source 'Installer.cs'),(Join-Path $source 'Setup.cs'),(Join-Path $source 'BridgeReadiness.cs'),(Join-Path $source 'InstallerPayload.cs'),(Join-Path $source 'AssemblyInfo.cs'))
& $compiler @installerArguments
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del instalador único.' }
if ($Test -or $HeadlessTest) {
  $headlessArguments=@('/nologo','/utf8output','/optimize+','/target:exe',('/out:'+(Join-Path $output 'BridgeInstallerTests.exe'))) + $references + @((Join-Path $source 'Model.cs'),(Join-Path $source 'Bridge.cs'),(Join-Path $source 'BridgeReadiness.cs'),(Join-Path $source 'InstallerPayload.cs'),(Join-Path $source 'BridgeInstallerTests.cs'))
  & $compiler @headlessArguments
  if ($LASTEXITCODE -ne 0) { throw 'No se pudieron compilar las pruebas del puente y paquete.' }
  & (Join-Path $output 'BridgeInstallerTests.exe') (Join-Path $output 'Instalar-StudyRoom.exe') $output
  if ($LASTEXITCODE -ne 0) { throw 'Falló una prueba del puente o paquete.' }
}
if ($Test) {
  $testArguments = @('/nologo','/utf8output','/optimize+','/target:exe',('/out:'+(Join-Path $output 'ModelTests.exe'))) + $references + @((Join-Path $source 'Model.cs'),(Join-Path $source 'Native.cs'),(Join-Path $source 'Bridge.cs'),(Join-Path $source 'ModelTests.cs'))
  & $compiler @testArguments
  if ($LASTEXITCODE -ne 0) { throw 'No se pudieron compilar las pruebas.' }
  & (Join-Path $output 'ModelTests.exe')
  if ($LASTEXITCODE -ne 0) { throw 'Falló una prueba del modelo de dibujo.' }
}
Write-Output 'Compilación lista: StudyRoom.Tools.exe e Instalar-StudyRoom.exe (.NET Framework 4.8).'
