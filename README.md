# FrameLab

Optimizador de Windows para juegos, en espaÃ±ol. Es portable: un solo `.exe`, sin instalaciÃ³n.

> Estado: beta privada (0.6.1). Probado en Windows 11. En Windows 10 22H2 se probÃ³ el diagnÃ³stico, que no cambia nada.

## CÃ³mo se usa, en 3 pasos

1. **DescargÃ¡ `FrameLab.exe`** desde [Releases](../../releases), a la derecha de esta pÃ¡gina. No se instala.
2. **Abrilo con doble clic** y aceptÃ¡ el permiso de administrador. Si Windows muestra "Windows protegiÃ³ su PC", tocÃ¡ **MÃ¡s informaciÃ³n** y despuÃ©s **Ejecutar de todas formas**. Aparece porque la app todavÃ­a no tiene firma digital.
3. **TocÃ¡ el botÃ³n Optimizar** y reiniciÃ¡ la PC.

Si algo no te gusta, abrÃ­ la app y tocÃ¡ **Deshacer todo**: todo vuelve a como estaba.

En la misma pantalla, **QuÃ© frena tu PC** te dice quÃ© es lo que mÃ¡s limita tus juegos. Si querÃ©s ajustar tus juegos, entrÃ¡ a **Juegos**. El resto de las herramientas estÃ¡ en **Opciones avanzadas**, abajo a la izquierda.

## QuÃ© hace

- **Optimizar (modo simple):** un botÃ³n que crea un punto de restauraciÃ³n y aplica solo los ajustes seguros que sirven en tu PC. Otro botÃ³n, **Deshacer todo**, los revierte.
- **QuÃ© frena tu PC:** ordena lo que mÃ¡s limita los juegos (placa de video, disco, RAM, procesador, drivers, apagados, programas al iniciar) y avisa cuÃ¡ndo los ajustes de Windows van a ayudar poco.
- **DiagnÃ³stico:** hardware, XMP, canales de RAM, discos, Hz del monitor, PCIe, BIOS, drivers y apagados inesperados.
- **Optimizaciones:** 98 ajustes de Windows en tres perfiles (Seguro, Gamer y Avanzado). Cada uno muestra quÃ© cambia, su impacto, su riesgo y si ya estÃ¡ aplicado.
- **Juegos:** ajusta Fortnite, Roblox y Minecraft (Java y Bedrock) usando solo sus archivos de configuraciÃ³n oficiales, con copia previa. Para Valorant y CS2 da una guÃ­a para ajustarlos a mano, porque tienen antitrampas.
- **Modo partida:** antes de jugar cierra los programas que elijas y le da prioridad al juego. Al terminar, todo vuelve a como estaba y los programas se vuelven a abrir.
- **PersonalizaciÃ³n:** fondo propio de FrameLab para la app y para el escritorio, con botÃ³n para volver al fondo anterior.
- **Medir partida:** FPS, mÃ­nimos del 1 % y del 0,1 % y tirones con PresentMon, para comparar antes y despuÃ©s.
- **Espacio en disco, Limpieza, Programas al iniciar, Red e Instalar:** liberar espacio sin riesgo, elegir quÃ© arranca con Windows, elegir el DNS e instalar programas y drivers de fuentes oficiales.

## CÃ³mo cuida tu PC

- Antes de aplicar cambios crea un punto de restauraciÃ³n.
- Guarda el valor anterior de cada ajuste y deja revertirlos de a uno o todos juntos.
- Si un ajuste falla a mitad de camino, se deshace solo.
- No incluye nada que baje la seguridad o pueda causar baneos: no apaga Defender ni las mitigaciones de CPU, no desactiva las actualizaciones ni el archivo de paginaciÃ³n, no usa limpiadores de RAM y no toca la memoria ni los archivos de juegos con antitrampas.
- No promete "+200 % FPS": lo que mejora se mide en Medir partida.

## Requisitos

