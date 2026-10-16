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

## Corrección de barra inferior y accesos a discos

- La ubicación **Abajo** ahora utiliza una franja baja (48–100 px) limitada a la zona de trabajo **por encima de la barra de tareas**, en lugar de registrar otra AppBar sobre el borde inferior de Windows. Así no debería tapar Inicio ni el área de notificaciones. En este modo la barra no reserva espacio adicional en Windows; puede superponerse a ventanas maximizadas y habrá que afinarlo en pruebas posteriores.
- El menú contextual incluye **Agregar Este equipo** (abre todas las unidades en el Explorador) y **Agregar unidad de disco**, enumerando C:, D: y cualquier unidad detectada con su nombre. Los iconos se obtienen desde Windows Shell.
- Si una versión anterior se queda cubriendo la barra de tareas, vuelve temporalmente a Derecha y cierra el Dock antes de lanzar la nueva compilación.

**Pendiente de prueba en Windows 11 y comprobación de compilación GitHub Actions.**

## Ventana de opciones

Clic derecho sobre la barra o en un icono → **Opciones**. Se abre una ventana con tres pestañas:

- **General**: Siempre visible, ocultar automáticamente, ejecutar al inicio, mostrar nombres, ubicación y tamaño de iconos.
- **Discos**: vista previa del uso de las unidades (indicador circular de ocupación y GB usados/libres), y casilla para incorporar indicadores a la barra.
- **Acerca de**: finalidad del proyecto y acceso a las compilaciones para comprobar actualizaciones. No instala actualizaciones silenciosamente.

Al pulsar **Guardar** se conservan las rutas actuales en JSON, y la opción de inicio crea o elimina únicamente el valor `JANUS Dock` en la clave `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run`. El ocultamiento conserva una franja de aproximadamente seis píxeles como zona de recuperación.

**Pendiente:** validar compilación de GitHub Actions y probar en Windows 11 los cambios de AppBar, especialmente con autoocultación, ubicación inferior y varios monitores. No distribuir como versión estable hasta completar estas pruebas.

## Plan de versión 2.1 (en desarrollo)

Versionado acordado: **10 modificaciones funcionales revisadas por versión publicada**. Los commits de corrección internos no cuentan automáticamente como versiones. Cada publicación requiere compilación exitosa en GitHub Actions y una prueba real en Windows 11, y se documentará con un changelog.

Lote de cambios previstos para 2.1:

1. Corregir solapamiento del selector de tamaño de iconos.
2. Mantener Guardar/Cancelar visibles en el cuadro de opciones.
3. Mostrar la letra C:, D:, etc. centrada en cada gráfico de disco.
4. Elegir el color de las donas desde la pestaña Discos.
5. Reutilizar el mismo color en los widgets de la barra.
6. Ajustar el porcentaje crítico de ocupación.
7. Mostrar exclamación pequeña en discos con ocupación crítica.
8. Integrar el bloque de discos en el mismo orden que accesos y separadores.
9. Mover el bloque de discos hacia arriba/abajo desde su menú contextual.
10. Permitir insertar y desplazar separadores entre el bloque de discos y los accesos.

**Estado:** cambios implementados en código en la rama de desarrollo. Quedan pendientes la compilación Windows, revisión de diseño con escalado de pantalla y prueba con datos reales antes de publicar la versión 2.1.
