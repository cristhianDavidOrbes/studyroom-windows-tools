# Privacidad de StudyRoom Windows Tools

Esta política corresponde únicamente al helper Windows de este repositorio. No describe el tratamiento de datos de la web, videollamada, almacenamiento, IA, transcripciones o reportes de StudyRoom.

## Datos que maneja el helper

El programa crea sus propias ventanas transparentes para dibujar sobre el escritorio y consulta monitores, tamaño de pantalla y posición de sus controles. Procesa los clics, trazos y texto que introduces en esas herramientas. Registra un atajo para volver a interactuar con Windows; no registra el contenido general de tus pulsaciones.

Las marcas, textos e historial de dibujo permanecen en memoria. No se guardan imágenes, documentos de dibujo, grabaciones ni transcripciones. Al finalizar la última transmisión activa —o vencer su señal— se limpian las marcas y el historial; también se pierden al salir del proceso.

La instalación guarda únicamente sus archivos de programa y una marca propia en `%LOCALAPPDATA%\StudyRoom\Herramientas`. Registra el protocolo, inicio automático y acceso directo descritos en [README.md](README.md), usando la ruta de instalación de tu usuario. Comprueba sus recursos y hashes para instalar o actualizar; no lee documentos personales como parte del puente de comunicación.

## Comunicación local

El helper escucha solamente en `127.0.0.1:8790`, en este mismo equipo. Su puente no se expone a Internet ni a otros dispositivos de la red. No tiene telemetría ni hace solicitudes a servicios externos.

El navegador autorizado consulta si las herramientas están disponibles mediante `GET /v1/health`, que devuelve nombre de aplicación, versión y disponibilidad. Mediante `POST /v1/sharing` envía únicamente un identificador aleatorio de sesión y un valor que indica si sigue compartiendo pantalla. El helper usa esas señales efímeras para mostrar u ocultar el menú; no recibe contenido de pantalla, voz, cámara, chat, archivos, reportes, tokens de sesión de la web ni contraseñas.

La comprobación del instalador también utiliza únicamente ese puente local. Consultar disponibilidad no activa dibujos ni renueva una transmisión.

## Pantalla compartida

El helper no inicia ni captura una transmisión y no utiliza cámara o micrófono. El navegador y la videollamada comparten la pantalla por separado, después de tu selección y permiso. Si eliges pantalla completa, los demás participantes pueden ver las marcas junto con cualquier otro contenido visible en esa pantalla. Decide qué compartes y evita mostrar información privada.

## Control y contacto

Puedes volver a **Interactuar**, salir completamente desde la bandeja o desinstalar siguiendo [README.md](README.md). No se conserva contenido de dibujo para recuperarlo después.

Para preguntas sobre este componente, contacta a [@cristhianDavidOrbes](https://github.com/cristhianDavidOrbes). No publiques credenciales, capturas privadas ni datos de otras personas al abrir una consulta.
