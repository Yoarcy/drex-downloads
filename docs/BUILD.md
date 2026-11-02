# Compilar y probar

## Aplicación

En Windows de 64 bits con .NET Framework 4.8, desde la raíz:

```powershell
.\src\build.ps1
```

Produce `DrexDownloads.exe` con el icono de `assets/`. Si la política de ejecución bloquea scripts, utiliza el procedimiento aprobado por el administrador del equipo. No hace falta restaurar paquetes NuGet.

## Distribución e instalador

```powershell
.\src\package.ps1
```

Compila la app, crea un payload limpio, compila el instalador y genera ZIP y SHA256SUMS en `dist/`. No recoge carpetas de datos, parciales o backups. El instalador exige destino vacío y no activa el supervisor de red.

## Pruebas del motor

Necesitan Python 3 en PATH además de .NET:

```powershell
.\tests\run-tests.ps1
```

Usan servidor HTTP local, archivos sintéticos y carpetas aisladas `tests/run-*`. Incluyen interrupción de procesos de prueba y corrupción deliberada de fixtures. No se deben apuntar a descargas reales.

## Portabilidad del componente de red

```powershell
.\tests\network-portability.ps1
```

Pruebas simuladas de rutas opcionales y detección WSL; no modifican firewall.