- **Windows 11**, o **Windows 10 versiÃ³n 2004 o mÃ¡s nuevo** (compilaciÃ³n 19041 a 19045). No se probÃ³ en versiones anteriores.
- **.NET Framework 4.8**, que ya viene con Windows 10 1903 o mÃ¡s nuevo y con Windows 11.
- **winget (App Installer)**, solo para la secciÃ³n Instalar. Si falta, la app avisa y abre su pÃ¡gina en Microsoft Store.

En Windows 10, los ajustes exclusivos de Windows 11 aparecen en gris con la etiqueta "Solo Windows 11". El campo `windows` de `catalogo.json` indica dÃ³nde aplica cada ajuste (`10,11` o `11`).

---

## Para desarrolladores

Lo que sigue es para quien quiera modificar la app. Para usarla alcanza con los 3 pasos de arriba.

### Compilar

```powershell
.\compilar.ps1
```

Genera `dist\FrameLab.exe`. Con `-Prueba` genera una copia que no pide administrador y sirve para revisar otra PC sin cambiar nada: `FrameLab-diagnostico.exe --prueba <carpeta>` guarda capturas de cada secciÃ³n y un `estado.txt`.

Usa el compilador de .NET Framework que ya trae Windows. No hace falta instalar nada.

### Archivos

- `catalogo.json`: ajustes, con impacto, riesgo y perfiles.
- `src\Motor.cs`: detectar, aplicar y revertir.
- `src\Salud.cs`: revisiÃ³n de hardware y lÃ­mite de FPS.
- `src\Medicion.cs`: mediciÃ³n con PresentMon y anÃ¡lisis.
- `src\Extras.cs`: juegos (Fortnite, Roblox y Minecraft), limpieza, apps y actualizaciones.
- `src\Partida.cs`: modo partida. `src\Fondo.cs`: fondo de FrameLab.
- `src\Frenos.cs`: "QuÃ© frena tu PC", la tabla de gamas de placas de video y el asistente de espacio.
- `src\Ventana.cs`: interfaz. `src\Tema.xaml`: colores y estilos.
- `crear_logo.ps1`: genera el logo.

### Datos que guarda

En `%LOCALAPPDATA%\FrameLab`: valores anteriores de cada ajuste (`respaldo.json`), mediciones, copias de archivos de juegos, PresentMon, preferencias (`preferencias.txt`) y errores imprevistos (`errores.log`).

### QuÃ© no incluye a propÃ³sito

Desactivar mitigaciones de CPU, Defender, SmartScreen o las actualizaciones para siempre; quitar el archivo de paginaciÃ³n; limpiadores de RAM; bloqueos IFEO; editar memoria o archivos de juegos con antitrampas; drivers no oficiales.

### Herramientas de terceros

PresentMon (Intel, licencia MIT) no se incluye: se descarga de su GitHub oficial la primera vez que se mide, y se comprueba su firma.

### Licencias de otros proyectos

Los ajustes se investigaron en Atlas (GPL-3.0), ReviOS (CC-BY-SA-4.0) y WinUtil (MIT), pero el cÃ³digo es propio. No copies cÃ³digo de esos proyectos sin cumplir su licencia. Falta elegir una licencia para esta app.

### Antes de publicarla

- **Firma de cÃ³digo:** sin ella, Windows muestra una advertencia a quien la descargue. Las opciones son un certificado de firma de cÃ³digo de una autoridad certificadora, o Azure Trusted Signing. Ambas tienen costo y requisitos de identidad; hay que revisar los precios actuales.
- **Actualizaciones:** ponÃ© en `%LOCALAPPDATA%\FrameLab\actualizaciones.txt` una direcciÃ³n `https://` que devuelva `{"version":"x.y.z","descarga":"https://..."}`.
- **Pruebas:** hasta ahora solo se probÃ³ en una PC de escritorio con AtlasOS y placa NVIDIA. Windows 10 no se probÃ³ en una PC real.
