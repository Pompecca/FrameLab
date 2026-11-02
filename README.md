# FrameLab

App portable para Windows 10 y 11 que analiza la PC, aplica optimizaciones reversibles y mide si de verdad mejoraron los juegos.

## Usar

Abrí `dist\FrameLab.exe`. No se instala; pide permiso de administrador al abrirse.

Arranca en modo simple: el botón **Optimizar** aplica solo los ajustes seguros y **Deshacer todo** los revierte. El resto está en "Opciones avanzadas".

Para revisar otra PC sin cambiar nada, copiá `dist\PC_hermano` y abrí `1_DIAGNOSTICO_sin_cambios.cmd`. Genera la carpeta `informe` y no pide permiso de administrador.

## Requisitos

- **Windows 11**, o **Windows 10 versión 2004 o más nuevo** (compilación 19041 a 19045). No se probó en versiones anteriores.
- **.NET Framework 4.8**, que ya viene con Windows 10 1903 o más nuevo y con Windows 11.
- **winget (App Installer)**, solo para la sección Instalar. Si falta, la app avisa y abre su página en Microsoft Store.

En Windows 10, los ajustes exclusivos de Windows 11 aparecen en gris con la etiqueta "Solo Windows 11". El campo `windows` de `catalogo.json` indica dónde aplica cada ajuste (`10,11` o `11`).

## Compilar

```powershell
.\compilar.ps1
```

Usa el compilador de .NET Framework que ya trae Windows. No hace falta instalar nada.

## Archivos

- `catalogo.json`: ajustes, con impacto, riesgo y perfiles.
- `src\Motor.cs`: detectar, aplicar y revertir.
- `src\Salud.cs`: revisión de hardware y límite de FPS.
- `src\Medicion.cs`: medición con PresentMon y análisis.
- `src\Extras.cs`: juegos (Fortnite, Roblox y Minecraft), limpieza, apps y actualizaciones.
- `src\Frenos.cs`: "Qué frena tu PC", la tabla de gamas de placas de video y el asistente de espacio.
- `src\Ventana.cs`: interfaz. `src\Tema.xaml`: colores y estilos.
- `crear_logo.ps1`: genera el logo.

## Datos que guarda

En `%LOCALAPPDATA%\FrameLab`: valores anteriores de cada ajuste (`respaldo.json`), mediciones, copias de archivos de juegos, PresentMon, preferencias (`preferencias.txt`) y errores imprevistos (`errores.log`).

## Qué no incluye a propósito

Desactivar mitigaciones de CPU, Defender, SmartScreen o las actualizaciones para siempre; quitar el archivo de paginación; limpiadores de RAM; bloqueos IFEO; editar memoria o archivos de juegos con antitrampas; drivers no oficiales.

## Herramientas de terceros

PresentMon (Intel, licencia MIT) no se incluye: se descarga de su GitHub oficial la primera vez que se mide, y se comprueba su firma.

## Licencias de otros proyectos

Los ajustes se investigaron en Atlas (GPL-3.0), ReviOS (CC-BY-SA-4.0) y WinUtil (MIT), pero el código es propio. No copies código de esos proyectos sin cumplir su licencia. Falta elegir una licencia para esta app.

## Antes de publicarla

- **Firma de código:** sin ella, Windows muestra una advertencia a quien la descargue. Las opciones son un certificado de firma de código de una autoridad certificadora, o Azure Trusted Signing. Ambas tienen costo y requisitos de identidad; hay que revisar los precios actuales.
- **Actualizaciones:** poné en `%LOCALAPPDATA%\FrameLab\actualizaciones.txt` una dirección `https://` que devuelva `{"version":"x.y.z","descarga":"https://..."}`.
- **Pruebas:** hasta ahora solo se probó en una PC de escritorio con AtlasOS y placa NVIDIA. Windows 10 no se probó en una PC real.
