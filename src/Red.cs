using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace OptimizadorPC
{
    public class OpcionDns { public string Nombre, Primario, Secundario; }

    public static class Red
    {
        public static readonly List<OpcionDns> Opciones = new List<OpcionDns> {
            new OpcionDns { Nombre = "Automático (de tu proveedor)" },
            new OpcionDns { Nombre = "Cloudflare", Primario = "1.1.1.1", Secundario = "1.0.0.1" },
            new OpcionDns { Nombre = "Google", Primario = "8.8.8.8", Secundario = "8.8.4.4" },
            new OpcionDns { Nombre = "Quad9", Primario = "9.9.9.9", Secundario = "149.112.112.112" }
        };

        static string RutaCopia { get { return Path.Combine(Medicion.Carpeta, "dns_anterior.json"); } }
        public static bool HayCopia { get { return File.Exists(RutaCopia); } }

        // Adaptador -> "auto" o sus DNS fijos separados por comas. La distinción viene del registro,
        // porque Get-DnsClientServerAddress muestra igual los DNS del proveedor que los fijos
        public static Dictionary<string, string> Estado()
        {
            const string ps = "Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | ForEach-Object { $k = 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces\\' + $_.InterfaceGuid; $v = (Get-ItemProperty $k -Name NameServer -ErrorAction SilentlyContinue).NameServer; $_.Name + '|' + $v }";
            int c;
            var d = new Dictionary<string, string>();
            foreach (var l in Util.Ps(ps, out c).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = l.Split('|');
                if (p.Length == 2) d[p[0]] = p[1].Trim() == "" ? "auto" : p[1].Trim().Replace(' ', ',');
            }
            return d;
        }

        static void Fijar(string adaptador, string servidores)
        {
            string nombre = adaptador.Replace("'", "''");
            string ps = servidores == "auto"
                ? "Set-DnsClientServerAddress -InterfaceAlias '" + nombre + "' -ResetServerAddresses -ErrorAction Stop"
                : "Set-DnsClientServerAddress -InterfaceAlias '" + nombre + "' -ServerAddresses " + string.Join(",", servidores.Split(',').Select(s => "'" + s + "'")) + " -ErrorAction Stop";
            int c;
            Util.Ps(ps, out c);
            if (c != 0) throw new InvalidOperationException("No se pudo cambiar el DNS de " + adaptador + ".");
        }

        static bool AdaptadorExiste(string adaptador)
        {
            int c;
            Util.Ps("if (Get-NetAdapter -Name '" + adaptador.Replace("'", "''") + "' -ErrorAction SilentlyContinue) { exit 0 } else { exit 3 }", out c);
            return c != 3;
        }

        static Dictionary<string, string> LeerCopia()
        {
            return (HayCopia ? new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(RutaCopia, Encoding.UTF8)) : null) ?? new Dictionary<string, string>();
        }

        static void GuardarCopia(Dictionary<string, string> d)
        {
            Util.EscribirSeguro(RutaCopia, new JavaScriptSerializer().Serialize(d), Encoding.UTF8, null);
        }

        public static void Aplicar(OpcionDns o)
        {
            var actual = Estado();
            if (actual.Count == 0) throw new InvalidOperationException("No hay ningún adaptador de red conectado.");
            // Se conserva la primera copia de cada adaptador, que es su configuración original.
            // Un adaptador conectado después (otro Wi-Fi, una VPN) también tiene que poder restaurarse
            var copia = LeerCopia();
            foreach (var a in actual) if (!copia.ContainsKey(a.Key)) copia[a.Key] = a.Value;
            GuardarCopia(copia);
            string destino = o.Primario == null ? "auto" : o.Primario + "," + o.Secundario;
            var errores = new List<string>();
            foreach (var a in actual.Keys)
            {
                try { Fijar(a, destino); }
                catch (InvalidOperationException ex) { errores.Add(ex.Message); }
            }
            Vaciar();
            if (errores.Any()) throw new InvalidOperationException(string.Join(" ", errores) + " Los demás adaptadores sí cambiaron.");
        }

        public static void Revertir()
        {
            var d = LeerCopia();
            var pendientes = new Dictionary<string, string>();
            var errores = new List<string>();
            foreach (var a in d)
            {
                try { Fijar(a.Key, a.Value); }
                catch (InvalidOperationException ex)
                {
                    // Un adaptador que ya no existe no debe dejar el botón de restaurar fallando para siempre
                    if (AdaptadorExiste(a.Key)) { pendientes[a.Key] = a.Value; errores.Add(ex.Message); }
                }
            }
            if (pendientes.Any()) GuardarCopia(pendientes); else File.Delete(RutaCopia);
            Vaciar();
            if (errores.Any()) throw new InvalidOperationException(string.Join(" ", errores));
        }

        public static void Vaciar()
        {
            int c;
            Util.Correr("ipconfig", "/flushdns", out c);
        }
    }

    public static class NoIncluido
    {
        public static readonly string[,] Lista = {
            {"NetworkThrottlingIndex desactivado", "Se promociona para reducir la latencia, pero no hay evidencia sólida de que ayude, y puede aumentar el uso de CPU."},
            {"NoLazyMode", "No hay evidencia de que mejore los juegos."},
            {"HPET y useplatformclock en bcdedit", "Pueden empeorar el temporizador y la latencia según la PC."},
            {"Desbloquear todos los núcleos", "Windows ya usa todos los núcleos; ese ajuste solo afecta al arranque."},
            {"Saltarse BitLocker o TPM", "Reduce la seguridad y no da más rendimiento."},
            {"Desactivar Defender o SmartScreen", "Quita protección real a cambio de muy poco."},
            {"Mitigaciones de CPU desactivadas", "Da algo de rendimiento de CPU, pero expone la PC a vulnerabilidades conocidas."},
            {"Quitar el archivo de paginación", "Puede cerrar juegos y programas de golpe cuando se llena la RAM."},
            {"Limpiadores de RAM", "La RAM libre no da FPS: pueden causar más tirones al recargar datos."},
            {"Bloquear con el archivo hosts", "Puede romper actualizaciones y apps, y es difícil de diagnosticar."},
            {"Editar memoria o archivos con antitrampas", "Puede hacer que te baneen."},
            {"Drivers de páginas no oficiales", "Son una vía habitual de malware y de inestabilidad."}
        };
    }
}
