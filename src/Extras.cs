using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace OptimizadorPC
{
    public class Juego { public string Id, Nombre, Guia; public bool Instalado, SePuedeEditar; }

    // Solo se editan los archivos de configuración oficiales de cada juego, con el juego cerrado y con copia previa
    public static class Juegos
    {
        static string Local { get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } }
        static string Roaming { get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); } }
        static string CarpetaCopias { get { return Path.Combine(Medicion.Carpeta, "copias_juegos"); } }
        // Solo para pruebas: permite usar copias temporales en lugar de los archivos reales
        public static readonly Dictionary<string, string> RutasPrueba = new Dictionary<string, string>();

        static string IniFortnite { get { return Path.Combine(Local, @"FortniteGame\Saved\Config\WindowsClient\GameUserSettings.ini"); } }
        static string OpcionesJava { get { return Path.Combine(Roaming, @".minecraft\options.txt"); } }

        // Roblox numera el archivo (GlobalBasicSettings_13.xml): se usa el más reciente
        static string ConfigRoblox
        {
            get
            {
                string d = Path.Combine(Local, "Roblox");
                try { return Directory.Exists(d) ? Directory.GetFiles(d, "GlobalBasicSettings_*.xml").OrderByDescending(File.GetLastWriteTime).FirstOrDefault() : null; }
                catch (Exception) { return null; }
            }
        }

        // La versión de Microsoft Store y la nueva versión de Bedrock guardan las opciones en lugares distintos
        static string OpcionesBedrock
        {
            get
            {
                var candidatas = new List<string> { Path.Combine(Local, @"Packages\Microsoft.MinecraftUWP_8wekyb3d8bbwe\LocalState\games\com.mojang\minecraftpe\options.txt") };
                string usuarios = Path.Combine(Roaming, @"Minecraft Bedrock\Users");
                try { if (Directory.Exists(usuarios)) candidatas.AddRange(Directory.GetDirectories(usuarios).Select(u => Path.Combine(u, @"games\com.mojang\minecraftpe\options.txt"))); }
                catch (Exception) { }
                return candidatas.Where(File.Exists).OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
            }
        }

        public static string RutaConfig(string id)
        {
            string r;
            if (RutasPrueba.TryGetValue(id, out r)) return r;
            if (id == "fortnite") return IniFortnite;
            if (id == "roblox") return ConfigRoblox;
            if (id == "minecraft") return OpcionesJava;
            if (id == "bedrock") return OpcionesBedrock;
            return null;
        }

        static string Nombre(string id)
        {
            return id == "fortnite" ? "Fortnite" : id == "roblox" ? "Roblox" : id == "minecraft" ? "Minecraft" : "Minecraft Bedrock";
        }

        // Se conserva el nombre de la copia de Fortnite de versiones anteriores
        public static string Copia(string id)
        {
            string n = id == "fortnite" ? "Fortnite_GameUserSettings.ini" : id == "roblox" ? "Roblox_GlobalBasicSettings.xml" : id == "minecraft" ? "MinecraftJava_options.txt" : "MinecraftBedrock_options.txt";
            return Path.Combine(CarpetaCopias, n);
        }

        // Nivel orientativo según la placa de video
        public static string Nivel(string gpus)
        {
            if (Regex.IsMatch(gpus, @"\bArc\b|\b(GTX 1[06-9]\d\d|GTX 16\d\d|RTX 20\d\d|RTX 30[56]0|RX 5[5-9]00|RX 66\d\d)", RegexOptions.IgnoreCase)
                && !Regex.IsMatch(gpus, @"GTX 10[35]0", RegexOptions.IgnoreCase)) return "Medio";
            if (Regex.IsMatch(gpus, @"\b(GT \d|GTX (7|9)\d\d|GTX 10[35]0|Intel|Vega|RX 5[0-9]0\b|RX 4[0-9]0\b)|Radeon(\(TM\))? Graphics", RegexOptions.IgnoreCase)) return "Bajo";
            return "Alto";
        }

        // Con gráfica integrada y dedicada, manda la mejor: la integrada no debe bajar el nivel
        public static string NivelDe(IEnumerable<string> gpus)
        {
            var niveles = gpus.Select(Nivel).ToList();
            return niveles.Contains("Alto") ? "Alto" : niveles.Contains("Medio") ? "Medio" : niveles.Any() ? "Bajo" : "Alto";
        }

        public static List<Juego> Detectar(string nivel, int limite)
        {
            string prog = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string cap = limite > 0 ? limite + " FPS" : "un poco menos que los Hz de tu monitor";
            string resolucion = nivel == "Bajo" ? "resolución nativa con escalado al 75-85 %" : "resolución nativa";
            const string copia = " Se guarda una copia del archivo antes de cambiarlo y se puede restaurar.";
            string roblox = ConfigRoblox, bedrock = OpcionesBedrock;
            return new List<Juego> {
                new Juego { Id = "fortnite", Nombre = "Fortnite", Instalado = File.Exists(IniFortnite), SePuedeEditar = true,
                    Guia = "Nivel " + nivel + ". Se puede aplicar desde acá con el juego cerrado: calidad baja, límite de " + cap + ", distancia de visión media." + copia + " El modo de renderizado (Rendimiento o DirectX 12) se elige dentro del juego." },
                new Juego { Id = "roblox", Nombre = "Roblox", Instalado = Directory.Exists(Path.Combine(Local, "Roblox")), SePuedeEditar = roblox != null,
                    Guia = "Se puede aplicar desde acá con Roblox cerrado: calidad de gráficos en manual y a " + CalidadRoblox(nivel) + " de 10 barras." + copia + " Muchos juegos de Roblox limitan más por CPU, así que cerrá programas de fondo." },
                new Juego { Id = "minecraft", Nombre = "Minecraft", Instalado = Directory.Exists(Path.Combine(Roaming, ".minecraft")), SePuedeEditar = File.Exists(OpcionesJava),
                    Guia = "Edición Java. Se puede aplicar desde acá con el juego cerrado: distancia de renderizado " + DistanciaMinecraft(nivel) + ", gráficos rápidos, nubes y partículas reducidas, sin sincronización vertical" + (limite > 0 ? " y límite de " + limite + " FPS" : "") + "." + copia + " Sodium (mod) puede dar una gran mejora si jugás con mods." },
                new Juego { Id = "bedrock", Nombre = "Minecraft Bedrock", Instalado = bedrock != null, SePuedeEditar = bedrock != null,
                    Guia = "La versión de Microsoft Store o Xbox. Se puede aplicar desde acá con el juego cerrado: distancia de renderizado " + DistanciaMinecraft(nivel) + ", gráficos simples y sin sincronización vertical." + copia },
                new Juego { Id = "valorant", Nombre = "Valorant", Instalado = Directory.Exists(Path.Combine(Local, "VALORANT")),
                    Guia = "En el juego: límite de FPS en " + cap + ", calidades de material, textura y detalle en bajo, y Reflex activado si tu placa lo admite. Se ajusta a mano, porque modificar archivos de un juego con antitrampas no es buena idea." },
                new Juego { Id = "cs2", Nombre = "Counter-Strike 2", Instalado = Directory.Exists(Path.Combine(prog, @"Steam\steamapps\common\Counter-Strike Global Offensive")),
                    Guia = "En el juego: " + resolucion + ", sombras y efectos en bajo, Reflex activado y límite con fps_max " + (limite > 0 ? limite.ToString() : "según tu monitor") + ". Se ajusta a mano: el antitrampas no admite cambios en archivos." }
            };
        }

        static int CalidadRoblox(string nivel) { return nivel == "Bajo" ? 2 : nivel == "Medio" ? 5 : 8; }
        static int ChunksMinecraft(string nivel) { return nivel == "Bajo" ? 8 : nivel == "Medio" ? 12 : 16; }
        static string DistanciaMinecraft(string nivel) { return ChunksMinecraft(nivel) + " chunks"; }

        // Solo para pruebas con archivos temporales: no mira si el juego real está abierto
        public static bool Simular;

        public static bool Abierto(string id)
        {
            if (Simular) return false;
            Func<string, bool> hay = n => Process.GetProcessesByName(n).Any();
            if (id == "fortnite") return new[] { "FortniteClient-Win64-Shipping", "FortniteClient-Win64-Shipping_EAC", "FortniteClient-Win64-Shipping_EAC_EOS", "FortniteClient-Win64-Shipping_BE", "FortniteLauncher" }.Any(hay);
            if (id == "roblox") return hay("RobloxPlayerBeta");
            if (id == "bedrock") return hay("Minecraft.Windows");
            // Minecraft Java corre dentro de Java: se reconoce por el título de la ventana
            if (id == "minecraft")
                return Process.GetProcessesByName("javaw").Concat(Process.GetProcessesByName("java")).Any(p =>
                {
                    try { return p.MainWindowTitle.IndexOf("Minecraft", StringComparison.OrdinalIgnoreCase) >= 0; }
                    catch (Exception) { return false; }
                });
            return false;
        }

        // El juego puede guardar el archivo en UTF-16 o en ANSI: hay que reescribirlo igual
        static string LeerConCodificacion(string ruta, out Encoding enc)
        {
            byte[] b = File.ReadAllBytes(ruta);
            enc = null;
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) enc = new UnicodeEncoding(false, true);
            else if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) enc = new UnicodeEncoding(true, true);
            else if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) enc = new UTF8Encoding(true);
            else
            {
                try { return new UTF8Encoding(false, true).GetString(b); }
                catch (ArgumentException) { enc = Encoding.Default; return enc.GetString(b); }
            }
            return enc.GetString(b, enc.GetPreamble().Length, b.Length - enc.GetPreamble().Length);
        }

        static void ComprobarEscribible(string ruta, string nombre)
        {
            if (File.Exists(ruta) && (File.GetAttributes(ruta) & FileAttributes.ReadOnly) != 0) throw new InvalidOperationException("El archivo de ajustes de " + nombre + " está en solo lectura. Quitale esa marca y volvé a intentar.");
        }

        public static void Aplicar(string id, string nivel, int limite)
        {
            string nombre = Nombre(id), ruta = RutaConfig(id);
            if (Abierto(id)) throw new InvalidOperationException("Cerrá " + nombre + " primero: el juego sobrescribiría los cambios.");
            if (ruta == null || !File.Exists(ruta)) throw new InvalidOperationException("No se encontró el archivo de ajustes de " + nombre + ". Abrí el juego una vez y cerralo.");
            ComprobarEscribible(ruta, nombre);
            Directory.CreateDirectory(CarpetaCopias);
            // Se conserva la primera copia para que restaurar siempre vuelva a tu configuración original
            if (!File.Exists(Copia(id))) File.Copy(ruta, Copia(id));
            Encoding enc;
            string texto = LeerConCodificacion(ruta, out enc);
            string fin = texto.Contains("\r\n") ? "\r\n" : "\n";
            string[] lineas = texto.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            string[] nuevas = id == "fortnite" ? TransformarIni(lineas, nivel, limite)
                : id == "roblox" ? TransformarRoblox(lineas, nivel)
                : id == "minecraft" ? TransformarMinecraft(lineas, nivel, limite)
                : TransformarBedrock(lineas, nivel);
            try { Util.EscribirSeguro(ruta, string.Join(fin, nuevas), enc ?? new UTF8Encoding(false), null); }
            catch (IOException) { throw new InvalidOperationException("Otro programa está usando el archivo de ajustes de " + nombre + ". Cerralo y volvé a intentar. No se cambió nada."); }
        }

        public static void Restaurar(string id)
        {
            string nombre = Nombre(id), ruta = RutaConfig(id);
            if (Abierto(id)) throw new InvalidOperationException("Cerrá " + nombre + " primero.");
            if (!File.Exists(Copia(id))) throw new InvalidOperationException("No hay copia guardada.");
            if (ruta == null) throw new InvalidOperationException("No se encontró dónde guarda " + nombre + " sus ajustes.");
            ComprobarEscribible(ruta, nombre);
            try { File.Copy(Copia(id), ruta, true); }
            catch (IOException) { throw new InvalidOperationException("Otro programa está usando el archivo de ajustes de " + nombre + ". Cerralo y volvé a intentar. La copia sigue guardada."); }
            File.Delete(Copia(id));
        }

        // Solo cambia las claves que ya existen; el resto del archivo queda igual
        public static string[] TransformarIni(string[] original, string nivel, int limite)
        {
            int calidad = nivel == "Bajo" ? 0 : nivel == "Medio" ? 1 : 2;
            var cambios = new Dictionary<string, string> {
                {"sg.ShadowQuality","0"}, {"sg.GlobalIlluminationQuality","0"}, {"sg.ReflectionQuality","0"},
                {"sg.PostProcessQuality","0"}, {"sg.EffectsQuality", calidad.ToString()}, {"sg.TextureQuality", calidad.ToString()},
                {"sg.ViewDistanceQuality", (nivel == "Bajo" ? "1" : "2")}, {"sg.FoliageQuality","0"},
                {"sg.ResolutionQuality", nivel == "Bajo" ? "85" : "100"}, {"bUseVSync","False"}
            };
            if (limite > 0) cambios["FrameRateLimit"] = limite + ".000000";
            return Reemplazar(original, '=', cambios);
        }

        // SavedQualityLevel es la calidad que se elige en el menú: 0 es automática y de 1 a 10 son las barras
        public static string[] TransformarRoblox(string[] original, string nivel)
        {
            string calidad = CalidadRoblox(nivel).ToString();
            return original.Select(l => Regex.Replace(l, @"(<token name=""SavedQualityLevel"">)\d+(</token>)", "${1}" + calidad + "${2}")).ToArray();
        }

        public static string[] TransformarMinecraft(string[] original, string nivel, int limite)
        {
            int chunks = ChunksMinecraft(nivel);
            var cambios = new Dictionary<string, string> {
                {"renderDistance", chunks.ToString()}, {"simulationDistance", Math.Min(chunks, nivel == "Bajo" ? 6 : 10).ToString()},
                {"graphicsMode", "0"}, {"fancyGraphics", "false"}, {"particles", nivel == "Bajo" ? "2" : "1"},
                {"renderClouds", nivel == "Bajo" ? "false" : "fast"}, {"entityShadows", nivel == "Bajo" ? "false" : "true"},
                {"biomeBlendRadius", nivel == "Bajo" ? "0" : "1"}, {"enableVsync", "false"}
            };
            if (limite > 0) cambios["maxFps"] = Math.Min(limite, 260).ToString();
            return Reemplazar(original, ':', cambios);
        }

        // En Bedrock la distancia se guarda en bloques: 16 por chunk
        public static string[] TransformarBedrock(string[] original, string nivel)
        {
            var cambios = new Dictionary<string, string> {
                {"gfx_viewdistance", (ChunksMinecraft(nivel) * 16).ToString()}, {"gfx_fancygraphics", "0"},
                {"gfx_vsync", "0"}, {"gfx_transparentleaves", nivel == "Bajo" ? "0" : "1"}, {"gfx_smoothlighting", nivel == "Bajo" ? "0" : "1"}
            };
            return Reemplazar(original, ':', cambios);
        }

        // Respeta las comillas del valor original: Minecraft guarda algunos así ("false")
        static string[] Reemplazar(string[] original, char separador, Dictionary<string, string> cambios)
        {
            return original.Select(l =>
            {
                int i = l.IndexOf(separador);
                if (i <= 0) return l;
                string k = l.Substring(0, i);
                if (!cambios.ContainsKey(k)) return l;
                string viejo = l.Substring(i + 1);
                bool comillas = viejo.StartsWith("\"") && viejo.EndsWith("\"") && viejo.Length >= 2;
                return k + separador + (comillas ? "\"" + cambios[k] + "\"" : cambios[k]);
            }).ToArray();
        }
    }

    public class ZonaLimpieza { public string Nombre, Nota; public List<string> Rutas = new List<string>(); public string[] Procesos = new string[0]; public long Bytes; public long Archivos; public bool Bloqueada, Papelera; }

    public static class Limpieza
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 8)]
        struct INFO_PAPELERA { public int cbSize; public long i64Size; public long i64NumItems; }
        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int SHQueryRecycleBin(string raiz, ref INFO_PAPELERA info);
        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int SHEmptyRecycleBin(IntPtr ventana, string raiz, uint opciones);

        static bool Abierto(params string[] procesos) { return procesos.Any(p => Process.GetProcessesByName(p).Any()); }

        // No entra en enlaces ni uniones: borrarían archivos de fuera de la zona. Una carpeta sin permiso se saltea
        public static IEnumerable<string> ArchivosDe(string raiz)
        {
            var pendientes = new Stack<string>();
            pendientes.Push(raiz);
            while (pendientes.Count > 0)
            {
                string d = pendientes.Pop();
                string[] archivos = new string[0], carpetas = new string[0];
                try { archivos = Directory.GetFiles(d); carpetas = Directory.GetDirectories(d); }
                catch (Exception) { }
                foreach (var f in archivos) yield return f;
                foreach (var c in carpetas)
                {
                    bool enlace = true;
                    try { enlace = (File.GetAttributes(c) & FileAttributes.ReparsePoint) != 0; }
                    catch (Exception) { }
                    if (!enlace) pendientes.Push(c);
                }
            }
        }

        static List<string> Subcarpetas(string raiz)
        {
            try { return Directory.Exists(raiz) ? Directory.GetDirectories(raiz).ToList() : new List<string>(); }
            catch (Exception) { return new List<string>(); }
        }

        // Cada perfil del navegador tiene su propia caché
        static IEnumerable<string> CacheChromium(string datos)
        {
            string raiz = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), datos);
            return Subcarpetas(raiz).Select(d => Path.Combine(d, @"Cache\Cache_Data")).Where(Directory.Exists).ToList();
        }

        static ZonaLimpieza Navegador(string nombre, string[] procesos, IEnumerable<string> rutas)
        {
            var z = new ZonaLimpieza { Nombre = "Caché de " + nombre, Rutas = rutas.ToList(), Procesos = procesos };
            if (Abierto(procesos)) { z.Bloqueada = true; z.Nota = "Cerrá " + nombre + " para limpiarla."; }
            return z;
        }

        public static List<ZonaLimpieza> Analizar()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string datos = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var firefox = Path.Combine(local, @"Mozilla\Firefox\Profiles");
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var cacheFirefox = Subcarpetas(firefox).Select(d => Path.Combine(d, "cache2")).Where(Directory.Exists).ToList();
            var z = new List<ZonaLimpieza> {
                new ZonaLimpieza { Nombre = "Papelera de reciclaje", Papelera = true },
                new ZonaLimpieza { Nombre = "Archivos temporales de tu usuario", Rutas = { Path.GetTempPath() } },
                new ZonaLimpieza { Nombre = "Archivos temporales de Windows", Rutas = { Path.Combine(win, "Temp") } },
                new ZonaLimpieza { Nombre = "Descargas de Windows Update", Rutas = { Path.Combine(win, @"SoftwareDistribution\Download") } },
                new ZonaLimpieza { Nombre = "Caché de Optimización de entrega", Rutas = { Path.Combine(win, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache") } },
                new ZonaLimpieza { Nombre = "Volcados de fallos", Rutas = { Path.Combine(win, "Minidump") }, Nota = "Sirven para diagnosticar pantallas azules recientes." },
                new ZonaLimpieza { Nombre = "Informes de errores antiguos", Rutas = { Path.Combine(datos, @"Microsoft\Windows\WER\ReportArchive") } },
                Navegador("Chrome", new[] { "chrome" }, CacheChromium(@"Google\Chrome\User Data")),
                Navegador("Edge", new[] { "msedge" }, CacheChromium(@"Microsoft\Edge\User Data")),
                Navegador("Brave", new[] { "brave" }, CacheChromium(@"BraveSoftware\Brave-Browser\User Data")),
                Navegador("Firefox", new[] { "firefox" }, cacheFirefox)
            };
            bool actualizando = Abierto("TiWorker", "wuauclt", "MoUsoCoreWorker");
            foreach (var a in z)
            {
                if (a.Papelera)
                {
                    var i = new INFO_PAPELERA { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(INFO_PAPELERA)) };
                    if (SHQueryRecycleBin(null, ref i) == 0) { a.Bytes = i.i64Size; a.Archivos = i.i64NumItems; }
                    continue;
                }
                if (a.Rutas.Any(r => r.Contains("SoftwareDistribution") || r.Contains("DeliveryOptimization")) && actualizando) { a.Bloqueada = true; a.Nota = "Windows está actualizando: probá más tarde."; }
                foreach (var ruta in a.Rutas)
                    foreach (var f in ArchivosDe(ruta))
                    {
                        try { a.Bytes += new FileInfo(f).Length; a.Archivos++; } catch { }
                    }
            }
            return z;
        }

        // Los archivos en uso se saltan: no se fuerza nada
        public static long Limpiar(ZonaLimpieza z)
        {
            if (z.Bloqueada) return 0;
            if (z.Papelera)
            {
                // Sin confirmación, sonido ni progreso: la app ya pidió confirmación
                if (SHEmptyRecycleBin(IntPtr.Zero, null, 7) != 0) throw new InvalidOperationException("Windows no pudo vaciar la papelera.");
                return z.Bytes;
            }
            // El navegador pudo abrirse después del análisis
            if (Abierto(z.Procesos)) throw new InvalidOperationException("Cerrá el navegador para limpiar su caché.");
            long liberado = 0;
            foreach (var ruta in z.Rutas)
                foreach (var f in ArchivosDe(ruta).ToList())
                {
                    try { long t = new FileInfo(f).Length; File.Delete(f); liberado += t; } catch { }
                }
            return liberado;
        }
    }

    public class AppQuitable { public string Nombre, Paquete, Grupo, Aviso; public bool Instalada, Quitada; }

    public class AppQuitada { public string Paquete { get; set; } public string Nombre { get; set; } public string Fecha { get; set; } public bool ParaNuevas { get; set; } }

    // Solo se ofrecen apps de esta lista: los paquetes del sistema, de seguridad y los que usan otras apps
    // (VCLibs, .NET, UI.Xaml, WebView2, Store, App Installer...) nunca aparecen
    public static class Apps
    {
        // Grupo: recomendado (se marca solo), opcional (lo usa alguna gente), xbox (lo necesitan Game Pass y algunos juegos)
        static readonly string[,] Lista = {
            {"Copilot","Microsoft.Copilot","recomendado",""},
            {"Noticias","Microsoft.BingNews","recomendado",""}, {"El Tiempo","Microsoft.BingWeather","recomendado",""}, {"Búsqueda de Bing","Microsoft.BingSearch","recomendado",""},
            {"Obtener ayuda","Microsoft.GetHelp","recomendado",""}, {"Consejos","Microsoft.Getstarted","recomendado",""}, {"Microsoft 365 (acceso directo)","Microsoft.MicrosoftOfficeHub","recomendado",""},
            {"Solitaire Collection","Microsoft.MicrosoftSolitaireCollection","recomendado",""}, {"Personas","Microsoft.People","recomendado",""},
            {"Centro de opiniones","Microsoft.WindowsFeedbackHub","recomendado",""}, {"Mapas","Microsoft.WindowsMaps","recomendado",""}, {"Películas y TV","Microsoft.ZuneVideo","recomendado",""},
            {"Clipchamp","Clipchamp.Clipchamp","recomendado",""}, {"Teams (personal)","MSTeams","recomendado",""}, {"Teams (personal, versión vieja)","MicrosoftTeams","recomendado",""},
            {"Cortana","Microsoft.549981C3F5F10","recomendado",""}, {"Power Automate","Microsoft.PowerAutomateDesktop","recomendado",""}, {"Family","MicrosoftCorporationII.MicrosoftFamily","recomendado",""},
            {"Dev Home","Microsoft.Windows.DevHome","recomendado",""}, {"Portal de realidad mixta","Microsoft.MixedReality.Portal","recomendado",""}, {"Visor 3D","Microsoft.Microsoft3DViewer","recomendado",""},
            {"Skype","Microsoft.SkypeApp","recomendado",""}, {"Correo y Calendario (versión vieja)","microsoft.windowscommunicationsapps","recomendado",""}, {"LinkedIn","7EE7776C.LinkedInforWindows","recomendado",""},
            {"Disney+ (promoción)","Disney.37853FA22FAF4","recomendado",""}, {"TikTok (promoción)","BytedancePte.Ltd.TikTok","recomendado",""}, {"Candy Crush Saga (promoción)","king.com.CandyCrushSaga","recomendado",""},
            {"Candy Crush Soda Saga (promoción)","king.com.CandyCrushSodaSaga","recomendado",""}, {"Facebook (promoción)","Facebook.Facebook","recomendado",""}, {"Prime Video (promoción)","AmazonVideo.PrimeVideo","recomendado",""},
            {"To Do","Microsoft.Todos","opcional",""}, {"Sticky Notes","Microsoft.MicrosoftStickyNotes","opcional",""}, {"Grabadora de sonido","Microsoft.WindowsSoundRecorder","opcional",""},
            {"Alarmas y reloj","Microsoft.WindowsAlarms","opcional",""}, {"Cámara","Microsoft.WindowsCamera","opcional","Algunas videollamadas y escáneres de documentos la usan."},
            {"Reproductor multimedia","Microsoft.ZuneMusic","opcional","Es el reproductor de música y video de Windows."}, {"Enlace móvil","Microsoft.YourPhone","opcional",""},
            {"Outlook nuevo","Microsoft.OutlookForWindows","opcional",""}, {"OneNote","Microsoft.Office.OneNote","opcional",""}, {"Asistencia rápida","MicrosoftCorporationII.QuickAssist","opcional","Sirve para que alguien te ayude a distancia."},
            {"Spotify","SpotifyAB.SpotifyMusic","opcional",""}, {"Netflix","4DF9E0F8.Netflix","opcional",""},
            {"Widgets","MicrosoftWindows.Client.WebExperience","opcional","El panel de widgets deja de funcionar."},
            {"App de Xbox","Microsoft.GamingApp","xbox","Hace falta para Game Pass y los juegos de la app de Xbox."},
            {"Xbox Game Bar","Microsoft.XboxGamingOverlay","xbox","Es el panel de Windows + G: capturas, rendimiento y chat."},
            {"Inicio de sesión de Xbox","Microsoft.XboxIdentityProvider","xbox","Lo usan los juegos que inician sesión con Xbox, como Minecraft Bedrock y algunos de Microsoft Store."},
            {"Ventanas de Xbox","Microsoft.Xbox.TCUI","xbox","Lo usan los juegos con cuenta de Xbox para invitaciones y perfiles."},
            {"Voz a texto de Xbox","Microsoft.XboxSpeechToTextOverlay","xbox",""}
        };

        public static readonly string[] MejorDejar = { "Microsoft Store", "Calculadora", "Fotos", "Bloc de notas", "Terminal", "Recortes", "Paint", "Instalador de aplicación (winget)", "Seguridad de Windows" };

        static string RutaRegistro { get { return Path.Combine(Medicion.Carpeta, "apps_quitadas.json"); } }

        // Solo para pruebas: reemplaza la consulta a Windows
        public static Func<HashSet<string>> Instaladas = () =>
        {
            int c;
            return new HashSet<string>(Util.Ps("Get-AppxPackage | ForEach-Object { $_.Name }", out c).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
        };
        public static bool Simular;

        public static List<AppQuitada> Quitadas()
        {
            try { return (File.Exists(RutaRegistro) ? new JavaScriptSerializer().Deserialize<List<AppQuitada>>(File.ReadAllText(RutaRegistro, Encoding.UTF8)) : null) ?? new List<AppQuitada>(); }
            catch (Exception) { return new List<AppQuitada>(); }
        }

        public static List<AppQuitable> Listar()
        {
            var inst = Instaladas();
            var quitadas = new HashSet<string>(Quitadas().Select(q => q.Paquete), StringComparer.OrdinalIgnoreCase);
            var l = new List<AppQuitable>();
            for (int i = 0; i < Lista.GetLength(0); i++)
            {
                bool esta = inst.Contains(Lista[i, 1]);
                if (!esta && !quitadas.Contains(Lista[i, 1])) continue;
                l.Add(new AppQuitable { Nombre = Lista[i, 0], Paquete = Lista[i, 1], Grupo = Lista[i, 2], Aviso = Lista[i, 3], Instalada = esta, Quitada = !esta });
            }
            return l;
        }

        // Para tu cuenta y, si lo marcás, también para las cuentas nuevas de esta PC
        public static void Quitar(AppQuitable a, bool paraNuevas)
        {
            if (!Lista.Cast<string>().Contains(a.Paquete)) throw new InvalidOperationException("Esa app no está en la lista de las que se pueden quitar.");
            if (!Simular)
            {
                int c;
                Util.Ps("Get-AppxPackage -Name '" + a.Paquete + "' | Remove-AppxPackage -ErrorAction Stop", out c, 300000);
                if (c != 0) throw new InvalidOperationException("Windows no permitió quitar " + a.Nombre + ".");
                if (paraNuevas)
                    Util.Ps("Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq '" + a.Paquete + "' } | Remove-AppxProvisionedPackage -Online -ErrorAction Stop | Out-Null", out c, 300000);
            }
            var l = Quitadas().Where(q => !q.Paquete.Equals(a.Paquete, StringComparison.OrdinalIgnoreCase)).ToList();
            l.Add(new AppQuitada { Paquete = a.Paquete, Nombre = a.Nombre, Fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm"), ParaNuevas = paraNuevas });
            Util.EscribirSeguro(RutaRegistro, new JavaScriptSerializer().Serialize(l), Encoding.UTF8, null);
        }

        // Cuando la reinstalás desde la Tienda, deja de figurar como quitada
        public static void OlvidarSiVolvio()
        {
            var inst = Instaladas();
            var l = Quitadas();
            var quedan = l.Where(q => !inst.Contains(q.Paquete)).ToList();
            if (quedan.Count != l.Count) Util.EscribirSeguro(RutaRegistro, new JavaScriptSerializer().Serialize(quedan), Encoding.UTF8, null);
        }

        public static string EnlaceTienda(AppQuitable a) { return "ms-windows-store://search/?query=" + Uri.EscapeDataString(a.Nombre.Split('(')[0].Trim()); }

        // ---------- Edge ----------
        // Solo el desinstalador del propio Edge. WebView2, que usan otras apps, está en otra carpeta y no se toca
        public static string InstaladorEdge()
        {
            string app = new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles }.Select(f => Path.Combine(Environment.GetFolderPath(f), @"Microsoft\Edge\Application")).FirstOrDefault(Directory.Exists);
            if (app == null) return null;
            return Directory.GetDirectories(app).Select(d => Path.Combine(d, @"Installer\setup.exe")).Where(File.Exists)
                .OrderByDescending(r => { System.Version v; return System.Version.TryParse(Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(r))), out v) ? v : new System.Version(0, 0); }).FirstOrDefault();
        }

        // Según la versión de Windows, Edge está en Archivos de programa o en Archivos de programa (x86)
        public static bool EdgeInstalado { get { return new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles }.Any(f => File.Exists(Path.Combine(Environment.GetFolderPath(f), @"Microsoft\Edge\Application\msedge.exe"))); } }

        public static void DesinstalarEdge()
        {
            string setup = InstaladorEdge();
            if (setup == null) throw new InvalidOperationException("No se encontró el desinstalador de Edge.");
            int c;
            Util.Correr(setup, "--uninstall --system-level --force-uninstall", out c, 300000);
            if (EdgeInstalado) throw new InvalidOperationException("Windows no permitió desinstalar Edge. En la mayoría de los países solo se puede desinstalar en Europa; usá \"Desactivar lo molesto de Edge\" y elegí otro navegador como predeterminado.");
        }

        public static void ReinstalarEdge()
        {
            int c;
            Util.Correr("winget", "install --id Microsoft.Edge --exact --source winget --silent --accept-package-agreements --accept-source-agreements --disable-interactivity", out c, 600000);
            if (c != 0 && !EdgeInstalado) throw new InvalidOperationException("No se pudo reinstalar Edge (código " + c + "). Bajalo de microsoft.com/edge.");
        }
    }

    public static class Version
    {
        public const string Actual = "1.0.3";
        static string RutaConfig { get { return Path.Combine(Medicion.Carpeta, "actualizaciones.txt"); } }
        public const string Repositorio = "Pompecca/FrameLab";
        public const string PaginaDescargas = "https://github.com/" + Repositorio + "/releases";
        public const string Cafecito = "https://cafecito.app/framelabapp";
        public const string Reportar = "https://github.com/" + Repositorio + "/issues/new/choose";

        public class Nueva { public System.Version Numero; public string Notas, UrlExe; public long Tamaño; public string Sha256; public bool Privado; }

        static WebClient Cliente()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var w = new WebClient { Encoding = Encoding.UTF8 };
            w.Headers[HttpRequestHeader.UserAgent] = "FrameLab/" + Actual;
            w.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return w;
        }

        // Fuente por defecto: la última versión publicada en GitHub. Devuelve null si no hay nada más nuevo
        public static Nueva BuscarEnGitHub()
        {
            string json;
            try { using (var w = Cliente()) json = w.DownloadString("https://api.github.com/repos/" + Repositorio + "/releases"); }
            catch (WebException ex)
            {
                var r = ex.Response as HttpWebResponse;
                // Un repositorio privado responde como si no existiera
                if (r != null && (r.StatusCode == HttpStatusCode.NotFound || r.StatusCode == HttpStatusCode.Unauthorized || r.StatusCode == HttpStatusCode.Forbidden)) return new Nueva { Privado = true };
                throw new InvalidOperationException("No se pudo conectar con GitHub. Revisá la conexión a internet.");
            }
            // Las versiones beta se publican como "prerelease": se toma la más nueva que no sea borrador
            var lista = new JavaScriptSerializer().Deserialize<List<Dictionary<string, object>>>(json) ?? new List<Dictionary<string, object>>();
            Nueva mejor = null;
            foreach (var r in lista)
            {
                if (r.ContainsKey("draft") && Convert.ToBoolean(r["draft"])) continue;
                System.Version v;
                if (!System.Version.TryParse(Convert.ToString(r["tag_name"]).TrimStart('v', 'V'), out v)) continue;
                var exe = (r["assets"] as System.Collections.ArrayList ?? new System.Collections.ArrayList()).OfType<Dictionary<string, object>>()
                    .FirstOrDefault(a => Convert.ToString(a["name"]).Equals("FrameLab.exe", StringComparison.OrdinalIgnoreCase));
                if (exe == null || (mejor != null && v <= mejor.Numero)) continue;
                string digest = exe.ContainsKey("digest") ? Convert.ToString(exe["digest"]) : "";
                mejor = new Nueva { Numero = v, Notas = Convert.ToString(r["body"]), UrlExe = Convert.ToString(exe["browser_download_url"]), Tamaño = Convert.ToInt64(exe["size"]), Sha256 = digest.StartsWith("sha256:") ? digest.Substring(7) : null };
            }
            return mejor != null && mejor.Numero > new System.Version(Actual) ? mejor : null;
        }

        // Baja el exe nuevo, lo comprueba y lo pone en lugar del actual. Si algo falla, deja todo como estaba
        public static void Instalar(Nueva n, string exe = null)
        {
            if (exe == null) exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string nuevo = exe + ".nuevo", viejo = Path.Combine(Path.GetDirectoryName(exe), Path.GetFileNameWithoutExtension(exe) + ".old.exe");
            if (File.Exists(nuevo)) File.Delete(nuevo);
            using (var w = Cliente()) w.DownloadFile(n.UrlExe, nuevo);
            try
            {
                var info = new FileInfo(nuevo);
                if (info.Length == 0 || (n.Tamaño > 0 && info.Length != n.Tamaño)) throw new InvalidOperationException("La descarga quedó incompleta.");
                if (n.Sha256 != null)
                    using (var s = File.OpenRead(nuevo))
                    using (var h = System.Security.Cryptography.SHA256.Create())
                        if (!string.Concat(h.ComputeHash(s).Select(b => b.ToString("x2"))).Equals(n.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("El archivo descargado no coincide con el publicado.");
                // Tiene que ser FrameLab y de la versión anunciada
                var nombre = System.Reflection.AssemblyName.GetAssemblyName(nuevo);
                if (!nombre.Name.Equals("FrameLab", StringComparison.OrdinalIgnoreCase) || new System.Version(nombre.Version.Major, nombre.Version.Minor, nombre.Version.Build) != new System.Version(n.Numero.Major, n.Numero.Minor, Math.Max(0, n.Numero.Build)))
                    throw new InvalidOperationException("El archivo descargado no es la versión " + n.Numero + " de FrameLab.");
            }
            catch (BadImageFormatException) { File.Delete(nuevo); throw new InvalidOperationException("El archivo descargado no es un programa válido."); }
            catch { File.Delete(nuevo); throw; }

            // Windows deja renombrar un programa abierto: el viejo se borra la próxima vez que se abre la app
            if (File.Exists(viejo)) File.Delete(viejo);
            File.Move(exe, viejo);
            try { File.Move(nuevo, exe); }
            catch { File.Move(viejo, exe); throw; }
        }

        public static void BorrarVersionVieja()
        {
            try
            {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string viejo = Path.Combine(Path.GetDirectoryName(exe), Path.GetFileNameWithoutExtension(exe) + ".old.exe");
                if (File.Exists(viejo)) File.Delete(viejo);
            }
            catch (Exception) { }
        }

        public static bool HayConfiguracionPropia { get { return File.Exists(RutaConfig) && File.ReadAllText(RutaConfig).Trim() != ""; } }

        // Alternativa: una URL propia en actualizaciones.txt que devuelva {"version":"x.y.z","descarga":"https://..."}
        public static string Buscar()
        {
            if (!File.Exists(RutaConfig) || File.ReadAllText(RutaConfig).Trim() == "")
                return "Todavía no hay un servidor de actualizaciones configurado. Tenés la versión " + Actual + ".";
            string url = File.ReadAllText(RutaConfig).Trim();
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return "La dirección de actualizaciones debe empezar con https://.";
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var w = new WebClient { Encoding = Encoding.UTF8 })
            {
                var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(w.DownloadString(url));
                var nueva = new System.Version(Convert.ToString(d["version"]));
                if (nueva > new System.Version(Actual)) return "Hay una versión nueva: " + nueva + ". Descarga: " + d["descarga"];
                return "Tenés la última versión (" + Actual + ").";
            }
        }
    }
}
