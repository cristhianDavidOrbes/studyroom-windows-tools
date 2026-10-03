# StudyRoom Windows Tools

Herramienta de escritorio para dibujar sobre Windows mientras compartes pantalla en StudyRoom. Incluye lápiz, borrador de un objeto, texto, figuras, colores, grosor, deshacer, rehacer y limpieza de las marcas.

Este repositorio contiene únicamente el helper de Windows. La web, videollamada, backend, inteligencia artificial, transcripciones y reportes de StudyRoom son componentes separados. El helper no captura pantalla, cámara ni micrófono y no transcribe conversaciones.

## Estado de la firma

**Firma de confianza pendiente.** El titular envió la solicitud a SignPath Foundation; todavía no se ha obtenido su aprobación ni certificado. La descarga directa actual no tiene firma de publicador y Windows puede bloquearla. Un análisis antivirus sin amenazas o una lista SHA256 no sustituyen una firma. Respeta los avisos de Windows: el proyecto no pide desactivar protecciones ni instalar certificados autofirmados.

Consulta la [política de firma de código / Code signing policy](CODE_SIGNING.md) para conocer el estado, responsables y requisitos.

## Instalación y uso

Requiere Windows con .NET Framework 4.8. Descarga el instalador desde la página de StudyRoom o, cuando exista una versión publicada aquí, desde [Releases](https://github.com/cristhianDavidOrbes/studyroom-windows-tools/releases). Este repositorio de fuentes no instala la aplicación por sí solo.

1. Abre `Instalar-StudyRoom.exe` una vez. Instala para tu usuario, sin solicitar administrador, e inicia las herramientas en segundo plano. Solo confirma éxito cuando el puente local responde correctamente.
2. Vuelve a StudyRoom, espera «La aplicación ya está instalada» y pulsa **Continuar**. El navegador puede pedir permiso de acceso a aplicaciones locales o confirmación para abrir `studyroom-tools://open`.
3. Comparte **Pantalla completa** desde la videollamada. El botón del menú aparece arriba a la derecha y se oculta al terminar. El helper no inicia la transmisión: el navegador la realiza con tu permiso.
4. Abre el botón para dibujar. Puedes pulsar sus controles con el lápiz activo. **Interactuar** o **Esc** devuelve los clics al escritorio. Puedes arrastrar el panel y elegir otro monitor.

Para que otros vean las marcas, el navegador debe capturar la pantalla completa donde dibujas. Compartir únicamente una pestaña o ventana suele excluir la superposición del escritorio. Las marcas y su historial viven en memoria; se limpian al acabar la última transmisión activa, también cuando vence su señal local. Durante una transmisión, cerrar el panel lo devuelve contraído.

Como respaldo, se puede extraer todo `StudyRoom-Herramientas.zip` y abrir `Instalar-Herramientas.cmd`. No basta con guardar un ZIP en Descargas: una página no puede instalarlo ni abrirlo silenciosamente.

## Cambios que hace la instalación

La integración copia únicamente sus siete archivos de paquete y una marca de propiedad a `%LOCALAPPDATA%\StudyRoom\Herramientas`. Registra:

- `HKCU\Software\Classes\studyroom-tools`, para abrir el protocolo propio `studyroom-tools://open`.
- El valor `StudyRoomTools` en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, con su ruta exacta y `--background`, para esperar oculto al iniciar sesión en Windows.
- Un acceso directo **Herramientas StudyRoom** en el escritorio del usuario, si Windows permite crearlo.

No usa servicios del sistema ni modifica protecciones, certificados o políticas de ejecución. Rechaza registros de otras aplicaciones y enlaces a ubicaciones ajenas. Una actualización puede cerrar y reiniciar únicamente la instalación propia después de comprobar ruta, sesión y hash conocido.

## Desinstalación

En el icono de StudyRoom de la bandeja de Windows, elige **Salir completamente**. Abre `Desinstalar-Herramientas.cmd` desde el ZIP de respaldo extraído para retirar el ejecutable instalado sin mantenerlo en uso. También existe ese comando en la carpeta instalada; si Windows mantiene archivos abiertos, el desinstalador lo informa y permite reintentar tras cerrarlos.

La desinstalación elimina únicamente protocolo, inicio automático, acceso directo y archivos propios. Conserva entradas modificadas por otra aplicación, archivos adicionales y enlaces; no borra recursivamente carpetas del usuario.

## Compilar y comprobar

Se utiliza el compilador de .NET Framework incluido en Windows, sin paquetes de Node ni dependencias de web/API:

```powershell
.\scripts\desktop\build.ps1 -HeadlessTest
```

Esta revisión de fuentes pasó **65 comprobaciones** del puente HTTP, contratos de disponibilidad, recursos incorporados y propiedad de instalación/actualización. Las pruebas Headless no crean ventanas, ejecutan la aplicación o el instalador, escriben el registro ni detienen procesos del usuario.

Para la CI en un runner Windows aislado:

```powershell
.\scripts\desktop\build.ps1 -Test
```

`-Test` añade pruebas de geometría, historial y regiones nativas con ventanas ocultas. Compilar o pasar estas pruebas no concede una firma de publicador.

Las salidas se generan en `apps/desktop/dist`. `scripts/desktop/package.ps1` empaqueta el resultado y conserva un destino local heredado, `apps/web/public/desktop`, bajo esta raíz. No requiere la web ni publica en Internet. `Iniciar-Herramientas.cmd` utiliza el ejecutable adyacente cuando existe; desde fuentes recurre a `scripts/desktop/start.ps1`, que compila si es necesario y abre la herramienta. Los envoltorios `install.ps1` y `uninstall.ps1` delegan en la instalación nativa.

El modo normal no define `STORE_PACKAGE`; los bloques condicionales conservados en `App.cs` no requieren archivos de Store.

## Privacidad, seguridad y licencia

Lee [PRIVACY.md](PRIVACY.md) y [SECURITY.md](SECURITY.md). Responsable del proyecto: [@cristhianDavidOrbes](https://github.com/cristhianDavidOrbes).

Este componente se publica con licencia MIT; el texto y titular se indican en [LICENSE](LICENSE). La licencia de este repositorio no se extiende automáticamente a los demás componentes de StudyRoom.
