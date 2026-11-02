# Novedades de FrameLab 1.0.1

## Cambios en 1.0.1

- **Cafecito:** botón "Invitame un cafecito" en Acerca de, un link discreto al final de Optimizar, y el link en el README y en la página web. La app sigue siendo gratis y completa.

# Novedades de FrameLab 1.0

## Versión 1.0

- **Primera versión estable.** Se probó con ajustes reales en dos PC muy distintas: una con Windows 11, AtlasOS, i5 y GTX 1050 Ti, y otra con Windows 10 22H2 de fábrica, i3-3220 y GT 710. En esta última la RAM en uso bajó unos 2 GB.
- **Licencia MIT** y código abierto en GitHub.
- Ya no dice "beta". Las actualizaciones desde la app funcionan solas porque el repositorio es público.
- **Límite de FPS recomendado:** ahora es el 85 % del promedio de tu última partida. Antes salía de los mínimos del 1 % y quedaba muy bajo: 80 FPS en una PC que hace 139.
- Correcciones visuales: la sección se llama "Quitar apps" para que entre en el menú, y los textos largos de Modo partida ya no se cortan.

# Novedades de FrameLab 0.8.2

## Cambios en 0.8.2

- **Medir partida, más justo:** en mediciones de más de 4 minutos se dejan afuera el primer minuto y los últimos 30 segundos. Ahí están las cargas: entrar a la partida, compilar los sombreadores y volver al menú. En la medición real de Fortnite esas pausas, de hasta 3 segundos, bajaban los mínimos del 1 % a 20 FPS, cuando durante el juego eran de 66.
- Si los tirones son menos del 0,2 % de los frames, el resultado dice que el juego va fluido en lugar de "muy irregular".

# Novedades de FrameLab 0.8.1

## Cambios en 0.8.1 (corrección importante)

- **Se podían perder copias de los valores anteriores.** Con dos ventanas de FrameLab abiertas a la vez, por ejemplo una de `dist` y otra bajada de GitHub, la segunda corría al abrirse un paso de migración de la versión vieja. Si la primera estaba guardando justo en ese momento, la copia buena quedaba pisada por la de una versión anterior. En la PC de prueba se perdieron así las copias de 32 de 38 ajustes. Los ajustes seguían aplicados, pero Revertir y Deshacer todo ya no los veían.
  - La migración ahora corre solo la primera vez, cuando la carpeta de datos todavía no existe.
  - Solo puede haber una ventana de FrameLab abierta. Al actualizarse, la versión nueva espera a que se cierre la anterior.
  - Al abrirse, la app recupera desde la copia anterior (`respaldo.json.bak`) los ajustes que faltan y siguen aplicados.
- **Modo prueba:** ya no toca la carpeta de datos real.

# Novedades de FrameLab 0.8.0

## Cambios en 0.8.0

- **Quitar apps de Windows:** la sección reemplaza a "Apps preinstaladas" y ahora se ve también en el modo simple.
  - **Copilot:** un botón quita la app, lo desactiva con una directiva y oculta su botón de la barra de tareas. Se deshace desde Optimizaciones.
  - **Microsoft Edge:** "Desactivar lo molesto de Edge" hace que no arranque solo, que no quede abierto en segundo plano y que no recree su acceso directo; después abre la ventana para elegir otro navegador. Es reversible. Desinstalarlo solo aparece en Opciones avanzadas, con doble confirmación y con el desinstalador del propio Edge. Si Windows no lo permite en tu región, la app lo dice. WebView2 no se toca, y hay un botón para reinstalar Edge con winget.
  - **Apps por grupo:**
    - **Recomendado quitar:** Noticias, Tiempo, promociones, Cortana, Clipchamp y otras. Vienen marcadas.
    - **Opcional:** apps que algunas personas usan. Vienen sin marcar.
    - **Xbox:** con aviso, porque las necesitan Game Pass y algunos juegos.

    Hay una opción para quitarlas también en las cuentas nuevas. Las apps quitadas quedan en una lista con botón **Reinstalar**. Solo se ofrecen apps de una lista conocida: los componentes del sistema, como la Store, WebView2, .NET o VCLibs, nunca aparecen.
- **Página web** (`web\index.html`): rehecha con las funciones nuevas, los pasos de uso y capturas nuevas.

# Novedades de FrameLab 0.7.0

## Cambios en 0.7.0

