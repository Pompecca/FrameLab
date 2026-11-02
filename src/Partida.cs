using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace OptimizadorPC
{
    public class AppFondo { public string Proceso, Nombre; public long Bytes; }

    // Lo que hay que deshacer al terminar. Se guarda en disco para restaurarlo aunque la app se cierre de golpe
    public class EstadoPartida
    {
        public string PlanAnterior { get; set; }
        public bool BusquedaDetenida { get; set; }
        public List<string> Reabrir { get; set; }
        public List<int> Subidos { get; set; }
        public List<int> Bajados { get; set; }
    }

    // Modo partida: cierra los programas que elegís, sube la prioridad del juego, baja la de launchers y navegadores que queden abiertos,
    // pausa la indexación de búsqueda y activa el plan de alto rendimiento. Todo es temporal y se deshace al terminar.
    // No toca procesos de Windows, de seguridad ni antitrampas, ni la memoria o los archivos de los juegos
    public static class Partida
    {
        // Solo programas de usuario conocidos: nunca se ofrece cerrar algo del sistema
        public static readonly string[,] Candidatos = {
            {"chrome","Google Chrome"}, {"msedge","Microsoft Edge"}, {"firefox","Firefox"}, {"opera","Opera"}, {"brave","Brave"},
            {"Discord","Discord"}, {"Spotify","Spotify"}, {"WhatsApp","WhatsApp"}, {"Telegram","Telegram"}, {"ms-teams","Teams"}, {"Teams","Teams (clásico)"},
            {"OneDrive","OneDrive"}, {"EpicGamesLauncher","Epic Games Launcher"}, {"steam","Steam"}, {"EADesktop","EA app"}, {"Battle.net","Battle.net"}
        };
        // Si quedan abiertos se les baja la prioridad. Los de voz y música no: se cortarían
        static readonly string[] Bajables = { "chrome", "msedge", "firefox", "opera", "brave", "OneDrive", "EpicGamesLauncher", "steam", "EADesktop", "Battle.net" };

        // Nunca tiempo real: puede congelar el mouse y el teclado
        public static readonly string[] JuegosConocidos = { "FortniteClient-Win64-Shipping", "RobloxPlayerBeta", "Minecraft.Windows", "javaw", "cs2", "VALORANT-Win64-Shipping", "GTA5", "GTA5_Enhanced", "RocketLeague", "r5apex" };
        const string PlanAlto = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", PlanMaximo = "e9a42b02-d5df-448d-aa00-03f14749eb61";

        // Solo para pruebas: no cambia el plan de energía ni la búsqueda y no vuelve a abrir programas
        public static bool Simular;
        public static string RutaEstado { get { return Path.Combine(Medicion.Carpeta, "partida.json"); } }
        public static bool Activo { get { return estado != null; } }
        static EstadoPartida estado;

        public static List<string> JuegosExtra()
        {
            return (Util.Preferencia("juegosExtra") ?? "").Split(',').Select(x => x.Trim()).Where(x => x != "").ToList();
        }

        public static void GuardarJuegosExtra(string texto)
        {
            // Se acepta "juego.exe" o "juego"; solo letras, números y signos comunes de nombres de archivo
            var l = texto.Split(',', ';', '\n').Select(x => Regex.Replace(x.Trim(), @"\.exe$", "", RegexOptions.IgnoreCase)).Where(x => Regex.IsMatch(x, @"^[\w .()\-]+$")).Distinct(StringComparer.OrdinalIgnoreCase);
            Util.GuardarPreferencia("juegosExtra", string.Join(",", l));
        }

        public static List<AppFondo> Listar()
        {
            var l = new List<AppFondo>();
            for (int i = 0; i < Candidatos.GetLength(0); i++)
            {
                var ps = Process.GetProcessesByName(Candidatos[i, 0]);
                if (ps.Length == 0) continue;
                long b = 0;
                foreach (var p in ps) { try { b += p.WorkingSet64; } catch (Exception) { } p.Dispose(); }
                l.Add(new AppFondo { Proceso = Candidatos[i, 0], Nombre = Candidatos[i, 1], Bytes = b });
            }
            return l.OrderByDescending(a => a.Bytes).ToList();
        }

        static void Guardar()
        {
            if (estado == null) { if (File.Exists(RutaEstado)) File.Delete(RutaEstado); return; }
            Util.EscribirSeguro(RutaEstado, new JavaScriptSerializer().Serialize(estado), Encoding.UTF8, null);
        }

        static string PlanActual()
        {
            int c;
            var m = Regex.Match(Util.Correr("powercfg", "/getactivescheme", out c), "[0-9a-fA-F-]{36}");
            return m.Success ? m.Value.ToLowerInvariant() : null;
        }

        // Devuelve cuánta memoria usaban los programas cerrados
        public static long Empezar(IEnumerable<string> cerrar)
        {
            if (estado != null) throw new InvalidOperationException("El modo partida ya está activo.");
            estado = new EstadoPartida { Reabrir = new List<string>(), Subidos = new List<int>(), Bajados = new List<int>() };
            if (!Simular)
            {
                int c;
                string plan = PlanActual();
                if (plan != null && plan != PlanAlto && plan != PlanMaximo)
                {
                    Util.Correr("powercfg", "/setactive " + PlanAlto, out c);
                    if (c == 0) estado.PlanAnterior = plan;
                }
                string sc = Util.Correr("sc", "query WSearch", out c);
                if (c == 0 && sc.Contains("RUNNING"))
                {
                    Util.Correr("sc", "stop WSearch", out c);
                    estado.BusquedaDetenida = c == 0;
                }
            }
            Guardar();
            long liberado = 0;
            foreach (var nombre in cerrar)
            {
                var ps = Process.GetProcessesByName(nombre);
                if (ps.Length == 0) continue;
                string ruta = null;
                // Primero se le pide a cada programa que se cierre solo; si queda junto al reloj, se cierra
                foreach (var p in ps)
                {
                    try { liberado += p.WorkingSet64; if (ruta == null) ruta = p.MainModule.FileName; } catch (Exception) { }
                    try { p.CloseMainWindow(); } catch (Exception) { }
                }
                for (int i = 0; i < 30 && Process.GetProcessesByName(nombre).Any(); i++) Thread.Sleep(200);
                foreach (var p in Process.GetProcessesByName(nombre))
                {
                    try { p.Kill(); } catch (Exception) { }
                    p.Dispose();
                }
                if (ruta != null && !estado.Reabrir.Contains(ruta)) estado.Reabrir.Add(ruta);
            }
            Guardar();
            return liberado;
        }

        // Se llama cada pocos segundos mientras dura. Devuelve los juegos que encontró
        public static List<string> Vigilar(bool gpuAlta, bool sinOptimizacionPantalla = false)
        {
            var encontrados = new List<string>();
            if (estado == null) return encontrados;
            bool cambio = false;
            foreach (var nombre in JuegosConocidos.Concat(JuegosExtra()))
                foreach (var p in Process.GetProcessesByName(nombre))
                {
                    try
                    {
                        if (nombre == "javaw" && p.MainWindowTitle.IndexOf("Minecraft", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        encontrados.Add(nombre);
                        if (!estado.Subidos.Contains(p.Id) && p.PriorityClass == ProcessPriorityClass.Normal)
                        {
                            p.PriorityClass = ProcessPriorityClass.High;
                            estado.Subidos.Add(p.Id);
                            cambio = true;
                        }
                        if (gpuAlta) GpuAlta(p.MainModule.FileName);
                        if (sinOptimizacionPantalla) PantallaCompleta(p.MainModule.FileName);
                    }
                    catch (Exception) { }
                    finally { p.Dispose(); }
                }
            if (encontrados.Any())
                foreach (var nombre in Bajables)
                    foreach (var p in Process.GetProcessesByName(nombre))
                    {
                        try
                        {
                            if (!estado.Bajados.Contains(p.Id) && p.PriorityClass == ProcessPriorityClass.Normal)
                            {
                                p.PriorityClass = ProcessPriorityClass.BelowNormal;
                                estado.Bajados.Add(p.Id);
                                cambio = true;
                            }
                        }
                        catch (Exception) { }
                        finally { p.Dispose(); }
                    }
            if (cambio) Guardar();
            return encontrados.Distinct().ToList();
        }

        static void Prioridad(IEnumerable<int> ids, ProcessPriorityClass desde)
        {
            foreach (var id in ids)
            {
                try { using (var p = Process.GetProcessById(id)) if (p.PriorityClass == desde) p.PriorityClass = ProcessPriorityClass.Normal; }
                catch (Exception) { }
            }
        }

        // Deshace todo. reabrir=false deja cerrados los programas (por ejemplo, si se restaura días después de un corte)
        public static List<string> Terminar(bool reabrir = true)
        {
            var e = estado ?? Leer();
            var abiertos = new List<string>();
            if (e == null) return abiertos;
            Prioridad(e.Subidos ?? new List<int>(), ProcessPriorityClass.High);
            Prioridad(e.Bajados ?? new List<int>(), ProcessPriorityClass.BelowNormal);
            int c;
            if (!Simular && e.PlanAnterior != null) Util.Correr("powercfg", "/setactive " + e.PlanAnterior, out c);
            if (!Simular && e.BusquedaDetenida) Util.Correr("sc", "start WSearch", out c);
            if (reabrir && !Simular)
                foreach (var ruta in e.Reabrir ?? new List<string>())
                {
                    // Con el Explorador, para que no hereden el permiso de administrador de esta app
                    try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + ruta + "\"") { UseShellExecute = true }); abiertos.Add(Path.GetFileNameWithoutExtension(ruta)); }
                    catch (Exception) { }
                }
            estado = null;
            Guardar();
            return abiertos;
        }

        public static EstadoPartida Leer()
        {
            try { return File.Exists(RutaEstado) ? new JavaScriptSerializer().Deserialize<EstadoPartida>(File.ReadAllText(RutaEstado, Encoding.UTF8)) : null; }
            catch (Exception) { return null; }
        }

        public static List<string> PendientesDeReabrir() { var e = estado ?? Leer(); return e == null || e.Reabrir == null ? new List<string>() : e.Reabrir; }

        // ---------- Placa de video de alto rendimiento por juego (notebooks con dos placas) ----------
        const string ClaveGpu = @"Software\Microsoft\DirectX\UserGpuPreferences";
        public static string RutaClaveGpu = ClaveGpu;

        // Solo se agregan juegos sin preferencia previa, y se recuerda cuáles para poder quitarlos
        static void GpuAlta(string exe)
        {
            var hechos = AgregadosGpu();
            using (var k = Registry.CurrentUser.CreateSubKey(RutaClaveGpu))
            {
                if (k.GetValue(exe) != null) return;
                k.SetValue(exe, "GpuPreference=2;", RegistryValueKind.String);
            }
            hechos.Add(exe);
            Util.GuardarPreferencia("gpuAgregados", string.Join("|", hechos));
        }

        public static List<string> AgregadosGpu() { return (Util.Preferencia("gpuAgregados") ?? "").Split('|').Where(x => x != "").ToList(); }

        public static void QuitarGpuAlta()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RutaClaveGpu, true))
                if (k != null)
                    foreach (var exe in AgregadosGpu())
                        if (Convert.ToString(k.GetValue(exe)) == "GpuPreference=2;") k.DeleteValue(exe, false);
            Util.GuardarPreferencia("gpuAgregados", "");
        }

        public static void GpuAltaPrueba(string exe) { GpuAlta(exe); }

        // ---------- Optimizaciones de pantalla completa por juego (la casilla oficial de Propiedades > Compatibilidad) ----------
        public static string RutaClaveCompat = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";
        const string SinOptimizacion = "~ DISABLEDXMAXIMIZEDWINDOWEDMODE";

        // Si el juego ya tiene opciones de compatibilidad propias, no se tocan
        public static void PantallaCompleta(string exe)
        {
            var hechos = AgregadosPantalla();
            using (var k = Registry.CurrentUser.CreateSubKey(RutaClaveCompat))
            {
                if (k.GetValue(exe) != null) return;
                k.SetValue(exe, SinOptimizacion, RegistryValueKind.String);
            }
            hechos.Add(exe);
            Util.GuardarPreferencia("pantallaAgregados", string.Join("|", hechos));
        }

        public static List<string> AgregadosPantalla() { return (Util.Preferencia("pantallaAgregados") ?? "").Split('|').Where(x => x != "").ToList(); }

        public static void QuitarPantallaCompleta()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RutaClaveCompat, true))
                if (k != null)
                    foreach (var exe in AgregadosPantalla())
                        if (Convert.ToString(k.GetValue(exe)) == SinOptimizacion) k.DeleteValue(exe, false);
            Util.GuardarPreferencia("pantallaAgregados", "");
        }
    }
}
