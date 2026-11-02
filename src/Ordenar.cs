using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

namespace OptimizadorPC
{
    public class Movimiento { public string Origen { get; set; } public string Destino { get; set; } public string Categoria { get; set; } }

    public class RegistroOrden
    {
        public string Raiz { get; set; }
        public List<Movimiento> Movidos { get; set; }
        public List<string> Creadas { get; set; }
    }

    // Ordena archivos sueltos en carpetas por tipo. Nunca mueve carpetas y guarda un registro para deshacerlo
    public static class Ordenar
    {
        static readonly string[,] Tipos = {
            {"Imágenes", "jpg jpeg png gif bmp webp heic tif tiff svg ico jfif"},
            {"Documentos", "pdf doc docx xls xlsx ppt pptx txt rtf odt ods odp csv md epub"},
            {"Videos", "mp4 mkv avi mov wmv webm flv m4v"},
            {"Música", "mp3 wav flac aac ogg m4a wma"},
            {"Instaladores", "exe msi msix msixbundle appx appinstaller"},
            {"Comprimidos", "zip rar 7z tar gz iso"},
            {"Accesos directos", "lnk url"}
        };

        public static string Categoria(string archivo)
        {
            string ext = Path.GetExtension(archivo).TrimStart('.').ToLowerInvariant();
            for (int i = 0; i < Tipos.GetLength(0); i++)
                if (Tipos[i, 1].Split(' ').Contains(ext)) return Tipos[i, 0];
            return "Otros";
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out string ruta);

        // El escritorio puede estar en OneDrive: Windows informa la ruta real
        public static string Escritorio { get { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); } }

        public static string Descargas
        {
            get
            {
                string r;
                if (SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out r) == 0) return r;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }
        }

        public static string RutaRegistro(string raiz)
        {
            string nombre = raiz.TrimEnd('\\').Equals(Escritorio.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ? "escritorio"
                : raiz.TrimEnd('\\').Equals(Descargas.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ? "descargas"
                : "carpeta_" + (raiz.ToLowerInvariant().GetHashCode() & 0x7FFFFFFF);
            return Path.Combine(Medicion.Carpeta, "orden_" + nombre + ".json");
        }

        public static bool HayParaDeshacer(string raiz) { return File.Exists(RutaRegistro(raiz)); }

        // Solo archivos sueltos visibles: no se tocan carpetas, desktop.ini, ocultos, del sistema ni el propio FrameLab
        public static List<Movimiento> Planear(string raiz, bool accesos)
        {
            string propio = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var l = new List<Movimiento>();
            foreach (var f in Directory.GetFiles(raiz))
            {
                var a = File.GetAttributes(f);
                if ((a & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                if (Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                if (f.Equals(propio, StringComparison.OrdinalIgnoreCase)) continue;
                string cat = Categoria(f);
                if (cat == "Accesos directos" && !accesos) continue;
                l.Add(new Movimiento { Origen = f, Categoria = cat, Destino = Path.Combine(raiz, cat, Path.GetFileName(f)) });
            }
            return l.OrderBy(m => m.Categoria).ThenBy(m => Path.GetFileName(m.Origen)).ToList();
        }

        // Si ya existe un archivo con ese nombre, se usa "nombre (2).ext", "nombre (3).ext"...
        static string Libre(string ruta)
        {
            if (!File.Exists(ruta) && !Directory.Exists(ruta)) return ruta;
            string dir = Path.GetDirectoryName(ruta), nom = Path.GetFileNameWithoutExtension(ruta), ext = Path.GetExtension(ruta);
            for (int i = 2; ; i++)
            {
                string r = Path.Combine(dir, nom + " (" + i + ")" + ext);
                if (!File.Exists(r) && !Directory.Exists(r)) return r;
            }
        }

        static RegistroOrden Leer(string raiz)
        {
            try { return File.Exists(RutaRegistro(raiz)) ? new JavaScriptSerializer().Deserialize<RegistroOrden>(File.ReadAllText(RutaRegistro(raiz), Encoding.UTF8)) : null; }
            catch (Exception) { return null; }
        }

        static void Guardar(string raiz, RegistroOrden r)
        {
            Util.EscribirSeguro(RutaRegistro(raiz), new JavaScriptSerializer().Serialize(r), Encoding.UTF8, null);
        }

        // Devuelve cuántos se movieron; los que están en uso se saltean y se informan
        public static int Aplicar(string raiz, IEnumerable<Movimiento> elegidos, List<string> salteados)
        {
            var reg = Leer(raiz) ?? new RegistroOrden { Raiz = raiz, Movidos = new List<Movimiento>(), Creadas = new List<string>() };
            int n = 0;
            foreach (var m in elegidos)
            {
                if (!File.Exists(m.Origen)) continue;
                string dir = Path.GetDirectoryName(m.Destino);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    if (!reg.Creadas.Contains(dir)) reg.Creadas.Add(dir);
                }
                string destino = Libre(m.Destino);
                try
                {
                    File.Move(m.Origen, destino);
                    reg.Movidos.Add(new Movimiento { Origen = m.Origen, Destino = destino, Categoria = m.Categoria });
                    n++;
                }
                catch (Exception) { salteados.Add(Path.GetFileName(m.Origen)); }
            }
            // Se guarda después de cada tanda: si algo se corta, lo movido igual se puede deshacer
            Guardar(raiz, reg);
            return n;
        }

        // Vuelve cada archivo a su lugar. Si ya hay otro con ese nombre o lo borraste, lo informa y sigue
        public static int Deshacer(string raiz, List<string> problemas)
        {
            var reg = Leer(raiz);
            if (reg == null) throw new InvalidOperationException("No hay un orden para deshacer.");
            int n = 0;
            var pendientes = new List<Movimiento>();
            foreach (var m in Enumerable.Reverse(reg.Movidos))
            {
                try
                {
                    if (!File.Exists(m.Destino)) { problemas.Add(Path.GetFileName(m.Destino) + ": ya no está en " + Path.GetFileName(Path.GetDirectoryName(m.Destino)) + "."); continue; }
                    if (File.Exists(m.Origen)) { problemas.Add(Path.GetFileName(m.Origen) + ": ya hay otro archivo con ese nombre, quedó en " + Path.GetFileName(Path.GetDirectoryName(m.Destino)) + "."); continue; }
                    File.Move(m.Destino, m.Origen);
                    n++;
                }
                catch (Exception ex) { problemas.Add(Path.GetFileName(m.Destino) + ": " + ex.Message); pendientes.Insert(0, m); }
            }
            foreach (var d in reg.Creadas)
            {
                try { if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); }
                catch (Exception) { }
            }
            if (pendientes.Any()) { reg.Movidos = pendientes; Guardar(raiz, reg); }
            else File.Delete(RutaRegistro(raiz));
            return n;
        }
    }
}
