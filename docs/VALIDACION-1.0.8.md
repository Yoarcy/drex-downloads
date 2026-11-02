# Validación 1.0.8

- 43/43 comprobaciones aprobadas; detalle en `tests-1.0.8.json`.
- Incluyen 4 flujos simultáneos, cero flujos al pausar, recuperación de parciales, identidad/hash, renovación tras HTTP 403 y espera tras cinco fallos de red.
- Desplegado en la PC principal Windows, conservando la misma tarea y sus parciales.
- Progreso confirmado al cierre: 4 786 233 496 bytes. Tras despliegue: más de 5 015 307 387 bytes.
- Muestra de 19,996 s: 368,9 KiB/s de tráfico recibido; 4/4 conexiones activas, límites por tarea/global en cero, modo exclusivo activo. Una muestra no demuestra una mejora causal ni garantiza velocidad futura.
- El origen estable se vuelve a resolver al reanudar; no se almacena una URL firmada en esta evidencia.
- Inicio condicional registrado y marcador presente mientras descarga. El último reinicio observado precedía al despliegue de la recuperación automática. No se ha realizado una prueba física posterior de reinicio/apagón.
- Segunda PC: pruebas detenidas por petición del usuario; las realizará personalmente. No se afirma validación de descarga allí.
- El ZIP contiene exactamente el ejecutable desplegado: SHA-256 `9ad956cc27fe1db7a4537569db056f525952e706c3cd412dc462aff9f2dc32c1`.

## Uso sin vigilancia

La aplicación gestiona los cortes transitorios de red y renueva enlaces recuperables desde el origen. No depende de un agente. Debe permanecer ejecutándose (cerrar la ventana la deja en bandeja), Windows no debe suspenderse y debe haber espacio en disco. No se promete que nunca se detenga: autorización denegada permanentemente, archivo remoto cambiado, error de disco o hash incorrecto requieren atención para proteger el progreso.
