# Migrador Seguro para Windows

Aplicación gráfica para trasladar Escritorio, Documentos, Descargas, Imágenes, Música y Vídeos a otra unidad sin arriesgar los archivos originales.

## Funciones

- Detecta las unidades disponibles y muestra capacidad usada/libre en un gráfico.
- Calcula el tamaño de cada carpeta y el espacio total necesario.
- Permite elegir carpetas individualmente y previsualiza origen → destino.
- Bloquea la migración si falta espacio o el destino ya contiene archivos.
- Respalda y restaura las rutas `Known Folders` y `User Shell Folders`.
- Registra cada destino mediante la API oficial `SHSetKnownFolderPath` para conservar la identidad y los iconos de Windows.
- Incluye una reparación de rutas e iconos para migraciones ya realizadas.
- Incluye icono propio de la aplicación y una pantalla animada **Acerca de**.
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

La versión distribuida está escrita en C# y usa Windows Forms. No necesita dependencias externas ni permisos de administrador porque solo modifica las rutas del usuario actual.

## Compilar el ejecutable

Ejecuta `build-native.ps1`. Utiliza el compilador de .NET Framework incluido con Windows y crea `dist\MigradorSeguro.exe`, sin descargar dependencias.

## Restauración

Pulsa **Restaurar desde respaldo…** y selecciona el JSON guardado en `Documentos\Respaldos Migrador Seguro`. La restauración solo repone las rutas del registro; nunca mueve ni borra datos.

Si las carpetas ya fueron migradas pero aparecen con iconos genéricos, pulsa **Restaurar / reparar rutas…** y elige **Sí**. La herramienta volverá a registrar las carpetas conocidas y reiniciará el Explorador sin copiar ni borrar datos.

## Diseño de recuperación

Después de verificar durante varios días que todo funciona, el usuario puede archivar o borrar manualmente las carpetas originales. La aplicación no ofrece borrado para evitar pérdida accidental.

## Pruebas

La validación del ejecutable comprueba que la interfaz nativa inicia y permanece estable. Las operaciones reales solo comienzan después de seleccionar destino y aceptar la confirmación final.
