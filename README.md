# Janus para Windows

Aplicación gráfica para trasladar Escritorio, Documentos, Descargas, Imágenes, Música y Vídeos a otra unidad sin arriesgar los archivos originales.

## ¿Por qué Janus?

La aplicación se llamó **Migrador Seguro** hasta la versión 1.x. Desde la versión 2.0 adopta el nombre **Janus** para representar mejor su propósito.

**Jano (Janus)** pertenece a la mitología romana. Es el dios de las **transiciones, los comienzos, los finales, las puertas y los cambios**. Sus dos rostros miran simultáneamente al pasado y al futuro.

La imagen encaja naturalmente con esta herramienta: acompaña una transición segura desde el sistema y las ubicaciones anteriores hacia una nueva organización, conservando la posibilidad de comprobar, respaldar y restaurar. En pocas palabras: **sistema viejo → sistema nuevo**.

El cambio de nombre no altera el objetivo ni las protecciones del programa. Janus continúa copiando y verificando los archivos antes de redirigir las carpetas conocidas de Windows, sin borrar automáticamente los originales.

## Funciones

- Detecta las unidades disponibles y muestra capacidad usada/libre en un gráfico.
- Calcula el tamaño de cada carpeta y el espacio total necesario.
- Permite elegir carpetas individualmente y previsualiza origen → destino.
- Bloquea la migración si falta espacio o el destino ya contiene archivos.
- Respalda y restaura las rutas `Known Folders` y `User Shell Folders`.
- Registra cada destino mediante la API oficial `SHSetKnownFolderPath` para conservar la identidad y los iconos de Windows.
- Incluye una reparación de rutas e iconos para migraciones ya realizadas.
- Incluye icono propio de la aplicación y una pantalla animada **Acerca de**.
- Muestra una pantalla de inicio de Janus durante la carga de la aplicación.
- Permite crear una carpeta contenedora directamente desde el selector de destino.
- Aplica un icono celeste de documentos a la unidad destino para el usuario actual, sin escribir `autorun.inf` en la raíz.
- El icono de la unidad usa transparencia real y se adapta a temas claros u oscuros.
- Muestra un gráfico de dona multicolor con la distribución de tamaños de las carpetas seleccionadas.
- La leyenda del gráfico reserva espacio completo para nombres y porcentajes, incluso con escalado alto de Windows.
- Muestra barra de progreso por bytes, porcentaje, tiempo transcurrido, tiempo restante estimado y archivos pendientes.
- Al finalizar muestra un resumen guardable con acciones, archivos copiados, idénticos, conflictos, volumen y tiempo total.
- La pantalla **Acerca de** informa versión, fecha, tecnología, autoría y procedencia; la animación arcade permanece como huevo de pascua.
- **Acerca de** utiliza el icono PNG de alta resolución y reserva un bloque completo para la autoría y ubicación.
- Incluye un panel ordenado de herramientas para Winver, MSInfo32, DxDiag, Terminal, SystemInfo, Modo Dios y configuración reversible de avisos UAC.
- Desde **Herramientas de Windows** permite activar la zona horaria automática, forzar la sincronización inmediata mediante el servicio oficial de hora y abrir el panel de Windows para ajustar el reloj.
- Desde el mismo panel permite mostrar u ocultar en el Escritorio **Equipo**, **Archivos del usuario**, **Red**, **Papelera de reciclaje** y **Panel de control**, respetando la selección actual de Windows.
- Integra accesos oficiales a VLC, Codec Guide, WinRAR, USB Image Tool, Adobe Reader y Microsoft PC Manager, junto a una fotografía monocromática integrada sin marco.
- Incluye una sección de descarga para Google Chrome, Mozilla Firefox, Brave, Opera y Comet.
- El botón **Desactivar OneDrive** aplica, con autorización de administrador, la directiva oficial `DisableFileSyncNGSC` que bloquea su sincronización; además cierra el cliente, retira su icono de la bandeja y elimina su inicio automático con respaldo. **Restaurar OneDrive** retira la directiva y repone el inicio. No desinstala, desvincula ni borra archivos.
- La configuración de UAC se realiza únicamente desde el panel oficial de Windows; la aplicación no altera directamente sus políticas.
- Reinicia el Explorador de Windows después de aplicar o restaurar cambios.
- Usa un icono de Janus claro y multirresolución para conservar legibilidad en el Explorador, accesos directos y ventanas de Windows.

## Seguridad

- Copia y verifica antes de cambiar las rutas de Windows.
- Conserva los originales; nunca borra automáticamente.
- Fusiona destinos existentes sin sobrescribir: omite archivos idénticos y conserva conflictos con un nombre nuevo.
- Bloquea raíces de disco, AppData, Windows, ProgramData y Program Files.
- Comprueba el espacio libre y mantiene 100 MB de margen.
- Respalda `Known Folders/User Shell Folders` en JSON antes del cambio.
- Si falla un cambio del registro, revierte los cambios ya realizados.

## Descargar

El ejecutable de Windows se publica en la sección **Releases** del repositorio. Windows SmartScreen puede advertir que el archivo no tiene firma digital; el hash SHA-256 se incluye en cada versión.

### Aviso de SmartScreen

El proyecto publica actualmente binarios sin certificado Authenticode comercial. En Windows puede ser necesario pulsar **Más información → Ejecutar de todas formas**. Para distribución sin ese aviso se requiere un certificado OV/EV de firma de código, firma con sello de tiempo y reputación de Microsoft SmartScreen. Una firma autofirmada no elimina el aviso en equipos ajenos.

## Ejecutar desde el código fuente

La versión distribuida está escrita en C# y usa Windows Forms. No necesita dependencias externas ni permisos de administrador porque solo modifica las rutas del usuario actual.

## Compilar el ejecutable

Ejecuta `build-native.ps1`. Utiliza el compilador de .NET Framework incluido con Windows y crea `dist\Janus.exe`, sin descargar dependencias.

## Restauración

Pulsa **Restaurar desde respaldo…** y selecciona el JSON guardado en `Documentos\Respaldos Migrador Seguro`. La restauración solo repone las rutas del registro; nunca mueve ni borra datos.

Si las carpetas ya fueron migradas pero aparecen con iconos genéricos, pulsa **Restaurar / reparar rutas…** y elige **Sí**. La herramienta volverá a registrar las carpetas conocidas y reiniciará el Explorador sin copiar ni borrar datos.

## Diseño de recuperación

Después de verificar durante varios días que todo funciona, el usuario puede archivar o borrar manualmente las carpetas originales. La aplicación no ofrece borrado para evitar pérdida accidental.

## Pruebas

La validación del ejecutable comprueba que la interfaz nativa inicia y permanece estable. Las operaciones reales solo comienzan después de seleccionar destino y aceptar la confirmación final.
