# Paquetes de temas externos

Los temas visuales no se incrustan en `Janus.exe`. Cada subcarpeta de este
directorio es una fuente de empaquetado independiente y su ZIP se publica como
archivo adjunto de una versión de GitHub.

Cada paquete debe contener estos 14 nombres, tanto en PNG como en ICO:

`este-equipo`, `archivos-usuario`, `red`, `papelera-vacia`, `papelera-llena`,
`documentos`, `descargas`, `escritorio`, `imagenes`, `musica`, `videos`,
`hdd-ssd`, `usb` y `unidad-red`.

También debe conservar los archivos de licencia y atribución exigidos por el
proyecto del cual proceden los iconos. Los diseños de terceros no forman parte
del ejecutable ni cambian la licencia del código de JANUS.

Ejemplo:

```powershell
.\tools\build-theme-package.ps1 -Theme papirus
```
