# FrameLab

Optimizador de Windows para juegos, en español. Es portable: un solo `.exe`, sin instalación.

> Estado: beta privada (0.8.0). Probado en Windows 11. En Windows 10 22H2 se probó el diagnóstico, que no cambia nada.

## Cómo se usa, en 3 pasos

1. **Descargá `FrameLab.exe`** desde [Releases](../../releases), a la derecha de esta página. No se instala.
2. **Abrilo con doble clic** y aceptá el permiso de administrador. Si Windows muestra "Windows protegió su PC", tocá **Más información** y después **Ejecutar de todas formas**. Aparece porque la app todavía no tiene firma digital.
3. **Tocá el botón Optimizar** y reiniciá la PC.

Si algo no te gusta, abrí la app y tocá **Deshacer todo**: todo vuelve a como estaba.

En la misma pantalla, **Qué frena tu PC** te dice qué es lo que más limita tus juegos. Si querés ajustar tus juegos, entrá a **Juegos**. El resto de las herramientas está en **Opciones avanzadas**, abajo a la izquierda.

## Qué hace

- **Optimizar (modo simple):** un botón que crea un punto de restauración y aplica solo los ajustes seguros que sirven en tu PC. Otro botón, **Deshacer todo**, los revierte.
- **Qué frena tu PC:** ordena lo que más limita los juegos (placa de video, disco, RAM, procesador, drivers, apagados, programas al iniciar) y avisa cuándo los ajustes de Windows van a ayudar poco.
- **Diagnóstico:** hardware, XMP, canales de RAM, discos, Hz del monitor, PCIe, BIOS, drivers y apagados inesperados.
- **Optimizaciones:** 102 ajustes de Windows en tres perfiles (Seguro, Gamer y Avanzado). Cada uno muestra qué cambia, su impacto, su riesgo y si ya está aplicado.
- **Juegos:** ajusta Fortnite, Roblox y Minecraft (Java y Bedrock) usando solo sus archivos de configuración oficiales, con copia previa. Para Valorant y CS2 da una guía para ajustarlos a mano, porque tienen antitrampas.
- **Modo partida:** antes de jugar cierra los programas que elijas y le da prioridad al juego. Al terminar, todo vuelve a como estaba y los programas se vuelven a abrir.
- **Quitar apps de Windows:** Copilot, promociones y apps que casi nadie usa, cada una con botón para reinstalarla. Edge deja de molestar; desinstalarlo es opcional, en Opciones avanzadas.
- **Ordenar escritorio:** pone en carpetas por tipo los archivos sueltos del escritorio o de Descargas, y se puede deshacer.
- **Personalización:** fondo propio de FrameLab para la app y para el escritorio, con botón para volver al fondo anterior.
- **Medir partida:** FPS, mínimos del 1 % y del 0,1 % y tirones con PresentMon, para comparar antes y después.
- **Espacio en disco, Limpieza, Programas al iniciar, Red e Instalar:** liberar espacio sin riesgo, elegir qué arranca con Windows, elegir el DNS e instalar programas y drivers de fuentes oficiales.

## Cómo cuida tu PC

- Antes de aplicar cambios crea un punto de restauración.
- Guarda el valor anterior de cada ajuste y deja revertirlos de a uno o todos juntos.
- Si un ajuste falla a mitad de camino, se deshace solo.
- No incluye nada que baje la seguridad o pueda causar baneos: no apaga Defender ni las mitigaciones de CPU, no desactiva las actualizaciones ni el archivo de paginación, no usa limpiadores de RAM y no toca la memoria ni los archivos de juegos con antitrampas.
- No promete "+200 % FPS": lo que mejora se mide en Medir partida.

## Requisitos

- **Windows 11**, o **Windows 10 versión 2004 o más nuevo** (compilación 19041 a 19045). No se probó en versiones anteriores.
- **.NET Framework 4.8**, que ya viene con Windows 10 1903 o más nuevo y con Windows 11.
- **winget (App Installer)**, solo para la sección Instalar. Si falta, la app avisa y abre su página en Microsoft Store.

En Windows 10, los ajustes exclusivos de Windows 11 aparecen en gris con la etiqueta "Solo Windows 11". El campo `windows` de `catalogo.json` indica dónde aplica cada ajuste (`10,11` o `11`).

---

## Para desarrolladores

Lo que sigue es para quien quiera modificar la app. Para usarla alcanza con los 3 pasos de arriba.

### Compilar

```powershell
.\compilar.ps1
```

Genera `dist\FrameLab.exe`. Con `-Prueba` genera una copia que no pide administrador y sirve para revisar otra PC sin cambiar nada: `FrameLab-diagnostico.exe --prueba <carpeta>` guarda capturas de cada sección y un `estado.txt`.

Usa el compilador de .NET Framework que ya trae Windows. No hace falta instalar nada.

### Archivos

- `catalogo.json`: ajustes, con impacto, riesgo y perfiles.
- `src\Motor.cs`: detectar, aplicar y revertir.
- `src\Salud.cs`: revisión de hardware y límite de FPS.
- `src\Medicion.cs`: medición con PresentMon y análisis.
- `src\Extras.cs`: juegos (Fortnite, Roblox y Minecraft), limpieza, apps y actualizaciones.
- `src\Partida.cs`: modo partida. `src\Fondo.cs`: fondo de FrameLab.
- `src\Ordenar.cs`: ordenar el escritorio y Descargas.
- `src\Frenos.cs`: "Qué frena tu PC", la tabla de gamas de placas de video y el asistente de espacio.
- `src\Ventana.cs`: interfaz. `src\Tema.xaml`: colores y estilos.
- `crear_logo.ps1`: genera el logo.

### Datos que guarda

En `%LOCALAPPDATA%\FrameLab`: valores anteriores de cada ajuste (`respaldo.json`), mediciones, copias de archivos de juegos, PresentMon, preferencias (`preferencias.txt`) y errores imprevistos (`errores.log`).

### Qué no incluye a propósito

Desactivar mitigaciones de CPU, Defender, SmartScreen o las actualizaciones para siempre; quitar el archivo de paginación; limpiadores de RAM; bloqueos IFEO; editar memoria o archivos de juegos con antitrampas; drivers no oficiales.

### Herramientas de terceros

PresentMon (Intel, licencia MIT) no se incluye: se descarga de su GitHub oficial la primera vez que se mide, y se comprueba su firma.

### Licencias de otros proyectos

Los ajustes se investigaron en Atlas (GPL-3.0), ReviOS (CC-BY-SA-4.0) y WinUtil (MIT), pero el código es propio. No copies código de esos proyectos sin cumplir su licencia. Falta elegir una licencia para esta app.

### Antes de publicarla

- **Firma de código:** sin ella, Windows muestra una advertencia a quien la descargue. Las opciones son un certificado de firma de código de una autoridad certificadora, o Azure Trusted Signing. Ambas tienen costo y requisitos de identidad; hay que revisar los precios actuales.
- **Actualizaciones:** poné en `%LOCALAPPDATA%\FrameLab\actualizaciones.txt` una dirección `https://` que devuelva `{"version":"x.y.z","descarga":"https://..."}`.
- **Pruebas:** hasta ahora solo se probó en una PC de escritorio con AtlasOS y placa NVIDIA. Windows 10 no se probó en una PC real.
