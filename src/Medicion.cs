using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace OptimizadorPC
{
    public class Muestra
    {
        public DateTime Hora;
        public double GpuUso = -1, GpuTemp = -1, GpuMhz = -1, GpuMemoria = -1, CpuTotal, CpuNucleo, RamMb, DiscoPct;
        public string GpuMotivos = "";
    }

    public class Resultado
    {
        public string Fecha { get; set; }
        public string Juego { get; set; }
        public double Segundos { get; set; }
        public int Frames { get; set; }
        public double Fps { get; set; }
        public double Bajo1 { get; set; }
        public double Bajo01 { get; set; }
        public int Picos { get; set; }
        public double CpuOcupada { get; set; }
        public double GpuOcupada { get; set; }
        public string Veredicto { get; set; }
    }

    public static class Muestreo
    {
        // Devuelve una muestra del sistema; las GPU que no son NVIDIA quedan en -1
        public static Muestra Tomar()
        {
            var m = new Muestra { Hora = DateTime.Now };
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT Name,PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor"))
                    foreach (ManagementObject o in s.Get())
                    {
                        double v = Convert.ToDouble(o["PercentProcessorTime"]);
                        if (Convert.ToString(o["Name"]) == "_Total") m.CpuTotal = v;
                        else if (v > m.CpuNucleo) m.CpuNucleo = v;
                    }
                using (var s = new ManagementObjectSearcher("SELECT AvailableMBytes FROM Win32_PerfFormattedData_PerfOS_Memory"))
                    foreach (ManagementObject o in s.Get()) m.RamMb = Convert.ToDouble(o["AvailableMBytes"]);
                using (var s = new ManagementObjectSearcher("SELECT PercentDiskTime FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk WHERE Name='_Total'"))
                    foreach (ManagementObject o in s.Get()) m.DiscoPct = Convert.ToDouble(o["PercentDiskTime"]);
            }
            catch { }
            string n = Salud.Nvidia("utilization.gpu,temperature.gpu,clocks.gr,memory.used,clocks_throttle_reasons.active");
            if (n != null)
            {
                var p = n.Split(',').Select(x => x.Trim()).ToArray();
                double d;
                if (p.Length == 5)
                {
                    if (double.TryParse(p[0], NumberStyles.Any, CultureInfo.InvariantCulture, out d)) m.GpuUso = d;
                    if (double.TryParse(p[1], NumberStyles.Any, CultureInfo.InvariantCulture, out d)) m.GpuTemp = d;
                    if (double.TryParse(p[2], NumberStyles.Any, CultureInfo.InvariantCulture, out d)) m.GpuMhz = d;
                    if (double.TryParse(p[3], NumberStyles.Any, CultureInfo.InvariantCulture, out d)) m.GpuMemoria = d;
                    m.GpuMotivos = Motivos(p[4]);
                }
            }
            return m;
        }

        // Bits de nvidia-smi que indican que la tarjeta se está limitando
        static string Motivos(string hex)
        {
            ulong b;
            if (!ulong.TryParse(hex.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b)) return "";
            var l = new List<string>();
            if ((b & 0x4) != 0) l.Add("límite de potencia");
            if ((b & 0x8) != 0) l.Add("freno por hardware");
            if ((b & 0x20) != 0 || (b & 0x40) != 0) l.Add("temperatura");
            if ((b & 0x80) != 0) l.Add("freno de energía");
            return string.Join(", ", l);
        }

        public static string TempCpu()
        {
            try
            {
                using (var s = new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature"))
                    foreach (ManagementObject o in s.Get())
                        return Math.Round(Convert.ToDouble(o["CurrentTemperature"]) / 10 - 273.15) + " °C (sensor de placa, aproximado)";
            }
            catch { }
            return "No disponible en esta PC";
        }
    }

    public class Medicion
    {
        const string UrlOficial = "https://github.com/GameTechDev/PresentMon/releases/download/v2.6.0/PresentMon-2.6.0-x64.exe";
        const string Sesion = "FrameLabMedicion";
        Process proceso;
        string csv;
        volatile bool midiendo;
        Thread hilo;
        readonly List<Muestra> muestras = new List<Muestra>();

        public static string Carpeta { get { return Path.GetDirectoryName(Motor.RutaRespaldo); } }
        static string RutaExe { get { return Path.Combine(Carpeta, "herramientas", "PresentMon-2.6.0-x64.exe"); } }
        public static bool HerramientaLista { get { return File.Exists(RutaExe); } }

        public static void Descargar()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaExe));
            string tmp = RutaExe + ".tmp";
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var w = new WebClient()) w.DownloadFile(UrlOficial, tmp);
            // Solo se acepta si lo firmó Intel; si no, no se ejecuta nunca
            string ps = "$s = Get-AuthenticodeSignature -LiteralPath '" + tmp.Replace("'", "''") + "'; if ($s.Status -eq 'Valid' -and $s.SignerCertificate.Subject -like '*Intel Corporation*') { exit 0 } else { exit 1 }";
            var si = new ProcessStartInfo("powershell", "-NoProfile -NonInteractive -Command \"" + ps + "\"") { UseShellExecute = false, CreateNoWindow = true };
            using (var p = Process.Start(si))
            {
                p.WaitForExit();
                if (p.ExitCode != 0) { File.Delete(tmp); throw new InvalidOperationException("La firma de la herramienta descargada no es válida."); }
            }
            if (File.Exists(RutaExe)) File.Delete(RutaExe);
            File.Move(tmp, RutaExe);
        }

        public void Iniciar()
        {
            csv = Path.Combine(Carpeta, "ultima_partida.csv");
            if (File.Exists(csv)) File.Delete(csv);
            var si = new ProcessStartInfo(RutaExe, "--output_file \"" + csv + "\" --date_time --no_console_stats --session_name " + Sesion + " --stop_existing_session --exclude dwm.exe --exclude explorer.exe") { UseShellExecute = false, CreateNoWindow = true };
            proceso = Process.Start(si);
            // Si no puede abrir la sesión de medición se cierra enseguida: mejor avisarlo ahora que al terminar la partida
            if (proceso.WaitForExit(1000))
            {
                int codigo = proceso.ExitCode;
                proceso.Dispose(); proceso = null;
                throw new InvalidOperationException("PresentMon se cerró al iniciar (código " + codigo + ").");
            }
            midiendo = true;
            muestras.Clear();
            hilo = new Thread(() =>
            {
                while (midiendo)
                {
                    try
                    {
                        var m = Muestreo.Tomar();
                        lock (muestras) muestras.Add(m);
                    }
                    catch (Exception) { }
                    Thread.Sleep(700);
                }
            }) { IsBackground = true };
            hilo.Start();
        }

        // Sin esto, PresentMon seguiría corriendo y agrandando el CSV aunque se cierre la app
        void Terminar()
        {
            midiendo = false;
            try { int c; Util.Correr(RutaExe, "--session_name " + Sesion + " --terminate_existing_session", out c, 15000); }
            catch (Exception) { }
            if (proceso == null) return;
            try { if (!proceso.WaitForExit(15000)) proceso.Kill(); }
            catch (Exception) { }
            proceso.Dispose();
            proceso = null;
        }

        public void Cancelar() { Terminar(); }

        public Resultado Detener()
        {
            Terminar();
            List<Muestra> copia;
            lock (muestras) copia = muestras.ToList();
            return Analizar(csv, copia);
        }

        public static Resultado Analizar(string ruta, List<Muestra> muestras)
        {
            if (!File.Exists(ruta)) throw new InvalidOperationException("No se generaron datos. ¿Había un juego abierto?");
            string[] lineas;
            using (var f = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var r = new StreamReader(f)) lineas = r.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lineas.Length < 2) throw new InvalidOperationException("No se registró ningún frame. ¿Había un juego abierto?");
            var cab = lineas[0].Split(',').ToList();
            int iApp = cab.IndexOf("Application"), iMs = cab.IndexOf("MsBetweenPresents"), iHora = cab.IndexOf("TimeInDateTime"), iCpu = cab.IndexOf("MsCPUBusy"), iGpu = cab.IndexOf("MsGPUBusy");
            if (iApp < 0 || iMs < 0) throw new InvalidOperationException("Formato de datos no reconocido.");

            var filas = lineas.Skip(1).Select(l => l.Split(',')).Where(p => p.Length == cab.Count).ToList();
            // Con varios programas dibujando, el juego es el que más frames tiene, descartando apps de escritorio
            var ajenos = new[] { "chrome", "msedge", "claude", "discord", "steamwebhelper", "epicwebhelper", "nvcontainer", "applicationframehost", "searchhost", "shellexperiencehost", "startmenuexperiencehost", "textinputhost", "framelab", "afinapc", "optimizadorpc" };
            var grupos = filas.GroupBy(p => p[iApp]).OrderByDescending(g => g.Count()).ToList();
            var elegido = grupos.FirstOrDefault(g => !ajenos.Any(a => g.Key.ToLowerInvariant().Contains(a))) ?? grupos.First();
            var f2 = elegido.ToList();

            Func<string[], int, double> num = (p, i) => { double d; return i >= 0 && double.TryParse(p[i], NumberStyles.Any, CultureInfo.InvariantCulture, out d) ? d : double.NaN; };
            var ms = f2.Select(p => num(p, iMs)).Where(d => !double.IsNaN(d) && d > 0).ToList();
            if (ms.Count < 30) throw new InvalidOperationException("Muy pocos frames para analizar (" + ms.Count + "). Medí al menos un minuto de juego.");
            double total = ms.Sum();
            var orden = ms.OrderByDescending(x => x).ToList();
            Func<double, double> bajo = pct => 1000 / orden.Take(Math.Max(1, (int)(orden.Count * pct))).Average();

            var res = new Resultado
            {
                Fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                Juego = elegido.Key.Replace(".exe", ""),
                Segundos = Math.Round(total / 1000),
                Frames = ms.Count,
                Fps = Math.Round(1000.0 * ms.Count / total, 1),
                Bajo1 = Math.Round(bajo(0.01), 1),
                Bajo01 = Math.Round(bajo(0.001), 1),
                Picos = ms.Count(x => x > 25)
            };
            var cpu = f2.Select(p => num(p, iCpu)).Where(d => !double.IsNaN(d)).ToList();
            var gpu = f2.Select(p => num(p, iGpu)).Where(d => !double.IsNaN(d)).ToList();
            res.CpuOcupada = cpu.Any() ? Math.Round(cpu.Average(), 1) : 0;
            res.GpuOcupada = gpu.Any() ? Math.Round(gpu.Average(), 1) : 0;

            // Qué tenían de distinto los frames lentos frente a los normales
            string causa = "";
            if (iCpu >= 0 && iGpu >= 0)
            {
                var lentos = f2.Where(p => num(p, iMs) > 25).ToList();
                if (lentos.Count >= 5)
                {
                    double lc = lentos.Select(p => num(p, iCpu)).Where(d => !double.IsNaN(d)).DefaultIfEmpty(0).Average();
                    double lg = lentos.Select(p => num(p, iGpu)).Where(d => !double.IsNaN(d)).DefaultIfEmpty(0).Average();
                    causa = lc > lg * 1.3 ? "En los frames lentos manda la CPU (" + Math.Round(lc) + " ms de CPU frente a " + Math.Round(lg) + " ms de GPU)."
                        : lg > lc * 1.3 ? "En los frames lentos manda la placa de video (" + Math.Round(lg) + " ms de GPU frente a " + Math.Round(lc) + " ms de CPU)."
                        : "En los frames lentos, CPU y placa de video pesan parecido.";
                }
            }

            var v = new StringBuilder();
            double ratio = res.Fps > 0 ? res.Bajo1 / res.Fps : 1;
            v.Append("Promedio de " + Math.Round(res.Fps) + " FPS, con mínimos del 1 % de " + Math.Round(res.Bajo1) + " FPS. ");
            if (ratio < 0.5) v.Append("Es muy irregular: esos saltos son lo que notás como tirones. ");
            else if (ratio < 0.7) v.Append("Algo irregular. ");
            else v.Append("Bastante estable. ");
            double pctPicos = 100.0 * res.Picos / ms.Count;
            v.Append(res.Picos + " frames de más de 25 ms (" + pctPicos.ToString("0.0") + " %). ");
            // Muchos frames igual de lentos no son tirones: suele ser un tope, y la CPU "ocupada" incluye esa espera
            if (ratio >= 0.85 && pctPicos > 80) v.Append("Los FPS son tan constantes que parece un límite activo (menú del juego, V-Sync o un tope de FPS), no un problema de hardware. Medí dentro de una partida. ");
            else if (causa != "") v.Append(causa + " ");
            else if (res.GpuOcupada > res.CpuOcupada * 1.3) v.Append("En general limita la placa de video: bajá la resolución 3D o la calidad. ");
            else if (res.CpuOcupada > res.GpuOcupada * 1.3) v.Append("En general limita la CPU: bajá la distancia de visión y cerrá programas de fondo. ");

            if (muestras != null && muestras.Count > 0)
            {
                double tmax = muestras.Max(m => m.GpuTemp);
                if (tmax >= 83) v.Append("La placa de video llegó a " + tmax + " °C: revisá ventiladores y polvo. ");
                int lim = muestras.Count(m => m.GpuMotivos.Contains("temperatura") || m.GpuMotivos.Contains("freno"));
                if (lim > 0) v.Append("La placa se frenó " + lim + " veces por temperatura o energía. ");
                double disco = muestras.Max(m => m.DiscoPct);
                if (muestras.Average(m => m.DiscoPct) > 60) v.Append("El disco estuvo muy ocupado (" + Math.Round(disco) + " % de pico): puede causar tirones. ");
                // RamMb queda en 0 si la lectura falló: no significa falta de memoria
                if (muestras.Any(m => m.RamMb > 0) && muestras.Where(m => m.RamMb > 0).Min(m => m.RamMb) < 800) v.Append("Quedó muy poca RAM libre: cerrá programas antes de jugar. ");
            }
            res.Veredicto = v.ToString().Trim();
            Historial.Guardar(res);
            return res;
        }
    }

    public static class Historial
    {
        static string Ruta { get { return Path.Combine(Medicion.Carpeta, "mediciones.json"); } }

        public static List<Resultado> Leer()
        {
            try { return (File.Exists(Ruta) ? new JavaScriptSerializer().Deserialize<List<Resultado>>(File.ReadAllText(Ruta, Encoding.UTF8)) : null) ?? new List<Resultado>(); }
            catch { return new List<Resultado>(); }
        }

        public static void Guardar(Resultado r)
        {
            var l = Leer();
            l.Add(r);
            Util.EscribirSeguro(Ruta, new JavaScriptSerializer().Serialize(l.Skip(Math.Max(0, l.Count - 30)).ToList()), Encoding.UTF8, null);
        }

        public static string Comparar(Resultado antes, Resultado despues)
        {
            Func<double, double, string> d = (a, b) => a <= 0 ? "" : ((b - a) / a * 100).ToString("+0;-0;0") + " %";
            var t = "Promedio: " + antes.Fps + " → " + despues.Fps + " FPS (" + d(antes.Fps, despues.Fps) + ")\n" +
                    "Mínimos 1 %: " + antes.Bajo1 + " → " + despues.Bajo1 + " FPS (" + d(antes.Bajo1, despues.Bajo1) + ")\n" +
                    "Mínimos 0,1 %: " + antes.Bajo01 + " → " + despues.Bajo01 + " FPS (" + d(antes.Bajo01, despues.Bajo01) + ")";
            if (Math.Abs(antes.Segundos - despues.Segundos) > Math.Max(antes.Segundos, despues.Segundos) * 0.5 || antes.Juego != despues.Juego)
                t += "\nAviso: las dos mediciones son muy distintas (duración o juego), así que la comparación es poco fiable.";
            else t += "\nDiferencias de menos del 5 % pueden ser variación normal entre partidas.";
            return t;
        }
    }
}