- **Ordenar escritorio (sección nueva):** pone en carpetas por tipo los archivos sueltos del escritorio o de Descargas: Imágenes, Documentos, Videos, Música, Instaladores, Comprimidos y Otros. Los accesos directos se incluyen solo si los marcás. Antes muestra la lista para desmarcar lo que quieras dejar, nunca mueve carpetas ni pisa archivos con el mismo nombre, y **Deshacer orden** devuelve todo a su lugar.
- **Más optimizaciones** (3 ajustes nuevos, 101 en total):
  - **Optimizaciones para juegos en ventana** (Windows 11): menos latencia en ventana y en ventana sin bordes.
  - **Xbox Game Bar sin interrupciones:** no muestra consejos ni se abre con el botón Xbox del control.
  - **Sin limitación de energía en segundo plano:** solo PC de escritorio, en el perfil Avanzado.
  - "Efectos visuales al mínimo" ahora mantiene el suavizado de letras.
- **Modo partida:** opción para desactivar las optimizaciones de pantalla completa en los juegos que detecta. Solo agrega la marca si el juego no tenía otra opción de compatibilidad, y al desmarcarla quita solo lo suyo.

Ya estaban en el catálogo y no se duplicaron: el mouse sin aceleración, HAGS, la suspensión selectiva de USB, el ahorro de energía de PCIe, el estado mínimo de la CPU, el retraso de inicio, el Modo de juego, las apps en segundo plano, las capturas de Game Bar, la Optimización de entrega y los consejos de Windows. La caché de sombreadores de NVIDIA ya figura en la guía de NVIDIA.

No se agregaron:
- **No molestar automático mientras jugás:** no encontré una forma documentada de activarlo.
- **Storage Sense activado:** el catálogo ya tiene la opción contraria.

# Novedades de FrameLab 0.6.1

## Cambios en 0.6.1

- **Actualizaciones desde la app:** "Buscar actualizaciones" consulta las versiones publicadas en GitHub. Si hay una más nueva, muestra qué trae y ofrece **Descargar e instalar**. Antes de reemplazar el programa comprueba la descarga: que esté completa, que coincida con la huella publicada y que sea FrameLab de la versión anunciada. Si algo falla, queda la versión que tenías. Mientras el repositorio sea privado, la app lo explica y abre la página de descargas.
- El exe ahora declara su número de versión, que es lo que comprueba el actualizador.

# Novedades de FrameLab 0.6.0

## Cambios en 0.6.0

- **Modo partida (en Juegos):** antes de jugar cierra los programas que marques, como navegadores, Discord o launchers, y al terminar los vuelve a abrir. Mientras dura:
  - le da prioridad alta al juego, nunca tiempo real;
  - baja la prioridad de los navegadores y launchers que queden abiertos;
  - pausa la indexación de búsqueda de Windows;
  - activa el plan de alto rendimiento.

  Todo vuelve a como estaba al terminar o al cerrar la app. Si la app se cierra de golpe, lo deshace la próxima vez que se abre. No toca procesos de Windows, de seguridad ni antitrampas, ni la memoria o los archivos de los juegos. Reconoce Fortnite, Roblox, Minecraft, CS2, Valorant, GTA V, Rocket League y Apex, y se pueden agregar otros juegos por el nombre del .exe.
- **Placa de video de alto rendimiento por juego:** opción para notebooks con dos placas. Se puede quitar.
- **Personalización (sección nueva):**
  - Fondo propio de FrameLab dentro de la app, con intensidad regulable. También podés elegir tu propia imagen o dejarla sin fondo.
  - Botón para poner un fondo de escritorio FrameLab, dibujado por la app a la resolución de tu monitor (1080p, 1440p o 4K), y otro para volver al fondo anterior.

Probado sin administrador: 58 pruebas OK. El modo partida se probó con un programa de prueba, sin tocar el plan de energía, la búsqueda ni otros programas. El fondo de escritorio se probó sobre una clave de prueba, sin cambiar el fondo real.

# Novedades de FrameLab 0.5.1

## Cambios en 0.5.1

- **Nombre nuevo: FrameLab.** Cambian la ventana, los exe (`FrameLab.exe` y `FrameLab-diagnostico.exe`), la web y los documentos.
- **Datos:** ahora se guardan en `%LOCALAPPDATA%\FrameLab`. Al abrir la versión nueva por primera vez, la app copia la carpeta de Afina PC entera, así que Revertir y Deshacer todo siguen funcionando. La carpeta vieja no se toca. Los puntos de restauración que ya existen conservan el nombre "Afina PC".

