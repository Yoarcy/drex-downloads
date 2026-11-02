# Drex Downloads 1.0 — Implementación entregable

Aplicación Windows Forms en C# sobre .NET Framework 4.8 instalado en el equipo. Ejecutable local, sin servidor ni acceso de red de entrada; no requiere Python en uso normal. Python solo interviene en las pruebas. Código fuente incluido. Motor HTTP construido sobre HttpWebRequest de Windows/.NET, no aria2. Se cambió el candidato del estudio para controlar el orden de confirmación de datos y metadatos.

## Checkpoint

Cada conexión retiene como máximo 4 MiB en RAM. Cada 5 segundos durante lectura o limitación de velocidad, o al llenar el bloque, el hilo publica lo recibido bajo un bloqueo del escritor. Secuencia:

1. Escribir bytes en su posición del archivo parcial.
2. FileStream.Flush(true) en ese archivo.
3. Añadir registro de offset, longitud y SHA-256 local a ledger.a; Flush(true).
4. Añadir el mismo registro a ledger.b; Flush(true).
5. Mostrar los bytes como confirmados.

Cada registro está envuelto en su propia comprobación SHA-256. Al cargar, se lee la unión de registros válidos de ambos diarios; se comprueba el contenido del parcial antes de reutilizar cada intervalo. Fragmentos corruptos o no registrados se vuelven a solicitar mediante rangos, siempre condicionados a la identidad remota. Se normalizan los dos diarios uno a uno; una escritura interrumpida no requiere destruir primero la otra copia.

Los metadatos de cada tarea alternan entre dos generaciones verificables con sustitución de archivo. El identificador propio de la tarea y el estado de pausa sobreviven al proceso. La publicación del archivo final se hace con un movimiento sin sobrescritura dentro del destino. Se guarda antes el hash calculado; si la aplicación se interrumpe después del movimiento, puede reconciliar el archivo final al volver a abrir.

## Límites concretos

No garantiza un tiempo máximo de pérdida de cinco segundos: bloqueos de disco, cachés del hardware o corrupción física pueden superar las garantías del software. Las pruebas de terminación del proceso y alteración deliberada de archivos no equivalen a probar un corte de energía real.

Las huellas locales detectan cambios de los bytes guardados, no verifican su procedencia. El SHA-256 esperado, cuando el usuario lo facilita, permite comparación con una referencia externa. No se presupone que un ETag sea un SHA-256.

La recuperación requiere identidad fuerte coincidente o hash esperado, tamaño compatible y soporte real de Range. Si no se cumplen, el programa bloquea la tarea sin borrar el parcial. El servidor también puede fallar o incumplir el protocolo.

## Dependencias y distribución

No modifica IDM, el navegador, WanGP, OpenClaw ni sus entornos. No hay captura automática, extensión, extracción de vídeos ni importación de temporales de IDM. Arranque con Windows opcional desde la bandeja; no se activa silenciosamente. El ejecutable no está firmado comercialmente.

No se ha implementado toda la lista de ampliaciones P1/P2 del estudio. Se entrega el núcleo cotidiano de archivos HTTP/HTTPS, cola, pausa, recuperación, renovación manual, fijado de revisión Hugging Face cuando esté disponible, límites de velocidad y verificación.
