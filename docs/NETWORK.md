# Internet exclusivo integrado · 1.0.6

El componente está incrustado en DrexDownloads.exe. No requiere scripts externos, OpenClaw, IA ni instalación aparte. Al marcar Internet exclusivo, Windows puede solicitar permiso de administrador. Si se cancela, el modo no se activa y las descargas normales siguen disponibles.

## LAN preservada

Las reglas permiten salida para todas las aplicaciones a LocalSubnet, redes privadas IPv4 10/8, 172.16/12 y 192.168/16, link-local IPv4/IPv6, ULA IPv6 y multicast de descubrimiento local. Esto incluye procesos nuevos, RDP y SMB. Redes enrutadas con direcciones públicas fuera de LocalSubnet no se clasifican automáticamente como LAN. Se conservan los bloqueos preexistentes: no se anulan restricciones impuestas por el administrador. Un proxy en la LAN podría dar acceso a Internet; el modo no es una frontera de seguridad.

La salida a Internet queda limitada al descargador y las excepciones disponibles (Chrome, DNS/DHCP y transporte WSL). WSL/System es una excepción amplia: no hay aislamiento por proceso Linux. No garantiza aumentar la velocidad.

## Ciclo de vida y recuperación

Un auxiliar elevado ejecuta el supervisor incluido en el recurso del ejecutable; no lee código PowerShell de archivos de la instalación. La app envía heartbeat cada segundo. Al desmarcar, pausar todas o terminar se restaura el snapshot. Si deja de llegar heartbeat, se restaura; el auxiliar termina tras unos 20 segundos sin heartbeat. Los intervalos no son garantías de tiempo real.

El propio programa crea automáticamente un auxiliar de recuperación en Program Files/DrexDownloads-Recovery y la tarea de arranque `Drex Downloads - Integrated Recovery` (SYSTEM), para restaurar la configuración tras reinicio sin depender de que el usuario abra la app. No se activa Internet exclusivo al arrancar Windows: la tarea solo restaura. La tarea antigua del supervisor se retira automáticamente si pertenece a esta instalación y está inactiva.

No borres `network/snapshot.json` mientras exista una restricción. Desactiva el modo y espera Normal antes de mover o desinstalar. Mantén la carpeta original accesible para recuperar el snapshot. No hay desinstalador automatizado en esta versión. Si deseas retirar completamente la recuperación, con el modo en Normal un administrador puede quitar la tarea y su carpeta protegida.

## Validación

Cinco pruebas simuladas de portabilidad aprobadas. Prueba real con modo activo: TCP LAN 3389 (RDP) y 445 (SMB) accesibles, PowerShell bloqueado al acceder a HTTPS público y descarga progresando. Al desactivar: estado Normal, snapshot retirado y HTTPS200. No se probó un apagón físico ni la instalación completa en otra PC.
