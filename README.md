# Drex Downloads

<p align="center"><img src="assets/drex-downloads.png" width="150" alt="Icono de Drex Downloads"></p>

**Gestor local de descargas HTTP/HTTPS para Windows, con recuperación por bloques y protección del progreso.**

Proyecto de [Yoarcy](https://github.com/Yoarcy). Desarrollado en C# / Windows Forms con motor HTTP propio sobre .NET Framework. No depende de IDM, aria2, OpenClaw ni servicios de IA para descargar.

[Descargar versión 1.0.7](https://github.com/Yoarcy/drex-downloads/releases/tag/v1.0.7) · [Manual](LEEME.txt) · [Arquitectura](docs/ARQUITECTURA.md) · [Compilación](docs/BUILD.md)

![Interfaz de Drex Downloads](docs/interfaz.png)

## Funciones

- Cola secuencial y hasta cuatro conexiones por archivo.
- Pausa, reanudación y renovación manual de enlaces.
- Guardado incremental con doble diario y comprobación SHA-256 por bloques.
- Recuperación de bloques válidos tras interrupciones; los parciales no se eliminan por un error.
- SHA-256 esperado opcional, límites de velocidad y soporte de enlaces de Hugging Face.
- Bandeja del sistema y arranque condicional: solo recupera tras reinicio si quedó una descarga activa interrumpida.
- Renovación automática desde el enlace de origen cuando sea renovable; espera de red con reintentos locales.
- Interfaz oscura texturada e icono propio en ventana, ejecutable, bandeja e instalador.
- Modo **Internet exclusivo**, componente opcional y experimental de control de salida de Windows.

## Requisitos e instalación

Windows de escritorio con **.NET Framework 4.8**. Compilación AnyCPU; la ruta de compilación incluida usa el compilador de Windows de 64 bits. No se ha validado una matriz completa de versiones de Windows.

1. Descarga `DrexDownloads-Setup-1.0.7.exe` desde Releases.
2. Ejecuta el instalador y elige una **carpeta vacía** con permisos de escritura.
3. Abre `DrexDownloads.exe`. El instalador no inicia descargas ni cambia el firewall.

También existe un ZIP portable. No requiere Python para el uso normal. Los ejecutables no están firmados digitalmente.

**Internet exclusivo está integrado en el ejecutable.** Al activarlo, Windows puede pedir permisos de administrador. La preparación del supervisor y de la recuperación al inicio es automática: no hay scripts ni paquetes que instalar aparte. Consulta [la guía del control de red](docs/NETWORK.md).

## Uso rápido

Pulsa **Nueva descarga**, pega un enlace directo HTTP/HTTPS y selecciona destino. Cerrar la ventana mantiene las descargas en la bandeja; para salir usa **Más → Salir y conservar progreso**. No borres `data/` ni `.drex-partials/` si quieres conservar la recuperación.

## Recuperación y límites

El motor intenta confirmar datos cada 5 segundos o 4 MiB por conexión: primero sincroniza el archivo y luego los diarios. Es un objetivo operativo, **no una garantía de pérdida máxima ante un apagón**. Se han probado fallos de proceso y corrupción controlada, no un corte eléctrico físico.

Reanudar requiere soporte de rangos y validación de identidad/tamaño. No descarga páginas de vídeo, enlaces `blob:`, ni importa parciales privados de IDM. No tiene extensión de navegador. No se garantiza una velocidad superior a otros gestores.

## Estado de las pruebas

- **41/41 pruebas locales aprobadas en 1.0.7**: motor, recuperación, identidad, pausas, arranque condicional, renovación de enlaces y espera de red.
- Compilación, interfaz, extracción del instalador y supervisor incrustado verificados.
- Cinco casos de portabilidad de red y pruebas reales LAN/RDP/SMB, bloqueo HTTPS y restauración aprobados en 1.0.6.
- No se realizó un apagón físico. El arranque condicional se prueba con metadatos y cambios simulados de arranque.

## Recuperación autónoma

Al descargar se prepara una entrada de inicio del usuario. Tras un nuevo arranque, al iniciar sesión en Windows, solo abre y reanuda si quedó una tarea activa interrumpida. No se activa por tareas pausadas, terminadas ni después de **Salir y conservar progreso**. Cerrar con X mantiene la descarga en la bandeja; no equivale a salir. No necesita agente ni créditos de IA.

Un enlace estable de origen se vuelve a solicitar y sus redirecciones se resuelven de nuevo. Se evitan respuestas de caché y se limita la renovación tras denegaciones de acceso. No se puede inventar un enlace nuevo si solo se dispone de una URL firmada caducada, ni superar una cuenta sin permisos, CAPTCHA o autenticación expirada. Se conserva el parcial y se informa el bloqueo. La identidad/tamaño/hash siguen validándose antes de reutilizar los bloques.

## Estructura

| Ruta | Contenido |
|---|---|
| `src/` | Motor, interfaz, instalador y scripts de compilación |
| `network/` | Fuente del supervisor, incrustada al compilar |
| `tests/` | Pruebas con servidor HTTP local y recuperación |
| `assets/` | Icono PNG/ICO y prompt de creación |
| `docs/` | Arquitectura, instalación, cambios y evidencias |

## Privacidad

El repositorio y las distribuciones no incluyen descargas, historial de tareas, tokens, enlaces firmados, snapshots del firewall ni respaldos del equipo del autor. Los tokens opcionales de Hugging Face se protegen con el usuario de Windows; no compartas tu carpeta de datos.

## Desarrollo y contribuciones

Consulta [BUILD.md](docs/BUILD.md). Reporta errores con pasos reproducibles y sin datos privados. No hay licencia de código abierto concedida en esta publicación; la disponibilidad del código no implica permiso general de redistribución o modificación. La licencia queda pendiente de decisión del titular.
