# Migrador Seguro para Windows

Aplicación gráfica para trasladar Escritorio, Documentos, Descargas, Imágenes, Música y Vídeos a otra unidad sin arriesgar los archivos originales.

## Funciones

- Detecta las unidades disponibles y muestra capacidad usada/libre en un gráfico.
- Calcula el tamaño de cada carpeta y el espacio total necesario.
- Permite elegir carpetas individualmente y previsualiza origen → destino.
- Bloquea la migración si falta espacio o el destino ya contiene archivos.
- Respalda y restaura las rutas `Known Folders` y `User Shell Folders`.
- Reinicia el Explorador de Windows después de aplicar o restaurar cambios.

## Seguridad

- Copia y verifica antes de cambiar las rutas de Windows.
- Conserva los originales; nunca borra automáticamente.
- No sobrescribe: si el destino contiene archivos, bloquea la operación.
- Bloquea raíces de disco, AppData, Windows, ProgramData y Program Files.
- Comprueba el espacio libre y mantiene 100 MB de margen.
- Respalda `Known Folders/User Shell Folders` en JSON antes del cambio.
- Si falla un cambio del registro, revierte los cambios ya realizados.

## Descargar

El ejecutable de Windows se publica en la sección **Releases** del repositorio. Windows SmartScreen puede advertir que el archivo no tiene firma digital; el hash SHA-256 se incluye en cada versión.

## Ejecutar desde el código fuente

Requiere Windows y Python 3.11 o superior (Python incluye Tkinter):

```powershell
py -3 run.py
```

No requiere permisos de administrador porque solo modifica las rutas del usuario actual.

## Compilar el ejecutable

Haz clic derecho en `build.ps1`, elige **Ejecutar con PowerShell**, o ejecútalo desde una terminal. Crea `dist\MigradorSeguro.exe` usando PyInstaller. La descarga de PyInstaller solo es necesaria durante la compilación.

## Restauración

Pulsa **Restaurar desde respaldo…** y selecciona el JSON guardado en `Documentos\Respaldos Migrador Seguro`. La restauración solo repone las rutas del registro; nunca mueve ni borra datos.

## Diseño de recuperación

Después de verificar durante varios días que todo funciona, el usuario puede archivar o borrar manualmente las carpetas originales. La aplicación no ofrece borrado para evitar pérdida accidental.

## Pruebas

```powershell
py -3 -m unittest discover -s tests -v
```

Las pruebas no modifican el registro ni las carpetas personales reales.
