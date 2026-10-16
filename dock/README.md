# JANUS Dock — complemento para Windows 11

Primera versión nativa experimental, **no sustituye el migrador JANUS**.

Barra lateral derecha integrada con Windows AppBar, que reserva espacio para las ventanas. Permite agregar programas, archivos, carpetas y accesos directos, reorganizarlos, abrirlos o quitarlos del Dock sin eliminarlos del disco. Admite arrastrar archivos desde el Explorador.

## Descarga de prueba
Ejecutar workflow **Build JANUS Dock** en GitHub Actions sobre la rama `feature/janus-dock-v02`, esperar compilación exitosa y descargar el artefacto `JANUS-Dock-Windows-x64`.

## Seguridad
No exige permisos de administrador, no modifica otros archivos JANUS, guarda datos en `%APPDATA%\JANUS\Dock\config.json` y no elimina programas al quitar accesos. Windows SmartScreen puede advertir porque el ejecutable aún no está firmado digitalmente. No desactivar protecciones de Windows: validar el origen antes de usarlo.

**Pendiente de prueba real en Windows 11.** No contiene aún autoocultación, monitor múltiple ni menú avanzado de configuración.

## Apariencia (actualización de octubre de 2026)

- Detecta el tema de aplicaciones claro/oscuro de Windows y lo sigue automáticamente.
- Omite el encabezado JANUS: presenta únicamente accesos y controles compactos al pie.
- Obtiene los iconos desde Windows Shell para programas y accesos `.lnk`.
- Botón de opciones ⚙ → **Mostrar nombres** (desactivado inicialmente) y **Tamaño de iconos** (24, 32, 40, 48, 56 o 64 px).
- Redimensiona el ancho (64 a 300 px) arrastrando el borde izquierdo. El ancho se guarda en `%APPDATA%\JANUS\Dock\config.json`.
- Los nombres completos aparecen en tooltip al pasar el mouse, incluso estando ocultos.

**Estado:** cambios subidos a la rama de pruebas y pendientes de validar compilación/funcionamiento en Windows 11. No es todavía una versión estable.

## Estilo barra de acceso rápido clásica (octubre de 2026)

- Sin botones visibles para Agregar/Opciones/Cerrar. **Clic derecho en el fondo** de la barra para agregar accesos, agregar separadores, abrir opciones o salir. En cualquier icono, clic derecho → Opciones de la barra.
- Iconos sin marcos individuales permanentes: se resalta solamente al pasar el puntero o hacer clic.
- Separadores finos verticales u horizontales según la ubicación elegida; pueden subirse, bajarse o quitarse sin modificar los accesos.
- Color derivado del modo claro/oscuro y del color de acento DWM de Windows, mediante mezcla aproximada; la barra de tareas de Windows 11 usa materiales/efectos que WinForms no reproduce exactamente.
- Conserva los accesos de la configuración anterior. El comportamiento aún requiere compilación y prueba en Windows 11; no se ha validado como versión estable.