# Novedades de Afina PC 0.5.0

## Cambios en 0.5.0

- **Modo simple:** es la vista inicial. Tiene un botón **Optimizar**, que crea un punto de restauración y aplica solo los ajustes del perfil Seguro que sirven en esta PC, y un botón **Deshacer todo**, que revierte todo lo que cambió la app. Las demás secciones aparecen al marcar "Opciones avanzadas", abajo a la izquierda, y la app recuerda la elección.
- **Qué frena tu PC:** ordena qué limita los juegos según el hardware y el estado de la PC. Revisa la placa de video (con una tabla de gamas propia), el disco lleno o mecánico, los apagados inesperados, la RAM, el procesador, el driver, el monitor y los programas al iniciar. Para placas débiles sugiere modelos usados por nombre y avisa qué fuente y conector necesitan, sin precios. Además, dice claramente cuándo los ajustes de Windows van a ayudar poco. Aparece en Optimizar y en Resumen.
- **Juegos:** ahora también se ajustan **Roblox** (calidad gráfica manual), **Minecraft Java** (`options.txt`) y **Minecraft Bedrock**. Siguen las mismas reglas que Fortnite: el juego tiene que estar cerrado, se guarda una copia, la escritura es atómica, se puede restaurar y solo se cambian las claves que ya existen en el archivo oficial.
- **Espacio en disco:** muestra el espacio libre y, con un botón, las carpetas que más ocupan: carpetas del usuario, Descargas, AppData y juegos de Steam (todas las bibliotecas), Epic, Minecraft y Roblox. Cada una tiene un botón para abrirla. Nunca borra nada.

Probado sin administrador: 48 pruebas del motor, con la clave de prueba y copias temporales de los archivos de cada juego, todas OK. También cargan las capturas de todas las secciones, y la búsqueda de espacio en esta PC tardó 1 minuto.

# Novedades de Afina PC 0.4.3

## Cambios en 0.4.3

Salen del diagnóstico de la PC con Windows 10, i3-3220 y GT 710:

- **"No aplica en esta PC":** si los servicios de un ajuste no existen, o si el adaptador de red no tiene las opciones de ahorro de energía, el ajuste aparece en gris con esa etiqueta. Antes figuraba como aplicado. No cuenta en los totales y los perfiles no lo marcan.
- **Monitor:** con placas NVIDIA viejas que no informan su uso, como la GT 710, ahora lo explica en lugar de decir que solo funciona con NVIDIA.

# Novedades de Afina PC 0.4.2

## Cambios en 0.4.2 (revisión de errores)

- **Aplicar y revertir:** si un ajuste falla a mitad de camino, se deshace. Revertir sigue con las demás acciones si una falla y guarda solo lo pendiente. Ya se restauran los DWord altos y los valores multilínea, y se borran las claves vacías que creó la app.
- **Copia de valores:** se escribe de forma atómica y con copia previa. Una copia dañada se recupera o se aparta con aviso, sin frenar la carga de la app.
- **Cuelgues y cierres:** los procesos externos ya no pueden colgar la app por la salida de errores y tienen tiempo límite. Los errores imprevistos se anotan en `errores.log`. Una sección que falla no deja a las demás sin cargar. No se puede cerrar la ventana mientras se aplican cambios.
- **Limpieza:** no entra en enlaces ni uniones, que podían borrar archivos de fuera de la zona, y no se cierra de golpe ante una carpeta sin permiso.
- **PresentMon:** se detiene al cerrar la app y avisa si no pudo arrancar.
- **Fortnite:** se conserva la codificación del archivo, la escritura es atómica y hay mensajes claros si está bloqueado o en solo lectura.
- **DNS:** un adaptador que falla no frena a los demás, los adaptadores conectados después también se guardan, y uno que ya no existe no traba Restaurar.
- **Detección:** una UPS ya no hace pasar una PC de escritorio por notebook. Los discos USB o de tipo desconocido ya no se informan como HDD. La gráfica integrada ya no baja el nivel recomendado. Windows fuera de `C:` ahora funciona.
- **winget:** mensajes claros para los códigos documentados. "Ya instalado" y "falta reiniciar" no cuentan como error.
- **Resumen:** se actualiza después de aplicar, revertir o medir, y ya no congela la ventana.

