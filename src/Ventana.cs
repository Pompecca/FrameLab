using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
// El actualizador comprueba que el exe descargado tenga esta versión
[assembly: AssemblyVersion(OptimizadorPC.Version.Actual + ".0")]
[assembly: AssemblyTitle("FrameLab")]

namespace OptimizadorPC
{
    public class Paquete { public string Nombre, Id; public CheckBox Marca; }

    public class Ventana : Window
    {
        static Brush Pincel(string c) { return (Brush)new BrushConverter().ConvertFromString(c); }
        static readonly Brush Fondo = Pincel("#0F1218"), Lateral = Pincel("#141821"), Tarjeta = Pincel("#1A1F2B"), Linea = Pincel("#2A3140"), Tinta = Pincel("#EEF1F6"), Suave = Pincel("#9AA5B8"),
            Acento = Pincel("#22C7D6"), Ok = Pincel("#3DD68C"), OkFondo = Pincel("#123527"), Pendiente = Pincel("#F5B14C"), PendienteFondo = Pincel("#3A2C12"), Gris = Pincel("#2A3140"), Peligro = Pincel("#FF7B72"), PeligroFondo = Pincel("#3C1B1B"), Azul = Pincel("#17333C");

        readonly List<Ajuste> catalogo = Motor.CargarCatalogo();
        readonly Dictionary<string, CheckBox> marcas = new Dictionary<string, CheckBox>();
        Diagnostico diag;
        readonly Dictionary<string, FrameworkElement> paginas = new Dictionary<string, FrameworkElement>();
        readonly Grid contenido = new Grid();
        readonly StackPanel menu = new StackPanel();
        StackPanel panelRed = new StackPanel(), panelResumen = new StackPanel(), panelDiag = new StackPanel(), panelSalud = new StackPanel(), panelAjustes = new StackPanel(), panelInicio = new StackPanel(),
            panelApps = new StackPanel(), panelLimpieza = new StackPanel(), panelJuegos = new StackPanel(), panelMonitor = new StackPanel(), panelMedir = new StackPanel(), panelDrivers = new StackPanel(), panelWinget = new StackPanel();
        TextBlock estadoAjustes = Texto("", 13, Suave);
        TextBox registro;
        List<Paquete> paquetes = new List<Paquete>();
        Button botonInstalar, botonAplicar;
        DispatcherTimer reloj;
        bool monitorVisible, muestreando;
        Medicion medicion;
        string textoCpu = "";
        List<Aviso> avisos = new List<Aviso>();
        // Trabajos que no deben cortarse al cerrar la ventana
        int ocupado;
        public readonly List<string> ErroresCarga = new List<string>();

        static TextBlock Texto(string t, double tam, Brush color, bool negrita = false)
        {
            return new TextBlock { Text = t, FontSize = tam, Foreground = color, FontWeight = negrita ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
        }

        Button Boton(string t, bool primario = false)
        {
            var b = new Button { Content = t, Margin = new Thickness(0, 0, 8, 8) };
            b.Style = (Style)FindResource(primario ? "BotonPrimario" : "BotonBase");
            return b;
        }

        static Border Caja(UIElement hijo)
        {
            return new Border { Background = Tarjeta, BorderBrush = Linea, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 14, 16, 14), Margin = new Thickness(0, 0, 0, 8), Child = hijo };
        }

