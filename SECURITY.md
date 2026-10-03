# Seguridad de StudyRoom Windows Tools

## Avisar de una vulnerabilidad

Responsable: [@cristhianDavidOrbes](https://github.com/cristhianDavidOrbes). Utiliza el reporte privado de vulnerabilidades del repositorio si está habilitado. Si no aparece esa opción, solicita un canal privado al responsable sin publicar detalles de explotación, archivos privados ni credenciales. No se promete un plazo de respuesta ni soporte para versiones históricas.

Incluye la versión, descripción, pasos de reproducción y alcance observado. No es necesario alterar protecciones ni ejecutar una instalación vulnerable para explicar el problema.

## Fronteras del componente

El helper dibuja en ventanas propias y recibe señales locales para mostrar u ocultar herramientas. No captura audio, video ni pantalla; el navegador realiza la transmisión por separado. Sus endpoints no proporcionan acceso a archivos, comandos, credenciales o contenido de la reunión.

El puente escucha exclusivamente en `127.0.0.1:8790`. Valida Host exacto, origen y métodos; acepta únicamente el origen de producción `https://studyroom-sut.onrender.com` y los orígenes de desarrollo `http://localhost:5173` y `http://127.0.0.1:5173`. Los contratos rechazan campos inesperados, parámetros, cuerpos excesivos y peticiones no admitidas. Los orígenes de desarrollo deben utilizarse solo en un entorno local de confianza.

Las comprobaciones de origen protegen frente a páginas web no autorizadas; no autentican a un programa local malicioso capaz de construir sus propias peticiones. El helper no pretende proteger un sistema Windows ya comprometido ni dibujar sobre el escritorio seguro de UAC, bloqueo o aplicaciones con pantalla completa exclusiva.

El protocolo externo acepta solamente `studyroom-tools://open`, sin consultas, comandos adicionales o tokens. La instalación es por usuario, utiliza rutas y registros propios y comprueba la integridad del paquete. No sobrescribe registros de otra aplicación. La actualización valida propiedad, ruta exacta, sesión y hash antes de detener únicamente el ejecutable propio. La desinstalación preserva registros ajenos y evita borrados recursivos.

## Distribución y comprobación

La [firma de confianza está pendiente](CODE_SIGNING.md). No confíes en un supuesto certificado del proyecto hasta que una versión documente una firma verificable. Respeta los bloqueos de Windows: no se recomienda desactivar Smart App Control, antivirus o reglas del navegador, añadir exclusiones ni confiar en un certificado autofirmado.

SHA256 permite comparar archivos con una publicación conocida; no demuestra por sí solo identidad del publicador ni ausencia de vulnerabilidades. Un resultado antivirus sin amenazas tampoco garantiza el resultado de todos los productos o de futuras versiones.

La verificación Headless comprueba el puente, contratos de salud, recursos incorporados y reglas de propiedad sin instalar ni crear ventanas. `build.ps1 -Test`, para CI Windows aislada, añade geometría, historial y regiones nativas. Estas pruebas no reemplazan revisión de código, firma ni validación interactiva de una distribución final.