Probado con la copia de prueba, sin administrador: 25 pruebas del motor sobre una clave propia de prueba, y carga de la interfaz en Windows 11 y con Windows 10 simulado. No se probó en real aplicar ajustes, DNS, limpieza, winget ni PresentMon.

# Novedades de Afina PC 0.4.1

"Afina PC" es un nombre provisional. Alternativas: **Pulso PC**, **Fluido** o **AjustaFPS**. No comprobé si estos nombres ya están registrados.

## Cambios en 0.4.1

- **Menú lateral:** las capturas siempre marcaban Resumen porque la rutina que las genera cambiaba de sección sin pasar por el menú. Ahora pasa por el menú, como un clic.
- **Idioma:** toda la app, el catálogo, los diálogos, la web y estos documentos están en español rioplatense con voseo. También cambió el vocabulario: placa de video, placa madre, mouse, notebook y "la PC".
- **Windows 10:** cada ajuste indica en qué Windows aplica. Los 11 exclusivos de Windows 11 aparecen en gris con "Solo Windows 11" y no cuentan en los totales.
- **Exclusivos de Windows 11:** widgets, menú contextual clásico, Finalizar tarea, barra de tareas a la izquierda, Recall, Click to Do, Copilot, recomendaciones de Inicio, IA del Bloc de notas, IA de Paint y servicio de IA.
- **winget:** si falta, la sección Instalar lo avisa y ofrece un botón para abrir App Installer en Microsoft Store.
- **Versiones viejas de Windows 10:** el diagnóstico lee bien la versión aunque no exista `DisplayVersion`, y avisa si la compilación es anterior a 19041.
- **.NET:** el exe ahora declara .NET Framework 4.8 como destino.
- **Revisión de pantallas:**
  - Las tarjetas de métricas de Resumen, Medir partida y Monitor ahora se alinean con las demás.
  - Las secciones que tardan en cargar muestran "Cargando...".
  - El Monitor muestra "Esperando datos..." en vez de afirmar que no hay frenos antes de leer nada.
  - El registro de Instalar aparece recién cuando hay algo que mostrar.
  - Se quitó un espacio vacío en Acerca de.
  - El texto de Medir partida nombra los botones tal como se llaman, y "Restaurar los míos" pasó a "Restaurar mis ajustes".
