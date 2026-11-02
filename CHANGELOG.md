## 1.0.8

- Contador de transferencias activas/configuradas en detalles y estado de la descarga.
- Diagnóstico local cada cinco segundos sin URLs ni credenciales: `data/runtime-status.json`.
- «Renovar enlace» pausa de forma segura y reabre el origen estable guardado; pide otro enlace solo si el guardado es temporal.
- Cuatro conexiones por defecto en descargas nuevas; las existentes conservan su configuración.
- Prueba de cuatro transferencias simultáneas y contador a cero tras pausa.
- Mantiene recuperación condicional al iniciar sesión y reintentos/renovación autónomos de 1.0.7.

# Cambios

## 1.0.7
- Inicio condicional del usuario tras interrupción durante descargas; pausas, cierre normal y finalización respetados.
- Renovación local desde enlace de origen, denegaciones acotadas y espera de red persistente.
- 41 pruebas aprobadas; metadatos y bloques conservados.

## 1.0.6
- La LAN queda fuera de la restricción de Internet: RDP, SMB, redes privadas y descubrimiento local.
- Supervisor incrustado en el ejecutable, activación elevada desde la app y recuperación al inicio preparada automáticamente.
- Eliminada la instalación manual de scripts.
- Cinco casos de portabilidad, LAN, bloqueo HTTPS y restauración verificados.

## 1.0.5
- Icono original multirresolución integrado en app e instalador.
- Barra de título con textura y colores de la cabecera.
- Instalador local para carpeta vacía y distribución portable.
- Documentación de limitaciones, privacidad y construcción.

## 1.0.4
- Modo Internet exclusivo con supervisor local y restauración.
- Revisión posterior: excepciones de software opcional condicionadas a presencia/actividad; validación completa pendiente.

## 1.0.1–1.0.3
- Mejoras de recuperación, solicitudes por rangos acotados, archivos dispersos y presentación.

## 1.0.0
- Motor HTTP propio, doble diario, cola, pausa/reanudación y verificación.
