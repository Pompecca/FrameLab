using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OptimizadorPC
{
    public class Freno { public string Titulo, Detalle; public int Peso; }

    // Qué limita los juegos en esta PC, de lo más a lo menos importante. Es orientativo: no mide, deduce del hardware
    public static class Frenos
    {
        // Gama aproximada para jugar en 1080p: 1 muy débil, 2 débil, 3 media, 4 buena, 5 alta.
        // Se revisa de la más débil a la más fuerte: el primer patrón que coincide decide
        static readonly string[,] Gamas = {
            {"1", @"\bGT \d{3,4}\b|\bGT\s?7[1-3]0|GT 1030|\bHD Graphics|\bUHD Graphics|Iris|Vega \d\b|Radeon(\(TM\))? Graphics|Radeon R[2-7]\b|Radeon HD|GeForce [2-9]\d{2}M?\b|Microsoft Basic"},
            {"2", @"GTX 7[45]0|GTX 6\d0|GTX 9[45]0|GTX 1050(?! Ti)|RX 4[56]0|RX 5[56]0\b|Arc A3"},
            {"3", @"GTX 1050 Ti|GTX 9[678]0|GTX 1060|GTX 1650|RX 4[78]0|RX 5[789]0\b|RX 6400|RX 6500|Arc A5"},
            {"4", @"GTX 1660|GTX 10[78]0|RTX 20\d0|RTX 3050|RTX 3060|RX 5[5-7]00|RX 66\d0|Arc A7"}
        };

        public static int Gama(string gpu)
        {
            for (int i = 0; i < Gamas.GetLength(0); i++)
                if (Regex.IsMatch(gpu, Gamas[i, 1], RegexOptions.IgnoreCase)) return int.Parse(Gamas[i, 0]);
            return 5;
        }

        // Con integrada y dedicada, cuenta la mejor
        public static int MejorGama(IEnumerable<string> gpus)
        {
            var l = gpus.Select(Gama).ToList();
            return l.Any() ? l.Max() : 3;
        }

        const string Mejoras = "Opciones usadas que suelen rendir mucho más (los precios cambian mucho: compará y pedí que te la muestren funcionando):\n" +
            "• GTX 1650 o GTX 1050 Ti: muchas no necesitan cable de la fuente. Sirven aunque la fuente sea chica.\n" +
            "• RX 570, RX 580 o GTX 1660: más potentes, pero necesitan un cable de 6 u 8 pines y una fuente de buena calidad de unos 450 W.\n" +
            "Todas van en la ranura PCIe x16. Revisá que entren en el gabinete.";

        public static List<Freno> Analizar(Diagnostico d, List<Aviso> avisos, int inicioActivos)
        {
            var l = new List<Freno>();
            Action<int, string, string> add = (p, t, x) => l.Add(new Freno { Peso = p, Titulo = t, Detalle = x });
            Func<string, Aviso> aviso = t => avisos.FirstOrDefault(a => a.Nivel == "aviso" && a.Titulo.StartsWith(t));

            int gama = MejorGama(d.Gpus);
            string gpu = d.Gpus.OrderByDescending(Gama).FirstOrDefault() ?? "tu placa de video";
            if (gama == 1) add(100, "La placa de video es muy débil para jugar", gpu + " solo sirve para juegos livianos en calidad mínima. Ningún ajuste de Windows lo compensa: lo que más se nota es bajar la calidad dentro de cada juego.\n\n" + Mejoras);
            else if (gama == 2) add(90, "La placa de video es lo que más limita", gpu + " es de gama baja: en juegos actuales hay que jugar en calidad baja y con escalado.\n\n" + Mejoras);
            else if (gama == 3) add(40, "Placa de video de gama media de entrada", gpu + " alcanza para 1080p en calidad baja o media en la mayoría de los juegos competitivos.");

            var espacio = aviso("Poco espacio");
            if (espacio != null) add(85, "Disco casi lleno", espacio.Detalle.Replace(" Liberá espacio.", "") + ". Con tan poco lugar, Windows y los juegos pueden trabarse o fallar al actualizarse. Usá Espacio en disco para ver qué ocupa más y Limpieza para borrar lo seguro.");
            if (d.TieneHdd) add(70, "Hay un disco mecánico (HDD)", "Si los juegos están ahí, cargan lento y dan tirones. Pasalos a un SSD.");

            var apagados = aviso("Apagados inesperados");
            if (apagados != null) add(80, "La PC se apagó sola", apagados.Detalle + " Antes de optimizar conviene resolverlo: revisá la temperatura y la fuente.");

            if (d.RamGb > 0 && d.RamGb < 7.5) add(75, "Poca memoria RAM", Math.Round(d.RamGb) + " GB. Para jugar hoy conviene tener 16 GB, o al menos 8 GB.");
            else if (aviso("RAM en un solo canal") != null) add(50, "RAM en un solo canal", "Con un solo módulo los mínimos de FPS bajan. Un segundo módulo igual al que tenés ayuda bastante.");

            if (d.Hilos > 0 && d.Hilos <= 4) add(gama >= 3 ? 80 : 55, "Procesador de pocos núcleos", d.Nucleos + " núcleos y " + d.Hilos + " hilos. En juegos como Fortnite limita los mínimos" + (gama <= 2 ? ", y con una placa de video mejor pasaría a ser el próximo límite" : "") + ". Cerrar programas de fondo ayuda.");

            if (aviso("Driver gráfico antiguo") != null) add(45, "Driver de video viejo", "Instalá el último driver oficial para tu placa desde la sección Instalar.");
            var monitor = aviso("El monitor no va");
            if (monitor != null) add(40, "El monitor no va a su máximo", monitor.Detalle);
            if (inicioActivos >= 6) add(30, inicioActivos + " programas arrancan con Windows", "Desactivá en Programas al iniciar los que no uses siempre: liberan memoria y CPU al jugar.");
            return l.OrderByDescending(f => f.Peso).ToList();
        }

        // Sé honesto sobre cuánto ayudan los ajustes de Windows
        public static string Conclusion(List<Freno> frenos)
        {
            if (frenos.Any(f => f.Peso >= 90)) return "En esta PC los ajustes de Windows van a ayudar poco: lo que más se nota es la configuración de cada juego y, si podés, cambiar la placa de video.";
            if (frenos.Any(f => f.Peso >= 70)) return "Primero resolvé lo de arriba. Los ajustes de Windows suman un poco, sobre todo en los mínimos de FPS.";
            return "No hay un límite claro de hardware. Los ajustes de Windows y un límite de FPS estable son lo que queda por hacer.";
        }
    }

    public class Carpeta { public string Nombre, Ruta; public long Bytes; }

    // Solo mide y abre carpetas: nunca borra nada
    public static class Espacio
    {
        static long Medir(string ruta)
        {
            long t = 0;
            foreach (var f in Limpieza.ArchivosDe(ruta))
            {
                try { t += new FileInfo(f).Length; } catch { }
            }
            return t;
        }

        static IEnumerable<string> Hijas(string ruta)
        {
            try { return Directory.Exists(ruta) ? Directory.GetDirectories(ruta).Where(d => (File.GetAttributes(d) & FileAttributes.ReparsePoint) == 0).ToList() : new List<string>(); }
            catch (Exception) { return new List<string>(); }
        }

        // Steam puede tener bibliotecas en otros discos: se leen de su archivo de configuración
        static IEnumerable<string> BibliotecasSteam()
        {
            string steam = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            var l = new List<string> { Path.Combine(steam, @"steamapps\common") };
            try
            {
                string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"(.+?)\""))
                        l.Add(Path.Combine(m.Groups[1].Value.Replace("\\\\", "\\"), @"steamapps\common"));
            }
            catch (Exception) { }
            return l.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public static List<Carpeta> Analizar()
        {
            string perfil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var c = new List<Carpeta>();
            Action<string, string> add = (n, r) => { if (Directory.Exists(r)) c.Add(new Carpeta { Nombre = n, Ruta = r }); };
            foreach (var d in Hijas(perfil).Where(x => !Path.GetFileName(x).Equals("AppData", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(x).StartsWith(".")))
                add("Tu carpeta " + Path.GetFileName(d), d);
            foreach (var d in Hijas(Path.Combine(perfil, "Downloads"))) add("En Descargas: " + Path.GetFileName(d), d);
            add("Datos de programas (AppData Local)", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            add("Datos de programas (AppData Roaming)", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            foreach (var b in BibliotecasSteam()) foreach (var d in Hijas(b)) add("Juego de Steam: " + Path.GetFileName(d), d);
            foreach (var d in Hijas(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Epic Games"))) add("Juego de Epic: " + Path.GetFileName(d), d);
            add("Minecraft (Java)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft"));
            add("Roblox", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox"));
            foreach (var x in c) x.Bytes = Medir(x.Ruta);
            return c.Where(x => x.Bytes > 0).OrderByDescending(x => x.Bytes).Take(25).ToList();
        }

        public static string Discos()
        {
            return string.Join("\n", DriveInfo.GetDrives().Where(u => u.DriveType == DriveType.Fixed && u.IsReady)
                .Select(u => u.Name.TrimEnd('\\') + "  " + Math.Round(u.AvailableFreeSpace / 1e9) + " GB libres de " + Math.Round(u.TotalSize / 1e9) + " GB"));
        }

        public static void Abrir(string ruta)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + ruta + "\"") { UseShellExecute = true });
        }
    }
}
