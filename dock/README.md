# JANUS Dock 5.0 — candidata pública

JANUS Dock es una barra independiente para Windows 10/11 que organiza accesos a programas, documentos, carpetas y unidades. Puede colocarse a los lados o arriba/abajo, con iconos de Windows o trazos tipo Lucide, e indicadores circulares por disco.

**Publicación:** el repositorio es público, pero la versión sigue siendo **candidata en pruebas**, no una versión estable. Los últimos binarios se descargan desde [Build JANUS Dock](https://github.com/momocrackcorp/janus-windows/actions/workflows/build-janus-dock.yml) → ejecución verde → **Artifacts**. Los ZIP de GitHub Actions pueden requerir iniciar sesión en GitHub.

## Distribución de botones de energía
Los botones Apagar y Reiniciar, cuando estén activados en General, se muestran en un grupo independiente: **derecha** para barra horizontal y **abajo** para barra vertical. Por seguridad, ambas acciones piden confirmación. Sus imágenes PNG se mantienen para el estilo Windows; el estilo Lucide usa iconos de trazo.

## Organización y privacidad
Opciones → Apariencia permite elegir estilo, reordenar accesos con arrastrar y soltar o botones y manejar separadores. Guardar aplica los cambios; Cancelar los descarta. La configuración se guarda en `%APPDATA%\JANUS\Dock\config.json`. El Dock no necesita JANUS Plus para funcionar.

## Pruebas solicitadas
Comprobar barra derecha/izquierda/arriba/abajo, energía separada, animación, escala de Windows 100/200 %, autoocultación y persistencia de configuración. Se publica el código fuente para evaluación; no se garantiza todavía la estabilidad en todas las configuraciones.

---

# JANUS Dock — barra de accesos rápidos para Windows 10/11

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

## Nueva tanda de ajustes (pendiente de pruebas)

- Ventana General: modo **Acoplada / Flotante**, iconos desde **16 px**, y tres formas de nombres (**Solo iconos**, **Al pasar el mouse**, **Iconos con nombres**).
- Menú contextual **Refrescar barra**: vuelve a resolver iconos/rutas, refresca indicadores de discos y solicita recalcular espacio/posición sin reiniciar Explorer.
- Modo flotante: ventana movible y redimensionable, sin registro AppBar. El modo acoplado sigue usando la lógica de bordes existente.
- El modo de nombres y el tipo de barra se almacenan en el JSON del usuario.

Estos cambios están en la rama de pruebas; **sin validación de compilación ni uso real en Windows 11**. Continuar revisando colocación, escalado, pantalla inferior y comportamiento de ocultación automática antes de declarar una release 2.1.

## Objetivo JANUS Dock 2.6 — cierre de calidad

Versión de trabajo: **2.6.0-preview**. No es una versión final certificada hasta pasar compilación y pruebas en Windows 11. El número de ejecución de GitHub Actions (#20, etc.) no es el número de versión.

- Mantener la apariencia de barra de acceso rápido tradicional (sin bordes ni encabezado en modo flotante).
- Verificar el icono de **Este equipo** y resolver los iconos de accesos .lnk sin flecha, sin alterar los accesos originales del escritorio.
- Centrar widgets de discos y permitir asignar un color distinto a cada unidad (pendiente de implementación).
- Corregir posicionamiento, reserva de área del escritorio y refresco sin tapar la barra de tareas.
- Finalizar menú contextual, separadores, tres modos de nombres y escalado compacto.
- La pestaña Acerca de identifica **JANUS Dock (LNK)**, idea desarrollada en **Pueblo Seco, Ñuble, Chile**, fecha **16 de octubre de 2026**, autoría **Omar Aguila / Laboratorios Momocrackcorp**.
- El proyecto liviano depende de **.NET Desktop Runtime 8**; para distribución pública se evaluará una versión LTS con soporte vigente.
- La versión final requiere validación en las cuatro ubicaciones, modo flotante y varios factores de escalado antes de integrar con JANUS Plus.

## 3.3 — nueva tanda de pruebas (16 de octubre de 2026)

- Color **independiente por unidad** (configurado por letra, guardado en `DiskColors` en JSON). Se usa tanto en el panel Discos como en los indicadores de la barra.
- Panel Discos con lista desplazable, botones individuales de color, porcentaje crítico separado y sin campos montados.
- Casilla para animar el icono bajo el mouse mediante ampliación de la imagen dentro de su espacio, sin alterar el área reservada para el escritorio; desactivable.
- Clic derecho: carpetas especiales Documentos y Descargas, selector de carpeta personalizada y acciones Apagar/Reiniciar. **Las acciones de energía siempre solicitan confirmación**.
- El contenido anterior de accesos y separadores se preserva; los campos nuevos del JSON usan valores predeterminados.

**Pendiente de validar**: GitHub Actions, escalado de pantalla, acceso a carpetas redirigidas, iconos de energía, colocación AppBar y animación en diferentes tamaños. La denominación 3.3 sigue siendo *preview* hasta completar las pruebas en Windows 10/11.
