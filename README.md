# FrameLab

Optimizador de Windows para juegos, en español. Portable: un solo `.exe`, sin instalación.

> Estado: beta privada (0.4.2). Probado en Windows 11 25H2; Windows 10 (build 19041 o superior) en prueba.

## Qué hace

- **Diagnóstico**: revisa hardware, temperaturas, drivers, energía, inicio y servicios, y te dice qué conviene tocar.
- **Optimizaciones**: 96 ajustes de Windows agrupados en tres perfiles (Seguro, Gamer, Avanzado). Cada uno muestra qué cambia y si ya está aplicado.
- **Juegos**: ajustes por juego usando solo sus archivos de configuración oficiales (Fortnite, CS2 y otros).
- **Medir partida**: mide FPS, 1 % y 0,1 % lows y tirones (frame-time) con PresentMon, para comparar antes y después.
- **Instalar**: programas y drivers desde sus fuentes oficiales (winget y páginas del fabricante).

## Cómo cuida tu PC

- Antes de aplicar cambios crea un punto de restauración.
- Guarda el valor anterior de cada ajuste y deja revertir uno por uno.
- Si un ajuste falla a mitad de camino, se deshace solo.
- No incluye nada que baje la seguridad o pueda causar baneos: no apaga Defender, mitigaciones de CPU, actualizaciones ni el archivo de paginación, no usa "limpiadores de RAM" y no toca la memoria ni los archivos de los juegos.
- No promete "+200 % FPS". Lo que mejora se mide en la pestaña Medir partida.

## Requisitos

- Windows 10 (19041+) u 11, 64 bits.
- .NET Framework 4.8 (viene con Windows).
- Permiso de administrador para aplicar cambios.

## Compilar

Proyecto C# WPF (.NET Framework 4.8, x64). Ver `App/` para el código fuente.

## Licencia

Pendiente de definir.