        static Border Insignia(string t, Brush color, Brush fondo)
        {
            return new Border { Background = fondo, CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 3, 10, 3), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = t, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = color } };
        }

        static TextBlock Subtitulo(string t)
        {
            var x = Texto(t, 16, Tinta, true); x.Margin = new Thickness(0, 14, 0, 8);
            return x;
        }

        void Pagina(string clave, string icono, string titulo, string intro, UIElement cuerpo)
        {
            var s = new StackPanel { Margin = new Thickness(28, 24, 28, 28), MaxWidth = 980 };
            s.Children.Add(Texto(titulo, 26, Tinta, true));
            var i = Texto(intro, 13.5, Suave); i.Margin = new Thickness(0, 4, 0, 18);
            s.Children.Add(i);
            s.Children.Add(cuerpo);
            var sv = new ScrollViewer { Content = s, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed };
            paginas[clave] = sv;
            contenido.Children.Add(sv);

            var fila = new StackPanel { Orientation = Orientation.Horizontal };
            fila.Children.Add(new TextBlock { Text = icono, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15, Width = 28, VerticalAlignment = VerticalAlignment.Center });
            fila.Children.Add(new TextBlock { Text = titulo, VerticalAlignment = VerticalAlignment.Center });
            var r = new RadioButton { Content = fila, GroupName = "nav", Style = (Style)FindResource("Nav"), Tag = clave };
            r.Checked += delegate { Mostrar(clave); };
            menu.Children.Add(r);
        }

        void Mostrar(string clave)
        {
            foreach (var p in paginas) p.Value.Visibility = p.Key == clave ? Visibility.Visible : Visibility.Collapsed;
            monitorVisible = clave == "monitor";
        }

        public Ventana()
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Tema.xaml"))
                Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(s));
            Title = "FrameLab";
            Width = 1120; Height = 760; MinWidth = 860; MinHeight = 560;
            Background = Fondo;
            FontFamily = new FontFamily("Segoe UI");
            Foreground = Tinta;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var logo = CargarLogo();
            Icon = logo;

            var raiz = new Grid();
            raiz.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(232) });
            raiz.ColumnDefinitions.Add(new ColumnDefinition());

            var lado = new DockPanel { Background = Lateral };
            var marca = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 20, 16, 18) };
            marca.Children.Add(new Image { Source = logo, Width = 36, Height = 36 });
            var nom = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            nom.Children.Add(Texto("FrameLab", 17, Tinta, true));
            nom.Children.Add(Texto("Versión " + Version.Actual, 11, Suave));
            marca.Children.Add(nom);
            DockPanel.SetDock(marca, Dock.Top);
            lado.Children.Add(marca);
            // Por defecto se ve el modo simple; la elección se recuerda
            var avanzado = new CheckBox { Content = "Opciones avanzadas", Foreground = Suave, FontSize = 13, Margin = new Thickness(22, 10, 16, 18), IsChecked = Util.Preferencia("avanzado") == "1" };
            avanzado.Click += async delegate { Util.GuardarPreferencia("avanzado", avanzado.IsChecked == true ? "1" : "0"); AplicarModo(avanzado.IsChecked == true); if (diag != null) await Etapa("Apps", panelApps, RefrescarApps); };
            DockPanel.SetDock(avanzado, Dock.Bottom);
            lado.Children.Add(avanzado);
            lado.Children.Add(new ScrollViewer { Content = menu, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            raiz.Children.Add(lado);
            contenido.Background = Fondo;
            Grid.SetColumn(contenido, 1);
            raiz.Children.Add(contenido);
            Content = raiz;

            panelResumen.Children.Add(Texto("Analizando la PC...", 13, Suave));
            panelDiag.Children.Add(Texto("Analizando la PC...", 13, Suave));
            foreach (var p in new[] { panelInicio, panelJuegos, panelLimpieza, panelApps, panelRed }) p.Children.Add(Texto("Cargando...", 13, Suave));
            var diagCuerpo = new StackPanel();
            diagCuerpo.Children.Add(panelDiag);
            diagCuerpo.Children.Add(Subtitulo("Revisión de hardware y salud"));
            diagCuerpo.Children.Add(panelSalud);

            Pagina("simple", "\uE768", "Optimizar", "Un botón: crea un punto de restauración y aplica solo los ajustes seguros que sirven en esta PC. Todo se puede deshacer.", ConstruirSimple());
            Pagina("resumen", "", "Resumen", "El estado de tu PC de un vistazo.", panelResumen);
            Pagina("diagnostico", "", "Diagnóstico", "Qué tiene esta PC y qué conviene revisar. Acá no se cambia nada.", diagCuerpo);
            Pagina("optimizaciones", "", "Optimizaciones", "Elegí qué aplicar. Antes se crea un punto de restauración y se guarda el valor anterior de cada ajuste.", ConstruirAjustes());
            Pagina("inicio", "", "Programas al iniciar", "Elegí qué arranca con Windows. No se desinstala nada.", panelInicio);
            Pagina("juegos", "", "Juegos", "Modo partida y ajustes recomendados para cada juego según tu PC.", CuerpoJuegos());
            Pagina("medir", "", "Medir partida", "Mide FPS y tirones durante un juego, explica qué los causa y compara antes y después.", panelMedir);
            Pagina("monitor", "", "Monitor en vivo", "Temperatura, uso y frenos del hardware. Dejalo abierto en otra pantalla o revisalo después de jugar.", panelMonitor);
            Pagina("limpieza", "", "Limpieza", "Libera espacio de forma segura. No borra la caché de shaders, porque causaría tirones.", panelLimpieza);
            Pagina("espacio", "\uEDA2", "Espacio en disco", "Muestra qué ocupa más lugar. No borra nada: abrí la carpeta y decidí vos qué sacar.", ConstruirEspacio());
            Pagina("apps", "\uE74D", "Quitar apps de Windows", "Copilot, Edge y las apps que vienen con Windows y casi nadie usa. Cada una se puede reinstalar.", panelApps);
            Pagina("red", "", "Red", "Elegí el DNS y vaciá su caché. Un DNS distinto no da más FPS ni menos ping en el juego; puede acelerar la carga de páginas.", panelRed);
            Pagina("instalar", "", "Instalar", "Instala programas y drivers desde fuentes oficiales.", ConstruirInstalar());
            Pagina("ordenar", "\uE8B7", "Ordenar escritorio", "Pone en carpetas los archivos sueltos del escritorio o de Descargas. No borra nada y se puede deshacer.", ConstruirOrdenar());
            Pagina("personalizacion", "\uE771", "Personalización", "El fondo de la app y un fondo de escritorio propio de FrameLab.", ConstruirPersonalizacion());
            Pagina("acerca", "", "Acerca de", "Versión y actualizaciones.", ConstruirAcerca());
            ((RadioButton)menu.Children[0]).IsChecked = true;
            AplicarModo(avanzado.IsChecked == true);
            contenido.Children.Insert(0, capaFondo);
            AplicarFondo();
            ConstruirMedir();
            ConstruirMonitorBase();
            Closing += AlCerrar;
        }

        // En modo simple solo quedan las secciones que no requieren saber de Windows
        static readonly string[] SeccionesSimples = { "simple", "juegos", "apps", "espacio", "ordenar", "personalizacion", "acerca" };

        void AplicarModo(bool avanzado)
        {
            foreach (var r in menu.Children.OfType<RadioButton>())
            {
                r.Visibility = avanzado || SeccionesSimples.Contains((string)r.Tag) ? Visibility.Visible : Visibility.Collapsed;
                if (r.IsChecked == true && r.Visibility != Visibility.Visible) ((RadioButton)menu.Children[0]).IsChecked = true;
            }
        }

        // ---------- Modo simple ----------
        readonly StackPanel panelFrenos = new StackPanel();
        readonly TextBlock estadoSimple = Texto("Analizando la PC...", 14, Tinta);
        Button botonOptimizar, botonDeshacer;
        List<Freno> frenos = new List<Freno>();
        int conRespaldo;

        UIElement ConstruirSimple()
        {
            var s = new StackPanel();
            var caja = new StackPanel();
            caja.Children.Add(estadoSimple);
            var fila = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
            botonOptimizar = Boton("Optimizar", true); botonOptimizar.FontSize = 16; botonOptimizar.Padding = new Thickness(28, 10, 28, 10); botonOptimizar.IsEnabled = false;
            botonOptimizar.Click += async delegate { await OptimizarSimple(); };
            botonDeshacer = Boton("Deshacer todo"); botonDeshacer.Padding = new Thickness(18, 10, 18, 10); botonDeshacer.IsEnabled = false;
            botonDeshacer.Click += async delegate { await DeshacerTodo(); };
            fila.Children.Add(botonOptimizar); fila.Children.Add(botonDeshacer);
            caja.Children.Add(fila);
            caja.Children.Add(Texto("Los ajustes avanzados, la limpieza y el resto de las herramientas están en Opciones avanzadas, abajo a la izquierda.", 12.5, Suave));
            s.Children.Add(Caja(caja));
            s.Children.Add(Subtitulo("Qué frena tu PC"));
            panelFrenos.Children.Add(Texto("Analizando la PC...", 13, Suave));
            s.Children.Add(panelFrenos);
            return s;
        }

        List<Ajuste> SegurosPendientes()
        {
            return catalogo.Where(a => a.perfiles != null && a.perfiles.Contains("Seguro") && marcas.ContainsKey(a.id) && marcas[a.id].IsEnabled && a.aviso == null).ToList();
        }

        void ActualizarSimple()
        {
            int pendientes = SegurosPendientes().Count;
            estadoSimple.Text = (pendientes == 0 ? "Ya están aplicados todos los ajustes seguros que sirven en esta PC." : pendientes + (pendientes == 1 ? " ajuste seguro por aplicar." : " ajustes seguros por aplicar."))
                + (conRespaldo > 0 ? " La app cambió " + conRespaldo + (conRespaldo == 1 ? " ajuste" : " ajustes") + " que se pueden deshacer." : "");
            botonOptimizar.IsEnabled = pendientes > 0 && ocupado == 0;
            botonDeshacer.IsEnabled = conRespaldo > 0 && ocupado == 0;
        }

        async Task OptimizarSimple()
        {
            var lista = SegurosPendientes();
            foreach (var c in marcas.Values) c.IsChecked = false;
            foreach (var a in lista) marcas[a.id].IsChecked = true;
            botonOptimizar.IsEnabled = false; botonDeshacer.IsEnabled = false;
            try { await AplicarSeleccion(); }
            finally { ActualizarSimple(); }
        }

        async Task DeshacerTodo()
        {
            var lista = await Task.Run(() => catalogo.Where(Motor.TieneRespaldo).ToList());
            if (lista.Count == 0) return;
            if (MessageBox.Show(this, "Se van a deshacer todos los ajustes que cambió la app, también los aplicados desde Optimizaciones:\n\n" + string.Join("\n", lista.Select(a => "• " + a.nombre)) + "\n\n¿Continuar?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            botonOptimizar.IsEnabled = false; botonDeshacer.IsEnabled = false;
            ocupado++;
            try
            {
                var errores = await Task.Run(() =>
                {
                    var e = new List<string>();
                    foreach (var a in lista)
                    {
                        try { Motor.Revertir(a); }
                        catch (Exception ex) { e.Add(a.nombre + ": " + ex.Message); }
                    }
                    return e;
                });
                await RefrescarTrasCambio();
                string fin = (lista.Count - errores.Count) + " ajustes volvieron a como estaban. Algunos cambios se notan después de reiniciar.";
                if (errores.Any()) fin += "\n\nNo se pudieron deshacer:\n" + string.Join("\n", errores);
                MessageBox.Show(this, fin, Title, MessageBoxButton.OK, errores.Any() ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            finally { ocupado--; ActualizarSimple(); }
        }

        async Task CalcularFrenos()
        {
            int activos = await Task.Run(() => Inicio.Listar().Count(e => e.Activo));
            frenos = Frenos.Analizar(diag, avisos, activos);
            panelFrenos.Children.Clear();
            LlenarFrenos(panelFrenos);
        }

        void LlenarFrenos(StackPanel p)
        {
            foreach (var f in frenos.Take(6))
            {
                var s = new StackPanel();
                s.Children.Add(Texto(f.Titulo, 14, Tinta, true));
                s.Children.Add(Texto(f.Detalle, 12.5, Suave));
                var b = Caja(s);
                if (f.Peso >= 70) b.BorderBrush = Pendiente;
                p.Children.Add(b);
            }
            if (!frenos.Any()) p.Children.Add(Caja(Texto("No se detectó nada que frene a esta PC más de lo normal.", 13, Tinta)));
            p.Children.Add(Caja(Texto(Frenos.Conclusion(frenos), 13.5, Tinta)));
        }

        // ---------- Espacio ----------
        readonly StackPanel panelEspacio = new StackPanel();

        UIElement ConstruirEspacio()
        {
            var s = new StackPanel();
            var discos = Texto("", 13.5, Tinta);
            var b = Boton("Buscar qué ocupa más", true); b.HorizontalAlignment = HorizontalAlignment.Left; b.Margin = new Thickness(0, 12, 0, 0);
            var caja = new StackPanel();
            caja.Children.Add(discos);
            caja.Children.Add(b);
            s.Children.Add(Caja(caja));
            s.Children.Add(panelEspacio);
            Loaded += delegate { try { discos.Text = Espacio.Discos(); } catch (Exception ex) { discos.Text = ex.Message; } };
            b.Click += async delegate
            {
                b.IsEnabled = false;
                panelEspacio.Children.Clear();
                panelEspacio.Children.Add(Texto("Midiendo carpetas. Puede tardar unos minutos si el disco es grande...", 13, Suave));
                try
                {
                    var l = await Task.Run(() => Espacio.Analizar());
                    panelEspacio.Children.Clear();
                    panelEspacio.Children.Add(Texto("Abrí la carpeta y decidí vos. Lo de AppData pertenece a programas instalados: no lo borres a mano, desinstalá el programa.", 12.5, Suave));
                    foreach (var c in l)
                    {
                        var carpeta = c;
                        var g = new Grid();
                        g.ColumnDefinitions.Add(new ColumnDefinition());
                        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        var info = new StackPanel();
                        info.Children.Add(Texto(c.Nombre + "  ·  " + (c.Bytes / 1e9).ToString("0.0") + " GB", 14, Tinta, true));
                        var r = Texto(c.Ruta, 12, Suave); r.TextTrimming = TextTrimming.CharacterEllipsis; r.TextWrapping = TextWrapping.NoWrap;
                        info.Children.Add(r);
                        g.Children.Add(info);
                        var a = Boton("Abrir"); a.Margin = new Thickness(14, 0, 0, 0);
                        a.Click += delegate { try { Espacio.Abrir(carpeta.Ruta); } catch (Exception ex) { MessageBox.Show(this, "No se pudo abrir: " + ex.Message, Title); } };
                        Grid.SetColumn(a, 1);
                        g.Children.Add(a);
                        panelEspacio.Children.Add(Caja(g));
                    }
                    discos.Text = Espacio.Discos();
                }
                catch (Exception ex) { panelEspacio.Children.Clear(); panelEspacio.Children.Add(Texto("No se pudo medir: " + ex.Message, 13, Pendiente)); }
                b.IsEnabled = true;
            };
            return s;
        }

        void AlCerrar(object emisor, System.ComponentModel.CancelEventArgs e)
        {
            if (ocupado > 0)
            {
                MessageBox.Show(this, "Hay cambios en curso. Esperá a que terminen para no dejar nada a medias.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                e.Cancel = true;
                return;
            }
            if (medicion != null)
            {
                if (MessageBox.Show(this, "Hay una medición en curso y se va a perder. ¿Cerrar igual?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) { e.Cancel = true; return; }
                medicion.Cancelar();
                medicion = null;
            }
            if (reloj != null) reloj.Stop();
            if (Partida.Activo) { try { Partida.Terminar(); } catch (Exception ex) { Util.RegistrarError(ex); } }
        }

        // Una sección que falla no debe dejar a las demás sin cargar
        async Task Etapa(string nombre, Panel panel, Func<Task> paso)
        {
            try { await paso(); }
            catch (Exception ex)
            {
                Util.RegistrarError(ex);
                ErroresCarga.Add(nombre + ": " + ex);
                panel.Children.Clear();
                panel.Children.Add(Caja(Texto("No se pudo cargar esta sección: " + ex.Message, 13, Pendiente)));
            }
        }

        static BitmapImage CargarLogo()
        {
            var i = new BitmapImage();
            i.BeginInit();
            i.StreamSource = Assembly.GetExecutingAssembly().GetManifestResourceStream("logo.png");
            i.CacheOption = BitmapCacheOption.OnLoad;
            i.EndInit();
            i.Freeze();
            return i;
        }

        public async Task Cargar()
        {
            try { Motor.MigrarRespaldo(); }
            catch (Exception ex) { Util.RegistrarError(ex); }
            diag = await Task.Run(() => Diagnostico.Recoger());
            panelDiag.Children.Clear();
            foreach (var f in diag.Filas)
            {
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                var v = Texto(f.Value, 14, Tinta); Grid.SetColumn(v, 1);
                g.Children.Add(Texto(f.Key, 13, Suave)); g.Children.Add(v);
                panelDiag.Children.Add(Caja(g));
            }
            await Etapa("Drivers", panelDrivers, ConstruirDrivers);
            await Etapa("Winget", panelWinget, RevisarWinget);
            await Etapa("Salud", panelSalud, async () =>
            {
                avisos = await Task.Run(() => Salud.Revisar(diag));
                panelSalud.Children.Clear();
                foreach (var a in avisos.OrderBy(x => x.Nivel == "aviso" ? 0 : x.Nivel == "info" ? 1 : 2)) panelSalud.Children.Add(TarjetaAviso(a));
            });
            await Etapa("Ajustes", panelAjustes, RefrescarAjustes);
            await Etapa("Inicio", panelInicio, RefrescarInicio);
            await Etapa("Juegos", panelJuegos, RefrescarJuegos);
            await Etapa("Limpieza", panelLimpieza, RefrescarLimpieza);
            await Etapa("Apps", panelApps, RefrescarApps);
            await Etapa("Red", panelRed, RefrescarRed);
            await Etapa("Frenos", panelFrenos, CalcularFrenos);
            await Etapa("Resumen", panelResumen, ConstruirResumen);
            reloj = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            reloj.Tick += async delegate { await TickMonitor(); };
            reloj.Start();
            MostrarAvisoRespaldo();
            RestaurarPartidaPendiente();
        }

        void MostrarAvisoRespaldo()
        {
            if (Motor.AvisoRespaldo == null) return;
            string t = Motor.AvisoRespaldo;
            Motor.AvisoRespaldo = null;
            MessageBox.Show(this, t, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        Border TarjetaAviso(Aviso a)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var s = new StackPanel();
            s.Children.Add(Texto(a.Titulo, 14, Tinta, true));
            s.Children.Add(Texto(a.Detalle, 12.5, Suave));
            g.Children.Add(s);
            var i = a.Nivel == "aviso" ? Insignia("Revisar", Pendiente, PendienteFondo) : a.Nivel == "ok" ? Insignia("Bien", Ok, OkFondo) : Insignia("Info", Acento, Azul);
            i.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(i, 1);
            g.Children.Add(i);
            return Caja(g);
        }

        async Task ConstruirResumen()
        {
            var ajustes = catalogo.Where(a => a.guia == null && !SoloWindows11(a)).ToList();
            // Detectar lanza procesos: en el hilo de la interfaz congelaría la ventana
            ajustes = await Task.Run(() => ajustes.Where(Motor.Disponible).ToList());
            int aplicados = await Task.Run(() => ajustes.Count(a => Motor.Aplicado(a)));
            panelResumen.Children.Clear();
            var fila = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, -8, 10) };
            fila.Children.Add(Metrica("Ajustes aplicados", aplicados + " de " + ajustes.Count));
            fila.Children.Add(Metrica("Para revisar", avisos.Count(a => a.Nivel == "aviso").ToString()));
            var ult = Historial.Leer().LastOrDefault();
            fila.Children.Add(Metrica("Última medición", ult == null ? "Sin datos" : Math.Round(ult.Fps) + " FPS"));
            panelResumen.Children.Add(fila);
            if (frenos.Any())
            {
                panelResumen.Children.Add(Subtitulo("Qué frena tu PC"));
                LlenarFrenos(panelResumen);
            }
            panelResumen.Children.Add(Subtitulo("Límite de FPS recomendado"));
            panelResumen.Children.Add(Caja(Texto(Salud.RecomendarLimite(ult), 13.5, Tinta)));
            var rev = avisos.Where(a => a.Nivel == "aviso").ToList();
            if (rev.Any())
            {
                panelResumen.Children.Add(Subtitulo("Lo primero que revisaría"));
                foreach (var a in rev) panelResumen.Children.Add(TarjetaAviso(a));
            }
        }

        static Border Metrica(string t, string v)
        {
            var s = new StackPanel();
            s.Children.Add(Texto(t, 12.5, Suave));
            var x = Texto(v, 24, Tinta, true); x.Margin = new Thickness(0, 4, 0, 0);
            s.Children.Add(x);
            var b = Caja(s); b.Margin = new Thickness(0, 0, 8, 0);
            return b;
        }

        UIElement ConstruirAjustes()
        {
            var s = new StackPanel();
            var barra = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            foreach (var p in new[] { "Seguro", "Gamer", "Avanzado" })
            {
                var perfil = p;
                var b = Boton("Perfil " + p);
                b.Click += delegate { Marcar(perfil); };
                barra.Children.Add(b);
            }
            var ninguno = Boton("Quitar selección");
            ninguno.Click += delegate { foreach (var c in marcas.Values) c.IsChecked = false; };
            barra.Children.Add(ninguno);
            botonAplicar = Boton("Aplicar seleccionados", true);
            botonAplicar.Click += async delegate { await AplicarSeleccion(); };
            barra.Children.Add(botonAplicar);
            s.Children.Add(barra);
            estadoAjustes.Margin = new Thickness(0, 0, 0, 8);
            s.Children.Add(estadoAjustes);
            s.Children.Add(panelAjustes);
            return s;
        }

        void Marcar(string perfil)
        {
            foreach (var a in catalogo)
            {
                CheckBox c;
                if (marcas.TryGetValue(a.id, out c)) c.IsChecked = c.IsEnabled && a.perfiles != null && a.perfiles.Contains(perfil);
            }
        }

        bool SoloWindows11(Ajuste a) { return diag != null && a.windows == "11" && diag.Build < 22000; }

        bool NoAplica(Ajuste a, out string motivo)
        {
            motivo = "";
            if (diag == null) return false;
            if (SoloWindows11(a)) { motivo = "Solo Windows 11"; return true; }
            if (a.soloEscritorio && diag.EsPortatil) { motivo = "No aplica en notebook"; return true; }
            if (a.condicion == "ssd" && !diag.SoloSsd) { motivo = "Solo con SSD"; return true; }
            if (a.condicion == "portatil" && !diag.EsPortatil) { motivo = "Solo en notebook"; return true; }
            if (a.condicion == "sinbt" && diag.TieneBluetooth) { motivo = "Hay Bluetooth"; return true; }
            return false;
        }

        async Task RefrescarAjustes()
        {
            var seleccion = marcas.Where(m => m.Value.IsChecked == true).Select(m => m.Key).ToList();
            var ajustes = catalogo.Where(a => a.guia == null).ToList();
            var estados = await Task.Run(() => ajustes.ToDictionary(a => a.id, a => new { Aplicado = Motor.Aplicado(a), Respaldo = Motor.TieneRespaldo(a), Disponible = Motor.Disponible(a) }));
            panelAjustes.Children.Clear();
            marcas.Clear();
            foreach (var grupo in ajustes.GroupBy(a => a.categoria))
            {
                panelAjustes.Children.Add(Subtitulo(grupo.Key));
                foreach (var a in grupo)
                {
                    var e = estados[a.id];
                    string motivo;
                    bool noAplica = NoAplica(a, out motivo);
                    if (!noAplica && !e.Disponible) { noAplica = true; motivo = "No aplica en esta PC"; }
                    var g = new Grid();
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var c = new CheckBox { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 14, 0), IsEnabled = !e.Aplicado && !noAplica, IsChecked = !e.Aplicado && !noAplica && seleccion.Contains(a.id) };
                    marcas[a.id] = c;

                    var info = new StackPanel();
                    info.Children.Add(Texto(a.nombre, 14, Tinta, true));
                    info.Children.Add(Texto(a.descripcion, 12.5, Suave));
                    var meta = Texto("Impacto: " + a.impacto + "  ·  Riesgo: " + a.riesgo, 12, a.riesgo == "Alto" ? Peligro : Suave); meta.Margin = new Thickness(0, 4, 0, 0);
                    info.Children.Add(meta);
                    Grid.SetColumn(info, 1);

                    var der = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
                    der.Children.Add(noAplica ? Insignia(motivo, Suave, Gris) : e.Aplicado ? Insignia("Aplicado", Ok, OkFondo) : Insignia("No aplicado", Pendiente, PendienteFondo));
                    if (e.Respaldo)
                    {
                        var aj = a;
                        var r = Boton("Revertir"); r.Margin = new Thickness(0, 8, 0, 0); r.Padding = new Thickness(10, 4, 10, 4); r.HorizontalAlignment = HorizontalAlignment.Right;
                        r.Click += async delegate { await RevertirAjuste(aj, r); };
                        der.Children.Add(r);
                    }
                    Grid.SetColumn(der, 2);
                    g.Children.Add(c); g.Children.Add(info); g.Children.Add(der);
                    panelAjustes.Children.Add(Caja(g));
                }
            }
            var guias = catalogo.Where(a => a.guia != null && diag != null && a.condicion == "gpu:" + diag.FabricanteGpu).ToList();
            if (guias.Any())
            {
                panelAjustes.Children.Add(Subtitulo("Guías para tu placa de video"));
                foreach (var a in guias)
                {
                    var s = new StackPanel();
                    s.Children.Add(Texto(a.nombre, 14, Tinta, true));
                    s.Children.Add(Texto(a.guia, 12.5, Suave));
                    panelAjustes.Children.Add(Caja(s));
                }
            }
            var vigentes = ajustes.Where(a => !SoloWindows11(a) && estados[a.id].Disponible).ToList();
            estadoAjustes.Text = vigentes.Count(a => estados[a.id].Aplicado) + " de " + vigentes.Count + " ajustes aplicados.";
            conRespaldo = estados.Count(x => x.Value.Respaldo);
            ActualizarSimple();
        }

        async Task AplicarSeleccion()
        {
            var lista = catalogo.Where(a => marcas.ContainsKey(a.id) && marcas[a.id].IsChecked == true).ToList();
            if (lista.Count == 0) { MessageBox.Show(this, "No hay ningún ajuste seleccionado.", Title); return; }
            foreach (var a in lista.Where(x => x.aviso != null))
                if (MessageBox.Show(this, a.nombre + "\n\n" + a.aviso + "\n\n¿Seguro que querés incluirlo?", Title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            string nombres = string.Join("\n", lista.Select(a => "• " + a.nombre));
            if (MessageBox.Show(this, "Se van a aplicar estos ajustes:\n\n" + nombres + "\n\n¿Continuar?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            botonAplicar.IsEnabled = false;
            ocupado++;
            try
            {
                estadoAjustes.Text = "Creando punto de restauración...";
                bool punto = false;
                try { punto = await Task.Run(() => Motor.CrearPuntoRestauracion()); }
                catch (Exception ex) { Util.RegistrarError(ex); }
                if (!punto && MessageBox.Show(this, "Windows no creó un punto de restauración nuevo. Puede estar desactivado o ya se creó uno en las últimas 24 horas.\n\nLos valores anteriores se guardan igual y podés revertir cada ajuste. ¿Continuar?", Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    estadoAjustes.Text = "No se aplicó ningún ajuste.";
                    return;
                }
                estadoAjustes.Text = "Aplicando...";
                var errores = await Task.Run(() =>
                {
                    var e = new List<string>();
                    foreach (var a in lista)
                    {
                        try { Motor.Aplicar(a); }
                        catch (Exception ex) { e.Add(a.nombre + ": " + ex.Message); }
                    }
                    return e;
                });
                await RefrescarTrasCambio();
                int hechos = lista.Count - errores.Count;
                string fin = hechos + (hechos == 1 ? " ajuste aplicado" : " ajustes aplicados") + ". Algunos cambios se notan después de reiniciar.";
                if (errores.Any()) fin += "\n\nFallaron:\n" + string.Join("\n", errores);
                MessageBox.Show(this, fin, Title, MessageBoxButton.OK, errores.Any() ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            finally
            {
                ocupado--;
                botonAplicar.IsEnabled = true;
            }
        }

        async Task RevertirAjuste(Ajuste a, Button boton)
        {
            boton.IsEnabled = false;
            ocupado++;
            try
            {
                try { await Task.Run(() => Motor.Revertir(a)); }
                catch (Exception ex) { MessageBox.Show(this, "No se pudo revertir: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
                await RefrescarTrasCambio();
            }
            finally { ocupado--; }
        }

        async Task RefrescarTrasCambio()
        {
            await Etapa("Ajustes", panelAjustes, RefrescarAjustes);
            await Etapa("Resumen", panelResumen, ConstruirResumen);
            MostrarAvisoRespaldo();
        }

        async Task RefrescarInicio()
        {
            var lista = await Task.Run(() => Inicio.Listar());
            panelInicio.Children.Clear();
            if (lista.Count == 0) panelInicio.Children.Add(Texto("No hay programas de inicio.", 13, Suave));
            foreach (var e in lista)
            {
                var entrada = e;
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var info = new StackPanel();
                info.Children.Add(Texto(e.Nombre, 14, Tinta, true));
                var o = Texto(e.Origen + "  ·  " + e.Comando, 12, Suave); o.TextTrimming = TextTrimming.CharacterEllipsis; o.TextWrapping = TextWrapping.NoWrap;
                info.Children.Add(o);
                var der = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
                der.Children.Add(e.Activo ? Insignia("Activo", Pendiente, PendienteFondo) : Insignia("Desactivado", Ok, OkFondo));
                var b = Boton(e.Activo ? "Desactivar" : "Activar"); b.Margin = new Thickness(0, 8, 0, 0); b.Padding = new Thickness(10, 4, 10, 4); b.HorizontalAlignment = HorizontalAlignment.Right;
                b.Click += async delegate
                {
                    try { Inicio.Cambiar(entrada, !entrada.Activo); }
                    catch (Exception ex) { MessageBox.Show(this, "No se pudo cambiar: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
                    await RefrescarInicio();
                };
                der.Children.Add(b);
                Grid.SetColumn(der, 1);
                g.Children.Add(info); g.Children.Add(der);
                panelInicio.Children.Add(Caja(g));
            }
        }

        async Task RefrescarJuegos()
        {
            string nivel = Juegos.NivelDe(diag.Filas.Where(f => f.Key == "Placa de video").Select(f => f.Value));
            var ult = Historial.Leer().LastOrDefault();
            int limite = Salud.LimiteFps();
            if (ult != null && ult.Bajo1 > 0) { int s = (int)Math.Round(ult.Bajo1 * 1.25 / 10) * 10; if (s < limite) limite = s; }
            var lista = await Task.Run(() => Juegos.Detectar(nivel, limite));
            panelJuegos.Children.Clear();
            panelJuegos.Children.Add(Caja(Texto(Salud.RecomendarLimite(ult), 13.5, Tinta)));
            foreach (var j in lista.OrderByDescending(x => x.Instalado))
            {
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var s = new StackPanel();
                s.Children.Add(Texto(j.Nombre, 15, Tinta, true));
                var d = Texto(j.Guia, 12.5, Suave); d.Margin = new Thickness(0, 3, 0, 0);
                s.Children.Add(d);
                if (j.SePuedeEditar && j.Instalado)
                {
                    var f = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
                    var ap = Boton("Aplicar ajustes", true);
                    int lim = limite; string niv = nivel; var juego = j;
                    ap.Click += async delegate
                    {
                        if (MessageBox.Show(this, "Se van a cambiar los ajustes gráficos de " + juego.Nombre + " y se va a guardar una copia del archivo. ¿Continuar?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                        try { Juegos.Aplicar(juego.Id, niv, lim); MessageBox.Show(this, "Listo. Los vas a ver al abrir " + juego.Nombre + ".", Title); }
                        catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
                        await RefrescarJuegos();
                    };
                    f.Children.Add(ap);
                    if (File.Exists(Juegos.Copia(j.Id)))
                    {
                        var re = Boton("Restaurar mis ajustes");
                        re.Click += async delegate
                        {
                            try { Juegos.Restaurar(juego.Id); MessageBox.Show(this, "Se restauró tu configuración.", Title); }
                            catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
                            await RefrescarJuegos();
                        };
                        f.Children.Add(re);
                    }
                    s.Children.Add(f);
                }
                g.Children.Add(s);
                var i = j.Instalado ? Insignia("Instalado", Ok, OkFondo) : Insignia("No detectado", Suave, Gris);
                i.Margin = new Thickness(14, 0, 0, 0);
                Grid.SetColumn(i, 1);
                g.Children.Add(i);
                panelJuegos.Children.Add(Caja(g));
            }
        }

        async Task RefrescarLimpieza()
        {
            var zonas = await Task.Run(() => Limpieza.Analizar());
            panelLimpieza.Children.Clear();
            foreach (var z in zonas)
            {
                var zona = z;
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var s = new StackPanel();
                s.Children.Add(Texto(z.Nombre, 14, Tinta, true));
                s.Children.Add(Texto((z.Bytes / 1048576.0).ToString("0") + " MB en " + z.Archivos + " archivos" + (z.Nota != null ? ". " + z.Nota : ""), 12.5, Suave));
                g.Children.Add(s);
                var b = Boton("Limpiar"); b.IsEnabled = !z.Bloqueada && z.Archivos > 0; b.Margin = new Thickness(14, 0, 0, 0);
                b.Click += async delegate
                {
                    if (MessageBox.Show(this, "Se van a borrar los archivos de: " + zona.Nombre + ". Los que estén en uso se van a saltear. ¿Continuar?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                    b.IsEnabled = false;
                    try
                    {
                        long l = await Task.Run(() => Limpieza.Limpiar(zona));
                        MessageBox.Show(this, "Se liberaron " + (l / 1048576.0).ToString("0") + " MB.", Title);
                    }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
                    await RefrescarLimpieza();
                };
                Grid.SetColumn(b, 1);
                g.Children.Add(b);
                panelLimpieza.Children.Add(Caja(g));
            }
        }

        async Task RefrescarApps()
        {
            await Task.Run(() => { try { Apps.OlvidarSiVolvio(); } catch (Exception) { } });
            var lista = await Task.Run(() => Apps.Listar());
            var copilot = catalogo.FirstOrDefault(a => a.id == "copilot");
            var edge = catalogo.FirstOrDefault(a => a.id == "edge-molesto");
            bool copilotAplicado = copilot != null && await Task.Run(() => Motor.Aplicado(copilot));
            bool edgeAplicado = edge != null && await Task.Run(() => Motor.Aplicado(edge));
            bool edgeRespaldo = edge != null && await Task.Run(() => Motor.TieneRespaldo(edge));
            bool hayEdge = await Task.Run(() => Apps.EdgeInstalado);
            panelApps.Children.Clear();

            // Copilot: la app, la directiva y el botón de la barra de tareas
            var co = new StackPanel();
            co.Children.Add(Texto("Copilot", 15, Tinta, true));
            var appCopilot = lista.FirstOrDefault(a => a.Paquete == "Microsoft.Copilot" && a.Instalada);
            co.Children.Add(Texto(appCopilot == null && copilotAplicado ? "Ya está quitado y desactivado." : "Quita la app de Copilot, lo desactiva con una directiva de Windows y oculta su botón. Se puede deshacer desde Optimizaciones > Desactivar Copilot.", 12.5, Suave));
            if (copilot != null && (appCopilot != null || !copilotAplicado) && !SoloWindows11(copilot))
            {
                var bc = Boton("Quitar Copilot", true); bc.HorizontalAlignment = HorizontalAlignment.Left; bc.Margin = new Thickness(0, 10, 0, 0);
                bc.Click += async delegate
                {
                    if (MessageBox.Show(this, "Se va a quitar Copilot: la app, la directiva y el botón de la barra de tareas. ¿Seguir?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                    bc.IsEnabled = false; ocupado++;
                    try
                    {
                        await Task.Run(() => { if (appCopilot != null) Apps.Quitar(appCopilot, false); if (!Motor.Aplicado(copilot)) Motor.Aplicar(copilot); });
                        MessageBox.Show(this, "Listo. El botón desaparece al reiniciar el Explorador o la PC.", Title);
                    }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
                    finally { ocupado--; }
                    await RefrescarApps();
                    await RefrescarTrasCambio();
                };
                co.Children.Add(bc);
            }
            panelApps.Children.Add(Caja(co));

            // Edge: primero lo que no rompe nada; desinstalar, solo en opciones avanzadas y con doble confirmación
            var ed = new StackPanel();
            ed.Children.Add(Texto("Microsoft Edge", 15, Tinta, true));
            ed.Children.Add(Texto("Windows y algunas apps lo usan por dentro, y Windows suele reinstalarlo al actualizarse. Lo recomendable es que deje de molestar y elegir otro navegador como predeterminado.", 12.5, Suave));
            var fe = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            if (edge != null && !edgeAplicado)
            {
                var bm = Boton("Desactivar lo molesto de Edge", true);
                bm.Click += async delegate
                {
                    bm.IsEnabled = false; ocupado++;
                    try { await Task.Run(() => Motor.Aplicar(edge)); MessageBox.Show(this, "Listo: Edge ya no arranca solo ni queda abierto en segundo plano. Para que deje de abrirse con los links, elegí otro navegador en la ventana que se abre ahora.\n\nPara sacarlo de la barra de tareas: clic derecho en su ícono > Desanclar.", Title); Abrir("ms-settings:defaultapps"); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
                    finally { ocupado--; }
                    await RefrescarApps();
                };
                fe.Children.Add(bm);
            }
            if (edge != null && edgeRespaldo)
            {
                var br = Boton("Volver Edge a como estaba");
                br.Click += async delegate
                {
                    try { await Task.Run(() => Motor.Revertir(edge)); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
                    await RefrescarApps();
                };
                fe.Children.Add(br);
            }
            var bd = Boton("Navegador predeterminado"); bd.Click += delegate { Abrir("ms-settings:defaultapps"); };
            fe.Children.Add(bd);
            if (Util.Preferencia("avanzado") == "1")
            {
                if (hayEdge)
                {
                    var bu = Boton("Desinstalar Edge...");
                    bu.Click += async delegate
                    {
                        if (MessageBox.Show(this, "Desinstalar Edge puede hacer que algunas partes de Windows (ayuda, widgets, algunos links) no abran bien, y Windows Update puede volver a instalarlo. WebView2, que usan otras apps, no se toca.\n\n¿Querés seguir?", Title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
                        if (MessageBox.Show(this, "Segunda confirmación: ¿desinstalar Microsoft Edge? Lo podés reinstalar desde acá.", Title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
                        bu.IsEnabled = false; ocupado++;
                        try { await Task.Run(() => Apps.DesinstalarEdge()); MessageBox.Show(this, "Edge se desinstaló.", Title); }
                        catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
                        finally { ocupado--; }
                        await RefrescarApps();
                    };
                    fe.Children.Add(bu);
                }
                else
                {
                    var bi = Boton("Reinstalar Edge");
                    bi.Click += async delegate
                    {
                        bi.IsEnabled = false; ocupado++;
                        try { await Task.Run(() => Apps.ReinstalarEdge()); MessageBox.Show(this, "Edge se reinstaló.", Title); }
                        catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
                        finally { ocupado--; }
                        await RefrescarApps();
                    };
                    fe.Children.Add(bi);
                }
            }
            ed.Children.Add(fe);
            panelApps.Children.Add(Caja(ed));

            // Apps de Windows, por grupo
            var nuevas = new CheckBox { Content = "Quitarlas también para las cuentas nuevas que se creen en esta PC", Foreground = Suave, FontSize = 12.5, Margin = new Thickness(0, 6, 0, 8) };
            var marcas = new List<CheckBox>();
            Action<string, string, string, bool> grupo = (clave, titulo, nota, marcadas) =>
            {
                var items = lista.Where(a => a.Grupo == clave && a.Instalada).ToList();
                if (!items.Any()) return;
                panelApps.Children.Add(Subtitulo(titulo));
                var p = new StackPanel();
                if (nota != "") { var n = Texto(nota, 12.5, clave == "xbox" ? Pendiente : Suave); n.Margin = new Thickness(0, 0, 0, 6); p.Children.Add(n); }
                foreach (var a in items)
                {
                    var c = new CheckBox { Content = a.Nombre + (a.Aviso != "" ? "  ·  " + a.Aviso : ""), Tag = a, IsChecked = marcadas, Foreground = Tinta, FontSize = 13.5, Margin = new Thickness(0, 4, 0, 0) };
                    marcas.Add(c); p.Children.Add(c);
                }
                panelApps.Children.Add(Caja(p));
            };
            grupo("recomendado", "Recomendado quitar", "Apps que casi nadie usa, promociones y accesos directos a servicios web.", true);
            grupo("opcional", "Opcional", "Algunas personas las usan: marcá solo las que no.", false);
            grupo("xbox", "Xbox", "Ojo: Game Pass, la app de Xbox, Minecraft Bedrock y otros juegos de Microsoft Store las necesitan. Quitalas solo si no jugás nada de eso.", false);
            if (marcas.Any())
            {
                var bq = Boton("Quitar las marcadas", true); bq.HorizontalAlignment = HorizontalAlignment.Left;
                bq.Click += async delegate
                {
                    var elegidas = marcas.Where(c => c.IsChecked == true).Select(c => (AppQuitable)c.Tag).ToList();
                    if (!elegidas.Any()) return;
                    bool paraNuevas = nuevas.IsChecked == true;
                    if (MessageBox.Show(this, "Se van a quitar:\n\n" + string.Join("\n", elegidas.Select(a => "• " + a.Nombre)) + (paraNuevas ? "\n\nTambién para las cuentas nuevas." : "") + "\n\nCada una se puede reinstalar desde Microsoft Store. ¿Seguir?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                    bq.IsEnabled = false; ocupado++;
                    var errores = new List<string>();
                    try
                    {
                        await Task.Run(() =>
                        {
                            foreach (var a in elegidas)
                            {
                                try { Apps.Quitar(a, paraNuevas); }
                                catch (Exception ex) { lock (errores) errores.Add(a.Nombre + ": " + ex.Message); }
                            }
                        });
                    }
                    finally { ocupado--; }
                    MessageBox.Show(this, (elegidas.Count - errores.Count) + " apps quitadas." + (errores.Any() ? "\n\nNo se pudieron quitar:\n" + string.Join("\n", errores) : ""), Title, MessageBoxButton.OK, errores.Any() ? MessageBoxImage.Warning : MessageBoxImage.Information);
                    await RefrescarApps();
                };
                var fq = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
                fq.Children.Add(nuevas); fq.Children.Add(bq);
                panelApps.Children.Add(fq);
            }

            var quitadas = lista.Where(a => a.Quitada).ToList();
            if (quitadas.Any())
            {
                panelApps.Children.Add(Subtitulo("Quitadas por FrameLab"));
                foreach (var a in quitadas)
                {
                    var app = a;
                    var g = new Grid();
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var t = Texto(a.Nombre, 14, Suave); t.VerticalAlignment = VerticalAlignment.Center;
                    g.Children.Add(t);
                    var b = Boton("Reinstalar"); b.Margin = new Thickness(14, 0, 0, 0);
                    b.Click += delegate { Abrir(Apps.EnlaceTienda(app)); };
                    Grid.SetColumn(b, 1);
                    g.Children.Add(b);
                    panelApps.Children.Add(Caja(g));
                }
            }

            panelApps.Children.Add(Subtitulo("Mejor dejar"));
            panelApps.Children.Add(Caja(Texto("No se ofrecen porque las usa Windows o hacen falta para instalar y actualizar: " + string.Join(", ", Apps.MejorDejar) + ". Los componentes del sistema, como WebView2, .NET o las bibliotecas que usan otras apps, nunca aparecen.", 12.5, Suave)));
        }

        async Task RefrescarRed()
        {
            var estado = await Task.Run(() => Red.Estado());
            panelRed.Children.Clear();
            var s = new StackPanel();
            s.Children.Add(Texto("DNS actual", 14, Tinta, true));
            foreach (var e in estado) s.Children.Add(Texto(e.Key + ": " + (e.Value == "auto" ? "automático" : e.Value), 13, Suave));
            if (estado.Count == 0) s.Children.Add(Texto("No hay ningún adaptador conectado.", 13, Suave));
            var fila = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            foreach (var o in Red.Opciones)
            {
                var op = o;
                var b = Boton(o.Nombre);
                b.Click += async delegate
                {
                    if (MessageBox.Show(this, "¿Cambiar el DNS a " + op.Nombre + " en los adaptadores conectados? La configuración original se guarda.", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                    try { await Task.Run(() => Red.Aplicar(op)); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
                    await RefrescarRed();
                };
                fila.Children.Add(b);
            }
            s.Children.Add(fila);
            var acc = new WrapPanel();
            var v = Boton("Vaciar caché de DNS");
            v.Click += async delegate
            {
                try { await Task.Run(() => Red.Vaciar()); MessageBox.Show(this, "Caché de DNS vaciada.", Title); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
            };
            acc.Children.Add(v);
            if (Red.HayCopia)
            {
                var r = Boton("Restaurar DNS original");
                r.Click += async delegate
                {
                    try { await Task.Run(() => Red.Revertir()); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
                    await RefrescarRed();
                };
                acc.Children.Add(r);
            }
            s.Children.Add(acc);
            panelRed.Children.Add(Caja(s));
        }

        // ---------- Medir ----------
        void ConstruirMedir()
        {
            panelMedir.Children.Clear();
            var est = Texto("Abrí el juego, tocá Empezar a medir y jugá al menos unos minutos. Después tocá Terminar y analizar.", 13.5, Tinta); est.Margin = new Thickness(0, 0, 0, 10);
            var fila = new WrapPanel();
            var ini = Boton("Empezar a medir", true);
            var fin = Boton("Terminar y analizar"); fin.IsEnabled = false;
            fila.Children.Add(ini); fila.Children.Add(fin);
            var s = new StackPanel();
            s.Children.Add(est); s.Children.Add(fila);
            panelMedir.Children.Add(Caja(s));
            var res = new StackPanel();
            panelMedir.Children.Add(res);
            MostrarMediciones(res);

            ini.Click += async delegate
            {
                ini.IsEnabled = false;
                try
                {
                    if (!Medicion.HerramientaLista)
                    {
                        if (MessageBox.Show(this, "Para medir frames hace falta PresentMon, una herramienta gratuita de Intel (1 MB). Se descarga de su GitHub oficial y se comprueba su firma. ¿Descargar?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) { ini.IsEnabled = true; return; }
                        est.Text = "Descargando PresentMon...";
                        await Task.Run(() => Medicion.Descargar());
                    }
                    var nueva = new Medicion();
                    await Task.Run(() => nueva.Iniciar());
                    medicion = nueva;
                    est.Text = "Midiendo... Jugá y volvé cuando termines.";
                    fin.IsEnabled = true;
                }
                catch (Exception ex) { est.Text = "No se pudo empezar: " + ex.Message; ini.IsEnabled = true; }
            };
            fin.Click += async delegate
            {
                fin.IsEnabled = false;
                est.Text = "Analizando...";
                try
                {
                    var m = medicion;
                    await Task.Run(() => m.Detener());
                    est.Text = "Medición terminada.";
                }
                catch (Exception ex) { est.Text = "No se pudo analizar: " + ex.Message; }
                medicion = null;
                ini.IsEnabled = true;
                MostrarMediciones(res);
                await Etapa("Resumen", panelResumen, ConstruirResumen);
                await Etapa("Juegos", panelJuegos, RefrescarJuegos);
            };
        }

        void MostrarMediciones(StackPanel res)
        {
            res.Children.Clear();
            var h = Historial.Leer();
            if (!h.Any()) return;
            var u = h.Last();
            res.Children.Add(Subtitulo("Última medición: " + u.Juego + " (" + u.Fecha + ")"));
            var fila = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, -8, 8) };
            fila.Children.Add(Metrica("Promedio", Math.Round(u.Fps) + " FPS"));
            fila.Children.Add(Metrica("Mínimos 1 %", Math.Round(u.Bajo1) + " FPS"));
            fila.Children.Add(Metrica("Mínimos 0,1 %", Math.Round(u.Bajo01) + " FPS"));
            res.Children.Add(fila);
            res.Children.Add(Caja(Texto(u.Veredicto, 13.5, Tinta)));
            if (h.Count >= 2)
            {
                res.Children.Add(Subtitulo("Antes y después"));
                res.Children.Add(Caja(Texto(Historial.Comparar(h[h.Count - 2], u), 13.5, Tinta)));
            }
        }

        // ---------- Monitor ----------
        TextBlock mGpuUso, mGpuTemp, mGpuMhz, mCpu, mNucleo, mRam, mDisco, mCpuTemp, mAviso;

        void ConstruirMonitorBase()
        {
            Func<string, TextBlock> v = t => Texto("--", 24, Tinta, true);
            Func<string, TextBlock, Border> caja = (t, x) =>
            {
                var s = new StackPanel();
                s.Children.Add(Texto(t, 12.5, Suave));
                x.Margin = new Thickness(0, 4, 0, 0);
                s.Children.Add(x);
                var b = Caja(s); b.Margin = new Thickness(0, 0, 8, 8);
                return b;
            };
            mGpuUso = v(""); mGpuTemp = v(""); mGpuMhz = v(""); mCpu = v(""); mNucleo = v(""); mRam = v(""); mDisco = v("");
            mCpuTemp = Texto("--", 13.5, Tinta); mAviso = Texto("Esperando datos...", 13.5, Suave);
            var g = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, -8, 0) };
            g.Children.Add(caja("Uso de GPU", mGpuUso)); g.Children.Add(caja("Temperatura de GPU", mGpuTemp)); g.Children.Add(caja("Frecuencia de GPU", mGpuMhz)); g.Children.Add(caja("Disco ocupado", mDisco));
            g.Children.Add(caja("CPU total", mCpu)); g.Children.Add(caja("Hilo más cargado", mNucleo)); g.Children.Add(caja("RAM libre", mRam));
            panelMonitor.Children.Add(g);
            var s2 = new StackPanel();
            s2.Children.Add(mAviso);
            var ct = Texto("Temperatura de CPU:", 12.5, Suave); ct.Margin = new Thickness(0, 8, 0, 0);
            s2.Children.Add(ct); s2.Children.Add(mCpuTemp);
            panelMonitor.Children.Add(Caja(s2));
        }

        async Task TickMonitor()
        {
            if (!monitorVisible || muestreando) return;
            muestreando = true;
            try
            {
                var m = await Task.Run(() => Muestreo.Tomar());
                if (textoCpu == "" || DateTime.Now.Second % 15 == 0) textoCpu = await Task.Run(() => Muestreo.TempCpu());
                mGpuUso.Text = m.GpuUso >= 0 ? m.GpuUso + " %" : "N/D";
                mGpuTemp.Text = m.GpuTemp >= 0 ? m.GpuTemp + " °C" : "N/D";
                mGpuMhz.Text = m.GpuMhz >= 0 ? m.GpuMhz + " MHz" : "N/D";
                mCpu.Text = Math.Round(m.CpuTotal) + " %";
                mNucleo.Text = Math.Round(m.CpuNucleo) + " %";
                mRam.Text = (m.RamMb / 1024).ToString("0.0") + " GB";
                mDisco.Text = Math.Round(Math.Min(m.DiscoPct, 100)) + " %";
                mCpuTemp.Text = textoCpu;
                mAviso.Text = m.GpuMotivos != "" ? "La placa de video se está frenando por: " + m.GpuMotivos + "." : m.GpuTemp >= 85 ? "La placa de video está muy caliente." : m.GpuUso < 0 ? (m.GpuTemp >= 0 ? "Esta placa de video no informa su uso ni su frecuencia (pasa en modelos viejos como la GT 710)." : "Los datos de GPU solo están disponibles en NVIDIA por ahora.") : "Sin frenos detectados.";
                mAviso.Foreground = m.GpuMotivos != "" || m.GpuTemp >= 85 ? Pendiente : Ok;
            }
            catch (Exception ex) { Util.RegistrarError(ex); mAviso.Text = "No se pudieron leer los datos."; }
            finally { muestreando = false; }
        }

        // ---------- Instalar ----------
        UIElement ConstruirInstalar()
        {
            paquetes = new List<Paquete> {
                new Paquete { Nombre = "Google Chrome", Id = "Google.Chrome" },
                new Paquete { Nombre = "Epic Games Launcher", Id = "EpicGames.EpicGamesLauncher" },
                new Paquete { Nombre = "Roblox", Id = "Roblox.Roblox" },
                new Paquete { Nombre = "Minecraft Launcher", Id = "Mojang.MinecraftLauncher" },
                new Paquete { Nombre = "Steam", Id = "Valve.Steam" },
                new Paquete { Nombre = "Discord", Id = "Discord.Discord" }
            };
            var cuerpo = new StackPanel();
            cuerpo.Children.Add(panelWinget);
            var panel = new StackPanel();
            foreach (var p in paquetes)
            {
                p.Marca = new CheckBox { Content = p.Nombre, FontSize = 14, Margin = new Thickness(0, 0, 0, 10) };
                panel.Children.Add(p.Marca);
            }
            botonInstalar = Boton("Instalar seleccionados", true);
            botonInstalar.HorizontalAlignment = HorizontalAlignment.Left;
            botonInstalar.Margin = new Thickness(0, 4, 0, 0);
            botonInstalar.Click += async delegate { await InstalarSeleccion(); };
            panel.Children.Add(botonInstalar);
            cuerpo.Children.Add(Caja(panel));
            cuerpo.Children.Add(Subtitulo("Drivers"));
            panelDrivers.Children.Add(Texto("Detectando la placa de video...", 13, Suave));
            cuerpo.Children.Add(Caja(panelDrivers));
            registro = new TextBox { IsReadOnly = true, Height = 170, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), FontSize = 12, Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(10), Background = Tarjeta, Foreground = Suave, BorderBrush = Linea, Visibility = Visibility.Collapsed };
            cuerpo.Children.Add(registro);
            return cuerpo;
        }

        // En Windows 10 viejo puede faltar winget: se avisa antes de que falle la instalación
        async Task RevisarWinget()
        {
            bool hay = await Task.Run(() =>
            {
                try
                {
                    int c;
                    Util.Correr("winget", "--version", out c, 20000);
                    return c == 0;
                }
                catch (System.ComponentModel.Win32Exception) { return false; }
            });
            panelWinget.Children.Clear();
            if (hay) return;
            var s = new StackPanel();
            s.Children.Add(Texto("Falta winget", 14, Tinta, true));
            var t = Texto("Para instalar programas desde acá hace falta App Installer (Instalador de aplicación), que puede no venir en versiones viejas de Windows 10. Instalalo o actualizalo desde Microsoft Store y volvé a abrir la app.", 12.5, Suave); t.Margin = new Thickness(0, 0, 0, 10);
            s.Children.Add(t);
            var b = Boton("Abrir App Installer en Microsoft Store", true); b.HorizontalAlignment = HorizontalAlignment.Left;
            b.Click += delegate { Abrir("ms-windows-store://pdp/?productid=9NBLGGH4NNS1"); };
            s.Children.Add(b);
            panelWinget.Children.Add(Caja(s));
        }

        async Task ConstruirDrivers()
        {
            string f = diag.FabricanteGpu;
            string ver = f == "NVIDIA" ? await Task.Run(() => Salud.Nvidia("driver_version")) : null;
            panelDrivers.Children.Clear();
            panelDrivers.Children.Add(Texto(f == "" ? "No se reconoció el fabricante de la placa de video." : "Placa de video: " + f + (ver != null ? ", driver " + ver : ""), 13.5, Tinta));
            var fila = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            string url = f == "NVIDIA" ? "https://www.nvidia.com/es-la/software/nvidia-app/" : f == "AMD" ? "https://www.amd.com/es/support/download/drivers.html" : f == "Intel" ? "https://www.intel.com/content/www/us/en/download-center/home.html" : "";
            if (url != "")
            {
                var b = Boton("Abrir la descarga oficial de " + f, true);
                b.Click += delegate { Abrir(url); };
                fila.Children.Add(b);
            }
            var w = Boton("Buscar drivers en Windows Update");
            w.Click += delegate { Abrir("ms-settings:windowsupdate-optionalupdates"); };
            fila.Children.Add(w);
            panelDrivers.Children.Add(fila);
            panelDrivers.Children.Add(Texto("La app de cada fabricante indica si hay una versión más nueva. Los drivers de la placa madre conviene descargarlos de la web del fabricante.", 12.5, Suave));
        }

        void Abrir(string destino)
        {
            try { Process.Start(new ProcessStartInfo(destino) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo abrir: " + ex.Message, Title); }
        }

        void Anotar(string t)
        {
            Dispatcher.Invoke(new Action(() => { registro.Visibility = Visibility.Visible; registro.AppendText(t + Environment.NewLine); registro.ScrollToEnd(); }));
        }

        async Task InstalarSeleccion()
        {
            var lista = paquetes.Where(p => p.Marca.IsChecked == true).ToList();
            if (lista.Count == 0) { MessageBox.Show(this, "No hay ningún programa seleccionado.", Title); return; }
            botonInstalar.IsEnabled = false;
            ocupado++;
            try
            {
                int fallos = 0;
                foreach (var p in lista)
                {
                    Anotar("Instalando " + p.Nombre + "...");
                    int c = await Task.Run(() => Winget(p.Id));
                    if (c == 0) Anotar(p.Nombre + ": listo.");
                    else if (c == WingetSinActualizacion || c == WingetYaInstalado) Anotar(p.Nombre + ": ya está instalado y actualizado.");
                    else if (c == WingetReiniciar) Anotar(p.Nombre + ": instalado. Reiniciá la PC para terminar.");
                    else if (c == -2) { Anotar("No se encontró winget. Instalá App Installer (Instalador de aplicación) desde Microsoft Store y volvé a abrir la app."); fallos++; break; }
                    else { Anotar(p.Nombre + ": " + ErrorWinget(c)); fallos++; }
                }
                Anotar(fallos == 0 ? "Instalación terminada." : "Terminado con " + fallos + " error(es).");
            }
            catch (Exception ex) { Anotar("La instalación se interrumpió: " + ex.Message); }
            finally
            {
                ocupado--;
                botonInstalar.IsEnabled = true;
            }
        }

        // Códigos publicados en la documentación de winget
        const int WingetSinActualizacion = unchecked((int)0x8A15002B), WingetYaInstalado = unchecked((int)0x8A150061), WingetReiniciar = unchecked((int)0x8A150109);

        static string ErrorWinget(int c)
        {
            switch (unchecked((uint)c))
            {
                case 0x8A150010: return "no hay un instalador compatible con esta PC.";
                case 0x8A150014: return "winget no encontró el programa.";
                case 0x8A150101: return "el programa está abierto: cerralo y volvé a intentar.";
                case 0x8A150102: return "hay otra instalación en curso: probá más tarde.";
                case 0x8A150105: return "no hay espacio suficiente en el disco.";
                case 0x8A150107: return "hace falta conexión a internet.";
                case 0x8A15010C: return "la instalación se canceló.";
                default: return "falló (código 0x" + c.ToString("X8") + ").";
            }
        }

        int Winget(string id)
        {
            try
            {
                var si = new ProcessStartInfo("winget", "install --id " + id + " --exact --source winget --silent --accept-package-agreements --accept-source-agreements --disable-interactivity")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
                using (var p = Process.Start(si))
                {
                    p.OutputDataReceived += (s, e) => { if (Util.LineaUtil(e.Data)) Anotar("  " + e.Data.Trim()); };
                    p.ErrorDataReceived += (s, e) => { if (Util.LineaUtil(e.Data)) Anotar("  " + e.Data.Trim()); };
                    p.BeginOutputReadLine(); p.BeginErrorReadLine();
                    p.WaitForExit();
                    return p.ExitCode;
                }
            }
            catch (System.ComponentModel.Win32Exception) { return -2; }
        }

        // ---------- Modo partida ----------
        readonly StackPanel listaPartida = new StackPanel();
        readonly TextBlock estadoPartida = Texto("", 13, Suave);
        Button botonPartida, botonFinPartida;
        CheckBox gpuAlta, sinOptPantalla;
        DispatcherTimer relojPartida;

        UIElement CuerpoJuegos()
        {
            var s = new StackPanel();
            var c = new StackPanel();
            c.Children.Add(Texto("Modo partida", 16, Tinta, true));
            var d = Texto("Activalo antes de abrir el juego. Cierra los programas que marques y los vuelve a abrir al terminar. Mientras dura, le da prioridad alta al juego, baja la de navegadores y launchers que queden abiertos, pausa la indexación de búsqueda de Windows y usa el plan de alto rendimiento. Todo vuelve a como estaba al terminar.", 12.5, Suave);
            d.Margin = new Thickness(0, 4, 0, 10);
            c.Children.Add(d);
            c.Children.Add(listaPartida);
            var fila = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            var act = Boton("Actualizar lista"); act.Click += async delegate { await ListarPartida(); };
            botonPartida = Boton("Empezar modo partida", true); botonPartida.Click += async delegate { await EmpezarPartida(); };
            botonFinPartida = Boton("Terminar y volver a abrir"); botonFinPartida.IsEnabled = false; botonFinPartida.Click += async delegate { await TerminarPartida(); };
            fila.Children.Add(botonPartida); fila.Children.Add(botonFinPartida); fila.Children.Add(act);
            c.Children.Add(fila);
            var extra = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            extra.Children.Add(new TextBlock { Text = "Otros juegos (nombre del .exe, separados por coma): ", Foreground = Suave, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center });
            var juegosExtra = new TextBox { Width = 260, Text = string.Join(", ", Partida.JuegosExtra()), Background = Tarjeta, Foreground = Tinta, BorderBrush = Linea, Padding = new Thickness(4, 2, 4, 2) };
            juegosExtra.LostFocus += delegate { Partida.GuardarJuegosExtra(juegosExtra.Text); juegosExtra.Text = string.Join(", ", Partida.JuegosExtra()); };
            extra.Children.Add(juegosExtra);
            c.Children.Add(extra);
            gpuAlta = new CheckBox { Content = "Usar la placa de video de alto rendimiento en los juegos (para notebooks con dos placas)", Foreground = Suave, FontSize = 12.5, Margin = new Thickness(0, 10, 0, 0), IsChecked = Util.Preferencia("gpuAlta") == "1", Visibility = Visibility.Collapsed };
            gpuAlta.Click += delegate
            {
                Util.GuardarPreferencia("gpuAlta", gpuAlta.IsChecked == true ? "1" : "0");
                if (gpuAlta.IsChecked != true) { try { Partida.QuitarGpuAlta(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, Title); } }
            };
            c.Children.Add(gpuAlta);
            sinOptPantalla = new CheckBox { Content = "Desactivar las optimizaciones de pantalla completa en los juegos que detecte (algunos juegos van más fluidos; si notás algo raro, desmarcalo)", Foreground = Suave, FontSize = 12.5, Margin = new Thickness(0, 8, 0, 0), IsChecked = Util.Preferencia("sinOptPantalla") == "1" };
            sinOptPantalla.Click += delegate
            {
                Util.GuardarPreferencia("sinOptPantalla", sinOptPantalla.IsChecked == true ? "1" : "0");
                if (sinOptPantalla.IsChecked != true) { try { Partida.QuitarPantallaCompleta(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, Title); } }
            };
            c.Children.Add(sinOptPantalla);
            estadoPartida.Margin = new Thickness(0, 8, 0, 0);
            c.Children.Add(estadoPartida);
            var nota = Texto("Para saber cuánto te sirve, medí una partida sin el modo partida y otra con el modo partida activo, desde Medir partida. En PCs con poca RAM o un procesador de 4 hilos suele notarse en menos tirones; en PCs potentes la diferencia es chica.", 12, Suave);
            nota.Margin = new Thickness(0, 8, 0, 0);
            c.Children.Add(nota);
            s.Children.Add(Caja(c));
            s.Children.Add(Subtitulo("Ajustes de cada juego"));
            s.Children.Add(panelJuegos);
            Loaded += async delegate { await ListarPartida(); };
            return s;
        }

        async Task ListarPartida()
        {
            if (diag != null && diag.Gpus.Count >= 2) gpuAlta.Visibility = Visibility.Visible;
            if (Partida.Activo) return;
            var l = await Task.Run(() => Partida.Listar());
            var elegidos = Util.Preferencia("partida");
            var marcados = elegidos == null ? new[] { "chrome", "msedge", "firefox", "opera", "brave" } : elegidos.Split(',');
            listaPartida.Children.Clear();
            if (!l.Any()) listaPartida.Children.Add(Texto("No hay programas abiertos para cerrar. Igual podés activarlo para el resto de las mejoras.", 12.5, Suave));
            foreach (var a in l)
                listaPartida.Children.Add(new CheckBox { Content = a.Nombre + "  (" + (a.Bytes / 1e9).ToString("0.0") + " GB de RAM)", Tag = a.Proceso, Foreground = Tinta, FontSize = 13.5, Margin = new Thickness(0, 0, 0, 6), IsChecked = marcados.Contains(a.Proceso) });
            listaPartida.Children.Add(Texto("No cierres el launcher del juego que vas a jugar (por ejemplo Steam o Epic), y guardá lo que tengas abierto en el navegador.", 12, Suave));
        }

        async Task EmpezarPartida()
        {
            var cerrar = listaPartida.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToList();
            Util.GuardarPreferencia("partida", string.Join(",", cerrar));
            if (cerrar.Any() && MessageBox.Show(this, "Se van a cerrar: " + string.Join(", ", listaPartida.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => ((string)c.Content).Split('(')[0].Trim())) + ".\n\nGuardá lo que tengas abierto. Al terminar el modo partida se vuelven a abrir. ¿Seguir?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            botonPartida.IsEnabled = false;
            estadoPartida.Text = "Preparando...";
            try
            {
                long liberado = await Task.Run(() => Partida.Empezar(cerrar));
                estadoPartida.Text = "Modo partida activo." + (liberado > 0 ? " Se liberaron unos " + (liberado / 1e9).ToString("0.0") + " GB de RAM." : "") + " Abrí el juego.";
                estadoPartida.Foreground = Ok;
                botonFinPartida.IsEnabled = true;
                listaPartida.IsEnabled = false;
                relojPartida = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                relojPartida.Tick += async delegate
                {
                    bool gpu = gpuAlta.IsChecked == true && gpuAlta.Visibility == Visibility.Visible, pantalla = sinOptPantalla.IsChecked == true;
                    try
                    {
                        var j = await Task.Run(() => Partida.Vigilar(gpu, pantalla));
                        if (j.Any()) estadoPartida.Text = "Modo partida activo. Juego detectado: " + string.Join(", ", j) + ", con prioridad alta.";
                    }
                    catch (Exception ex) { Util.RegistrarError(ex); }
                };
                relojPartida.Start();
            }
            catch (Exception ex)
            {
                estadoPartida.Text = "No se pudo activar: " + ex.Message;
                estadoPartida.Foreground = Pendiente;
                botonPartida.IsEnabled = true;
            }
        }

        async Task TerminarPartida()
        {
            botonFinPartida.IsEnabled = false;
            if (relojPartida != null) relojPartida.Stop();
            try
            {
                var abiertos = await Task.Run(() => Partida.Terminar());
                estadoPartida.Text = "Modo partida terminado. Todo volvió a como estaba." + (abiertos.Any() ? " Se volvieron a abrir: " + string.Join(", ", abiertos) + "." : "");
                estadoPartida.Foreground = Suave;
            }
            catch (Exception ex) { estadoPartida.Text = "No se pudo terminar del todo: " + ex.Message; estadoPartida.Foreground = Pendiente; }
            listaPartida.IsEnabled = true;
            botonPartida.IsEnabled = true;
            await ListarPartida();
        }

        // Si la app se cerró de golpe durante una partida, al abrirla se deshace lo que quedó cambiado
        void RestaurarPartidaPendiente()
        {
            if (Partida.Leer() == null) return;
            var pendientes = Partida.PendientesDeReabrir();
            bool reabrir = pendientes.Any() && MessageBox.Show(this, "El modo partida no terminó bien la última vez. Ya se restauró el plan de energía y la búsqueda de Windows.\n\n¿Volver a abrir los programas que se habían cerrado (" + string.Join(", ", pendientes.Select(Path.GetFileNameWithoutExtension)) + ")?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            try { Partida.Terminar(reabrir); }
            catch (Exception ex) { Util.RegistrarError(ex); }
        }

        // ---------- Ordenar escritorio ----------
        readonly StackPanel listaOrden = new StackPanel();
        readonly TextBlock estadoOrden = Texto("", 13, Suave);
        string raizOrden;
        Button botonOrdenar, botonDeshacerOrden;

        UIElement ConstruirOrdenar()
        {
            var s = new StackPanel();
            var c = new StackPanel();
            c.Children.Add(Texto("Mueve los archivos sueltos a carpetas por tipo: Imágenes, Documentos, Videos, Música, Instaladores, Comprimidos y Otros. Las carpetas que ya tenés no se tocan. Antes de mover nada te muestra la lista, y después lo podés deshacer.", 12.5, Suave));
            var accesos = new CheckBox { Content = "Incluir los accesos directos (si no, se quedan en el escritorio)", Foreground = Suave, FontSize = 12.5, Margin = new Thickness(0, 10, 0, 0) };
            c.Children.Add(accesos);
            var fila = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            var esc = Boton("Revisar el escritorio", true);
            var des = Boton("Revisar Descargas");
            botonOrdenar = Boton("Ordenar los marcados", true); botonOrdenar.IsEnabled = false;
            botonDeshacerOrden = Boton("Deshacer orden");
            fila.Children.Add(esc); fila.Children.Add(des); fila.Children.Add(botonOrdenar); fila.Children.Add(botonDeshacerOrden);
            c.Children.Add(fila);
            estadoOrden.Margin = new Thickness(0, 6, 0, 0);
            c.Children.Add(estadoOrden);
            s.Children.Add(Caja(c));
            s.Children.Add(listaOrden);

            Action<string> revisar = raiz =>
            {
                raizOrden = raiz;
                listaOrden.Children.Clear();
                botonDeshacerOrden.IsEnabled = Ordenar.HayParaDeshacer(raiz);
                List<Movimiento> plan;
                try { plan = Ordenar.Planear(raiz, accesos.IsChecked == true); }
                catch (Exception ex) { estadoOrden.Text = "No se pudo revisar: " + ex.Message; return; }
                estadoOrden.Text = plan.Count == 0 ? "No hay archivos sueltos para ordenar en " + raiz + "." : plan.Count + " archivos sueltos en " + raiz + ". Desmarcá los que quieras dejar donde están.";
                botonOrdenar.IsEnabled = plan.Count > 0;
                foreach (var g in plan.GroupBy(m => m.Categoria))
                {
                    var p = new StackPanel();
                    p.Children.Add(Texto(g.Key + "  (" + g.Count() + ")", 14, Tinta, true));
                    foreach (var m in g)
                        p.Children.Add(new CheckBox { Content = Path.GetFileName(m.Origen), Tag = m, IsChecked = true, Foreground = Suave, FontSize = 12.5, Margin = new Thickness(0, 4, 0, 0) });
                    listaOrden.Children.Add(Caja(p));
                }
            };
            esc.Click += delegate { revisar(Ordenar.Escritorio); };
            des.Click += delegate { revisar(Ordenar.Descargas); };
            accesos.Click += delegate { if (raizOrden != null) revisar(raizOrden); };
            botonDeshacerOrden.IsEnabled = Ordenar.HayParaDeshacer(Ordenar.Escritorio);
            raizOrden = Ordenar.Escritorio;

            botonOrdenar.Click += async delegate
            {
                var elegidos = listaOrden.Children.OfType<Border>().Select(b => b.Child).OfType<StackPanel>().SelectMany(p => p.Children.OfType<CheckBox>()).Where(x => x.IsChecked == true).Select(x => (Movimiento)x.Tag).ToList();
                if (elegidos.Count == 0) return;
                if (MessageBox.Show(this, "Se van a mover " + elegidos.Count + " archivos a carpetas por tipo dentro de " + raizOrden + ". Lo podés deshacer después. ¿Seguir?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                botonOrdenar.IsEnabled = false;
                var salteados = new List<string>();
                string raiz = raizOrden;
                try
                {
                    int n = await Task.Run(() => Ordenar.Aplicar(raiz, elegidos, salteados));
                    listaOrden.Children.Clear();
                    estadoOrden.Text = "Listo: se ordenaron " + n + " archivos." + (salteados.Any() ? " Quedaron sin mover porque estaban en uso: " + string.Join(", ", salteados) + "." : "");
                }
                catch (Exception ex) { estadoOrden.Text = "No se pudo terminar: " + ex.Message + " Lo que se movió se puede deshacer."; }
                botonDeshacerOrden.IsEnabled = Ordenar.HayParaDeshacer(raiz);
            };
            botonDeshacerOrden.Click += async delegate
            {
                string raiz = raizOrden;
                if (!Ordenar.HayParaDeshacer(raiz)) { estadoOrden.Text = "No hay un orden para deshacer en " + raiz + "."; return; }
                if (MessageBox.Show(this, "Los archivos que ordenó la app en " + raiz + " van a volver a donde estaban. ¿Seguir?", Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                var problemas = new List<string>();
                try
                {
                    int n = await Task.Run(() => Ordenar.Deshacer(raiz, problemas));
                    estadoOrden.Text = n + " archivos volvieron a su lugar." + (problemas.Any() ? "\n" + string.Join("\n", problemas) : "");
                }
                catch (Exception ex) { estadoOrden.Text = ex.Message; }
                listaOrden.Children.Clear();
                botonDeshacerOrden.IsEnabled = Ordenar.HayParaDeshacer(raiz);
            };
            return s;
        }

        // ---------- Personalización ----------
        readonly Border capaFondo = new Border { IsHitTestVisible = false };

        void AplicarFondo()
        {
            string tipo = Util.Preferencia("fondoApp") ?? "framelab";
            double op;
            if (!double.TryParse(Util.Preferencia("fondoIntensidad"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out op)) op = 0.35;
            capaFondo.Background = null;
            try
            {
                if (tipo == "framelab")
                {
                    var dv = new DrawingVisual();
                    using (var dc = dv.RenderOpen()) FondoFrameLab.Dibujar(dc, 1920, 1080, null, false);
                    var bmp = new RenderTargetBitmap(1920, 1080, 96, 96, PixelFormats.Pbgra32);
                    bmp.Render(dv); bmp.Freeze();
                    capaFondo.Background = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
                }
                else if (tipo == "imagen")
                {
                    string ruta = Util.Preferencia("fondoImagen");
                    if (ruta == null || !File.Exists(ruta)) return;
                    var i = new BitmapImage();
                    i.BeginInit(); i.UriSource = new Uri(ruta); i.CacheOption = BitmapCacheOption.OnLoad; i.DecodePixelWidth = 1920; i.EndInit(); i.Freeze();
                    capaFondo.Background = new ImageBrush(i) { Stretch = Stretch.UniformToFill };
                }
            }
            catch (Exception ex) { Util.RegistrarError(ex); }
            capaFondo.Opacity = Math.Max(0.05, Math.Min(1, op));
        }

        UIElement ConstruirPersonalizacion()
        {
            var s = new StackPanel();

            var app = new StackPanel();
            app.Children.Add(Texto("Fondo de la app", 15, Tinta, true));
            var fa = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            Action<string, string> opcion = (texto, tipo) =>
            {
                var b = Boton(texto);
                b.Click += delegate { Util.GuardarPreferencia("fondoApp", tipo); AplicarFondo(); };
                fa.Children.Add(b);
            };
            opcion("Fondo FrameLab", "framelab");
            opcion("Sin fondo", "ninguno");
            var propia = Boton("Elegir una imagen...");
            propia.Click += delegate
            {
                var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.bmp" };
                if (dlg.ShowDialog(this) != true) return;
                try
                {
                    // Se guarda una copia: si movés o borrás la original, el fondo sigue
                    string destino = Path.Combine(Medicion.Carpeta, "fondo_app" + Path.GetExtension(dlg.FileName).ToLowerInvariant());
                    Directory.CreateDirectory(Medicion.Carpeta);
                    File.Copy(dlg.FileName, destino, true);
                    Util.GuardarPreferencia("fondoImagen", destino);
                    Util.GuardarPreferencia("fondoApp", "imagen");
                    AplicarFondo();
                }
                catch (Exception ex) { MessageBox.Show(this, "No se pudo usar esa imagen: " + ex.Message, Title); }
            };
            fa.Children.Add(propia);
            app.Children.Add(fa);
            var intensidad = new Slider { Minimum = 0.1, Maximum = 0.8, Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Value = capaFondo.Opacity };
            intensidad.ValueChanged += delegate { capaFondo.Opacity = intensidad.Value; Util.GuardarPreferencia("fondoIntensidad", intensidad.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)); };
            app.Children.Add(Texto("Intensidad", 12.5, Suave));
            app.Children.Add(intensidad);
            s.Children.Add(Caja(app));

            var esc = new StackPanel();
            esc.Children.Add(Texto("Fondo de escritorio FrameLab", 15, Tinta, true));
            esc.Children.Add(Texto("Pone en Windows un fondo propio de FrameLab, del tamaño de tu monitor. Antes guarda el que tenés para poder volver.", 12.5, Suave));
            var fe = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            var poner = Boton("Poner fondo FrameLab", true);
            var volver = Boton("Volver al fondo anterior");
            volver.IsEnabled = FondoFrameLab.HayCopia;
            var res = Texto("", 12.5, Suave);
            poner.Click += delegate
            {
                try { FondoFrameLab.PonerEscritorio(CargarLogo()); res.Text = "Listo. Mirá el escritorio."; volver.IsEnabled = true; }
                catch (Exception ex) { res.Text = ex.Message; }
            };
            volver.Click += delegate
            {
                try { FondoFrameLab.VolverAnterior(); res.Text = "Volvió tu fondo anterior."; volver.IsEnabled = false; }
                catch (Exception ex) { res.Text = ex.Message; }
            };
            fe.Children.Add(poner); fe.Children.Add(volver);
            esc.Children.Add(fe);
            esc.Children.Add(res);
            s.Children.Add(Caja(esc));
            return s;
        }

        UIElement ConstruirAcerca()
        {
            var s = new StackPanel();
            s.Children.Add(Texto("FrameLab " + Version.Actual, 15, Tinta, true));
            var d = Texto("Cada ajuste indica su impacto y su riesgo, y se puede revertir. No incluye limpiadores de RAM, desactivar Defender, desactivar actualizaciones para siempre ni drivers no oficiales.", 13, Suave); d.Margin = new Thickness(0, 6, 0, 12);
            s.Children.Add(d);
            var res = Texto("", 13, Tinta); res.Visibility = Visibility.Collapsed;
            var b = Boton("Buscar actualizaciones"); b.HorizontalAlignment = HorizontalAlignment.Left;
            var acciones = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            b.Click += async delegate
            {
                res.Visibility = Visibility.Visible; res.Text = "Buscando...";
                acciones.Children.Clear();
                b.IsEnabled = false;
                try
                {
                    if (Version.HayConfiguracionPropia) { res.Text = await Task.Run(() => Version.Buscar()); return; }
                    var n = await Task.Run(() => Version.BuscarEnGitHub());
                    if (n == null) { res.Text = "Tenés la última versión (" + Version.Actual + ")."; return; }
                    if (n.Privado)
                    {
                        res.Text = "Las actualizaciones automáticas van a funcionar cuando FrameLab sea público. Por ahora bajá la versión nueva desde GitHub.";
                        var g = Boton("Abrir la página de descargas", true); g.Click += delegate { Abrir(Version.PaginaDescargas); };
                        acciones.Children.Add(g);
                        return;
                    }
                    res.Text = "Hay una versión nueva: " + n.Numero + "." + (string.IsNullOrWhiteSpace(n.Notas) ? "" : "\n\n" + n.Notas.Replace("**", "").Trim());
                    var inst = Boton("Descargar e instalar", true);
                    inst.Click += async delegate
                    {
                        if (ocupado > 0 || Partida.Activo || medicion != null) { MessageBox.Show(this, "Terminá lo que está en curso (cambios, modo partida o medición) antes de actualizar.", Title); return; }
                        inst.IsEnabled = false;
                        res.Text = "Descargando la versión " + n.Numero + "...";
                        try
                        {
                            await Task.Run(() => Version.Instalar(n));
                            res.Text = "Listo. Se abre la versión nueva.";
                            Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location) { UseShellExecute = true });
                            Application.Current.Shutdown();
                        }
                        catch (Exception ex) { res.Text = "No se pudo actualizar: " + ex.Message + " Seguís con la versión " + Version.Actual + "."; inst.IsEnabled = true; }
                    };
                    acciones.Children.Add(inst);
                }
                catch (Exception ex) { res.Text = "No se pudo comprobar: " + ex.Message; }
                finally { b.IsEnabled = true; }
            };
            s.Children.Add(b); s.Children.Add(res); s.Children.Add(acciones);
            var todo = new StackPanel();
            todo.Children.Add(Caja(s));
            todo.Children.Add(Subtitulo("Por qué no está"));
            for (int i = 0; i < NoIncluido.Lista.GetLength(0); i++)
            {
                var n = new StackPanel();
                n.Children.Add(Texto(NoIncluido.Lista[i, 0], 14, Tinta, true));
                n.Children.Add(Texto(NoIncluido.Lista[i, 1], 12.5, Suave));
                todo.Children.Add(Caja(n));
            }
            return todo;
        }

        // Solo para comprobar la app: guarda una imagen de cada página y no toca ajustes
        public void GuardarCapturas(string carpeta)
        {
            Directory.CreateDirectory(carpeta);
            Action<FrameworkElement, string> guardar = (fe, ruta) =>
            {
                // Se dibuja con un pincel para que el margen del elemento no desplace la imagen
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen()) { dc.DrawRectangle(Fondo, null, new Rect(0, 0, fe.ActualWidth, fe.ActualHeight)); dc.DrawRectangle(new VisualBrush(fe), null, new Rect(0, 0, fe.ActualWidth, fe.ActualHeight)); }
                var bmp = new RenderTargetBitmap((int)fe.ActualWidth, (int)fe.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(dv);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                using (var f = File.Create(ruta)) enc.Save(f);
            };
            AplicarModo(true);
            foreach (var r in menu.Children.OfType<RadioButton>())
            {
                // Se pasa por el menú, igual que un clic, para que la captura muestre la sección marcada
                r.IsChecked = true;
                UpdateLayout();
                Dispatcher.Invoke(new Action(() => { }), DispatcherPriority.Loaded);
                string clave = (string)r.Tag;
                guardar((FrameworkElement)Content, Path.Combine(carpeta, clave + ".png"));
                guardar((FrameworkElement)((ScrollViewer)paginas[clave]).Content, Path.Combine(carpeta, clave + "-completa.png"));
            }
            monitorVisible = false;
            var sb = new StringBuilder();
            foreach (var f in diag.Filas) sb.AppendLine(f.Key + ": " + f.Value);
            sb.AppendLine("GPU: " + diag.FabricanteGpu + " | Portatil: " + diag.EsPortatil + " | SoloSSD: " + diag.SoloSsd);
            foreach (var a in Salud.Revisar(diag)) sb.AppendLine("Salud | " + a.Nivel + " | " + a.Titulo + " | " + a.Detalle);
            sb.AppendLine("Limite | " + Salud.RecomendarLimite(null));
            foreach (var f in frenos) sb.AppendLine("Freno | " + f.Peso + " | " + f.Titulo);
            sb.AppendLine("Gama GPU | " + Frenos.MejorGama(diag.Gpus));
            foreach (var a in catalogo.Where(x => x.guia == null)) sb.AppendLine(a.categoria + " | " + Motor.Detalle(a) + " | " + a.id);
            foreach (var e in Inicio.Listar()) sb.AppendLine("Inicio | " + (e.Activo ? "activo" : "desactivado") + " | " + e.Nombre);
            foreach (var j in Juegos.Detectar(Juegos.NivelDe(diag.Filas.Where(f => f.Key == "Placa de video").Select(f => f.Value)), Salud.LimiteFps())) sb.AppendLine("Juego | " + j.Instalado + " | " + j.Nombre);
            foreach (var z in Limpieza.Analizar()) sb.AppendLine("Limpieza | " + (z.Bytes / 1048576) + " MB | " + z.Nombre);
            foreach (var a in Apps.Listar().Where(x => x.Instalada)) sb.AppendLine("App | " + a.Nombre);
            var m = Muestreo.Tomar();
            sb.AppendLine("Muestra | GPU " + m.GpuUso + "% " + m.GpuTemp + "C " + m.GpuMhz + "MHz motivos=" + m.GpuMotivos + " | CPU " + m.CpuTotal + "% nucleo " + m.CpuNucleo + "% | RAM " + m.RamMb + "MB | disco " + m.DiscoPct + "%");
            File.WriteAllText(Path.Combine(carpeta, "estado.txt"), sb.ToString(), Encoding.UTF8);
        }
    }

    public static class Util
    {
        // Lee las dos salidas a la vez: si se llena la de errores sin leerla, el proceso y la app quedan colgados.
        // Si no termina a tiempo se cierra y devuelve el código -1
        public static string Correr(string exe, string args, out int codigo, int ms = 120000)
        {
            var si = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            using (var p = Process.Start(si))
            {
                var o = p.StandardOutput.ReadToEndAsync();
                var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(ms))
                {
                    try { p.Kill(); } catch (Exception) { }
                    codigo = -1;
                    return "";
                }
                codigo = p.ExitCode;
                // Un proceso hijo puede dejar la salida abierta: no se espera para siempre
                Task.WaitAll(new Task[] { o, e }, 3000);
                return o.Status == TaskStatus.RanToCompletion ? o.Result : "";
            }
        }

        public static string Ps(string comando, out int codigo, int ms = 120000)
        {
            return Correr("powershell", "-NoProfile -NonInteractive -Command \"[Console]::OutputEncoding=[Text.Encoding]::UTF8; " + comando + "\"", out codigo, ms);
        }

        // Escribe en un archivo aparte y lo reemplaza de una vez: un corte a mitad de camino no deja el original dañado
        public static void EscribirSeguro(string ruta, string texto, Encoding enc, string copiaAnterior)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            string tmp = ruta + ".tmp";
            using (var f = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bom = enc.GetPreamble(), datos = enc.GetBytes(texto);
                f.Write(bom, 0, bom.Length);
                f.Write(datos, 0, datos.Length);
                f.Flush(true);
            }
            if (File.Exists(ruta)) File.Replace(tmp, ruta, copiaAnterior);
            else File.Move(tmp, ruta);
        }

        static string RutaPreferencias { get { return Path.Combine(Medicion.Carpeta, "preferencias.txt"); } }

        public static string Preferencia(string clave)
        {
            try
            {
                if (!File.Exists(RutaPreferencias)) return null;
                return File.ReadAllLines(RutaPreferencias).Where(l => l.StartsWith(clave + "=")).Select(l => l.Substring(clave.Length + 1)).LastOrDefault();
            }
            catch (Exception) { return null; }
        }

        public static void GuardarPreferencia(string clave, string valor)
        {
            try
            {
                var l = File.Exists(RutaPreferencias) ? File.ReadAllLines(RutaPreferencias).Where(x => !x.StartsWith(clave + "=")).ToList() : new List<string>();
                l.Add(clave + "=" + valor);
                EscribirSeguro(RutaPreferencias, string.Join(Environment.NewLine, l), new UTF8Encoding(false), null);
            }
            catch (Exception ex) { RegistrarError(ex); }
        }

        public static void RegistrarError(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Medicion.Carpeta);
                File.AppendAllText(Path.Combine(Medicion.Carpeta, "errores.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + Version.Actual + " " + ex + Environment.NewLine + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception) { }
        }

        // winget dibuja barras y animaciones que ensucian el registro
        public static bool LineaUtil(string l)
        {
            if (string.IsNullOrWhiteSpace(l)) return false;
            string t = l.Trim();
            return t.Length > 2 && t.Any(char.IsLetter) && t.IndexOf('█') < 0 && t.IndexOf('▒') < 0;
        }
    }

    public class Programa
    {
        [STAThread]
        public static int Main(string[] args)
        {
            var app = new Application();
            if (args.Length == 2 && args[0] == "--prueba-motor") return PruebaMotor(args[1]);
            if (args.Length == 3 && args[0] == "--prueba-analisis") return PruebaAnalisis(args[1], args[2]);
            bool prueba = args.Length >= 2 && args[0] == "--prueba";
            if (prueba && args.Length == 3) Diagnostico.BuildSimulado = int.Parse(args[2]);
            // Último recurso: un error no previsto se anota y se avisa, sin cerrar la app de golpe
            if (!prueba)
            {
                app.DispatcherUnhandledException += (s, e) =>
                {
                    Util.RegistrarError(e.Exception);
                    MessageBox.Show("Ocurrió un error inesperado: " + e.Exception.Message + "\n\nQuedó anotado en errores.log, en %LOCALAPPDATA%\\FrameLab.", "FrameLab", MessageBoxButton.OK, MessageBoxImage.Error);
                    e.Handled = true;
                };
                TaskScheduler.UnobservedTaskException += (s, e) => { Util.RegistrarError(e.Exception); e.SetObserved(); };
                AppDomain.CurrentDomain.UnhandledException += (s, e) => { var ex = e.ExceptionObject as Exception; if (ex != null) Util.RegistrarError(ex); };
            }
            Action<string> etapa = t => { if (prueba) File.AppendAllText(Path.Combine(args[1], "etapas.txt"), DateTime.Now.ToString("HH:mm:ss") + " " + t + Environment.NewLine); };
            etapa("inicio");
            if (!prueba) Version.BorrarVersionVieja();
            // Antes de leer preferencias o copias: la carpeta de datos cambió de nombre
            if (!prueba) { try { Motor.MigrarRespaldo(); } catch (Exception ex) { Util.RegistrarError(ex); } }
            Ventana v;
            try { v = new Ventana(); }
            catch (Exception ex)
            {
                Util.RegistrarError(ex);
                if (prueba) File.WriteAllText(Path.Combine(args[1], "error.txt"), ex.ToString());
                else MessageBox.Show("La app no pudo abrirse: " + ex.Message, "FrameLab", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
            etapa("ventana creada");
            v.Loaded += async delegate
            {
                try
                {
                    await v.Cargar();
                    etapa("datos cargados");
                    if (prueba)
                    {
                        v.GuardarCapturas(args[1]);
                        etapa("capturas guardadas");
                        if (v.ErroresCarga.Any()) File.WriteAllText(Path.Combine(args[1], "error.txt"), string.Join(Environment.NewLine, v.ErroresCarga));
                        app.Shutdown(v.ErroresCarga.Any() ? 1 : 0);
                    }
                }
                catch (Exception ex)
                {
                    if (prueba) { File.WriteAllText(Path.Combine(args[1], "error.txt"), ex.ToString()); app.Shutdown(1); }
                    else MessageBox.Show(v, "Error al analizar la PC: " + ex.Message, v.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            return app.Run(v);
        }

        static int PruebaAnalisis(string csv, string salida)
        {
            // Sin esto, la prueba ensuciaría el historial real de mediciones
            Motor.RutaRespaldo = Path.Combine(Path.GetDirectoryName(salida), "respaldo_prueba.json");
            try
            {
                var r = Medicion.Analizar(csv, new List<Muestra>());
                File.WriteAllText(salida, r.Juego + " | " + r.Segundos + " s | " + r.Frames + " frames | " + r.Fps + " FPS | 1% " + r.Bajo1 + " | 0.1% " + r.Bajo01 + " | picos " + r.Picos + " | cpu " + r.CpuOcupada + " | gpu " + r.GpuOcupada + "\n" + r.Veredicto, Encoding.UTF8);
                return 0;
            }
            catch (Exception ex) { File.WriteAllText(salida, ex.ToString()); return 1; }
        }

        // Prueba aplicar y revertir con una clave propia de prueba; no toca ajustes reales
        static int PruebaMotor(string carpeta)
        {
            Directory.CreateDirectory(carpeta);
            var log = new StringBuilder();
            const string clave = "Software\\OptimizadorPCPrueba";
            Motor.RutaRespaldo = Path.Combine(carpeta, "respaldo_prueba.json");
            foreach (var viejo in new[] { Motor.RutaRespaldo, Motor.RutaRespaldo + ".bak" }) if (File.Exists(viejo)) File.Delete(viejo);
            int fallos = 0;
            Action<string, bool> ok = (n, c) => { log.AppendLine((c ? "OK    " : "FALLO ") + n); if (!c) fallos++; };
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(clave)) { k.SetValue("Existente", 5, Microsoft.Win32.RegistryValueKind.DWord); k.SetValue("Texto", "viejo"); }
                var a = new Ajuste { id = "prueba", acciones = new List<Accion> {
                    new Accion { tipo = "registro", ruta = "HKCU\\" + clave, valor = "Existente", tipoValor = "DWord", dato = "38" },
                    new Accion { tipo = "registro", ruta = "HKCU\\" + clave, valor = "Texto", tipoValor = "String", dato = "0" },
                    new Accion { tipo = "registro", ruta = "HKCU\\" + clave, valor = "Nuevo", tipoValor = "DWord", dato = "1" } } };
                ok("detecta no aplicado", !Motor.Aplicado(a));
                Motor.Aplicar(a);
                ok("detecta aplicado", Motor.Aplicado(a));
                ok("hay respaldo", Motor.TieneRespaldo(a));
                Motor.Aplicar(a);
                Motor.Revertir(a);
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(clave))
                {
                    ok("restaura DWord", (int)k.GetValue("Existente") == 5);
                    ok("restaura texto", (string)k.GetValue("Texto") == "viejo");
                    ok("borra valor que no existía", k.GetValue("Nuevo") == null);
                }
                ok("respaldo eliminado", !Motor.TieneRespaldo(a));
                var ini = Juegos.TransformarIni(new[] { "[Grupo]", "sg.ShadowQuality=3", "FrameRateLimit=160.000000", "OtraClave=7" }, "Bajo", 141);
                ok("ini cambia sombras", ini[1] == "sg.ShadowQuality=0");
                ok("ini cambia límite", ini[2] == "FrameRateLimit=141.000000");
                ok("ini conserva lo demás", ini[0] == "[Grupo]" && ini[3] == "OtraClave=7");

                // DWord alto y multilínea: antes no se podían restaurar
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(clave))
                {
                    k.SetValue("Alto", unchecked((int)0xFFFFFFFF), Microsoft.Win32.RegistryValueKind.DWord);
                    k.SetValue("Multi", new[] { "uno", "dos" }, Microsoft.Win32.RegistryValueKind.MultiString);
                    k.SetValue("Vacio", 1, Microsoft.Win32.RegistryValueKind.DWord);
                }
                var b = new Ajuste { id = "prueba2", acciones = new List<Accion> {
                    new Accion { tipo = "registro", ruta = "HKCU\\" + clave, valor = "Alto", tipoValor = "DWord", dato = "4294967294" },
                    new Accion { tipo = "registro", ruta = "HKCU\\" + clave, valor = "Multi", tipoValor = "String", dato = "x" } } };
                Motor.Aplicar(b);
                ok("detecta DWord alto", Motor.Aplicado(b));
                Motor.Revertir(b);
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(clave))
                {
                    ok("restaura DWord alto", (int)k.GetValue("Alto") == unchecked((int)0xFFFFFFFF));
                    var m = k.GetValue("Multi") as string[];
                    ok("restaura multilínea", m != null && m.Length == 2 && m[1] == "dos" && k.GetValueKind("Multi") == Microsoft.Win32.RegistryValueKind.MultiString);
                }

                // Claves nuevas: al revertir no quedan vacías, y lo ajeno no se borra
                var c2 = new Ajuste { id = "prueba3", acciones = new List<Accion> {
                    new Accion { tipo = "registro", ruta = "HKCU\\" + clave + "\\Nueva\\Hija", valor = "V", tipoValor = "DWord", dato = "1" } } };
                Motor.Aplicar(c2);
                Motor.Revertir(c2);
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(clave))
                {
                    ok("borra las claves que creó", k.OpenSubKey("Nueva") == null);
                    ok("conserva la clave que ya existía", k.GetValue("Vacio") != null);
                }

                // Una acción que falla deshace las anteriores y no deja copia
                var d = new Ajuste { id = "prueba4", acciones = new List<Accion> {
                    new Accion { tipo = "registro", ruta = "HKCU\\" + clave, valor = "Existente", tipoValor = "DWord", dato = "99" },
                    new Accion { tipo = "registro", ruta = "HKCU\\" + clave, valor = "Roto", tipoValor = "DWord", dato = "noesnumero" } } };
                bool fallo = false;
                try { Motor.Aplicar(d); } catch (InvalidOperationException) { fallo = true; }
                ok("informa el fallo", fallo);
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(clave))
                    ok("deshace lo aplicado", (int)k.GetValue("Existente") == 5 && k.GetValue("Roto") == null);
                ok("no deja copia tras deshacer", !Motor.TieneRespaldo(d));

                // Copia dañada: se recupera la anterior
                Motor.Aplicar(a);
                Motor.Aplicar(new Ajuste { id = "prueba5", acciones = new List<Accion> { new Accion { tipo = "registro", ruta = "HKCU\\" + clave, valor = "Vacio", tipoValor = "DWord", dato = "2" } } });
                File.WriteAllText(Motor.RutaRespaldo, "{ esto no es json");
                ok("recupera la copia dañada", Motor.TieneRespaldo(a) && Motor.AvisoRespaldo != null);
                Motor.AvisoRespaldo = null;
                Motor.Revertir(a);

                ok("integrada + dedicada usa la dedicada", Juegos.NivelDe(new[] { "Intel(R) UHD Graphics 770", "NVIDIA GeForce RTX 4070" }) == "Alto");
                ok("solo integrada AMD es bajo", Juegos.NivelDe(new[] { "AMD Radeon(TM) Graphics" }) == "Bajo");
                ok("GTX 1050 es bajo", Juegos.Nivel("NVIDIA GeForce GTX 1050 Ti") == "Bajo");
                ok("GTX 1660 es medio", Juegos.Nivel("NVIDIA GeForce GTX 1660 SUPER") == "Medio");

                // Juegos: se prueba con copias temporales, nunca con los archivos reales
                string tmpJuegos = Path.Combine(carpeta, "juegos");
                Directory.CreateDirectory(tmpJuegos);
                var archivos = new Dictionary<string, string> {
                    { "roblox", "<roblox>\r\n<Item class=\"UserGameSettings\">\r\n<Properties>\r\n<int name=\"GraphicsQualityLevel\">10</int>\r\n<token name=\"SavedQualityLevel\">0</token>\r\n</Properties>\r\n</Item>\r\n</roblox>\r\n" },
                    { "minecraft", "version:3953\nrenderDistance:24\nsimulationDistance:12\ngraphicsMode:1\nparticles:0\nrenderClouds:\"true\"\nenableVsync:true\nmaxFps:260\nlang:es_ar\n" },
                    { "bedrock", "gfx_viewdistance:512\ngfx_fancygraphics:1\ngfx_vsync:1\nmp_username:Steve\n" },
                    { "fortnite", "[ScalabilityGroups]\r\nsg.ShadowQuality=3\r\n[/Script/FortniteGame.FortGameUserSettings]\r\nFrameRateLimit=0.000000\r\n" } };
                foreach (var x in archivos)
                {
                    string ruta = Path.Combine(tmpJuegos, x.Key + ".cfg");
                    File.WriteAllText(ruta, x.Value, new UTF8Encoding(false));
                    Juegos.RutasPrueba[x.Key] = ruta;
                    Juegos.Aplicar(x.Key, "Bajo", 57);
                    string nuevo = File.ReadAllText(ruta);
                    ok(x.Key + ": guarda copia", File.Exists(Juegos.Copia(x.Key)));
                    ok(x.Key + ": cambia el archivo", nuevo != x.Value);
                    Juegos.Restaurar(x.Key);
                    ok(x.Key + ": restaura el original", File.ReadAllText(ruta) == x.Value && !File.Exists(Juegos.Copia(x.Key)));
                    if (x.Key == "roblox") ok("roblox: calidad manual baja", nuevo.Contains("<token name=\"SavedQualityLevel\">2</token>") && nuevo.Contains("GraphicsQualityLevel\">10<"));
                    if (x.Key == "minecraft") ok("minecraft: valores y comillas", nuevo.Contains("renderDistance:8\n") && nuevo.Contains("renderClouds:\"false\"") && nuevo.Contains("maxFps:57") && nuevo.Contains("lang:es_ar") && !nuevo.Contains("\r\n"));
                    if (x.Key == "bedrock") ok("bedrock: distancia en bloques", nuevo.Contains("gfx_viewdistance:128") && nuevo.Contains("mp_username:Steve"));
                    if (x.Key == "fortnite") ok("fortnite: conserva CRLF", nuevo.Contains("sg.ShadowQuality=0\r\n"));
                }
                Juegos.RutasPrueba.Clear();
                Directory.Delete(tmpJuegos, true);

                ok("gama GT 710", Frenos.Gama("NVIDIA GeForce GT 710") == 1);
                ok("gama GTX 1050 Ti", Frenos.Gama("NVIDIA GeForce GTX 1050 Ti") == 3);
                ok("gama GTX 1050", Frenos.Gama("NVIDIA GeForce GTX 1050") == 2);
                ok("gama RX 580", Frenos.Gama("Radeon RX 580 Series") == 3);
                ok("gama RX 5700", Frenos.Gama("AMD Radeon RX 5700 XT") == 4);
                ok("gama integrada + dedicada", Frenos.MejorGama(new[] { "Intel(R) UHD Graphics 630", "NVIDIA GeForce RTX 3060" }) == 4);
                var dh = new Diagnostico { Gpus = { "NVIDIA GeForce GT 710" }, Nucleos = 2, Hilos = 4, RamGb = 16 };
                var fh = Frenos.Analizar(dh, new List<Aviso> { new Aviso { Nivel = "aviso", Titulo = "Poco espacio en C:", Detalle = "9 GB libres de 479 GB. Liberá espacio." } }, 8);
                ok("frenos: primero la placa de video", fh.Count >= 3 && fh[0].Titulo.Contains("placa de video") && fh[1].Titulo == "Disco casi lleno");

                // Cambio de nombre: la carpeta de Afina PC se copia entera y no se toca la original
                string local = Path.Combine(carpeta, "local"), vieja = Path.Combine(local, "AfinaPC");
                Directory.CreateDirectory(Path.Combine(vieja, "copias_juegos"));
                File.WriteAllText(Path.Combine(vieja, "respaldo.json"), "{\"x\":[]}");
                File.WriteAllText(Path.Combine(vieja, "copias_juegos", "a.ini"), "1");
                string rutaReal = Motor.RutaRespaldo;
                Motor.RutaRespaldo = Path.Combine(local, "FrameLab", "respaldo.json");
                Motor.MigrarRespaldo(local);
                ok("migra la carpeta de Afina PC", File.Exists(Motor.RutaRespaldo) && File.Exists(Path.Combine(local, @"FrameLab\copias_juegos\a.ini")) && File.Exists(Path.Combine(vieja, "respaldo.json")));
                Motor.RutaRespaldo = rutaReal;

                // Modo partida con un proceso de prueba: cierra, sube prioridad y deshace. No toca plan, búsqueda ni otros programas
                Partida.Simular = true;
                string exePrueba = Path.Combine(carpeta, "framelabprueba.exe");
                File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "PING.EXE"), exePrueba, true);
                Func<Process> lanzar = () => Process.Start(new ProcessStartInfo(exePrueba, "-n 120 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true });
                var pj = lanzar();
                string extraAntes = Util.Preferencia("juegosExtra");
                Partida.GuardarJuegosExtra("framelabprueba.exe");
                Partida.Empezar(new string[0]);
                Partida.Vigilar(false);
                pj.Refresh();
                ok("partida: sube prioridad del juego", pj.PriorityClass == ProcessPriorityClass.High);
                ok("partida: guarda estado en disco", File.Exists(Partida.RutaEstado));
                Partida.Terminar();
                pj.Refresh();
                ok("partida: devuelve la prioridad", pj.PriorityClass == ProcessPriorityClass.Normal && !File.Exists(Partida.RutaEstado));
                Partida.Empezar(new[] { "framelabprueba" });
                ok("partida: cierra el programa elegido", pj.WaitForExit(8000));
                ok("partida: recuerda para reabrir", Partida.PendientesDeReabrir().Any(r => r.EndsWith("framelabprueba.exe", StringComparison.OrdinalIgnoreCase)));
                Partida.Terminar();
                Util.GuardarPreferencia("juegosExtra", extraAntes ?? "");
                Partida.Simular = false;
                File.Delete(exePrueba);

                // Preferencia de placa de video, sobre una clave de prueba
                Partida.RutaClaveGpu = clave + "\\Gpu";
                Partida.GpuAltaPrueba(@"C:\Juegos\juego.exe");
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Partida.RutaClaveGpu))
                    ok("gpu: agrega preferencia", (string)k.GetValue(@"C:\Juegos\juego.exe") == "GpuPreference=2;");
                Partida.QuitarGpuAlta();
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Partida.RutaClaveGpu))
                    ok("gpu: la quita", k.GetValue(@"C:\Juegos\juego.exe") == null);

                // Pantalla completa por juego, sobre una clave de prueba; respeta opciones previas
                Partida.RutaClaveCompat = clave + "\\Compat";
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(Partida.RutaClaveCompat)) k.SetValue(@"C:\Juegos\otro.exe", "~ RUNASADMIN");
                Partida.PantallaCompleta(@"C:\Juegos\juego.exe");
                Partida.PantallaCompleta(@"C:\Juegos\otro.exe");
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Partida.RutaClaveCompat))
                    ok("pantalla: agrega y respeta lo previo", (string)k.GetValue(@"C:\Juegos\juego.exe") == "~ DISABLEDXMAXIMIZEDWINDOWEDMODE" && (string)k.GetValue(@"C:\Juegos\otro.exe") == "~ RUNASADMIN");
                Partida.QuitarPantallaCompleta();
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Partida.RutaClaveCompat))
                    ok("pantalla: quita solo lo suyo", k.GetValue(@"C:\Juegos\juego.exe") == null && k.GetValue(@"C:\Juegos\otro.exe") != null);

                // Quitar apps, con una lista de paquetes simulada: no se quita nada real
                var instaladasPrueba = new HashSet<string>(new[] { "Microsoft.Copilot", "Microsoft.BingNews", "Microsoft.WindowsStore", "Microsoft.VCLibs.140.00", "Microsoft.GamingApp", "SpotifyAB.SpotifyMusic", "Microsoft.Win32WebViewHost" }, StringComparer.OrdinalIgnoreCase);
                Apps.Instaladas = () => instaladasPrueba;
                Apps.Simular = true;
                var apps = Apps.Listar();
                ok("apps: solo ofrece las de la lista", apps.Count == 4 && !apps.Any(x => x.Paquete.Contains("Store") || x.Paquete.Contains("VCLibs") || x.Paquete.Contains("WebView")));
                ok("apps: grupos", apps.First(x => x.Paquete == "Microsoft.Copilot").Grupo == "recomendado" && apps.First(x => x.Paquete == "Microsoft.GamingApp").Grupo == "xbox" && apps.First(x => x.Paquete == "SpotifyAB.SpotifyMusic").Grupo == "opcional");
                bool rechaza = false;
                try { Apps.Quitar(new AppQuitable { Nombre = "Store", Paquete = "Microsoft.WindowsStore" }, false); } catch (InvalidOperationException) { rechaza = true; }
                ok("apps: no quita lo que no está en la lista", rechaza);
                Apps.Quitar(apps.First(x => x.Paquete == "Microsoft.BingNews"), false);
                instaladasPrueba.Remove("Microsoft.BingNews");
                var despues = Apps.Listar();
                ok("apps: recuerda lo quitado para reinstalar", despues.Any(x => x.Paquete == "Microsoft.BingNews" && x.Quitada && !x.Instalada));
                instaladasPrueba.Add("Microsoft.BingNews");
                Apps.OlvidarSiVolvio();
                ok("apps: si se reinstala deja de figurar como quitada", !Apps.Quitadas().Any());
                Apps.Simular = false;

                // Ordenar: carpeta de prueba con archivos, una carpeta propia, un oculto y un nombre repetido
                string esc = Path.Combine(carpeta, "escritorio");
                Directory.CreateDirectory(Path.Combine(esc, "Mis cosas"));
                Directory.CreateDirectory(Path.Combine(esc, "Imágenes"));
                foreach (var n in new[] { "foto.jpg", "tarea.docx", "juego.lnk", "setup.exe", "raro.xyz", "canción.mp3" }) File.WriteAllText(Path.Combine(esc, n), n);
                File.WriteAllText(Path.Combine(esc, "Imágenes", "foto.jpg"), "ya estaba");
                File.WriteAllText(Path.Combine(esc, "oculto.txt"), "x"); File.SetAttributes(Path.Combine(esc, "oculto.txt"), FileAttributes.Hidden);
                File.WriteAllText(Path.Combine(esc, "desktop.ini"), "x");
                var plan = Ordenar.Planear(esc, false);
                ok("ordenar: solo archivos visibles y sin accesos", plan.Count == 5 && !plan.Any(m => m.Origen.EndsWith(".lnk") || m.Origen.EndsWith("oculto.txt") || m.Origen.EndsWith("desktop.ini")));
                var salteados = new List<string>();
                int movidos = Ordenar.Aplicar(esc, plan, salteados);
                ok("ordenar: mueve por tipo", movidos == 5 && File.Exists(Path.Combine(esc, @"Documentos\tarea.docx")) && File.Exists(Path.Combine(esc, @"Otros\raro.xyz")) && File.Exists(Path.Combine(esc, @"Instaladores\setup.exe")));
                ok("ordenar: no pisa archivos con el mismo nombre", File.Exists(Path.Combine(esc, @"Imágenes\foto (2).jpg")) && File.ReadAllText(Path.Combine(esc, @"Imágenes\foto.jpg")) == "ya estaba");
                ok("ordenar: no toca carpetas propias", Directory.Exists(Path.Combine(esc, "Mis cosas")));
                var problemas = new List<string>();
                int vueltos = Ordenar.Deshacer(esc, problemas);
                ok("ordenar: deshace todo", vueltos == 5 && File.ReadAllText(Path.Combine(esc, "foto.jpg")) == "foto.jpg" && File.Exists(Path.Combine(esc, "raro.xyz")) && problemas.Count == 0);
                ok("ordenar: borra solo las carpetas que creó y quedaron vacías", !Directory.Exists(Path.Combine(esc, "Documentos")) && Directory.Exists(Path.Combine(esc, "Imágenes")) && !Ordenar.HayParaDeshacer(esc));
                File.SetAttributes(Path.Combine(esc, "oculto.txt"), FileAttributes.Normal);
                Directory.Delete(esc, true);

                // Fondo: se dibuja y se guarda/restaura el anterior sobre una clave de prueba, sin cambiar el fondo real
                string png = Path.Combine(carpeta, "fondo.png");
                FondoFrameLab.GuardarPng(png, 1920, 1080, null);
                var dec = new System.Windows.Media.Imaging.PngBitmapDecoder(new Uri(png), System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                ok("fondo: imagen 1920x1080", dec.Frames[0].PixelWidth == 1920 && dec.Frames[0].PixelHeight == 1080);
                FondoFrameLab.ClaveEscritorioPrueba = clave + "\\Escritorio";
                using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(FondoFrameLab.ClaveEscritorioPrueba)) { k.SetValue("WallPaper", @"C:\fotos\mi fondo.jpg"); k.SetValue("WallpaperStyle", "6"); k.SetValue("TileWallpaper", "0"); }
                FondoFrameLab.GuardarAnterior();
                var copia = FondoFrameLab.LeerCopia();
                ok("fondo: guarda el anterior", copia[0] == @"C:\fotos\mi fondo.jpg" && copia[1] == "6");
                File.Delete(FondoFrameLab.RutaCopia);
                FondoFrameLab.ClaveEscritorioPrueba = null;

                // La limpieza no entra en enlaces
                string raiz = Path.Combine(carpeta, "limpieza"), fuera = Path.Combine(carpeta, "fuera");
                Directory.CreateDirectory(Path.Combine(raiz, "sub")); Directory.CreateDirectory(fuera);
                File.WriteAllText(Path.Combine(raiz, "sub", "a.txt"), "x"); File.WriteAllText(Path.Combine(fuera, "b.txt"), "x");
                int cj; Util.Correr("cmd", "/c mklink /J \"" + Path.Combine(raiz, "enlace") + "\" \"" + fuera + "\"", out cj);
                var vistos = Limpieza.ArchivosDe(raiz).ToList();
                ok("no entra en uniones", cj == 0 && vistos.Count == 1 && vistos[0].EndsWith("a.txt"));
                Directory.Delete(Path.Combine(raiz, "enlace"));

                // Salida de errores abundante: antes colgaba
                int cs; Util.Ps("1..3000 | ForEach-Object { [Console]::Error.WriteLine('xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx') }; 'fin'", out cs, 60000);
                ok("no se cuelga con muchos errores", cs == 0);
            }
            catch (Exception ex) { log.AppendLine("EXCEPCION " + ex); fallos++; }
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(clave, false);
            File.WriteAllText(Path.Combine(carpeta, "motor.txt"), log.ToString(), Encoding.UTF8);
            return fallos;
        }
    }
}
