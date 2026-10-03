# Code signing policy

## Estado actual: PENDIENTE

El titular envió la solicitud de firma gratuita a SignPath Foundation para este componente de código abierto. El formulario oficial confirmó «Form submitted». **No se ha recibido aprobación, no se ha obtenido un certificado de la Fundación y no se afirma que las descargas actuales estén firmadas.** La firma y su identidad se documentarán únicamente cuando exista una aprobación real y un resultado verificable.

No se atribuye a SignPath la firma de versiones anteriores o actuales. No se utiliza el aviso «firma proporcionada por SignPath» mientras la entidad no haya aceptado el proyecto y firmado sus artefactos.

## Responsables

| Rol | Responsable |
| --- | --- |
| Autor y mantenedor de fuentes y scripts | [@cristhianDavidOrbes](https://github.com/cristhianDavidOrbes) |
| Revisor de cambios | [@cristhianDavidOrbes](https://github.com/cristhianDavidOrbes) |
| Aprobador de cada solicitud de firma | [@cristhianDavidOrbes](https://github.com/cristhianDavidOrbes) |

MFA para el acceso al repositorio y a SignPath es un requisito de elegibilidad. **Su cumplimiento todavía no está acreditado por esta documentación.** El responsable debe completarlo y verificarlo antes de solicitar acceso a la firma. Cada versión requiere aprobación manual; agregar un workflow no equivale a obtener aprobación del proveedor.

## Qué se propone firmar

Solo `StudyRoom.Tools.exe` y su instalador `Instalar-StudyRoom.exe`, compilados desde las fuentes Windows y scripts de este repositorio. La web, API, IA, transcripciones y reportes no están incluidos. No se propone firmar binarios ajenos ni aceptar ejecutables enviados fuera de una compilación verificable.

El orden de publicación firmado propuesto, sujeto a aceptación y configuración real, es:

1. Compilar el helper desde una revisión revisada y comprobarlo en CI Windows.
2. Solicitar aprobación manual y firmar el helper mediante el proceso autorizado por SignPath.
3. Generar la lista de integridad con el helper ya firmado e incorporar esos recursos al instalador.
4. Solicitar aprobación manual y firmar el instalador final.
5. Verificar las firmas y sus cadenas de confianza; calcular los hashes de los artefactos finales y publicar únicamente los resultados aprobados de esa revisión.

Los scripts existentes aún producen binarios sin firma. Una futura integración debe conservar ese orden para que la lista de hashes y los recursos incorporados correspondan al helper firmado. No se han añadido certificados, secretos ni una acción de firma por declarar esta política.

## Verificación y privacidad

`scripts/desktop/build.ps1 -HeadlessTest` pasó 65 comprobaciones durante la revisión aislada de este componente. En CI Windows, `-Test` añade pruebas de dibujo y regiones nativas. Las comprobaciones no conceden confianza de publicador ni garantizan ausencia de todos los avisos de Windows.

La [política de privacidad](PRIVACY.md) explica los datos locales. Este programa no transfiere información a otros sistemas de red: el helper usa solamente loopback en este equipo. La transmisión de pantalla pertenece al navegador y es una acción separada del usuario.

SignPath exige una licencia OSI, mantenimiento, documentación, reputación verificable, procedencia de compilación, roles y MFA. Puede aceptar o rechazar la solicitud. El proyecto no promete firma gratuita concedida, un plazo de aprobación ni instalación sin avisos en todos los equipos.

Fuentes: [SignPath Foundation](https://signpath.org/), [condiciones](https://signpath.org/terms.html), [solicitud](https://signpath.org/apply) y [opciones de firma Windows](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options).
