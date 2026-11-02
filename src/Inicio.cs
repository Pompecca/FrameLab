using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace OptimizadorPC
{
    public class EntradaInicio
    {
        public string Nombre, Origen, Comando, ClaveAprobado;
        public RegistryKey Colmena;
        public bool Activo;
    }

    // Usa los mismos valores que la pestaña Inicio del Administrador de tareas, así que se puede deshacer desde ahí
    public static class Inicio
    {
        const string Base = @"Software\Microsoft\Windows\CurrentVersion";
        const string Aprobado = Base + @"\Explorer\StartupApproved\";

        public static List<EntradaInicio> Listar()
        {
            var l = new List<EntradaInicio>();
            Registro(l, Registry.CurrentUser, Base + @"\Run", "Run", "Usuario");
            Registro(l, Registry.LocalMachine, Base + @"\Run", "Run", "Todos los usuarios");
            Registro(l, Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Run32", "Todos los usuarios");
            Carpeta(l, Registry.CurrentUser, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Carpeta de inicio");
            Carpeta(l, Registry.LocalMachine, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Carpeta de inicio (todos)");
            return l.OrderBy(e => e.Nombre, StringComparer.OrdinalIgnoreCase).ToList();
        }

        static void Registro(List<EntradaInicio> l, RegistryKey h, string ruta, string aprobado, string origen)
        {
            using (var k = h.OpenSubKey(ruta))
            {
                if (k == null) return;
                foreach (var n in k.GetValueNames().Where(x => x != ""))
                    l.Add(Crear(h, n, origen, Convert.ToString(k.GetValue(n)), Aprobado + aprobado));
            }
        }

        static void Carpeta(List<EntradaInicio> l, RegistryKey h, string carpeta, string origen)
        {
            if (!Directory.Exists(carpeta)) return;
            foreach (var f in Directory.GetFiles(carpeta).Where(x => !x.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)))
                l.Add(Crear(h, Path.GetFileName(f), origen, f, Aprobado + "StartupFolder"));
        }

        static EntradaInicio Crear(RegistryKey h, string nombre, string origen, string comando, string clave)
        {
            bool activo = true;
            using (var a = h.OpenSubKey(clave))
            {
                var b = a == null ? null : a.GetValue(nombre) as byte[];
                // Windows marca como desactivadas las entradas con el primer byte impar
                if (b != null && b.Length > 0) activo = (b[0] & 1) == 0;
            }
            return new EntradaInicio { Nombre = nombre, Origen = origen, Comando = comando, ClaveAprobado = clave, Colmena = h, Activo = activo };
        }

        public static void Cambiar(EntradaInicio e, bool activar)
        {
            var v = new byte[12];
            v[0] = (byte)(activar ? 2 : 3);
            if (!activar) Array.Copy(BitConverter.GetBytes(DateTime.Now.ToFileTime()), 0, v, 4, 8);
            using (var k = e.Colmena.CreateSubKey(e.ClaveAprobado))
                k.SetValue(e.Nombre, v, RegistryValueKind.Binary);
            e.Activo = activar;
        }
    }
}
