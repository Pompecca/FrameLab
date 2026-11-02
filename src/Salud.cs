using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace OptimizadorPC
{
    public class Aviso { public string Titulo, Detalle, Nivel; } // Nivel: ok, aviso, info

    public static class Salud
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }
        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        static extern bool EnumDisplaySettings(string dev, int modo, ref DEVMODE dm);

        // Hz actuales y máximos a la resolución actual del monitor principal
        public static void Hz(out int actual, out int maximo)
        {
            var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
            actual = 0; maximo = 0;
            if (!EnumDisplaySettings(null, -1, ref dm)) return;
            actual = dm.dmDisplayFrequency;
            int w = dm.dmPelsWidth, h = dm.dmPelsHeight;
            for (int i = 0; EnumDisplaySettings(null, i, ref dm); i++)
                if (dm.dmPelsWidth == w && dm.dmPelsHeight == h && dm.dmDisplayFrequency > maximo) maximo = dm.dmDisplayFrequency;
        }

        public static int LimiteFps()
        {
            int a, m; Hz(out a, out m);
            return a > 3 ? a - 3 : 0;
        }

        static List<ManagementObject> Wmi(string q)
        {
            try { using (var s = new ManagementObjectSearcher(q)) return s.Get().Cast<ManagementObject>().ToList(); }
            catch { return new List<ManagementObject>(); }
        }

        public static string Nvidia(string campos)
        {
            try
            {
                int c;
                string o = Util.Correr("nvidia-smi", "--query-gpu=" + campos + " --format=csv,noheader,nounits", out c, 8000);
                if (c != 0) return null;
                // Con varias placas NVIDIA devuelve una línea por cada una: se usa la primera
                return o.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).FirstOrDefault();
            }
            catch { return null; }
        }

        public static List<Aviso> Revisar(Diagnostico d)
        {
            var l = new List<Aviso>();
            Action<string, string, string> add = (t, x, n) => l.Add(new Aviso { Titulo = t, Detalle = x, Nivel = n });

            if (d.Build > 0 && d.Build < 19041) add("Windows 10 anterior a la versión 2004", "La app está pensada para Windows 10 2004 o más nuevo. En esta versión algunos ajustes pueden no tener efecto: actualizá Windows si podés.", "aviso");

            var ram = Wmi("SELECT Speed,ConfiguredClockSpeed,DeviceLocator,BankLabel FROM Win32_PhysicalMemory");
            if (ram.Any())
            {
                int nominal = Convert.ToInt32(ram[0]["Speed"] ?? 0), real = Convert.ToInt32(ram[0]["ConfiguredClockSpeed"] ?? 0);
                if (real > 0 && nominal > real) add("La RAM va más lenta que su velocidad declarada", real + " MHz de " + nominal + " MHz. Revisá XMP o EXPO en la BIOS.", "aviso");
                else add("Velocidad de la RAM", real + " MHz, igual a la que declaran los módulos. Windows no puede saber si tu placa admite más: si la RAM es de mayor velocidad, revisá XMP o EXPO en la BIOS.", "info");
                if (ram.Count == 1) add("RAM en un solo canal", "Con un solo módulo los juegos pueden rendir menos, sobre todo los mínimos. Un segundo módulo igual ayudaría.", "aviso");
                else add("RAM en varios módulos", ram.Count + " módulos instalados.", "ok");
            }

            if (d.TieneHdd) add("Hay un disco duro (HDD)", "Instalá los juegos en un SSD: reduce tirones y tiempos de carga.", "aviso");
            else if (d.SoloSsd) add("Discos SSD", "No se detectaron discos duros mecánicos.", "ok");
            else add("Tipo de disco", "Windows no informa si alguno de tus discos es SSD o mecánico.", "info");

            foreach (var v in Wmi("SELECT DeviceID,Size,FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3"))
            {
                double total = Convert.ToDouble(v["Size"]), libre = Convert.ToDouble(v["FreeSpace"]);
                if (total > 0 && libre / total < 0.10) add("Poco espacio en " + v["DeviceID"], Math.Round(libre / 1e9) + " GB libres de " + Math.Round(total / 1e9) + " GB. Liberá espacio.", "aviso");
            }

            int ha, hm; Hz(out ha, out hm);
            if (ha > 0 && hm > ha + 1) add("El monitor no va a su máximo", "Funciona a " + ha + " Hz y admite " + hm + " Hz. Cambialo en Configuración > Pantalla > Pantalla avanzada.", "aviso");
            else if (ha > 0) add("Frecuencia del monitor", "Funciona a " + ha + " Hz, su máximo a esta resolución.", "ok");

            string pcie = Nvidia("pcie.link.width.current,pcie.link.width.max");
            if (pcie != null)
            {
                var p = pcie.Split(',').Select(x => x.Trim()).ToArray();
                int a, m;
                if (p.Length == 2 && int.TryParse(p[0], out a) && int.TryParse(p[1], out m))
                {
                    if (a < m) add("La placa de video usa menos carriles PCIe", "x" + a + " de x" + m + ". Revisá que esté en la ranura principal y bien colocada.", "aviso");
                    else add("Conexión PCIe de la placa de video", "x" + a + ", su máximo.", "ok");
                }
            }

            foreach (var b in Wmi("SELECT ReleaseDate,SMBIOSBIOSVersion FROM Win32_BIOS"))
            {
                // Algunas máquinas virtuales y BIOS no informan la fecha
                try
                {
                    var f = ManagementDateTimeConverter.ToDateTime(Convert.ToString(b["ReleaseDate"]));
                    double años = (DateTime.Now - f).TotalDays / 365;
                    if (años > 3) add("BIOS antigua", "Versión " + b["SMBIOSBIOSVersion"] + " de " + f.ToString("MM/yyyy") + ". Revisá la web del fabricante de tu placa. Actualizar BIOS tiene riesgo: hacelo solo si la versión nueva arregla algo que necesites.", "info");
                }
                catch { }
            }

            try
            {
                var q = new EventLogQuery("System", PathType.LogName, "*[System[(EventID=41) and TimeCreated[timediff(@SystemTime) <= 2592000000]]]");
                int n = 0;
                using (var r = new EventLogReader(q)) while (r.ReadEvent() != null) n++;
                if (n > 0) add("Apagados inesperados", n + " en los últimos 30 días. Si no fuiste vos, puede ser la fuente de alimentación, temperatura o cortes de luz.", "aviso");
                else add("Apagados inesperados", "Ninguno en los últimos 30 días.", "ok");
            }
            catch { }

            var hf = Wmi("SELECT InstalledOn FROM Win32_QuickFixEngineering").Select(m => { DateTime x; return DateTime.TryParse(Convert.ToString(m["InstalledOn"]), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out x) ? x : DateTime.MinValue; }).ToList();
            if (hf.Any())
            {
                var ult = hf.Max();
                if (ult > DateTime.MinValue && (DateTime.Now - ult).TotalDays > 60) add("Actualizaciones atrasadas", "La última se instaló el " + ult.ToString("dd/MM/yyyy") + ". Instalá las de seguridad desde Windows Update.", "aviso");
            }

            foreach (var g in Wmi("SELECT Name,DriverDate FROM Win32_VideoController"))
            {
                try
                {
                    var f = ManagementDateTimeConverter.ToDateTime(Convert.ToString(g["DriverDate"]));
                    if ((DateTime.Now - f).TotalDays > 270) add("Driver gráfico antiguo", g["Name"] + ": driver de " + f.ToString("MM/yyyy") + ". Buscá uno nuevo en Instalar.", "aviso");
                }
                catch { }
            }

            using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel"))
            {
                var v = k == null ? null : k.GetValue("MitigationOptions");
                if (v != null) add("Protecciones de CPU modificadas", "Se detectó una configuración no predeterminada de mitigaciones. Da algo de rendimiento, pero reduce la seguridad.", "info");
            }
            return l;
        }

        // Un tope un poco por debajo de lo que la PC sostiene: estable se siente mejor que picos altos.
        // Se usa el 85 % del promedio: con los mínimos del 1 % el tope quedaba muy bajo (80 FPS en una PC que hace 139)
        public static int LimiteSostenible(Resultado r)
        {
            if (r == null || r.Fps <= 0) return 0;
            return (int)Math.Round(r.Fps * 0.85 / 10) * 10;
        }

        // Capa la cifra para no pedir más de lo que la PC sostiene: un tope estable se nota más que picos altos
        public static string RecomendarLimite(Resultado ultimo)
        {
            int ha, hm; Hz(out ha, out hm);
            if (ha <= 3) return "No se pudo leer la frecuencia del monitor.";
            int tope = ha - 3;
            if (ultimo != null && ultimo.Fps > 0)
            {
                int sostenible = LimiteSostenible(ultimo);
                if (sostenible > 0 && sostenible < tope)
                    return "Limitá los FPS a unos " + sostenible + " en el juego: en tu última partida hiciste " + Math.Round(ultimo.Fps) + " FPS de promedio y tu monitor va a " + ha + " Hz. Un tope un poco por debajo de lo que tu PC sostiene se siente más parejo. Si notás tirones, bajá de 10 en 10.";
            }
            return "Limitá los FPS a " + tope + " en el juego, justo por debajo de los " + ha + " Hz de tu monitor. Es una orientación: medí una partida para ajustarla a tu PC.";
        }
    }
}