- **Capturas de `web\`:** se regeneraron y se sumaron Diagnóstico y Resumen.

## Qué hay en la app

| Sección | Qué hace |
|---|---|
| Resumen | Ajustes aplicados, qué revisar y límite de FPS recomendado |
| Diagnóstico | Hardware y revisión de salud: XMP, canales de RAM, HDD, Hz del monitor, PCIe, BIOS, espacio, apagados, parches y driver |
| Optimizaciones | 96 ajustes con estado, impacto, riesgo, perfiles, copia y botón de revertir |
| Programas al iniciar | Activar o desactivar cada programa |
| Juegos | Detecta Fortnite, Valorant, CS2, Minecraft y Roblox, con recomendaciones según la PC. Edita solo Fortnite, con copia |
| Medir partida | FPS, mínimos, tirones, explicación y comparación de antes y después |
| Monitor en vivo | Uso y temperatura de GPU, CPU, RAM, disco y avisos de freno |
| Limpieza | Papelera, temporales, Windows Update, Optimización de entrega, volcados, informes y caché de navegadores cerrados. Muestra el tamaño antes de borrar. No toca la caché de shaders |
| Apps preinstaladas | Quitar y reinstalar desde Microsoft Store |
| Red | Elegir DNS (automático, Cloudflare, Google o Quad9), restaurar el original y vaciar la caché |
| Instalar | Chrome, Epic, Roblox, Minecraft, Steam y Discord con winget; drivers desde la fuente oficial |
| Acerca de | Versión, búsqueda de actualizaciones y lista "Por qué no está" |

Ajustes por categoría: Privacidad 36, Sistema 17, Servicios 13, Personalización 8, Rendimiento 7, Energía 6, Juegos 5, Placa de video 3, Avanzado 1. Además, hay guías para NVIDIA y AMD.

En esta PC hay 56 de 96 aplicados. Incluye los de Atlas y 6 que se aplicaron desde la app anterior (Modo de juego, efectos visuales, apps sugeridas, contenido sugerido, seguimiento de apps e informes de errores).

## Qué se probó en 0.4.1

- **Compilación:** la versión final y la copia de prueba compilan.
- **Interfaz:** se generó la imagen de cada sección con la copia de prueba, sin permisos de administrador y sin aplicar nada. El menú marca la sección correcta en todas. Las secciones se revisaron al tamaño normal de la ventana, no al tamaño mínimo.
- **Windows 10 simulado:** la copia de prueba, con la compilación 19045 simulada, carga sin errores y muestra los ajustes de Windows 11 en gris. Solo simula esa etiqueta: el resto corre sobre Windows 11.

## Sin probar

- **Abrir `AfinaPC.exe` con permisos de administrador.** Pide una ventana de permiso de Windows, así que esta versión final no se abrió.
- **Un clic real en el menú.** La prueba selecciona cada sección por código, por el mismo camino que un clic, pero sin mouse.
- **Windows 10 real.** No hay una PC con Windows 10 disponible.
- **Medir partida completa:** descarga de PresentMon, medición real y comparación.
- **Aplicar ajustes reales de la mayoría de tipos nuevos.** Detectar un ajuste no demuestra que Windows lo respete: varios dependen de la versión de Windows o del programa. Probá uno por vez.
- **Cambiar el DNS, quitar apps, limpiar e instalar con winget.**
- **Temperatura de CPU:** usa un sensor de placa aproximado, si existe.
- **Otras PC:** notebooks, AMD, Intel y Windows sin Atlas.

## Qué probar en una PC con Windows 10

1. Abrir la app en Windows 10 22H2 (19045) y, si podés, en 2004 (19041): tiene que abrir sin errores ni pedir instalar .NET.
2. Diagnóstico: que muestre bien la versión y no falte hardware (discos, RAM y placa de video).
3. Optimizaciones: que los 11 exclusivos aparezcan en gris con "Solo Windows 11" y que los perfiles no los marquen.
4. Aplicar y revertir algunos ajustes marcados `10,11`, sobre todo `actualizaciones-tempranas`, `config-anuncios`, `gpu-hags` y `bing-cortana`: mi clasificación sale del conocimiento general de Windows, no de una prueba.
5. Instalar: en una PC sin winget, que aparezca el aviso y el botón abra la página de App Installer. Con winget, instalar un programa.
6. Instalar > "Buscar drivers en Windows Update": que abra la página correcta de Configuración.
7. Red, Limpieza, Apps preinstaladas y Monitor: que carguen y funcionen.
8. Medir partida con un juego real.

## Lo que no se hizo, y por qué

- **Phone Link en Inicio:** no tengo un valor de registro confirmado, así que no se incluyó.
- **Animaciones por separado:** ya están dentro de "Efectos visuales al mínimo".
- **Comparar con el driver más nuevo y actualizar con un clic:** no encontré una fuente oficial documentada para consultarlo. La app muestra tu versión y abre la descarga oficial.
- **Ajustes del panel de NVIDIA y AMD:** se dan como guía, porque aplicarlos requiere herramientas de terceros.
- **Resolución del temporizador:** no se incluyó. Sin un programa que la mantenga no tiene efecto, y forzarla es de los ajustes con menos evidencia.
- **Datos de GPU AMD e Intel:** el monitor y la medición por segundo solo los leen en NVIDIA.
- **Repositorio de git:** `git` no está instalado. Quedan `README.md` y `.gitignore` listos.
- **Investigación:** Atlas, ReviOS y WinUtil se revisaron con su código. La lista de Win11Debloat, Optimizer y djdallmann la aportó otra sesión de Claude, y sus fuentes no se comprobaron una por una. Por eso la app no cita esas mediciones.

## Excluido a propósito

La lista y los motivos están en la app, en Acerca de > Por qué no está.

## Cambios en 0.4.0

- 32 ajustes nuevos: IA y telemetría de Edge, Chrome, Paint, Office, Visual Studio y NVIDIA; Recall; Bing; atajo de teclas especiales; reinicios automáticos; servicios opcionales; personalización; Modern Standby solo en notebooks.
- Se quitó `NetworkThrottlingIndex` del catálogo, por falta de evidencia.
- Secciones nuevas: Red y "Por qué no está".
- Ampliada la Limpieza.
- Copia del código con fecha en `versiones\`, porque `git` no está instalado.
