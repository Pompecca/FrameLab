using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace OptimizadorPC
{
    // Fondo propio de FrameLab, dibujado por código: no usa arte de terceros
    public static class FondoFrameLab
    {
        static Color C(string h) { return (Color)ColorConverter.ConvertFromString(h); }

        // Se dibuja en una unidad de 1920x1080 y se escala, así se ve igual en cualquier resolución
        public static void Dibujar(DrawingContext dc, double ancho, double alto, ImageSource logo, bool conMarca)
        {
            dc.DrawRectangle(new SolidColorBrush(C("#0B0E14")), null, new Rect(0, 0, ancho, alto));
            Action<double, double, double, string> brillo = (x, y, r, color) =>
            {
                var g = new RadialGradientBrush(C(color), Color.FromArgb(0, 0x0B, 0x0E, 0x14)) { Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5) };
                dc.DrawEllipse(g, null, new Point(x * ancho, y * alto), r * ancho, r * ancho);
            };
            brillo(0.82, 0.12, 0.55, "#4022C7D6");
            brillo(0.10, 0.95, 0.50, "#307A5CFF");
            brillo(0.45, 0.55, 0.35, "#1022C7D6");

            // Cuadrícula tenue, como la de un gráfico
            var linea = new Pen(new SolidColorBrush(Color.FromArgb(0x10, 0xEE, 0xF1, 0xF6)), Math.Max(1, ancho / 1920));
            double paso = ancho / 24;
            for (double x = paso; x < ancho; x += paso) dc.DrawLine(linea, new Point(x, 0), new Point(x, alto));
            for (double y = paso; y < alto; y += paso) dc.DrawLine(linea, new Point(0, y), new Point(ancho, y));

            // Curva de frametime: estable, con un par de picos, que se suaviza hacia la derecha
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                var rnd = new Random(7);
                double baseY = alto * 0.66;
                g.BeginFigure(new Point(0, baseY), false, false);
                for (double x = 0; x <= ancho; x += ancho / 160)
                {
                    double t = x / ancho;
                    double ruido = (rnd.NextDouble() - 0.5) * alto * 0.03 * (1 - t);
                    double pico = (Math.Abs(t - 0.18) < 0.004 || Math.Abs(t - 0.37) < 0.004) ? -alto * 0.12 : 0;
                    g.LineTo(new Point(x, baseY + ruido + pico - Math.Sin(t * Math.PI) * alto * 0.04), true, true);
                }
            }
            geo.Freeze();
            var trazo = new LinearGradientBrush(C("#0022C7D6"), C("#FF22C7D6"), 0);
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(0x30, 0x22, 0xC7, 0xD6)), ancho / 160), geo);
            dc.DrawGeometry(null, new Pen(trazo, Math.Max(2, ancho / 640)), geo);

            if (conMarca && logo != null)
            {
                double t = alto * 0.07;
                double x = ancho - t * 5.2, y = alto - t * 1.9;
                dc.PushOpacity(0.85);
                dc.DrawImage(logo, new Rect(x, y, t, t));
                var texto = new FormattedText("FrameLab", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), t * 0.55, new SolidColorBrush(C("#EEF1F6")));
                dc.DrawText(texto, new Point(x + t * 1.25, y + (t - texto.Height) / 2));
                dc.Pop();
            }
        }

        public static void GuardarPng(string ruta, int ancho, int alto, ImageSource logo)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen()) Dibujar(dc, ancho, alto, logo, true);
            var bmp = new RenderTargetBitmap(ancho, alto, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            using (var f = File.Create(ruta)) enc.Save(f);
        }

        // Tamaño fijo según el monitor principal: 1080p, 1440p o 4K
        public static void Tamaño(out int ancho, out int alto)
        {
            double w = SystemParameters.PrimaryScreenWidth, h = SystemParameters.PrimaryScreenHeight;
            try
            {
                var src = PresentationSource.FromVisual(Application.Current.MainWindow);
                if (src != null) { w *= src.CompositionTarget.TransformToDevice.M11; h *= src.CompositionTarget.TransformToDevice.M22; }
            }
            catch (Exception) { }
            if (w > 2560 || h > 1440) { ancho = 3840; alto = 2160; }
            else if (w > 1920 || h > 1080) { ancho = 2560; alto = 1440; }
            else { ancho = 1920; alto = 1080; }
        }

        // ---------- Fondo de escritorio ----------
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool SystemParametersInfo(uint accion, uint param, string valor, uint flags);
        const uint SPI_SETDESKWALLPAPER = 0x14, SPIF_UPDATEINIFILE = 1, SPIF_SENDCHANGE = 2;
        const string ClaveEscritorio = @"Control Panel\Desktop";
        public static string ClaveEscritorioPrueba;

        static string Clave { get { return ClaveEscritorioPrueba ?? ClaveEscritorio; } }
        public static string RutaCopia { get { return Path.Combine(Medicion.Carpeta, "fondo_anterior.txt"); } }
        public static bool HayCopia { get { return File.Exists(RutaCopia); } }

        // Se guarda la primera vez, para que volver siempre lleve al fondo original
        public static void GuardarAnterior()
        {
            if (HayCopia) return;
            using (var k = Registry.CurrentUser.OpenSubKey(Clave))
            {
                string papel = k == null ? "" : Convert.ToString(k.GetValue("WallPaper", ""));
                string estilo = k == null ? "" : Convert.ToString(k.GetValue("WallpaperStyle", ""));
                string mosaico = k == null ? "" : Convert.ToString(k.GetValue("TileWallpaper", ""));
                Util.EscribirSeguro(RutaCopia, papel + "\n" + estilo + "\n" + mosaico, new System.Text.UTF8Encoding(false), null);
            }
        }

        static void Estilo(string estilo, string mosaico)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(Clave))
            {
                if (estilo != "") k.SetValue("WallpaperStyle", estilo, RegistryValueKind.String);
                if (mosaico != "") k.SetValue("TileWallpaper", mosaico, RegistryValueKind.String);
            }
        }

        public static void PonerEscritorio(ImageSource logo)
        {
            int w, h; Tamaño(out w, out h);
            string ruta = Path.Combine(Medicion.Carpeta, "fondo_framelab_" + w + "x" + h + ".png");
            GuardarPng(ruta, w, h, logo);
            GuardarAnterior();
            Estilo("10", "0"); // Rellenar
            if (!SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, ruta, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE))
                throw new InvalidOperationException("Windows no dejó cambiar el fondo (código " + Marshal.GetLastWin32Error() + "). Si Windows no está activado, puede estar bloqueado. La imagen quedó en " + ruta + ".");
        }

        public static void VolverAnterior()
        {
            if (!HayCopia) throw new InvalidOperationException("No hay un fondo anterior guardado.");
            var l = File.ReadAllText(RutaCopia).Split('\n');
            string papel = l[0], estilo = l.Length > 1 ? l[1] : "", mosaico = l.Length > 2 ? l[2] : "";
            Estilo(estilo, mosaico);
            // Un fondo vacío es un color sólido: también es válido
            if (!SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, papel, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE))
                throw new InvalidOperationException("Windows no dejó volver al fondo anterior (código " + Marshal.GetLastWin32Error() + ").");
            File.Delete(RutaCopia);
        }

        public static string[] LeerCopia() { return File.ReadAllText(RutaCopia).Split('\n'); }
    }
}
