using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using Microsoft.Win32;

namespace OptimizadorPC
{
    public class Diagnostico
    {
        public List<KeyValuePair<string, string>> Filas = new List<KeyValuePair<string, string>>();
        public bool EsPortatil;
        public string FabricanteGpu = "";
        public bool SoloSsd = true;
        public bool TieneHdd;
        public int Nucleos, Hilos;
        public double RamGb;
        public bool RamUnCanal;
        public List<string> Gpus = new List<string>();
        public bool TieneBluetooth;
        public int Build;
        public static int BuildSimulado; // Solo para pruebas: permite ver la interfaz como en Windows 10

        void Fila(string k, string v) { Filas.Add(new KeyValuePair<string, string>(k, string.IsNullOrWhiteSpace(v) ? "No disponible" : v)); }

        static IEnumerable<ManagementObject> Wmi(string consulta, string ns = @"root\cimv2")
        {
            try
            {
                using (var s = new ManagementObjectSearcher(ns, consulta))
                    return s.Get().Cast<ManagementObject>().ToList();
            }
            catch { return new List<ManagementObject>(); }
        }

        static string Reg(string ruta, string valor)
        {
            object v = Registry.GetValue(ruta, valor, null);
            return v == null ? "" : v.ToString();
        }

        public static Diagnostico Recoger()
        {
            var d = new Diagnostico();

            string nt = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            int build; int.TryParse(Reg(nt, "CurrentBuild"), out build);
            d.Build = BuildSimulado > 0 ? BuildSimulado : build;
            // DisplayVersion no existe antes de Windows 10 20H2
            string edicion = Reg(nt, "DisplayVersion");
            if (edicion == "") edicion = Reg(nt, "ReleaseId");
            d.Fila("Windows", (build >= 22000 ? "Windows 11" : "Windows 10") + " " + edicion + " (compilación " + build + "." + Reg(nt, "UBR") + ")");

            string atlas = "";
            if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\AtlasOS\Toolbox", "Version", null) != null || Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "AtlasModules")))
                atlas = "AtlasOS " + Reg(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\OEMInformation", "Model").Replace("Atlas Playbook ", "");
            else if (Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Revision Tool")))
                atlas = "ReviOS";
            d.Fila("Sistema modificado", atlas == "" ? "Ninguno detectado" : atlas);

            var chasis = Wmi("SELECT ChassisTypes FROM Win32_SystemEnclosure").SelectMany(m => (m["ChassisTypes"] as ushort[]) ?? new ushort[0]).ToList();
            ushort[] portatiles = { 8, 9, 10, 11, 12, 14, 30, 31, 32 };
            ushort[] fijos = { 3, 4, 5, 6, 7, 13, 15, 16, 17, 23, 24, 35, 36 };
            // Una UPS por USB también aparece como batería: esta solo decide si el chasis no lo aclara
            d.EsPortatil = chasis.Any(c => portatiles.Contains(c)) || (!chasis.Any(c => fijos.Contains(c)) && Wmi("SELECT Name FROM Win32_Battery").Any());
            d.Fila("Tipo de equipo", d.EsPortatil ? "Notebook" : "Escritorio");

            foreach (var m in Wmi("SELECT Name,NumberOfCores,NumberOfLogicalProcessors FROM Win32_Processor"))
            {
                d.Nucleos += Convert.ToInt32(m["NumberOfCores"]);
                d.Hilos += Convert.ToInt32(m["NumberOfLogicalProcessors"]);
                d.Fila("Procesador", Convert.ToString(m["Name"]).Trim() + " ("+ m["NumberOfCores"] + " núcleos, " + m["NumberOfLogicalProcessors"] + " hilos)");
            }

            foreach (var m in Wmi("SELECT Name,DriverVersion FROM Win32_VideoController"))
            {
                string n = Convert.ToString(m["Name"]);
                d.Gpus.Add(n);
                d.Fila("Placa de video", n + " (driver " + m["DriverVersion"] + ")");
                string f = n.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0 ? "NVIDIA"
                    : n.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0 ? "AMD"
                    : n.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 ? "Intel" : "";
                // Con gráfica integrada y dedicada, manda la dedicada
                if (f != "" && (d.FabricanteGpu == "" || d.FabricanteGpu == "Intel")) d.FabricanteGpu = f;
            }

            var ram = Wmi("SELECT Capacity,Speed,ConfiguredClockSpeed,DeviceLocator,BankLabel FROM Win32_PhysicalMemory").ToList();
            if (ram.Any())
            {
                double gb = ram.Sum(m => Convert.ToDouble(m["Capacity"])) / (1024 * 1024 * 1024);
                object vel = ram[0]["ConfiguredClockSpeed"] ?? ram[0]["Speed"];
                var canales = ram.Select(m => System.Text.RegularExpressions.Regex.Match(Convert.ToString(m["DeviceLocator"]) + " " + Convert.ToString(m["BankLabel"]), @"(?i)(?:channel\s*|chan\s*)([A-D])").Groups[1].Value.ToUpperInvariant()).Where(x => x != "").Distinct().Count();
                string canal = canales >= 2 ? "doble canal" : canales == 1 ? "un solo canal" : ram.Count >= 2 ? "probablemente doble canal" : "un solo canal";
                d.RamGb = gb;
                d.RamUnCanal = canal == "un solo canal";
                d.Fila("Memoria RAM", Math.Round(gb) + " GB a " + vel + " MHz, " + ram.Count + " módulo(s), " + canal);
            }

            int discos = 0;
            foreach (var m in Wmi("SELECT FriendlyName,MediaType,BusType,Size FROM MSFT_PhysicalDisk", @"root\Microsoft\Windows\Storage"))
            {
                int media = Convert.ToInt32(m["MediaType"]), bus = Convert.ToInt32(m["BusType"]);
                string tipo = bus == 17 ? "SSD NVMe" : media == 4 ? "SSD" : media == 3 ? "Disco duro (HDD)" : "Tipo desconocido";
                // Un pendrive o disco externo por USB no cuenta para decidir ajustes del sistema
                if (bus != 7)
                {
                    discos++;
                    if (bus != 17 && media != 4) d.SoloSsd = false;
                    if (bus != 17 && media == 3) d.TieneHdd = true;
                }
                d.Fila("Disco", m["FriendlyName"] + ", " + tipo + ", " + Math.Round(Convert.ToDouble(m["Size"]) / 1e9) + " GB");
            }
            // Sin datos de discos no se puede afirmar que sean SSD
            if (discos == 0) d.SoloSsd = false;

            d.TieneBluetooth = Wmi("SELECT Name FROM Win32_PnPEntity WHERE PNPClass='Bluetooth'").Any();
            return d;
        }
    }
}
