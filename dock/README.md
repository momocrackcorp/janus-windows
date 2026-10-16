# JANUS Dock — complemento para Windows 11

Primera versión nativa experimental, **no sustituye el migrador JANUS**.

Barra lateral derecha integrada con Windows AppBar, que reserva espacio para las ventanas. Permite agregar programas, archivos, carpetas y accesos directos, reorganizarlos, abrirlos o quitarlos del Dock sin eliminarlos del disco. Admite arrastrar archivos desde el Explorador.

## Descarga de prueba
Ejecutar workflow **Build JANUS Dock** en GitHub Actions sobre la rama `feature/janus-dock-v02`, esperar compilación exitosa y descargar el artefacto `JANUS-Dock-Windows-x64`.

## Seguridad
No exige permisos de administrador, no modifica otros archivos JANUS, guarda datos en `%APPDATA%\JANUS\Dock\config.json` y no elimina programas al quitar accesos. Windows SmartScreen puede advertir porque el ejecutable aún no está firmado digitalmente. No desactivar protecciones de Windows: validar el origen antes de usarlo.

**Pendiente de prueba real en Windows 11.** No contiene aún autoocultación, monitor múltiple ni menú avanzado de configuración.
