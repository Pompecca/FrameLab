using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace OptimizadorPC
{
    public class Accion
    {
        public string tipo { get; set; }
        public string ruta { get; set; }
        public string valor { get; set; }
        public string tipoValor { get; set; }
        public string dato { get; set; }
        public string sub { get; set; }
        public string ajuste { get; set; }
    }

    public class Ajuste
    {
        public string id { get; set; }
        public string nombre { get; set; }
        public string descripcion { get; set; }
        public string categoria { get; set; }
        public string impacto { get; set; }
        public string riesgo { get; set; }
        public List<string> perfiles { get; set; }
        public bool soloEscritorio { get; set; }
        public string condicion { get; set; }
        public string windows { get; set; } // "10,11" o "11"
        public string aviso { get; set; }
        public string guia { get; set; }
        public List<Accion> acciones { get; set; }
    }

    // Estado previo de una acción, para poder deshacerla
    public class Previo
    {
        public string tipo { get; set; }
        public string ruta { get; set; }
        public string valor { get; set; }
        public bool existia { get; set; }
        public string tipoValor { get; set; }
        public string dato { get; set; }
        // Si la clave no existía: el antepasado más cercano que sí existía, para no dejar claves vacías al revertir
        public string claveBase { get; set; }
    }

    public static class Motor
    {
        const string PlanAlto = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        // Alto rendimiento, máximo rendimiento y el plan de AtlasOS ya cumplen este ajuste
        static readonly string[] PlanesValidos = { PlanAlto, "e9a42b02-d5df-448d-aa00-03f14749eb61", "11111111-1111-1111-1111-111111111111" };

        public static string RutaRespaldo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FrameLab", "respaldo.json");

        // Las versiones anteriores guardaban la copia en otra carpeta; sin esto se perdería la opción de revertir
        public static void MigrarRespaldo(string local = null)
        {
            // Hasta la 0.5.0 la app se llamaba Afina PC: se copia su carpeta entera (copias, mediciones, PresentMon)
            // para que Revertir y Deshacer todo sigan funcionando. La carpeta vieja queda como estaba
            // Solo la primera vez, cuando la carpeta nueva todavía no existe. Antes también copiaba si faltaba respaldo.json,
            // y si otra ventana de la app justo estaba guardando, pisaba la copia buena con la de la versión vieja
            string nueva = Path.GetDirectoryName(RutaRespaldo);
            if (Directory.Exists(nueva)) return;
            if (local == null) local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string afina = Path.Combine(local, "AfinaPC");
            if (Directory.Exists(afina))
            {
                string tmp = nueva + ".migrando";
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                CopiarCarpeta(afina, tmp);
                Directory.Move(tmp, nueva);
                return;
            }
            string viejo = Path.Combine(local, "OptimizadorPC", "respaldo.json");
            if (File.Exists(viejo))
            {
                Directory.CreateDirectory(nueva);
                File.Copy(viejo, RutaRespaldo);
            }
        }

        static void CopiarCarpeta(string origen, string destino)
        {
            Directory.CreateDirectory(destino);
            foreach (var f in Directory.GetFiles(origen)) File.Copy(f, Path.Combine(destino, Path.GetFileName(f)));
            foreach (var d in Directory.GetDirectories(origen))
                if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) == 0) CopiarCarpeta(d, Path.Combine(destino, Path.GetFileName(d)));
        }

        public static List<Ajuste> CargarCatalogo()
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("catalogo.json"))
            using (var r = new StreamReader(s, Encoding.UTF8))
                return new JavaScriptSerializer().Deserialize<List<Ajuste>>(r.ReadToEnd());
        }

        static RegistryKey Colmena(string ruta, out string sub)
        {
            int i = ruta.IndexOf('\\');
            string h = ruta.Substring(0, i);
            sub = ruta.Substring(i + 1);
            if (h == "HKLM") return Registry.LocalMachine;
            if (h == "HKCU") return Registry.CurrentUser;
            throw new InvalidOperationException("Colmena no admitida: " + h);
        }

        static object Leer(string ruta, string valor)
        {
            string sub;
            var c = Colmena(ruta, out sub);
            using (var k = c.OpenSubKey(sub))
                return k == null ? null : k.GetValue(valor, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        }

        static string Texto(object v)
        {
            if (v == null) return null;
            var b = v as byte[];
            if (b != null) return string.Concat(b.Select(x => x.ToString("x2")));
            // El registro devuelve los DWord con signo: sin esto, 0xFFFFFFFF sería "-1"
            if (v is int) return unchecked((uint)(int)v).ToString();
            if (v is long) return unchecked((ulong)(long)v).ToString();
            var m = v as string[];
            if (m != null) return string.Join("\n", m);
            return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        static string Ejecutar(string exe, string args, out int codigo)
        {
            return Util.Correr(exe, args, out codigo);
        }

        static string planCache; static DateTime planHora;
        // Se consulta muchas veces seguidas al detectar; cada consulta lanza un proceso
        static string PlanActivo()
        {
            if (planCache != null && (DateTime.Now - planHora).TotalSeconds < 5) return planCache;
            int c;
            string o = Ejecutar("powercfg", "/getactivescheme", out c);
            var m = System.Text.RegularExpressions.Regex.Match(o, "[0-9a-fA-F-]{36}");
            planCache = m.Success ? m.Value.ToLowerInvariant() : "";
            planHora = DateTime.Now;
            return planCache;
        }

        static bool HibernacionActiva()
        {
            object v = Leer("HKLM\\SYSTEM\\CurrentControlSet\\Control\\Power", "HibernateEnabled");
            return v != null && Convert.ToInt32(v) != 0;
        }

        public static bool Aplicado(Ajuste a)
        {
            try { return a.acciones.All(AccionAplicada); }
            catch { return false; }
        }

        // Un ajuste cuyos servicios o propiedades de red no existen en esta PC no hace nada: no debe contar como aplicado
        public static bool Disponible(Ajuste a)
        {
            try { return a.acciones.Any(AccionDisponible); }
            catch { return true; }
        }

        static bool AccionDisponible(Accion x)
        {
            if (x.tipo == "servicio") return ExisteClave(RutaServicio(x.valor));
            if (x.tipo == "red") return PropiedadesRed().Any();
            return true;
        }

        // Para comprobar la detección: a diferencia de Aplicado, muestra los errores de lectura
        public static string Detalle(Ajuste a)
        {
            if (!Disponible(a)) return "no aplica";
            try { return a.acciones.All(AccionAplicada) ? "aplicado" : "no aplicado"; }
            catch (Exception ex) { return "ERROR " + ex.Message; }
        }

        static string gpuId;
        static string GpuId()
        {
            if (gpuId != null) return gpuId;
            using (var s = new System.Management.ManagementObjectSearcher("SELECT PNPDeviceID,Name FROM Win32_VideoController"))
            {
                var todas = s.Get().Cast<System.Management.ManagementObject>().ToList();
                // Con gráfica integrada y dedicada, el modo MSI se aplica a la dedicada
                var g = todas.FirstOrDefault(m => System.Text.RegularExpressions.Regex.IsMatch(Convert.ToString(m["Name"]), "NVIDIA|AMD|Radeon")) ?? todas.FirstOrDefault();
                gpuId = g == null ? "" : Convert.ToString(g["PNPDeviceID"]);
            }
            return gpuId;
        }

        static string RutaMsi()
        {
            if (GpuId() == "") throw new InvalidOperationException("No se detectó la placa de video.");
            return "HKLM\\SYSTEM\\CurrentControlSet\\Enum\\" + GpuId() + "\\Device Parameters\\Interrupt Management\\MessageSignaledInterruptProperties";
        }

        static string DatoReal(string dato)
        {
            if (dato != "ram") return dato;
            using (var s = new System.Management.ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                foreach (System.Management.ManagementObject m in s.Get())
                    return (Convert.ToUInt64(m["TotalPhysicalMemory"]) / 1024).ToString();
            throw new InvalidOperationException("No se pudo leer la RAM.");
        }

        static string RutaServicio(string nombre) { return "HKLM\\SYSTEM\\CurrentControlSet\\Services\\" + nombre; }

        static bool ExisteClave(string ruta)
        {
            string sub;
            var c = Colmena(ruta, out sub);
            using (var k = c.OpenSubKey(sub)) return k != null;
        }

        static string RutaEnergia(Accion x)
        {
            return "HKLM\\SYSTEM\\CurrentControlSet\\Control\\Power\\User\\PowerSchemes\\" + PlanActivo() + "\\" + x.sub + "\\" + x.ajuste;
        }

        // Windows usa el menú clásico mientras exista esta clave, aunque esté vacía
        const string ClaveMenuClasico = "HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32";
        const string FiltroRed = "Energy.?Efficient|Green.?Ethernet|Power Saving|Ahorro de energ|\\bEEE\\b|Gigabit Lite";

        // Una línea por propiedad: adaptador|palabra clave|valor
        static List<string> PropiedadesRed()
        {
            string ps = "Get-NetAdapterAdvancedProperty -Name * -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -match '" + FiltroRed + "' } | ForEach-Object { $_.Name + '|' + $_.RegistryKeyword + '|' + ($_.RegistryValue -join ',') }";
            int c;
            string o = Util.Ps(ps, out c);
            // Una propiedad sin valor no se puede ni cambiar ni restaurar: se ignora
            return o.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Where(l => { var p = l.Split('|'); return p.Length == 3 && p[2] != ""; }).ToList();
        }

        static void FijarRed(string linea, string valor)
        {
            var p = linea.Split('|');
            string valores = string.Join(",", valor.Split(',').Select(v => "'" + v.Replace("'", "''") + "'"));
            string ps = "Set-NetAdapterAdvancedProperty -Name '" + p[0].Replace("'", "''") + "' -RegistryKeyword '" + p[1].Replace("'", "''") + "' -RegistryValue " + valores + " -ErrorAction Stop";
            int c;
            Util.Ps(ps, out c);
            if (c != 0) throw new InvalidOperationException("No se pudo cambiar " + p[1] + " en " + p[0] + ".");
        }

        static string AncestroExistente(string ruta)
        {
            while (ruta.Contains('\\') && !ExisteClave(ruta)) ruta = ruta.Substring(0, ruta.LastIndexOf('\\'));
            return ruta;
        }

        // Borra las claves que creó la app, de la más profunda hacia arriba, solo si quedaron vacías
        static void BorrarClavesVacias(string ruta, string claveBase)
        {
            while (ruta.Length > claveBase.Length && ruta.StartsWith(claveBase, StringComparison.OrdinalIgnoreCase))
            {
                string sub;
                var h = Colmena(ruta, out sub);
                using (var k = h.OpenSubKey(sub))
                    if (k != null && (k.SubKeyCount > 0 || k.ValueCount > 0)) return;
                h.DeleteSubKey(sub, false);
                ruta = ruta.Substring(0, ruta.LastIndexOf('\\'));
            }
        }

        static bool AccionAplicada(Accion x)
        {
            if (x.tipo == "planEnergia") return PlanesValidos.Contains(PlanActivo());
            if (x.tipo == "hibernacion") return !HibernacionActiva();
            if (x.tipo == "red") return PropiedadesRed().All(l => l.Split('|')[2] == "0");
            if (x.tipo == "menuClasico") return ExisteClave(ClaveMenuClasico);
            if (x.tipo == "msi") return Texto(Leer(RutaMsi(), "MSISupported")) == "1";
            if (x.tipo == "energia") return Texto(Leer(RutaEnergia(x), "ACSettingIndex")) == x.dato;
            if (x.tipo == "servicio")
            {
                if (!ExisteClave(RutaServicio(x.valor))) return true;
                object s = Leer(RutaServicio(x.valor), "Start");
                if (s == null) return true;
                // "Manual" también se cumple si el servicio ya está desactivado
                return x.dato == "3" ? Convert.ToInt32(s) >= 3 : Texto(s) == x.dato;
            }
            return string.Equals(Texto(Leer(x.ruta, x.valor)), DatoReal(x.dato), StringComparison.OrdinalIgnoreCase);
        }

        static Previo Capturar(Accion x)
        {
            if (x.tipo == "planEnergia") return new Previo { tipo = x.tipo, dato = PlanActivo(), existia = true };
            if (x.tipo == "hibernacion") return new Previo { tipo = x.tipo, existia = HibernacionActiva() };
            if (x.tipo == "red") return new Previo { tipo = x.tipo, existia = true, dato = string.Join("\n", PropiedadesRed()) };
            if (x.tipo == "menuClasico") return new Previo { tipo = x.tipo, existia = ExisteClave(ClaveMenuClasico), claveBase = AncestroExistente(ClaveMenuClasico) };
            if (x.tipo == "msi") x = new Accion { tipo = "registro", ruta = RutaMsi(), valor = "MSISupported" };
            if (x.tipo == "servicio") x = new Accion { tipo = "registro", ruta = RutaServicio(x.valor), valor = "Start" };
            if (x.tipo == "energia")
            {
                var e = Capturar(new Accion { tipo = "registro", ruta = RutaEnergia(x), valor = "ACSettingIndex" });
                e.tipo = "energia";
                return e;
            }
            string sub;
            var c = Colmena(x.ruta, out sub);
            using (var k = c.OpenSubKey(sub))
            {
                object v = k == null ? null : k.GetValue(x.valor, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                var p = new Previo { tipo = x.tipo, ruta = x.ruta, valor = x.valor, existia = v != null };
                if (v != null) { p.tipoValor = k.GetValueKind(x.valor).ToString(); p.dato = Texto(v); }
                if (k == null) p.claveBase = AncestroExistente(x.ruta);
                return p;
            }
        }

        static void Escribir(string ruta, string valor, string tipoValor, string dato)
        {
            string sub;
            var c = Colmena(ruta, out sub);
            var kind = (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), tipoValor);
            object v = dato;
            // Las copias de versiones anteriores pueden traer DWord con signo ("-1")
            if (kind == RegistryValueKind.DWord) v = unchecked((int)long.Parse(dato));
            else if (kind == RegistryValueKind.QWord) v = dato.StartsWith("-") ? long.Parse(dato) : unchecked((long)ulong.Parse(dato));
            else if (kind == RegistryValueKind.Binary || kind == RegistryValueKind.None || kind == RegistryValueKind.Unknown)
                v = Enumerable.Range(0, dato.Length / 2).Select(i => Convert.ToByte(dato.Substring(i * 2, 2), 16)).ToArray();
            else if (kind == RegistryValueKind.MultiString)
            {
                // Las versiones anteriores no guardaban el contenido de estos valores
                if (dato == "System.String[]") throw new InvalidOperationException("La copia de " + valor + " se hizo con una versión anterior y no guardó su contenido.");
                v = dato == "" ? new string[0] : dato.Split('\n');
            }
            using (var k = c.CreateSubKey(sub))
                k.SetValue(valor, v, kind);
        }

        // Una copia de la copia: si respaldo.json queda dañado, no se pierde la posibilidad de revertir
        static string RutaRespaldoAnterior { get { return RutaRespaldo + ".bak"; } }
        static readonly object cerrojo = new object();
        // Mensaje para mostrar una vez si hubo que recuperar la copia
        public static string AvisoRespaldo;

        static Dictionary<string, List<Previo>> Deserializar(string ruta)
        {
            var d = new JavaScriptSerializer().Deserialize<Dictionary<string, List<Previo>>>(File.ReadAllText(ruta, Encoding.UTF8));
            if (d == null) throw new ArgumentException("Copia vacía.");
            return d;
        }

        static Dictionary<string, List<Previo>> LeerRespaldo()
        {
            bool hay = File.Exists(RutaRespaldo), hayAnterior = File.Exists(RutaRespaldoAnterior);
            if (!hay && !hayAnterior) return new Dictionary<string, List<Previo>>();
            // Solo el formato inválido cuenta como daño: un archivo bloqueado por un momento debe dar error, no apartarse
            try { if (hay) return Deserializar(RutaRespaldo); }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            Dictionary<string, List<Previo>> recuperada = null;
            try { if (hayAnterior) recuperada = Deserializar(RutaRespaldoAnterior); }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            if (hay) File.Move(RutaRespaldo, RutaRespaldo + ".danado-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            if (recuperada != null)
            {
                AvisoRespaldo = "La copia de los valores anteriores estaba dañada y se recuperó la versión previa. El último ajuste aplicado puede no tener botón de revertir.";
                GuardarRespaldo(recuperada);
                return recuperada;
            }
            AvisoRespaldo = "La copia de los valores anteriores estaba dañada y no se pudo recuperar. Se apartó el archivo: los ajustes aplicados antes no se pueden revertir desde la app, pero sí con el punto de restauración de Windows.";
            return new Dictionary<string, List<Previo>>();
        }

        static void GuardarRespaldo(Dictionary<string, List<Previo>> d)
        {
            Util.EscribirSeguro(RutaRespaldo, new JavaScriptSerializer().Serialize(d), Encoding.UTF8, RutaRespaldoAnterior);
        }

        // Si la copia anterior tiene ajustes que faltan en la actual y siguen aplicados, se perdió su valor original:
        // se recuperan para que Revertir y Deshacer todo sigan funcionando. Un ajuste revertido ya no está aplicado,
        // así que no vuelve a aparecer. Devuelve cuántos recuperó
        public static int RecuperarPerdidos(IEnumerable<Ajuste> catalogo)
        {
            lock (cerrojo)
            {
                if (!File.Exists(RutaRespaldoAnterior)) return 0;
                Dictionary<string, List<Previo>> anterior;
                try { anterior = Deserializar(RutaRespaldoAnterior); }
                catch (Exception) { return 0; }
                var actual = LeerRespaldo();
                int n = 0;
                foreach (var a in catalogo)
                {
                    List<Previo> p;
                    if (actual.ContainsKey(a.id) || !anterior.TryGetValue(a.id, out p) || !Aplicado(a)) continue;
                    actual[a.id] = p;
                    n++;
                }
                if (n > 0)
                {
                    GuardarRespaldo(actual);
                    Util.RegistrarError(new InvalidOperationException("Se recuperaron " + n + " copias de ajustes que faltaban en respaldo.json."));
                }
                return n;
            }
        }

        public static bool TieneRespaldo(Ajuste a)
        {
            lock (cerrojo) return LeerRespaldo().ContainsKey(a.id);
        }

        public static void Aplicar(Ajuste a)
        {
            lock (cerrojo)
            {
                var d = LeerRespaldo();
                // El primer respaldo guarda el estado original: reaplicar no debe sobrescribirlo
                bool nuevo = !d.ContainsKey(a.id);
                if (nuevo)
                {
                    d[a.id] = a.acciones.Select(Capturar).ToList();
                    GuardarRespaldo(d);
                }
                try { EjecutarAcciones(a); }
                catch (Exception ex)
                {
                    // Un ajuste no debe quedar a medias. Si la copia ya existía, se conserva para el botón Revertir
                    if (!nuevo) throw;
                    string fin;
                    try { Revertir(a); fin = " Se deshicieron los cambios de este ajuste."; }
                    catch { fin = " No se pudo deshacer todo: probá con el botón Revertir."; }
                    throw new InvalidOperationException(ex.Message + fin);
                }
            }
        }

        static void EjecutarAcciones(Ajuste a)
        {
            foreach (var x in a.acciones)
            {
                int c;
                if (x.tipo == "planEnergia")
                {
                    Ejecutar("powercfg", "/setactive " + PlanAlto, out c);
                    if (c != 0) throw new InvalidOperationException("Esta PC no tiene el plan de alto rendimiento.");
                    planCache = null;
                }
                else if (x.tipo == "hibernacion")
                {
                    Ejecutar("powercfg", "/hibernate off", out c);
                    if (c != 0) throw new InvalidOperationException("No se pudo desactivar la hibernación.");
                }
                else if (x.tipo == "red")
                {
                    foreach (var l in PropiedadesRed()) if (l.Split('|')[2] != "0") FijarRed(l, "0");
                }
                else if (x.tipo == "menuClasico") Escribir(ClaveMenuClasico, "", "String", "");
                else if (x.tipo == "msi")
                {
                    if (!ExisteClave(RutaMsi())) throw new InvalidOperationException("Esta placa de video no admite el modo MSI.");
                    Escribir(RutaMsi(), "MSISupported", "DWord", "1");
                }
                else if (x.tipo == "energia")
                {
                    string plan = PlanActivo();
                    Ejecutar("powercfg", "/setacvalueindex " + plan + " " + x.sub + " " + x.ajuste + " " + x.dato, out c);
                    if (c != 0) throw new InvalidOperationException("powercfg no aceptó el cambio.");
                    Ejecutar("powercfg", "/setactive " + plan, out c);
                }
                else if (x.tipo == "servicio")
                {
                    if (!ExisteClave(RutaServicio(x.valor)) || AccionAplicada(x)) continue;
                    Escribir(RutaServicio(x.valor), "Start", "DWord", x.dato);
                }
                else Escribir(x.ruta, x.valor, x.tipoValor, DatoReal(x.dato));
            }
        }

        public static void Revertir(Ajuste a)
        {
            lock (cerrojo)
            {
                var d = LeerRespaldo();
                List<Previo> lista;
                if (!d.TryGetValue(a.id, out lista)) throw new InvalidOperationException("No hay copia de este ajuste.");
                // Si una acción falla se sigue con las demás, y en la copia queda solo lo que falta restaurar.
                // En orden inverso al de aplicar, para que cada una vuelva al estado en que se capturó
                var pendientes = new List<Previo>();
                var errores = new List<string>();
                foreach (var p in Enumerable.Reverse(lista))
                {
                    try { RestaurarAccion(p); }
                    catch (Exception ex) { pendientes.Insert(0, p); errores.Add(ex.Message); }
                }
                if (pendientes.Any()) d[a.id] = pendientes; else d.Remove(a.id);
                GuardarRespaldo(d);
                if (errores.Any()) throw new InvalidOperationException(string.Join(" ", errores));
            }
        }

        static void RestaurarAccion(Previo p)
        {
            int c;
            if (p.tipo == "planEnergia")
            {
                Ejecutar("powercfg", "/setactive " + p.dato, out c);
                planCache = null;
                if (c != 0) throw new InvalidOperationException("No se pudo volver al plan de energía anterior.");
            }
            else if (p.tipo == "hibernacion")
            {
                if (!p.existia) return;
                Ejecutar("powercfg", "/hibernate on", out c);
                if (c != 0) throw new InvalidOperationException("No se pudo volver a activar la hibernación.");
            }
            else if (p.tipo == "red")
            {
                foreach (var l in (p.dato ?? "").Split('\n').Where(z => z != "")) FijarRed(l, l.Split('|')[2]);
            }
            else if (p.tipo == "menuClasico")
            {
                if (p.existia) return;
                // Las copias anteriores no guardan claveBase
                if (p.claveBase == null) { Registry.CurrentUser.DeleteSubKeyTree("Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}", false); return; }
                string sub;
                var h = Colmena(ClaveMenuClasico, out sub);
                using (var k = h.OpenSubKey(sub, true))
                    if (k != null) k.DeleteValue("", false);
                BorrarClavesVacias(ClaveMenuClasico, p.claveBase);
            }
            else
            {
                if (p.existia) Escribir(p.ruta, p.valor, p.tipoValor, p.dato);
                else
                {
                    string sub;
                    var h = Colmena(p.ruta, out sub);
                    using (var k = h.OpenSubKey(sub, true))
                        if (k != null) k.DeleteValue(p.valor, false);
                    if (p.claveBase != null) BorrarClavesVacias(p.ruta, p.claveBase);
                }
                // Los cambios del plan de energía se activan al volver a seleccionarlo
                if (p.tipo == "energia") Ejecutar("powercfg", "/setactive scheme_current", out c);
            }
        }

        // Devuelve true solo si Windows creó un punto nuevo: limita a uno cada 24 horas sin dar error
        public static bool CrearPuntoRestauracion()
        {
            const string ps = "Checkpoint-Computer -Description 'FrameLab' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; " +
                "$l = Get-ComputerRestorePoint | Select-Object -Last 1; " +
                "if ($l -and $l.Description -eq 'FrameLab' -and ([Management.ManagementDateTimeConverter]::ToDateTime($l.CreationTime) -gt (Get-Date).AddMinutes(-3))) { exit 0 } else { exit 2 }";
            int c;
            // Puede tardar varios minutos si Windows está ocupado
            Util.Ps(ps, out c, 300000);
            return c == 0;
        }
    }
}
