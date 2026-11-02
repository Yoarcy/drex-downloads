# Internet exclusivo — componente opcional experimental

La aplicación escribe un heartbeat local. Un supervisor PowerShell instalado como tarea de Windows aplica reglas de firewall; no invoca IA ni consume créditos de Drex.

## Instalación

Desde PowerShell **como administrador**, en la carpeta instalada, ejecuta `network/install.ps1` conforme a la política de scripts de tu equipo. Registra la tarea `Drex Downloads - Network Guard` con cuenta SYSTEM al inicio de Windows. El script actual configura la ejecución del supervisor con `-ExecutionPolicy Bypass` para ese proceso; no cambia la política global.

Activa «Internet exclusivo» en la aplicación. Si no existe el supervisor se informa que no está disponible; el descargador normal sigue siendo independiente.

## Comportamiento

Bloqueo de salida por defecto, descargador permitido, Chrome si está en las rutas estándar, DNS/DHCP y transporte WSL si se detecta activo. No requiere OpenClaw instalado. Los procesos nuevos también están sujetos a la política de salida; no es una barrera frente a administradores o software que cambie las reglas.

La excepción WSL/System es amplia y no aísla un proceso Linux concreto. No es una herramienta de seguridad certificada ni garantiza aumentar la velocidad. La detección de Chrome no cubre todas las instalaciones por usuario.

El supervisor guarda snapshot y desactiva temporalmente reglas de permiso de salida. Restaura al pausar todas las descargas, terminar, desmarcar el modo o caducar el heartbeat. El umbral de heartbeat es 15 segundos más el tiempo de comprobación/aplicación. No elimines `network/snapshot.json` mientras exista una restricción activa.

## Precauciones operativas

Desmarca el modo y espera estado Normal antes de mover o desinstalar. Instala en una ruta con permisos adecuados: el supervisor corre como SYSTEM y sus scripts no deben quedar modificables por usuarios no confiables. No desplegar sin revisión adicional en equipos multiusuario o administrados. No se ha realizado auditoría independiente.

Pruebas anteriores comprobaron bloqueo/restauración y continuidad de descarga. La revisión de detección opcional tiene sintaxis validada; sus cinco casos simulados aún requieren ejecución. No se ha probado un apagón físico ni una segunda PC completa.
