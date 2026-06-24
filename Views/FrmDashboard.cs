using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using ProyectoFinal_YaradaPalos.Controllers;
using ProyectoFinal_YaradaPalos.Models;

namespace ProyectoFinal_YaradaPalos.Views
{
    /// <summary>
    /// Formulario Principal (Dashboard) del Sistema Comercial "Yarada HydroSense".
    /// Implementa de forma segura el control de hilos (Invoke) para evitar congelamiento de UI.
    /// Soporta monitoreo multipuerto en paralelo y CRUD de sectores.
    /// </summary>
    public class FrmDashboard : Form
    {
        private UsuarioModel usuarioSesion;
        private RiegoController riegoController;

        // Contenedores Principales
        private Panel panelSidebar;
        private Panel panelHeader;
        private Panel panelMainContainer;

        // Vistas en forma de sub-paneles
        private Panel panelMonitoreo;
        private Panel panelOperadores;
        private Panel panelSectores; // CRUD Sectores
        private Panel panelReportes;
        private Panel panelEstadisticas; // Dashboard Analítico

        // Elementos de la Barra Lateral
        private Label lblLogo;
        private Label lblUserPerfil;
        private Button btnNavMonitoreo;
        private Button btnNavOperadores;
        private Button btnNavSectores; // Navegación CRUD Sectores
        private Button btnNavReportes;
        private Button btnNavEstadisticas; // Navegación Dashboard Estadístico
        private Button btnNavCerrarSesion;

        // Elementos de Monitoreo
        private Panel panelConexionStatus;
        private Label lblConexionTexto;
        private Button btnRiegoManual;

        // Elementos de Dashboard Estadístico (Históricos)
        private ComboBox cboEstSectores;
        private DateTimePicker dtpEstInicio;
        private DateTimePicker dtpEstFin;
        private Button btnProcesarEst;
        private Label lblEstTempMax;
        private Label lblEstTempMin;
        private Label lblEstTempAvg;
        private System.Windows.Forms.DataVisualization.Charting.Chart chartConsumo;

        // Elementos de Monitoreo
        private Button btnConectar;
        private DataGridView dgvMonitoreoSectores; // Grid de sectores en paralelo
        private Label lblHumedadValor;
        private Label lblHumedadTendencia;
        private Label lblHumedadTiempo;
        private Label lblTemperaturaValor;
        private Panel panelReleStatus;
        private Label lblReleTexto;
        private Label lblEstadoSueloValor;
        private ListBox lstLogAlertas;

        // Sectores con alerta crítica activa para evitar spam de MessageBox
        private HashSet<int> sectoresAlertados = new HashSet<int>();

        // Elementos de CRUD Operadores
        private DataGridView dgvOperadores;
        private TextBox txtOpUsername;
        private TextBox txtOpNombre;
        private TextBox txtOpPassword;
        private TextBox txtOpCelular; // Nuevo campo
        private ComboBox cboOpSector; // Nuevo combo asignación sector
        private TextBox txtOpBuscar;
        private Button btnOpCrear;
        private Button btnOpModificar;
        private Button btnOpLimpiar; // Nuevo control de limpiar
        private PictureBox picBoxQR; // Contenedor QR
        private GroupBox gbTwilioQR; // Grupo QR
        private Button btnOpFisicoEliminar;
        private Label lblOpMensajeBloqueo;
        private int selectedOpId = -1;

        // Elementos de CRUD Sectores
        private DataGridView dgvSectores;
        private TextBox txtSecNombre;
        private TextBox txtSecEncargado;
        private ComboBox cboSecCultivo;
        private ComboBox cboSecPuerto;
        private TextBox txtSecTarifa;
        private TextBox txtSecCaudal;
        private TextBox txtSecBuscar;
        private CheckBox chkSecActivo;
        private Button btnSecCrear;
        private Button btnSecModificar;
        private Button btnSecEliminar;
        private Button btnSecLimpiar;
        private Button btnSecFisicoEliminar;
        private Label lblSecMensajeBloqueo;
        private int selectedSecId = -1;

        // Filtros de Sectores
        private ComboBox cboFiltroSecEstado;
        private DateTimePicker dtpSecDesde;
        private DateTimePicker dtpSecHasta;
        private Button btnFiltroSecAplicar;
        private bool? estadoFiltradoActual = true; // Por defecto Activos

        // Elementos de Reportes
        private ComboBox cboReportes;
        private ComboBox cboSectores;
        private DateTimePicker dtpFechaInicio;
        private DateTimePicker dtpFechaFin;
        private Button btnEjecutarReporte;
        private Button btnImprimirPDF;
        private DataGridView dgvReportes;

        public FrmDashboard(UsuarioModel usuario)
        {
            usuarioSesion = usuario;
            riegoController = new RiegoController();

            // Suscribirse a los Eventos del Controlador (Patrón MVC)
            riegoController.OnDatosRecibidos += RiegoController_OnDatosRecibidos;
            riegoController.OnEstadoBombaChanged += RiegoController_OnEstadoBombaChanged;
            riegoController.OnAlertaRegistrada += RiegoController_OnAlertaRegistrada;
            riegoController.OnConexionEstadoChanged += RiegoController_OnConexionEstadoChanged;
            riegoController.OnDatosSectorRecibidos += RiegoController_OnDatosSectorRecibidos;

            InitializeComponent();
            GestionNotificaciones.GenerarQrVinculacion(picBoxQR);
            ConfigurarAccesoPorRol();
            CargarSectoresCombo();
            CargarCultivosEnCombo();
            CargarSectoresOpCombo();
            CargarSectoresMonitoreoGrid();
            SwitchPanel(panelMonitoreo);
        }

        private void InitializeComponent()
        {
            this.Size = new Size(1120, 680);
            this.MinimumSize = new Size(960, 640);
            this.Text = "Yarada HydroSense - Riego de Precisión IoT (Tacna)";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 244, 241);

            // =================================================================================
            // 1. PANEL SIDEBAR (Barra Lateral Izquierda)
            // =================================================================================
            panelSidebar = new Panel
            {
                Width = 240,
                Dock = DockStyle.Left,
                BackColor = Color.FromArgb(30, 86, 49) // Verde Bosque
            };

            lblLogo = new Label
            {
                Text = "Yarada HydroSense",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                Location = new Point(15, 20),
                Size = new Size(210, 40)
            };

            lblUserPerfil = new Label
            {
                Text = "Usuario: " + usuarioSesion.Nombre + "\nRol: " + usuarioSesion.NombreRol,
                ForeColor = Color.FromArgb(200, 230, 210),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                Location = new Point(15, 70),
                Size = new Size(210, 40)
            };

            btnNavMonitoreo = CrearBotonNavegacion("Monitoreo en Tiempo Real", 140);
            btnNavMonitoreo.Click += (s, e) => SwitchPanel(panelMonitoreo);

            btnNavOperadores = CrearBotonNavegacion("Gestión de Operadores", 200);
            btnNavOperadores.Click += (s, e) => {
                SwitchPanel(panelOperadores);
                if (usuarioSesion.EsAdministrador()) ListarOperadores();
            };

            btnNavSectores = CrearBotonNavegacion("Gestión de Sectores", 260);
            btnNavSectores.Click += (s, e) => {
                SwitchPanel(panelSectores);
                if (cboFiltroSecEstado != null)
                {
                    cboFiltroSecEstado.SelectedIndex = 0;
                    dtpSecDesde.Value = DateTime.Today.AddMonths(-1);
                    dtpSecHasta.Value = DateTime.Today;
                }
                ListarSectores(true);
            };

            btnNavReportes = CrearBotonNavegacion("Reportes y Estadísticas", 320);
            btnNavReportes.Click += (s, e) => SwitchPanel(panelReportes);

            btnNavEstadisticas = CrearBotonNavegacion("Dashboard Estadístico", 380);
            btnNavEstadisticas.Click += (s, e) => {
                SwitchPanel(panelEstadisticas);
                CargarSectoresComboEstadisticas();
            };

            btnNavCerrarSesion = CrearBotonNavegacion("Cerrar Sesión", 540);
            btnNavCerrarSesion.BackColor = Color.FromArgb(120, 40, 40);
            btnNavCerrarSesion.Click += BtnCerrarSesion_Click;

            panelSidebar.Controls.Add(lblLogo);
            panelSidebar.Controls.Add(lblUserPerfil);
            panelSidebar.Controls.Add(btnNavMonitoreo);
            panelSidebar.Controls.Add(btnNavOperadores);
            panelSidebar.Controls.Add(btnNavSectores);
            panelSidebar.Controls.Add(btnNavReportes);
            panelSidebar.Controls.Add(btnNavEstadisticas);
            panelSidebar.Controls.Add(btnNavCerrarSesion);

            // =================================================================================
            // 2. PANEL CONTENEDOR PRINCIPAL
            // =================================================================================
            panelMainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Location = new Point(240, 0),
                Size = new Size(880, 680),
                BackColor = Color.FromArgb(240, 244, 241)
            };

            // =================================================================================
            // 3. VISTA: MONITOREO (Tiempo Real / Multipuerto)
            // =================================================================================
            panelMonitoreo = new Panel { Dock = DockStyle.Fill };

            Label lblMonTitulo = new Label
            {
                Text = "Monitoreo de Telemetría IoT (Multipuerto)",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 86, 49),
                Location = new Point(20, 20),
                AutoSize = true
            };

            // Caja de Control Multipuerto
            GroupBox gbControl = new GroupBox
            {
                Text = "Conectividad Multipuerto en Paralelo",
                Location = new Point(20, 70),
                Size = new Size(820, 80),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            btnConectar = new Button
            {
                Text = "Conectar Dispositivos",
                Location = new Point(20, 27),
                Size = new Size(220, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 125, 50),
                ForeColor = Color.White
            };
            btnConectar.Click += BtnConectar_Click;

            Label lblStatusMulti = new Label
            {
                Text = "El sistema conectará automáticamente todos los puertos seriales asignados a los sectores activos de cultivo.",
                Location = new Point(260, 31),
                Size = new Size(540, 35),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.DimGray,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            gbControl.Controls.Add(btnConectar);
            gbControl.Controls.Add(lblStatusMulti);

            // 3.1. Grid Lateral de Sectores Monitoreados
            Label lblGridSectores = new Label
            {
                Text = "Sectores Monitoreados:",
                Location = new Point(20, 160),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                AutoSize = true
            };

            dgvMonitoreoSectores = new DataGridView
            {
                Location = new Point(20, 185),
                Size = new Size(320, 420),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.Both,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            StyleDataGridView(dgvMonitoreoSectores, Color.FromArgb(41, 128, 185));
            dgvMonitoreoSectores.Columns.Add("IdSector", "ID");
            dgvMonitoreoSectores.Columns["IdSector"].Visible = false;
            dgvMonitoreoSectores.Columns.Add("Sector", "Sector");
            dgvMonitoreoSectores.Columns.Add("Puerto", "Puerto");
            dgvMonitoreoSectores.Columns.Add("Planta", "Planta");
            dgvMonitoreoSectores.Columns.Add("Humedad", "Humedad");
            dgvMonitoreoSectores.Columns.Add("Temperatura", "Temp.");
            dgvMonitoreoSectores.Columns.Add("Salud", "Salud %");
            dgvMonitoreoSectores.Columns.Add("Bomba", "Bomba");
            dgvMonitoreoSectores.Columns.Add("EstadoRed", "Red"); // Keep-Alive Column

            // Ajustar anchos mínimos iniciales para columnas
            dgvMonitoreoSectores.Columns["Sector"].MinimumWidth = 80;
            dgvMonitoreoSectores.Columns["Puerto"].MinimumWidth = 60;
            dgvMonitoreoSectores.Columns["Planta"].MinimumWidth = 80;
            dgvMonitoreoSectores.Columns["Humedad"].MinimumWidth = 70;
            dgvMonitoreoSectores.Columns["Temperatura"].MinimumWidth = 60;
            dgvMonitoreoSectores.Columns["Salud"].MinimumWidth = 70;
            dgvMonitoreoSectores.Columns["Bomba"].MinimumWidth = 70;
            dgvMonitoreoSectores.Columns["EstadoRed"].MinimumWidth = 80;

            dgvMonitoreoSectores.SelectionChanged += DgvMonitoreoSectores_SelectionChanged;

            // Contenedor del Bloque Derecho de Monitoreo
            Panel panelTelemetryRight = new Panel
            {
                Location = new Point(360, 185),
                Size = new Size(400, 420),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            // 3.2. Tarjetas de Telemetría Detallada (Sector Seleccionado)
            Panel cardHumedad = CrearTarjeta("HUMEDAD DEL SUELO", Color.FromArgb(41, 128, 185), 0, 0, out lblHumedadValor);
            cardHumedad.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            
            lblHumedadTendencia = new Label
            {
                Text = "Tendencia: --",
                Location = new Point(15, 92),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                ForeColor = Color.Gray,
                AutoSize = true
            };
            lblHumedadTiempo = new Label
            {
                Text = "Límite: --",
                Location = new Point(15, 105),
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                ForeColor = Color.Gray,
                AutoSize = true
            };
            cardHumedad.Controls.Add(lblHumedadTendencia);
            cardHumedad.Controls.Add(lblHumedadTiempo);

            Panel cardTemperatura = CrearTarjeta("TEMPERATURA AMBIENTE", Color.FromArgb(230, 126, 34), 210, 0, out lblTemperaturaValor);
            cardTemperatura.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            // Tarjeta de Estado del Relé / Bomba
            Panel cardRele = new Panel
            {
                Location = new Point(0, 130),
                Size = new Size(190, 95),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            Label lblReleTitulo = new Label
            {
                Text = "ESTADO DE BOMBA / RELÉ",
                Location = new Point(15, 12),
                Font = new Font("Segoe UI", 8.0F, FontStyle.Bold),
                ForeColor = Color.DarkGray,
                AutoSize = true
            };
            panelReleStatus = new Panel
            {
                Location = new Point(20, 35),
                Size = new Size(20, 20),
                BackColor = Color.Gray // Apagado por defecto
            };
            lblReleTexto = new Label
            {
                Text = "BOMBA APAGADA",
                Location = new Point(45, 36),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.DimGray,
                AutoSize = true
            };
            lblEstadoSueloValor = new Label
            {
                Text = "Estado: Desconectado",
                Location = new Point(15, 68),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.Gray,
                Size = new Size(160, 20)
            };
            cardRele.Controls.Add(lblReleTitulo);
            cardRele.Controls.Add(panelReleStatus);
            cardRele.Controls.Add(lblReleTexto);
            cardRele.Controls.Add(lblEstadoSueloValor);

            // Tarjeta de Conexión de Red / Heartbeat
            Panel cardConexion = new Panel
            {
                Location = new Point(210, 130),
                Size = new Size(190, 95),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            Label lblConexionTitulo = new Label
            {
                Text = "CONEXIÓN DE RED (HEARTBEAT)",
                Location = new Point(15, 12),
                Font = new Font("Segoe UI", 8.0F, FontStyle.Bold),
                ForeColor = Color.DarkGray,
                AutoSize = true
            };
            panelConexionStatus = new Panel
            {
                Location = new Point(20, 35),
                Size = new Size(20, 20),
                BackColor = Color.FromArgb(192, 57, 43) // Rojo por defecto
            };
            panelConexionStatus.Paint += PanelConexionStatus_Paint;

            lblConexionTexto = new Label
            {
                Text = "FUERA DE LÍNEA",
                Location = new Point(45, 36),
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(192, 57, 43),
                AutoSize = true
            };
            Label lblTiempoChequeo = new Label
            {
                Text = "Timeout: 15s (Keep-Alive)",
                Location = new Point(15, 68),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.Gray,
                Size = new Size(160, 20)
            };
            cardConexion.Controls.Add(lblConexionTitulo);
            cardConexion.Controls.Add(panelConexionStatus);
            cardConexion.Controls.Add(lblConexionTexto);
            cardConexion.Controls.Add(lblTiempoChequeo);

            // Botón de Riego Manual (Rápido)
            btnRiegoManual = new Button
            {
                Text = "Activar Riego Manual",
                Location = new Point(0, 235),
                Size = new Size(400, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 125, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Enabled = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            btnRiegoManual.Click += BtnRiegoManual_Click;

            // Grupo de Simulación de Alertas WhatsApp (Twilio)
            GroupBox gbSimuladorAlertas = new GroupBox
            {
                Text = "Simulador de Alertas WhatsApp (Twilio)",
                Location = new Point(0, 280),
                Size = new Size(400, 75),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 86, 49),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            Button btnSimulaHumedad = new Button
            {
                Text = "Estrés Humedad",
                Location = new Point(10, 25),
                Size = new Size(115, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(192, 57, 43),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            btnSimulaHumedad.Click += (senderObj, argsObj) =>
            {
                string msg = "🚨 *SIMULACIÓN DE ALERTA CRÍTICA* 🚨\n\nSector: *Sector 1 - Olivo Joven (Pozo 5)*\nLa salud del cultivo ha caído a *35.0%* (Humedad: *15.2%*, Temperatura: *26.5°C*).\nEl sistema ha iniciado automáticamente el riego de emergencia.";
                System.Threading.Tasks.Task.Run(() => GestionNotificaciones.EnviarAlertaWhatsAppPorPuerto("COM3", msg));
                lstLogAlertas.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] [Simulador] Alerta de Estrés enviada a WhatsApp.");
            };

            Button btnSimulaFuga = new Button
            {
                Text = "Fuga Bomba",
                Location = new Point(135, 25),
                Size = new Size(115, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(230, 126, 34),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            btnSimulaFuga.Click += (senderObj, argsObj) =>
            {
                string msg = "⚠️ *SIMULACIÓN DE ALERTA DE SEGURIDAD* ⚠️\n\n¡ALERTA DE SEGURIDAD [Sector 1 - Olivo Joven (Pozo 5)]: Bomba apagada por sospecha de fuga de agua. Bomba encendida por más de 5 minutos y la humedad no subió.";
                System.Threading.Tasks.Task.Run(() => GestionNotificaciones.EnviarAlertaWhatsAppPorPuerto("COM3", msg));
                lstLogAlertas.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] [Simulador] Alerta de Fuga enviada a WhatsApp.");
            };

            Button btnSimulaConexion = new Button
            {
                Text = "Pérdida Red",
                Location = new Point(260, 25),
                Size = new Size(130, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(127, 140, 141),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            btnSimulaConexion.Click += (senderObj, argsObj) =>
            {
                string msg = "⚠️ *SIMULACIÓN DE AVISO CRÍTICO* ⚠️\n\n¡CONEXIÓN PERDIDA! El sector 'Sector 2 - Granada Joven' en COM4 no responde (timeout 15s).";
                System.Threading.Tasks.Task.Run(() => GestionNotificaciones.EnviarAlertaWhatsAppPorPuerto("COM4", msg));
                lstLogAlertas.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] [Simulador] Alerta de Conexión enviada a WhatsApp.");
            };

            gbSimuladorAlertas.Controls.Add(btnSimulaHumedad);
            gbSimuladorAlertas.Controls.Add(btnSimulaFuga);
            gbSimuladorAlertas.Controls.Add(btnSimulaConexion);

            // Listbox para registros y alertas en tiempo real (Desplazado hacia abajo)
            Label lblLogTitulo = new Label { Text = "Registro de Eventos y Alertas Críticas:", Location = new Point(0, 365), Font = new Font("Segoe UI", 10F, FontStyle.Bold), AutoSize = true };
            lstLogAlertas = new ListBox
            {
                Location = new Point(0, 390),
                Size = new Size(400, 115),
                Font = new Font("Consolas", 8.5F),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // Agregar a los contenedores
            panelTelemetryRight.Controls.Add(cardHumedad);
            panelTelemetryRight.Controls.Add(cardTemperatura);
            panelTelemetryRight.Controls.Add(cardRele);
            panelTelemetryRight.Controls.Add(cardConexion);
            panelTelemetryRight.Controls.Add(btnRiegoManual);
            panelTelemetryRight.Controls.Add(gbSimuladorAlertas);
            panelTelemetryRight.Controls.Add(lblLogTitulo);
            panelTelemetryRight.Controls.Add(lstLogAlertas);

            panelMonitoreo.Controls.Add(lblMonTitulo);
            panelMonitoreo.Controls.Add(gbControl);
            panelMonitoreo.Controls.Add(lblGridSectores);
            panelMonitoreo.Controls.Add(dgvMonitoreoSectores);
            panelMonitoreo.Controls.Add(panelTelemetryRight);

            // Evento Resize de Monitoreo
            panelMonitoreo.Resize += (s, e) =>
            {
                int w = panelMonitoreo.Width;
                int h = panelMonitoreo.Height;
                if (w < 200 || h < 200) return;

                gbControl.Width = w - 40;

                panelTelemetryRight.Left = w - panelTelemetryRight.Width - 20;
                panelTelemetryRight.Height = h - panelTelemetryRight.Top - 20;

                lstLogAlertas.Height = panelTelemetryRight.Height - lstLogAlertas.Top;

                int gridWidth = panelTelemetryRight.Left - 40;
                if (gridWidth < 100) gridWidth = 100;
                dgvMonitoreoSectores.Width = gridWidth;
                dgvMonitoreoSectores.Height = h - dgvMonitoreoSectores.Top - 20;
            };

            // =================================================================================
            // 4. VISTA: OPERADORES (CRUD para Administrador)
            // =================================================================================
            panelOperadores = new Panel { Dock = DockStyle.Fill };

            Label lblOpTitulo = new Label
            {
                Text = "Administración de Operadores del Sistema",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 86, 49),
                Location = new Point(20, 20),
                AutoSize = true
            };

            lblOpMensajeBloqueo = new Label
            {
                Text = "Acceso Restringido. Se requieren privilegios de Administrador para gestionar operadores.",
                ForeColor = Color.DarkRed,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Location = new Point(20, 80),
                Size = new Size(800, 100),
                Visible = false
            };

            Label lblOpBuscar = new Label
            {
                Text = "Buscar (User/Nombre):",
                Location = new Point(20, 85),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            txtOpBuscar = new TextBox
            {
                Location = new Point(175, 82),
                Size = new Size(325, 25),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            txtOpBuscar.TextChanged += TxtOpBuscar_TextChanged;

            dgvOperadores = new DataGridView
            {
                Location = new Point(20, 120),
                Size = new Size(480, 480),
                AllowUserToAddRows = false,
                ReadOnly = false, // Permite hacer click en el Checkbox
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            StyleDataGridView(dgvOperadores, Color.FromArgb(41, 128, 185));
            dgvOperadores.SelectionChanged += DgvOperadores_SelectionChanged;
            dgvOperadores.CurrentCellDirtyStateChanged += DgvOperadores_CurrentCellDirtyStateChanged;
            dgvOperadores.CellValueChanged += DgvOperadores_CellValueChanged;

            // Formulario lateral de datos del CRUD
            GroupBox gbOpForm = new GroupBox
            {
                Text = "Detalles del Operador",
                Location = new Point(520, 70),
                Size = new Size(250, 420), // Altura incrementada a 420
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            Label lblOpUser = new Label { Text = "Username:", Location = new Point(15, 30), AutoSize = true };
            txtOpUsername = new TextBox { Location = new Point(15, 50), Size = new Size(220, 25) };

            Label lblOpNom = new Label { Text = "Nombre Completo:", Location = new Point(15, 90), AutoSize = true };
            txtOpNombre = new TextBox { Location = new Point(15, 110), Size = new Size(220, 25) };

            Label lblOpPass = new Label { Text = "Contraseña (Nueva):", Location = new Point(15, 150), AutoSize = true };
            txtOpPassword = new TextBox { Location = new Point(15, 170), Size = new Size(220, 25), UseSystemPasswordChar = true };

            Label lblOpCelular = new Label { Text = "Celular (Ej: +51990877875):", Location = new Point(15, 210), AutoSize = true };
            txtOpCelular = new TextBox { Location = new Point(15, 230), Size = new Size(220, 25) };

            Label lblOpSector = new Label { Text = "Sector Asignado:", Location = new Point(15, 270), AutoSize = true };
            cboOpSector = new ComboBox { Location = new Point(15, 290), Size = new Size(220, 25), DropDownStyle = ComboBoxStyle.DropDownList };

            btnOpCrear = new Button { Text = "Crear", Location = new Point(15, 325), Size = new Size(105, 35), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(46, 125, 50), ForeColor = Color.White };
            btnOpCrear.Click += BtnOpCrear_Click;

            btnOpModificar = new Button { Text = "Modificar", Location = new Point(130, 325), Size = new Size(105, 35), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(230, 126, 34), ForeColor = Color.White };
            btnOpModificar.Click += BtnOpModificar_Click;

            btnOpLimpiar = new Button { Text = "Limpiar", Location = new Point(15, 370), Size = new Size(105, 35), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(127, 140, 141), ForeColor = Color.White };
            btnOpLimpiar.Click += BtnOpLimpiar_Click;

            btnOpFisicoEliminar = new Button { Text = "Eliminar", Location = new Point(130, 370), Size = new Size(105, 35), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(192, 57, 43), ForeColor = Color.White };
            btnOpFisicoEliminar.Click += BtnOpFisicoEliminar_Click;

            gbOpForm.Controls.Add(lblOpUser);
            gbOpForm.Controls.Add(txtOpUsername);
            gbOpForm.Controls.Add(lblOpNom);
            gbOpForm.Controls.Add(txtOpNombre);
            gbOpForm.Controls.Add(lblOpPass);
            gbOpForm.Controls.Add(txtOpPassword);
            gbOpForm.Controls.Add(lblOpCelular);
            gbOpForm.Controls.Add(txtOpCelular);
            gbOpForm.Controls.Add(lblOpSector);
            gbOpForm.Controls.Add(cboOpSector);
            gbOpForm.Controls.Add(btnOpCrear);
            gbOpForm.Controls.Add(btnOpModificar);
            gbOpForm.Controls.Add(btnOpLimpiar);
            gbOpForm.Controls.Add(btnOpFisicoEliminar);

            // Vinculación QR (Más compacto)
            gbTwilioQR = new GroupBox
            {
                Text = "Vinculación Twilio WhatsApp",
                Location = new Point(850, 70),
                Size = new Size(240, 420), // Altura incrementada a 420
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            Label lblQRDesc = new Label
            {
                Text = "Escanea el código QR con tu celular para unirte al Sandbox de Twilio y recibir alertas automáticas.",
                Location = new Point(10, 25),
                Size = new Size(220, 45),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                TextAlign = ContentAlignment.TopCenter
            };

            picBoxQR = new PictureBox
            {
                Location = new Point(50, 75),
                Size = new Size(140, 140),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblQRInstrucciones = new Label
            {
                Text = "Si el QR no funciona, envía por WhatsApp:\n\n*join particles-bridge*\n\nAl número:\n*+1 415 523 8886*",
                Location = new Point(10, 235), // Desplazado a Y = 235
                Size = new Size(220, 165), // Altura de 165
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(41, 128, 185)
            };

            gbTwilioQR.Controls.Add(lblQRDesc);
            gbTwilioQR.Controls.Add(picBoxQR);
            gbTwilioQR.Controls.Add(lblQRInstrucciones);

            panelOperadores.Controls.Add(lblOpTitulo);
            panelOperadores.Controls.Add(lblOpMensajeBloqueo);
            panelOperadores.Controls.Add(lblOpBuscar);
            panelOperadores.Controls.Add(txtOpBuscar);
            panelOperadores.Controls.Add(dgvOperadores);
            panelOperadores.Controls.Add(gbOpForm);
            panelOperadores.Controls.Add(gbTwilioQR);

            // Evento Resize de Operadores
            panelOperadores.Resize += (s, e) =>
            {
                int w = panelOperadores.Width;
                int h = panelOperadores.Height;
                if (w < 200 || h < 200) return;

                gbTwilioQR.Left = w - gbTwilioQR.Width - 20;
                gbOpForm.Left = gbTwilioQR.Left - gbOpForm.Width - 20;

                int leftWidth = gbOpForm.Left - 40;
                if (leftWidth < 100) leftWidth = 100;

                txtOpBuscar.Width = leftWidth + 20 - txtOpBuscar.Left;
                dgvOperadores.Width = leftWidth;
                dgvOperadores.Height = h - dgvOperadores.Top - 20;

                lblOpMensajeBloqueo.Width = w - 40;
            };


            // =================================================================================
            // 4.5. VISTA: SECTORES (CRUD para Administrador)
            // =================================================================================
            panelSectores = new Panel { Dock = DockStyle.Fill };

            Label lblSecTitulo = new Label
            {
                Text = "Administración de Sectores de Cultivo",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 86, 49),
                Location = new Point(20, 20),
                AutoSize = true
            };

            lblSecMensajeBloqueo = new Label
            {
                Text = "Acceso Restringido. Se requieren privilegios de Administrador para gestionar sectores de cultivo.",
                ForeColor = Color.DarkRed,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Location = new Point(20, 80),
                Size = new Size(800, 100),
                Visible = false
            };

            Label lblSecBuscar = new Label
            {
                Text = "Buscar (Sector/Encargado):",
                Location = new Point(20, 180),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            txtSecBuscar = new TextBox
            {
                Location = new Point(200, 177),
                Size = new Size(300, 25),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            txtSecBuscar.TextChanged += TxtSecBuscar_TextChanged;

            dgvSectores = new DataGridView
            {
                Location = new Point(20, 210),
                Size = new Size(480, 390),
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
            };
            StyleDataGridView(dgvSectores, Color.FromArgb(41, 128, 185));
            dgvSectores.SelectionChanged += DgvSectores_SelectionChanged;

            // Filtros de Sectores
            GroupBox gbFiltrosSectores = new GroupBox
            {
                Text = "Filtros de Búsqueda",
                Location = new Point(20, 70),
                Size = new Size(480, 95),
                Font = new Font("Segoe UI", 9.0F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            Label lblFiltroEstado = new Label { Text = "Estado:", Location = new Point(12, 22), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Regular) };
            cboFiltroSecEstado = new ComboBox { Location = new Point(12, 45), Size = new Size(95, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            cboFiltroSecEstado.Items.AddRange(new string[] { "Activos", "Inactivos", "Todos" });
            cboFiltroSecEstado.SelectedIndex = 0; // Default a Activos

            Label lblFiltroDesde = new Label { Text = "Desde:", Location = new Point(117, 22), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Regular) };
            dtpSecDesde = new DateTimePicker { Location = new Point(117, 45), Size = new Size(100, 25), Format = DateTimePickerFormat.Short };
            dtpSecDesde.Value = DateTime.Today.AddMonths(-1);

            Label lblFiltroHasta = new Label { Text = "Hasta:", Location = new Point(227, 22), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Regular) };
            dtpSecHasta = new DateTimePicker { Location = new Point(227, 45), Size = new Size(100, 25), Format = DateTimePickerFormat.Short };

            btnFiltroSecAplicar = new Button
            {
                Text = "Aplicar Filtros",
                Location = new Point(337, 40),
                Size = new Size(130, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 86, 49),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.0F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnFiltroSecAplicar.Click += BtnFiltroSecAplicar_Click;

            gbFiltrosSectores.Controls.Add(lblFiltroEstado);
            gbFiltrosSectores.Controls.Add(cboFiltroSecEstado);
            gbFiltrosSectores.Controls.Add(lblFiltroDesde);
            gbFiltrosSectores.Controls.Add(dtpSecDesde);
            gbFiltrosSectores.Controls.Add(lblFiltroHasta);
            gbFiltrosSectores.Controls.Add(dtpSecHasta);
            gbFiltrosSectores.Controls.Add(btnFiltroSecAplicar);

            // Formulario CRUD de Sectores
            GroupBox gbSecForm = new GroupBox
            {
                Text = "Detalles del Sector",
                Location = new Point(520, 70),
                Size = new Size(310, 520),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            Label lblSecNom = new Label { Text = "Nombre del Sector:", Location = new Point(15, 30), AutoSize = true };
            txtSecNombre = new TextBox { Location = new Point(15, 55), Size = new Size(280, 25) };

            Label lblSecEnc = new Label { Text = "Nombre del Encargado / Dueño:", Location = new Point(15, 95), AutoSize = true };
            txtSecEncargado = new TextBox { Location = new Point(15, 120), Size = new Size(280, 25) };

            Label lblSecCult = new Label { Text = "Tipo de Planta instalada:", Location = new Point(15, 160), AutoSize = true };
            cboSecCultivo = new ComboBox { Location = new Point(15, 185), Size = new Size(280, 25), DropDownStyle = ComboBoxStyle.DropDownList };

            Label lblSecPort = new Label { Text = "Puerto Serial Asignado (ej: COM3):", Location = new Point(15, 225), AutoSize = true };
            cboSecPuerto = new ComboBox { Location = new Point(15, 250), Size = new Size(280, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            cboSecPuerto.Items.AddRange(new string[] { "COM3", "COM4", "COM5", "COM6", "COM7" });
            cboSecPuerto.SelectedIndex = 0; // Default to COM3

            Label lblSecTar = new Label { Text = "Tarifa de Consumo (USD / Litro):", Location = new Point(15, 290), AutoSize = true };
            txtSecTarifa = new TextBox { Location = new Point(15, 315), Size = new Size(280, 25) };

            Label lblSecCau = new Label { Text = "Caudal Límite (Litros/Minuto):", Location = new Point(15, 355), AutoSize = true };
            txtSecCaudal = new TextBox { Location = new Point(15, 380), Size = new Size(280, 25) };

            chkSecActivo = new CheckBox { Text = "Sector Habilitado y Activo", Location = new Point(15, 415), Size = new Size(280, 20), Checked = true };

            btnSecCrear = new Button { Text = "Crear", Location = new Point(15, 440), Size = new Size(80, 35), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(46, 125, 50), ForeColor = Color.White };
            btnSecCrear.Click += BtnSecCrear_Click;

            btnSecModificar = new Button { Text = "Modificar", Location = new Point(110, 440), Size = new Size(90, 35), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(230, 126, 34), ForeColor = Color.White };
            btnSecModificar.Click += BtnSecModificar_Click;

            btnSecEliminar = new Button { Text = "Baja", Location = new Point(215, 440), Size = new Size(80, 35), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(192, 57, 43), ForeColor = Color.White };
            btnSecEliminar.Click += BtnSecEliminar_Click;

            btnSecLimpiar = new Button { Text = "Limpiar", Location = new Point(15, 480), Size = new Size(130, 35), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(127, 140, 141), ForeColor = Color.White };
            btnSecLimpiar.Click += BtnSecLimpiar_Click;

            btnSecFisicoEliminar = new Button { Text = "Eliminar", Location = new Point(165, 480), Size = new Size(130, 35), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(192, 57, 43), ForeColor = Color.White };
            btnSecFisicoEliminar.Click += BtnSecFisicoEliminar_Click;

            gbSecForm.Controls.Add(lblSecNom);
            gbSecForm.Controls.Add(txtSecNombre);
            gbSecForm.Controls.Add(lblSecEnc);
            gbSecForm.Controls.Add(txtSecEncargado);
            gbSecForm.Controls.Add(lblSecCult);
            gbSecForm.Controls.Add(cboSecCultivo);
            gbSecForm.Controls.Add(lblSecPort);
            gbSecForm.Controls.Add(cboSecPuerto);
            gbSecForm.Controls.Add(lblSecTar);
            gbSecForm.Controls.Add(txtSecTarifa);
            gbSecForm.Controls.Add(lblSecCau);
            gbSecForm.Controls.Add(txtSecCaudal);
            gbSecForm.Controls.Add(chkSecActivo);
            gbSecForm.Controls.Add(btnSecCrear);
            gbSecForm.Controls.Add(btnSecModificar);
            gbSecForm.Controls.Add(btnSecEliminar);
            gbSecForm.Controls.Add(btnSecLimpiar);
            gbSecForm.Controls.Add(btnSecFisicoEliminar);

            panelSectores.Controls.Add(lblSecTitulo);
            panelSectores.Controls.Add(lblSecMensajeBloqueo);
            panelSectores.Controls.Add(gbFiltrosSectores);
            panelSectores.Controls.Add(lblSecBuscar);
            panelSectores.Controls.Add(txtSecBuscar);
            panelSectores.Controls.Add(dgvSectores);
            panelSectores.Controls.Add(gbSecForm);

            // Evento Resize de Sectores
            panelSectores.Resize += (s, e) =>
            {
                int w = panelSectores.Width;
                int h = panelSectores.Height;
                if (w < 400 || h < 400) return;

                gbSecForm.Left = w - gbSecForm.Width - 20;

                int leftWidth = gbSecForm.Left - 40;
                if (leftWidth < 100) leftWidth = 100;

                gbFiltrosSectores.Width = leftWidth;
                txtSecBuscar.Width = leftWidth + 20 - txtSecBuscar.Left;
                dgvSectores.Width = leftWidth;
                dgvSectores.Height = h - dgvSectores.Top - 20;

                lblSecMensajeBloqueo.Width = w - 40;
            };

            // =================================================================================
            // 5. VISTA: REPORTES
            // =================================================================================
            panelReportes = new Panel { Dock = DockStyle.Fill };

            Label lblRepTitulo = new Label
            {
                Text = "Reportes Comerciales y Analíticos",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 86, 49),
                Location = new Point(20, 20),
                AutoSize = true
            };

            GroupBox gbFiltros = new GroupBox
            {
                Text = "Filtros del Reporte",
                Location = new Point(20, 70),
                Size = new Size(810, 90),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            Label lblTipoRep = new Label { Text = "Reporte:", Location = new Point(15, 25), AutoSize = true };
            cboReportes = new ComboBox { Location = new Point(15, 48), Size = new Size(200, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            cboReportes.Items.AddRange(new string[] { "Reporte Agro-Climático", "Reporte de Costos y Consumo de Agua", "Reporte de Anomalías y Fugas" });
            cboReportes.SelectedIndex = 0;

            Label lblRepSector = new Label { Text = "Sector:", Location = new Point(230, 25), AutoSize = true };
            cboSectores = new ComboBox { Location = new Point(230, 48), Size = new Size(180, 25), DropDownStyle = ComboBoxStyle.DropDownList };

            Label lblRepIni = new Label { Text = "Desde:", Location = new Point(430, 25), AutoSize = true };
            dtpFechaInicio = new DateTimePicker { Location = new Point(430, 48), Size = new Size(110, 25), Format = DateTimePickerFormat.Short };
            dtpFechaInicio.Value = DateTime.Today.AddDays(-7);

            Label lblRepFin = new Label { Text = "Hasta:", Location = new Point(560, 25), AutoSize = true };
            dtpFechaFin = new DateTimePicker { Location = new Point(560, 48), Size = new Size(110, 25), Format = DateTimePickerFormat.Short };

            btnEjecutarReporte = new Button
            {
                Text = "Ejecutar Reporte",
                Location = new Point(690, 43),
                Size = new Size(110, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 86, 49),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnEjecutarReporte.Click += BtnEjecutarReporte_Click;

            gbFiltros.Controls.Add(lblTipoRep);
            gbFiltros.Controls.Add(cboReportes);
            gbFiltros.Controls.Add(lblRepSector);
            gbFiltros.Controls.Add(cboSectores);
            gbFiltros.Controls.Add(lblRepIni);
            gbFiltros.Controls.Add(dtpFechaInicio);
            gbFiltros.Controls.Add(lblRepFin);
            gbFiltros.Controls.Add(dtpFechaFin);
            gbFiltros.Controls.Add(btnEjecutarReporte);

            dgvReportes = new DataGridView
            {
                Location = new Point(20, 180),
                Size = new Size(810, 420),
                AllowUserToAddRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            StyleDataGridView(dgvReportes, Color.FromArgb(41, 128, 185));

            btnImprimirPDF = new Button
            {
                Text = "Imprimir Reporte PDF",
                Location = new Point(620, 20),
                Size = new Size(180, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(192, 57, 43), // Carmesí formal
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            btnImprimirPDF.Click += BtnImprimirPDF_Click;

            panelReportes.Controls.Add(lblRepTitulo);
            panelReportes.Controls.Add(btnImprimirPDF);
            panelReportes.Controls.Add(gbFiltros);
            panelReportes.Controls.Add(dgvReportes);

            // Evento Resize de Reportes
            panelReportes.Resize += (s, e) =>
            {
                int w = panelReportes.Width;
                int h = panelReportes.Height;
                if (w < 200 || h < 200) return;

                btnImprimirPDF.Left = w - btnImprimirPDF.Width - 20;
                gbFiltros.Width = w - 40;
                dgvReportes.Width = w - 40;
                dgvReportes.Height = h - dgvReportes.Top - 20;
            };

            // =================================================================================
            // 6. VISTA: ESTADÍSTICAS HISTÓRICAS (Dashboard Analítico)
            // =================================================================================
            panelEstadisticas = new Panel { Dock = DockStyle.Fill };

            Label lblEstTitulo = new Label
            {
                Text = "Dashboard Analítico de Estadísticas Históricas",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 86, 49),
                Location = new Point(20, 20),
                AutoSize = true
            };

            GroupBox gbFiltrosEst = new GroupBox
            {
                Text = "Filtros de Análisis",
                Location = new Point(20, 70),
                Size = new Size(810, 80),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            Label lblEstSector = new Label { Text = "Sector de Cultivo:", Location = new Point(15, 22), AutoSize = true };
            cboEstSectores = new ComboBox { Location = new Point(15, 45), Size = new Size(200, 25), DropDownStyle = ComboBoxStyle.DropDownList };

            Label lblEstIni = new Label { Text = "Fecha Inicio:", Location = new Point(240, 22), AutoSize = true };
            dtpEstInicio = new DateTimePicker { Location = new Point(240, 45), Size = new Size(130, 25), Format = DateTimePickerFormat.Short };
            dtpEstInicio.Value = DateTime.Today.AddDays(-7);

            Label lblEstFin = new Label { Text = "Fecha Fin:", Location = new Point(400, 22), AutoSize = true };
            dtpEstFin = new DateTimePicker { Location = new Point(400, 45), Size = new Size(130, 25), Format = DateTimePickerFormat.Short };

            btnProcesarEst = new Button
            {
                Text = "Procesar Análisis",
                Location = new Point(560, 40),
                Size = new Size(150, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 86, 49),
                ForeColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnProcesarEst.Click += BtnProcesarEst_Click;

            gbFiltrosEst.Controls.Add(lblEstSector);
            gbFiltrosEst.Controls.Add(cboEstSectores);
            gbFiltrosEst.Controls.Add(lblEstIni);
            gbFiltrosEst.Controls.Add(dtpEstInicio);
            gbFiltrosEst.Controls.Add(lblEstFin);
            gbFiltrosEst.Controls.Add(dtpEstFin);
            gbFiltrosEst.Controls.Add(btnProcesarEst);

            // Panel de Tarjetas de Métricas de Temperatura
            GroupBox gbTemperaturaMetricas = new GroupBox
            {
                Text = "Métricas Agregadas de Temperatura del Suelo",
                Location = new Point(20, 165),
                Size = new Size(810, 100),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            Panel cardTempMax = CrearTarjetaMetrica("TEMP. MÁXIMA", Color.FromArgb(192, 57, 43), 20, 30, out lblEstTempMax);
            cardTempMax.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            Panel cardTempMin = CrearTarjetaMetrica("TEMP. MÍNIMA", Color.FromArgb(41, 128, 185), 280, 30, out lblEstTempMin);
            cardTempMin.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            Panel cardTempAvg = CrearTarjetaMetrica("TEMP. PROMEDIO", Color.FromArgb(230, 126, 34), 540, 30, out lblEstTempAvg);
            cardTempAvg.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            gbTemperaturaMetricas.Controls.Add(cardTempMax);
            gbTemperaturaMetricas.Controls.Add(cardTempMin);
            gbTemperaturaMetricas.Controls.Add(cardTempAvg);

            // Gráfico de Consumo Hídrico
            GroupBox gbGrafico = new GroupBox
            {
                Text = "Optimización Hídrica - Consumo de Agua Diario (Litros)",
                Location = new Point(20, 280),
                Size = new Size(810, 320),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            // Crear el componente Chart de WinForms
            chartConsumo = new System.Windows.Forms.DataVisualization.Charting.Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            
            // Configurar ChartArea
            var chartArea = new System.Windows.Forms.DataVisualization.Charting.ChartArea("AreaPrincipal");
            chartArea.AxisX.Title = "Fecha";
            chartArea.AxisX.TitleFont = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            chartArea.AxisX.LabelStyle.Font = new Font("Segoe UI", 8F);
            chartArea.AxisX.LabelStyle.Format = "dd/MM";
            chartArea.AxisX.Interval = 1;
            chartArea.AxisX.MajorGrid.LineColor = Color.FromArgb(240, 240, 240);
            
            chartArea.AxisY.Title = "Litros Consumidos (L)";
            chartArea.AxisY.TitleFont = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            chartArea.AxisY.LabelStyle.Font = new Font("Segoe UI", 8F);
            chartArea.AxisY.MajorGrid.LineColor = Color.FromArgb(240, 240, 240);
            
            chartConsumo.ChartAreas.Add(chartArea);

            // Configurar Serie
            var serieConsumo = new System.Windows.Forms.DataVisualization.Charting.Series("Consumo Hídrico")
            {
                ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Column,
                Color = Color.FromArgb(41, 128, 185), // Azul
                BorderWidth = 1,
                XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.DateTime
            };
            chartConsumo.Series.Add(serieConsumo);
            
            // Leyenda
            var leyenda = new System.Windows.Forms.DataVisualization.Charting.Legend("Legend1")
            {
                Docking = System.Windows.Forms.DataVisualization.Charting.Docking.Top,
                Font = new Font("Segoe UI", 8F)
            };
            chartConsumo.Legends.Add(leyenda);

            gbGrafico.Controls.Add(chartConsumo);

            panelEstadisticas.Controls.Add(lblEstTitulo);
            panelEstadisticas.Controls.Add(gbFiltrosEst);
            panelEstadisticas.Controls.Add(gbTemperaturaMetricas);
            panelEstadisticas.Controls.Add(gbGrafico);

            // Evento Resize de Estadísticas
            panelEstadisticas.Resize += (s, e) =>
            {
                int w = panelEstadisticas.Width;
                int h = panelEstadisticas.Height;
                if (w < 200 || h < 200) return;

                gbFiltrosEst.Width = w - 40;
                gbTemperaturaMetricas.Width = w - 40;
                gbGrafico.Width = w - 40;
                gbGrafico.Height = h - gbGrafico.Top - 20;

                // Posicionar dinámicamente las 3 tarjetas de métricas en gbTemperaturaMetricas
                int availableWidth = gbTemperaturaMetricas.Width;
                int cardW = 240;
                int remainingSpace = availableWidth - (3 * cardW);
                if (remainingSpace < 40)
                {
                    cardTempMax.Left = 20;
                    cardTempMin.Left = 280;
                    cardTempAvg.Left = 540;
                }
                else
                {
                    int gap = remainingSpace / 4;
                    cardTempMax.Left = gap;
                    cardTempMin.Left = gap + cardW + gap;
                    cardTempAvg.Left = gap + cardW + gap + cardW + gap;
                }
            };

            // Agregar Paneles Contenedores al Formulario
            this.Controls.Add(panelMainContainer);
            this.Controls.Add(panelSidebar);
        }

        // =================================================================================
        // MÉTODOS DE SOPORTE DISEÑO Y NAVEGACIÓN
        // =================================================================================

        private Button CrearBotonNavegacion(string texto, int posicionY)
        {
            Button btn = new Button
            {
                Text = texto,
                Location = new Point(0, posicionY),
                Size = new Size(240, 50),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(46, 125, 50);
            return btn;
        }

        private Panel CrearTarjeta(string titulo, Color colorAcortado, int posX, int posY, out Label valorLabel)
        {
            Panel panel = new Panel
            {
                Location = new Point(posX, posY),
                Size = new Size(190, 120),
                BackColor = Color.White
            };

            Label lblTitulo = new Label
            {
                Text = titulo,
                Location = new Point(15, 12),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.DarkGray,
                AutoSize = true
            };

            valorLabel = new Label
            {
                Text = "--",
                Location = new Point(15, 35),
                Font = new Font("Segoe UI", 28F, FontStyle.Bold),
                ForeColor = colorAcortado,
                Size = new Size(160, 55)
            };

            panel.Controls.Add(lblTitulo);
            panel.Controls.Add(valorLabel);
            return panel;
        }

        private void ActualizarTendenciasUI(RiegoController.SectorState sec)
        {
            if (sec != null && sec.IsOnline)
            {
                if (sec.PendienteEvaporacion < 0)
                {
                    lblHumedadTendencia.Text = $"Tendencia: {sec.PendienteEvaporacion:F2} %/hora";
                    lblHumedadTendencia.ForeColor = Color.Crimson;

                    if (sec.TiempoLimiteEstimado == 0)
                    {
                        lblHumedadTiempo.Text = "Límite: Crítico superado";
                        lblHumedadTiempo.ForeColor = Color.Crimson;
                    }
                    else if (sec.TiempoLimiteEstimado < 60)
                    {
                        lblHumedadTiempo.Text = $"Límite: Riego Inminente ({sec.TiempoLimiteEstimado} min)";
                        lblHumedadTiempo.ForeColor = Color.Crimson;
                    }
                    else
                    {
                        lblHumedadTiempo.Text = $"Límite: {sec.TiempoLimiteEstimado} minutos";
                        lblHumedadTiempo.ForeColor = Color.OrangeRed;
                    }
                }
                else if (sec.PendienteEvaporacion > 0)
                {
                    lblHumedadTendencia.Text = $"Tendencia: +{sec.PendienteEvaporacion:F2} %/hora";
                    lblHumedadTendencia.ForeColor = Color.Green;
                    lblHumedadTiempo.Text = "Límite: Estable / Riego Activo";
                    lblHumedadTiempo.ForeColor = Color.Green;
                }
                else
                {
                    lblHumedadTendencia.Text = "Tendencia: Estable (0.00 %/h)";
                    lblHumedadTendencia.ForeColor = Color.Gray;
                    lblHumedadTiempo.Text = "Límite: Estable";
                    lblHumedadTiempo.ForeColor = Color.Gray;
                }
            }
            else
            {
                lblHumedadTendencia.Text = "Tendencia: --";
                lblHumedadTendencia.ForeColor = Color.Gray;
                lblHumedadTiempo.Text = "Límite: --";
                lblHumedadTiempo.ForeColor = Color.Gray;
            }
        }

        private void StyleDataGridView(DataGridView dgv, Color headerColor)
        {
            dgv.EnableHeadersVisualStyles = false;
            dgv.GridColor = Color.FromArgb(235, 235, 235);
            dgv.RowTemplate.Height = 32;

            // Header Style
            dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = headerColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.75F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft
            };
            dgv.ColumnHeadersHeight = 36;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // Rows Style
            dgv.DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(50, 50, 50),
                SelectionBackColor = Color.FromArgb(230, 240, 250),
                SelectionForeColor = Color.Black
            };

            // Alternating Row Style
            dgv.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(248, 249, 250)
            };
        }

        private void SwitchPanel(Panel panelDestino)
        {
            panelMonitoreo.Visible = false;
            panelOperadores.Visible = false;
            panelSectores.Visible = false;
            panelReportes.Visible = false;
            if (panelEstadisticas != null) panelEstadisticas.Visible = false;

            if (!panelMainContainer.Controls.Contains(panelDestino))
            {
                panelMainContainer.Controls.Add(panelDestino);
            }

            panelDestino.Visible = true;
        }

        private void ConfigurarAccesoPorRol()
        {
            if (!usuarioSesion.EsAdministrador())
            {
                // Operador no puede gestionar otros operadores o sectores
                btnNavOperadores.Visible = false;
                btnNavSectores.Visible = false;

                // Ocultar botón de impresión PDF
                btnImprimirPDF.Visible = false;

                // Ajustar posiciones de botones de navegación restantes para evitar huecos vacíos
                btnNavReportes.Location = new Point(0, 200);
                btnNavCerrarSesion.Location = new Point(0, 520);

                // Operador no puede gestionar otros operadores
                dgvOperadores.Visible = false;
                txtOpUsername.Enabled = false;
                txtOpNombre.Enabled = false;
                txtOpPassword.Enabled = false;
                btnOpCrear.Enabled = false;
                btnOpModificar.Enabled = false;
                btnOpLimpiar.Enabled = false;
                btnOpFisicoEliminar.Enabled = false;
                txtOpBuscar.Enabled = false;
                lblOpMensajeBloqueo.Visible = true;

                // Operador no puede gestionar sectores
                dgvSectores.Visible = false;
                txtSecNombre.Enabled = false;
                txtSecEncargado.Enabled = false;
                cboSecCultivo.Enabled = false;
                cboSecPuerto.Enabled = false;
                txtSecTarifa.Enabled = false;
                txtSecCaudal.Enabled = false;
                chkSecActivo.Enabled = false;
                btnSecCrear.Enabled = false;
                btnSecModificar.Enabled = false;
                btnSecEliminar.Enabled = false;
                btnSecLimpiar.Enabled = false;
                btnSecFisicoEliminar.Enabled = false;
                txtSecBuscar.Enabled = false;
                lblSecMensajeBloqueo.Visible = true;
            }
        }

        private void CargarSectoresMonitoreoGrid()
        {
            dgvMonitoreoSectores.Rows.Clear();
            List<RiegoController.SectorState> activos = riegoController.ObtenerSectoresActivos();
            foreach (var sec in activos)
            {
                dgvMonitoreoSectores.Rows.Add(sec.IdSector, sec.NombreSector, sec.PuertoSerial, sec.NombreCultivo, "--", "--", "--", "Apagada", "Fuera de Línea");
            }
        }

        private void CargarSectoresCombo()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT IdSector, NombreSector FROM dbo.Sectores WHERE Activo = 1", conn))
                    {
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        DataRow dr = dt.NewRow();
                        dr["IdSector"] = 0;
                        dr["NombreSector"] = "-- Todos los Sectores --";
                        dt.Rows.InsertAt(dr, 0);

                        cboSectores.DataSource = dt;
                        cboSectores.DisplayMember = "NombreSector";
                        cboSectores.ValueMember = "IdSector";
                    }
                }
            }
            catch
            {
                cboSectores.Items.Add("-- Todos los Sectores --");
                cboSectores.SelectedIndex = 0;
            }
        }

        private void CargarSectoresOpCombo()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT IdSector, NombreSector FROM dbo.Sectores WHERE Activo = 1", conn))
                    {
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        DataRow dr = dt.NewRow();
                        dr["IdSector"] = 0;
                        dr["NombreSector"] = "-- Sin Asignar --";
                        dt.Rows.InsertAt(dr, 0);

                        cboOpSector.DataSource = dt;
                        cboOpSector.DisplayMember = "NombreSector";
                        cboOpSector.ValueMember = "IdSector";
                    }
                }
            }
            catch (Exception ex)
            {
                cboOpSector.DataSource = null;
                cboOpSector.Items.Clear();
                cboOpSector.Items.Add("-- Sin Asignar --");
                cboOpSector.SelectedIndex = 0;
                Console.WriteLine("Error al cargar sectores en combo operadores: " + ex.Message);
            }
        }

        private void CargarCultivosEnCombo()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT IdCultivo, NombreCultivo FROM dbo.Cultivos", conn))
                    {
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        cboSecCultivo.DataSource = dt;
                        cboSecCultivo.DisplayMember = "NombreCultivo";
                        cboSecCultivo.ValueMember = "IdCultivo";
                    }
                }
            }
            catch
            {
                cboSecCultivo.Items.Add("Olivo");
                cboSecCultivo.SelectedIndex = 0;
            }
        }

        // =================================================================================
        // ACCIONES DE MONITOREO Y CONEXIÓN DE HARDWARE (Multipuerto en Paralelo)
        // =================================================================================

        private void BtnConectar_Click(object sender, EventArgs e)
        {
            if (btnConectar.Text == "Conectar Dispositivos" || btnConectar.Text == "Conectar Dispositivo")
            {
                sectoresAlertados.Clear();
                CargarSectoresMonitoreoGrid();

                bool ok = riegoController.ConectarTodosLosSectores();
                if (ok)
                {
                    btnConectar.Text = "Desconectar Dispositivos";
                    btnConectar.BackColor = Color.FromArgb(192, 57, 43);
                }
            }
            else
            {
                riegoController.DesconectarTodos();
                btnConectar.Text = "Conectar Dispositivos";
                btnConectar.BackColor = Color.FromArgb(46, 125, 50);

                // Limpiar valores en pantalla
                lblHumedadValor.Text = "--";
                lblTemperaturaValor.Text = "--";
                lblEstadoSueloValor.Text = "Estado: Desconectado";
                ActualizarTendenciasUI(null);
                panelReleStatus.BackColor = Color.Gray;
                lblReleTexto.Text = "BOMBA APAGADA";
                lblReleTexto.ForeColor = Color.DimGray;

                panelConexionStatus.BackColor = Color.FromArgb(192, 57, 43);
                lblConexionTexto.Text = "FUERA DE LÍNEA";
                lblConexionTexto.ForeColor = Color.FromArgb(192, 57, 43);
                btnRiegoManual.Enabled = false;
                btnRiegoManual.Text = "Activar Riego Manual";
                btnRiegoManual.BackColor = Color.FromArgb(46, 125, 50);

                CargarSectoresMonitoreoGrid();
            }
        }

        private void DgvMonitoreoSectores_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvMonitoreoSectores.SelectedRows.Count > 0)
            {
                DataGridViewRow row = dgvMonitoreoSectores.SelectedRows[0];
                lblHumedadValor.Text = row.Cells["Humedad"].Value?.ToString() ?? "--";
                lblTemperaturaValor.Text = row.Cells["Temperatura"].Value?.ToString() ?? "--";
                
                string bombaText = row.Cells["Bomba"].Value?.ToString() ?? "Apagada";
                if (bombaText.Equals("Encendida", StringComparison.OrdinalIgnoreCase))
                {
                    panelReleStatus.BackColor = Color.FromArgb(46, 125, 50);
                    lblReleTexto.Text = "BOMBA ACTIVADA";
                    lblReleTexto.ForeColor = Color.FromArgb(46, 125, 50);
                }
                else
                {
                    panelReleStatus.BackColor = Color.Gray;
                    lblReleTexto.Text = "BOMBA APAGADA";
                    lblReleTexto.ForeColor = Color.DimGray;
                }

                // Buscar sector para actualizar el texto de estado radicular y conexión
                int idSector = Convert.ToInt32(row.Cells["IdSector"].Value);
                var dict = riegoController.ObtenerSectoresMonitoreados();
                lblEstadoSueloValor.Text = "Estado: Desconectado";
                
                bool found = false;
                foreach (var kvp in dict)
                {
                    if (kvp.Value.IdSector == idSector)
                    {
                        var sec = kvp.Value;
                        lblEstadoSueloValor.Text = "Estado: " + sec.EstadoSuelo;

                        // Tarjeta de Estado de Conexión
                        if (sec.IsOnline)
                        {
                            panelConexionStatus.BackColor = Color.FromArgb(46, 125, 50);
                            lblConexionTexto.Text = "EN LÍNEA";
                            lblConexionTexto.ForeColor = Color.FromArgb(46, 125, 50);

                            // Activar botón de riego manual si es Administrador
                            if (usuarioSesion.EsAdministrador())
                            {
                                btnRiegoManual.Enabled = true;
                                if (sec.ModoManual)
                                {
                                    btnRiegoManual.Text = "Detener Riego";
                                    btnRiegoManual.BackColor = Color.FromArgb(192, 57, 43);
                                }
                                else
                                {
                                    btnRiegoManual.Text = "Activar Riego Manual";
                                    btnRiegoManual.BackColor = Color.FromArgb(46, 125, 50);
                                }
                            }
                        }
                        else
                        {
                            panelConexionStatus.BackColor = Color.FromArgb(192, 57, 43);
                            lblConexionTexto.Text = "FUERA DE LÍNEA";
                            lblConexionTexto.ForeColor = Color.FromArgb(192, 57, 43);
                            btnRiegoManual.Enabled = false;
                            btnRiegoManual.Text = "Activar Riego Manual";
                            btnRiegoManual.BackColor = Color.FromArgb(46, 125, 50);
                        }

                        ActualizarTendenciasUI(sec);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    panelConexionStatus.BackColor = Color.FromArgb(192, 57, 43);
                    lblConexionTexto.Text = "FUERA DE LÍNEA";
                    lblConexionTexto.ForeColor = Color.FromArgb(192, 57, 43);
                    btnRiegoManual.Enabled = false;
                    btnRiegoManual.Text = "Activar Riego Manual";
                    btnRiegoManual.BackColor = Color.FromArgb(46, 125, 50);
                    ActualizarTendenciasUI(null);
                }
            }
            else
            {
                ActualizarTendenciasUI(null);
            }
        }

        private void RiegoController_OnConexionEstadoChanged(bool conectado, string mensaje)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => RiegoController_OnConexionEstadoChanged(conectado, mensaje)));
                return;
            }

            lstLogAlertas.Items.Add($"[{DateTime.Now:HH:mm:ss}] {mensaje}");

            if (conectado)
            {
                btnConectar.Text = "Desconectar Dispositivos";
                btnConectar.BackColor = Color.FromArgb(192, 57, 43);
            }
            else
            {
                btnConectar.Text = "Conectar Dispositivos";
                btnConectar.BackColor = Color.FromArgb(46, 125, 50);
            }
        }

        private void RiegoController_OnDatosSectorRecibidos(int idSector, string puerto, double humedad, double temperatura, string estadoSuelo, double salud, bool bombaEncendida, bool isOnline)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => RiegoController_OnDatosSectorRecibidos(idSector, puerto, humedad, temperatura, estadoSuelo, salud, bombaEncendida, isOnline)));
                return;
            }

            // Actualizar fila del Grid
            foreach (DataGridViewRow row in dgvMonitoreoSectores.Rows)
            {
                if (Convert.ToInt32(row.Cells["IdSector"].Value) == idSector)
                {
                    row.Cells["EstadoRed"].Value = isOnline ? "En Línea" : "Fuera de Línea";
                    row.Cells["EstadoRed"].Style.BackColor = isOnline ? Color.FromArgb(46, 125, 50) : Color.FromArgb(192, 57, 43);
                    row.Cells["EstadoRed"].Style.ForeColor = Color.White;

                    if (!isOnline)
                    {
                        row.Cells["Humedad"].Value = "--";
                        row.Cells["Temperatura"].Value = "--";
                        row.Cells["Salud"].Value = "--";
                        row.Cells["Salud"].Style.BackColor = Color.Empty;
                        row.Cells["Salud"].Style.ForeColor = Color.Empty;
                        row.Cells["Bomba"].Value = "Apagada";

                        if (sectoresAlertados.Contains(idSector))
                        {
                            sectoresAlertados.Remove(idSector);
                        }
                    }
                    else
                    {
                        row.Cells["Humedad"].Value = humedad.ToString("0.0") + "%";
                        row.Cells["Temperatura"].Value = temperatura.ToString("0.0") + "°C";
                        row.Cells["Salud"].Value = salud.ToString("0.0") + "%";
                        row.Cells["Bomba"].Value = bombaEncendida ? "Encendida" : "Apagada";

                        // Formato y colores según la salud del cultivo
                        if (salud >= 80.0)
                        {
                            row.Cells["Salud"].Style.BackColor = Color.FromArgb(46, 125, 50); // Verde
                            row.Cells["Salud"].Style.ForeColor = Color.White;
                        }
                        else if (salud >= 50.0)
                        {
                            row.Cells["Salud"].Style.BackColor = Color.FromArgb(241, 196, 15); // Amarillo
                            row.Cells["Salud"].Style.ForeColor = Color.Black;
                        }
                        else
                        {
                            row.Cells["Salud"].Style.BackColor = Color.FromArgb(192, 57, 43); // Rojo
                            row.Cells["Salud"].Style.ForeColor = Color.White;

                            // Alerta crítica automática
                            if (!sectoresAlertados.Contains(idSector))
                            {
                                sectoresAlertados.Add(idSector);
                                string secName = row.Cells["Sector"].Value?.ToString() ?? "Sector Desconocido";

                                // Enviar alerta por WhatsApp de forma asíncrona
                                string msg = $"🚨 *ALERTA CRÍTICA EN {secName.ToUpper()}* 🚨\n\nLa salud del cultivo ha caído a {salud:0.0}%. Humedad: {humedad:0.0}%, Temperatura: {temperatura:0.0}°C. El sistema ha iniciado automáticamente el riego de emergencia.";
                                System.Threading.Tasks.Task.Run(() => GestionNotificaciones.EnviarAlertaWhatsAppPorPuerto(puerto, msg));

                                MessageBox.Show($"¡ALERTA CRÍTICA EN {secName.ToUpper()}!\n\nLa salud del cultivo ha caído a {salud:0.0}%. El sistema ha iniciado automáticamente el riego de emergencia.",
                                                "Estrés de Cultivo Detectado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }

                        // Resetear alerta si sale del estado rojo
                        if (salud >= 50.0 && sectoresAlertados.Contains(idSector))
                        {
                            sectoresAlertados.Remove(idSector);
                        }
                    }
                    break;
                }
            }

            // Si es el sector seleccionado actualmente, actualizar las tarjetas
            if (dgvMonitoreoSectores.SelectedRows.Count > 0)
            {
                int currentSelId = Convert.ToInt32(dgvMonitoreoSectores.SelectedRows[0].Cells["IdSector"].Value);
                if (currentSelId == idSector)
                {
                    if (isOnline)
                    {
                        lblHumedadValor.Text = humedad.ToString("0.0") + "%";
                        lblTemperaturaValor.Text = temperatura.ToString("0.0") + "°C";
                        lblEstadoSueloValor.Text = "Estado: " + estadoSuelo;

                        RiegoController.SectorState sec = null;
                        var sectoresDict = riegoController.ObtenerSectoresMonitoreados();
                        foreach (var kvp in sectoresDict)
                        {
                            if (kvp.Value.IdSector == idSector)
                            {
                                sec = kvp.Value;
                                break;
                            }
                        }
                        ActualizarTendenciasUI(sec);

                        if (bombaEncendida)
                        {
                            panelReleStatus.BackColor = Color.FromArgb(46, 125, 50);
                            lblReleTexto.Text = "BOMBA ACTIVADA";
                            lblReleTexto.ForeColor = Color.FromArgb(46, 125, 50);
                        }
                        else
                        {
                            panelReleStatus.BackColor = Color.Gray;
                            lblReleTexto.Text = "BOMBA APAGADA";
                            lblReleTexto.ForeColor = Color.DimGray;
                        }

                        // Conexión Card
                        panelConexionStatus.BackColor = Color.FromArgb(46, 125, 50);
                        lblConexionTexto.Text = "EN LÍNEA";
                        lblConexionTexto.ForeColor = Color.FromArgb(46, 125, 50);

                        // Configurar botón de riego manual
                        if (usuarioSesion.EsAdministrador())
                        {
                            btnRiegoManual.Enabled = true;
                            
                            // Buscar sector para ver si está en modo manual
                            bool isModoManual = false;
                            var dict = riegoController.ObtenerSectoresMonitoreados();
                            foreach (var kvp in dict)
                            {
                                if (kvp.Value.IdSector == idSector)
                                {
                                    isModoManual = kvp.Value.ModoManual;
                                    break;
                                }
                            }

                            if (isModoManual)
                            {
                                btnRiegoManual.Text = "Detener Riego";
                                btnRiegoManual.BackColor = Color.FromArgb(192, 57, 43);
                            }
                            else
                            {
                                btnRiegoManual.Text = "Activar Riego Manual";
                                btnRiegoManual.BackColor = Color.FromArgb(46, 125, 50);
                            }
                        }
                    }
                    else
                    {
                        lblHumedadValor.Text = "--";
                        lblTemperaturaValor.Text = "--";
                        lblEstadoSueloValor.Text = "Estado: Sector Fuera de Línea";
                        ActualizarTendenciasUI(null);

                        panelReleStatus.BackColor = Color.Gray;
                        lblReleTexto.Text = "BOMBA APAGADA";
                        lblReleTexto.ForeColor = Color.DimGray;

                        // Conexión Card
                        panelConexionStatus.BackColor = Color.FromArgb(192, 57, 43);
                        lblConexionTexto.Text = "FUERA DE LÍNEA";
                        lblConexionTexto.ForeColor = Color.FromArgb(192, 57, 43);

                        btnRiegoManual.Enabled = false;
                        btnRiegoManual.Text = "Activar Riego Manual";
                        btnRiegoManual.BackColor = Color.FromArgb(46, 125, 50);
                    }
                }
            }
        }

        private void RiegoController_OnDatosRecibidos(double humedad, double temperatura, string estadoSuelo)
        {
            // Método legado - compatibilidad
        }

        private void RiegoController_OnEstadoBombaChanged(bool encendida)
        {
            // Controlado dinámicamente en RiegoController_OnDatosSectorRecibidos
        }

        private void RiegoController_OnAlertaRegistrada(string mensaje)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => RiegoController_OnAlertaRegistrada(mensaje)));
                return;
            }

            lstLogAlertas.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {mensaje}");

            // Enviar notificaciones WhatsApp automáticas para fugas y pérdida de conexión
            if (mensaje.Contains("¡ALERTA DE SEGURIDAD") || mensaje.Contains("¡CONEXIÓN PERDIDA"))
            {
                string puerto = ObtenerPuertoComDesdeMensaje(mensaje);
                string msgText = $"⚠️ *AVISO CRÍTICO* ⚠️\n\n{mensaje}";
                if (!string.IsNullOrEmpty(puerto))
                {
                    System.Threading.Tasks.Task.Run(() => GestionNotificaciones.EnviarAlertaWhatsAppPorPuerto(puerto, msgText));
                }
                else
                {
                    System.Threading.Tasks.Task.Run(() => GestionNotificaciones.EnviarAlertaWhatsAppAOperadores(msgText));
                }
            }
        }

        private string ObtenerPuertoComDesdeMensaje(string mensaje)
        {
            // 1. Intentar buscar un patrón tipo COM3, COM4, COM5...
            for (int i = 3; i <= 20; i++)
            {
                string comStr = "COM" + i;
                if (mensaje.Contains(comStr))
                {
                    return comStr;
                }
            }

            // 2. Si no se especifica el COM directamente, intentar buscar el nombre del sector entre corchetes [] o comillas ''
            string nombreSector = null;
            if (mensaje.Contains("[") && mensaje.Contains("]"))
            {
                int start = mensaje.IndexOf("[") + 1;
                int end = mensaje.IndexOf("]", start);
                if (end > start)
                {
                    nombreSector = mensaje.Substring(start, end - start).Trim();
                }
            }
            else if (mensaje.Contains("'") && mensaje.IndexOf("'", mensaje.IndexOf("'") + 1) > 0)
            {
                int start = mensaje.IndexOf("'") + 1;
                int end = mensaje.IndexOf("'", start);
                if (end > start)
                {
                    nombreSector = mensaje.Substring(start, end - start).Trim();
                }
            }

            // Si pudimos extraer un nombre de sector, buscamos su puerto serial en la BD
            if (!string.IsNullOrEmpty(nombreSector))
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                    {
                        using (SqlCommand cmd = new SqlCommand("SELECT PuertoSerial FROM dbo.Sectores WHERE NombreSector = @NombreSector", conn))
                        {
                            cmd.Parameters.AddWithValue("@NombreSector", nombreSector);
                            conn.Open();
                            object res = cmd.ExecuteScalar();
                            if (res != null && res != DBNull.Value)
                            {
                                return res.ToString();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error al buscar puerto serial por nombre de sector: " + ex.Message);
                }
            }

            return null;
        }

        // =================================================================================
        // LÓGICA GESTIÓN OPERADORES (CRUD ADO.NET)
        // =================================================================================

        private void ListarOperadores()
        {
            try
            {
                if (txtOpBuscar != null) txtOpBuscar.Text = "";
                using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                {
                    using (SqlCommand cmd = new SqlCommand("dbo.sp_ListarOperadores", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);
                        dgvOperadores.DataSource = dt;

                        if (dgvOperadores.Columns.Contains("IdSector"))
                            dgvOperadores.Columns["IdSector"].Visible = false;
                        if (dgvOperadores.Columns.Contains("NombreSector"))
                            dgvOperadores.Columns["NombreSector"].HeaderText = "Sector";

                        // Configurar que solo el checkbox de Activo sea editable
                        dgvOperadores.ReadOnly = false;
                        foreach (DataGridViewColumn col in dgvOperadores.Columns)
                        {
                            if (col.Name == "Activo")
                                col.ReadOnly = false;
                            else
                                col.ReadOnly = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al listar operadores: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvOperadores_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvOperadores.SelectedRows.Count > 0)
            {
                try
                {
                    DataGridViewRow row = dgvOperadores.SelectedRows[0];
                    if (row.Cells["IdUsuario"].Value != null && row.Cells["IdUsuario"].Value != DBNull.Value)
                    {
                        selectedOpId = Convert.ToInt32(row.Cells["IdUsuario"].Value);
                        txtOpUsername.Text = row.Cells["Username"].Value?.ToString() ?? "";
                        txtOpNombre.Text = row.Cells["Nombre"].Value?.ToString() ?? "";
                        txtOpPassword.Text = "";
                        txtOpCelular.Text = row.Cells["Celular"].Value?.ToString() ?? ""; // Cargar celular
                        
                        if (dgvOperadores.Columns.Contains("IdSector") && row.Cells["IdSector"].Value != null && row.Cells["IdSector"].Value != DBNull.Value)
                        {
                            cboOpSector.SelectedValue = Convert.ToInt32(row.Cells["IdSector"].Value);
                        }
                        else
                        {
                            cboOpSector.SelectedIndex = 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al seleccionar operador: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnOpCrear_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtOpUsername.Text) || string.IsNullOrWhiteSpace(txtOpNombre.Text) || string.IsNullOrWhiteSpace(txtOpPassword.Text))
            {
                MessageBox.Show("Por favor, complete todos los campos.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Validar nomenclatura del número de celular
            string celular = txtOpCelular.Text.Trim().Replace(" ", "");
            if (!string.IsNullOrEmpty(celular))
            {
                if (!celular.StartsWith("+") || celular.Length < 9 || celular.Length > 15 || !System.Text.RegularExpressions.Regex.IsMatch(celular.Substring(1), @"^\d+$"))
                {
                    MessageBox.Show("El número de celular debe usar formato internacional completo (ej: +51990877875), empezando con '+' seguido del código de país y dígitos sin espacios.", "Validación de Celular", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            try
            {
                using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                {
                    using (SqlCommand cmd = new SqlCommand("dbo.sp_CrearOperador", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Username", txtOpUsername.Text.Trim());
                        cmd.Parameters.AddWithValue("@PasswordTextoPlano", txtOpPassword.Text);
                        cmd.Parameters.AddWithValue("@Nombre", txtOpNombre.Text.Trim());
                        cmd.Parameters.AddWithValue("@Celular", string.IsNullOrEmpty(celular) ? (object)DBNull.Value : celular);
                        
                        object idSectorParam = DBNull.Value;
                        if (cboOpSector.SelectedValue != null)
                        {
                            int val = Convert.ToInt32(cboOpSector.SelectedValue);
                            if (val > 0) idSectorParam = val;
                        }
                        cmd.Parameters.AddWithValue("@IdSector", idSectorParam);

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Operador creado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ListarOperadores();
                LimpiarFormularioOperador();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al crear operador: " + ex.Message, "Fallo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnOpModificar_Click(object sender, EventArgs e)
        {
            if (selectedOpId == -1)
            {
                MessageBox.Show("Seleccione un operador de la lista para modificar.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Validar nomenclatura del número de celular
            string celular = txtOpCelular.Text.Trim().Replace(" ", "");
            if (!string.IsNullOrEmpty(celular))
            {
                if (!celular.StartsWith("+") || celular.Length < 9 || celular.Length > 15 || !System.Text.RegularExpressions.Regex.IsMatch(celular.Substring(1), @"^\d+$"))
                {
                    MessageBox.Show("El número de celular debe usar formato internacional completo (ej: +51990877875), empezando con '+' seguido del código de país y dígitos sin espacios.", "Validación de Celular", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            try
            {
                using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                {
                    using (SqlCommand cmd = new SqlCommand("dbo.sp_ModificarOperador", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IdUsuario", selectedOpId);
                        cmd.Parameters.AddWithValue("@Username", txtOpUsername.Text.Trim());
                        cmd.Parameters.AddWithValue("@Nombre", txtOpNombre.Text.Trim());
                        cmd.Parameters.AddWithValue("@Celular", string.IsNullOrEmpty(celular) ? (object)DBNull.Value : celular);
                        
                        object idSectorParam = DBNull.Value;
                        if (cboOpSector.SelectedValue != null)
                        {
                            int val = Convert.ToInt32(cboOpSector.SelectedValue);
                            if (val > 0) idSectorParam = val;
                        }
                        cmd.Parameters.AddWithValue("@IdSector", idSectorParam);
                        
                        if (string.IsNullOrWhiteSpace(txtOpPassword.Text))
                            cmd.Parameters.AddWithValue("@PasswordTextoPlano", DBNull.Value);
                        else
                            cmd.Parameters.AddWithValue("@PasswordTextoPlano", txtOpPassword.Text);

                        cmd.Parameters.AddWithValue("@Activo", 1);

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Operador modificado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ListarOperadores();
                LimpiarFormularioOperador();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al modificar operador: " + ex.Message, "Fallo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvOperadores_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dgvOperadores.IsCurrentCellDirty)
            {
                dgvOperadores.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private void DgvOperadores_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dgvOperadores.Columns[e.ColumnIndex].Name == "Activo")
            {
                try
                {
                    DataGridViewRow row = dgvOperadores.Rows[e.RowIndex];
                    int idUsuario = Convert.ToInt32(row.Cells["IdUsuario"].Value);
                    bool nuevoEstado = Convert.ToBoolean(row.Cells["Activo"].Value);

                    using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                    {
                        string query = "UPDATE dbo.Usuarios SET Activo = @Activo WHERE IdUsuario = @IdUsuario";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@Activo", nuevoEstado);
                            cmd.Parameters.AddWithValue("@IdUsuario", idUsuario);
                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }
                    Console.WriteLine($"[DB] Estado del usuario ID {idUsuario} actualizado a {(nuevoEstado ? "Activo" : "Inactivo")}.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al actualizar el estado en la base de datos: " + ex.Message, 
                                    "Error de Conexión", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnOpLimpiar_Click(object sender, EventArgs e)
        {
            LimpiarFormularioOperador();
        }

        private void BtnOpFisicoEliminar_Click(object sender, EventArgs e)
        {
            if (selectedOpId == -1)
            {
                MessageBox.Show("Seleccione un operador para eliminar permanentemente.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirmResult = MessageBox.Show("¿Está seguro de eliminar PERMANENTEMENTE al operador seleccionado de la base de datos?\nEsta acción no se puede deshacer.", "Confirmar Eliminación Física", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirmResult == DialogResult.No) return;

            try
            {
                using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                {
                    using (SqlCommand cmd = new SqlCommand("DELETE FROM dbo.Usuarios WHERE IdUsuario = @IdUsuario", conn))
                    {
                        cmd.Parameters.AddWithValue("@IdUsuario", selectedOpId);
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Operador eliminado permanentemente de la base de datos.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ListarOperadores();
                LimpiarFormularioOperador();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al eliminar físicamente al operador: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LimpiarFormularioOperador()
        {
            selectedOpId = -1;
            txtOpUsername.Text = "";
            txtOpNombre.Text = "";
            txtOpPassword.Text = "";
            txtOpCelular.Text = "";
            if (cboOpSector != null && cboOpSector.Items.Count > 0)
            {
                cboOpSector.SelectedIndex = 0;
            }
        }

        // =================================================================================
        // LÓGICA GESTIÓN SECTORES (CRUD ADO.NET)
        // =================================================================================

        private void ListarSectores(bool? activo = true, DateTime? fechaInicio = null, DateTime? fechaFin = null)
        {
            estadoFiltradoActual = activo;
            try
            {
                if (txtSecBuscar != null) txtSecBuscar.Text = "";
                using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                {
                    using (SqlCommand cmd = new SqlCommand("dbo.sp_ListarTodosLosSectores", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Activo", (object)activo ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@FechaInicio", (object)fechaInicio ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@FechaFin", (object)fechaFin ?? DBNull.Value);

                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);
                        dgvSectores.DataSource = dt;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al listar sectores: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnFiltroSecAplicar_Click(object sender, EventArgs e)
        {
            bool? activo = null;
            if (cboFiltroSecEstado.SelectedIndex == 0) activo = true;   // Activos
            else if (cboFiltroSecEstado.SelectedIndex == 1) activo = false; // Inactivos
            
            DateTime desde = dtpSecDesde.Value.Date;
            DateTime hasta = dtpSecHasta.Value.Date.AddDays(1).AddTicks(-1);

            if (desde > hasta)
            {
                MessageBox.Show("La fecha 'Desde' no puede ser mayor que la fecha 'Hasta'.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ListarSectores(activo, desde, hasta);
        }

        private void DgvSectores_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvSectores.SelectedRows.Count > 0)
            {
                try
                {
                    DataGridViewRow row = dgvSectores.SelectedRows[0];
                    if (row.Cells["IdSector"].Value != null && row.Cells["IdSector"].Value != DBNull.Value)
                    {
                        selectedSecId = Convert.ToInt32(row.Cells["IdSector"].Value);
                        txtSecNombre.Text = row.Cells["NombreSector"].Value?.ToString() ?? "";
                        txtSecEncargado.Text = row.Cells["NombreEncargado"].Value?.ToString() ?? "";
                        
                        // Seleccionar cultivo en el combo por su ID (más robusto que usar Text)
                        if (row.Cells["IdCultivo"].Value != null && row.Cells["IdCultivo"].Value != DBNull.Value)
                        {
                            cboSecCultivo.SelectedValue = Convert.ToInt32(row.Cells["IdCultivo"].Value);
                        }
                        else
                        {
                            cboSecCultivo.SelectedIndex = -1;
                        }

                        string puerto = row.Cells["PuertoSerial"].Value?.ToString() ?? "COM3";
                        if (!cboSecPuerto.Items.Contains(puerto))
                        {
                            cboSecPuerto.Items.Add(puerto);
                        }
                        cboSecPuerto.SelectedItem = puerto;

                        txtSecTarifa.Text = row.Cells["TarifaAguaReferencial"].Value?.ToString() ?? "";
                        txtSecCaudal.Text = row.Cells["CaudalBombaLPM"].Value?.ToString() ?? "";
                        
                        if (row.Cells["Activo"].Value != null && row.Cells["Activo"].Value != DBNull.Value)
                        {
                            chkSecActivo.Checked = Convert.ToBoolean(row.Cells["Activo"].Value);
                        }
                        else
                        {
                            chkSecActivo.Checked = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al seleccionar sector: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnSecCrear_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSecNombre.Text) || string.IsNullOrWhiteSpace(txtSecEncargado.Text) || 
                cboSecPuerto.SelectedIndex == -1 || cboSecCultivo.SelectedValue == null)
            {
                MessageBox.Show("Por favor, complete todos los campos obligatorios.", "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                double.TryParse(txtSecTarifa.Text, out double tarifa);
                double.TryParse(txtSecCaudal.Text, out double caudal);
                int idCultivo = Convert.ToInt32(cboSecCultivo.SelectedValue);

                using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                {
                    using (SqlCommand cmd = new SqlCommand("dbo.sp_CrearSector", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@NombreSector", txtSecNombre.Text.Trim());
                        cmd.Parameters.AddWithValue("@NombreEncargado", txtSecEncargado.Text.Trim());
                        cmd.Parameters.AddWithValue("@IdCultivo", idCultivo);
                        cmd.Parameters.AddWithValue("@PuertoSerial", cboSecPuerto.Text.Trim());
                        cmd.Parameters.AddWithValue("@TarifaAguaReferencial", tarifa);
                        cmd.Parameters.AddWithValue("@CaudalBombaLPM", caudal);

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Sector de cultivo creado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ListarSectores(estadoFiltradoActual);
                CargarSectoresCombo();
                CargarSectoresOpCombo();
                CargarSectoresMonitoreoGrid();
                LimpiarFormularioSector();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al crear el sector: " + ex.Message, "Fallo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSecModificar_Click(object sender, EventArgs e)
        {
            if (selectedSecId == -1)
            {
                MessageBox.Show("Seleccione un sector de la lista para modificar.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Validar si el sector seleccionado está activo antes de proceder
            if (dgvSectores.SelectedRows.Count > 0)
            {
                DataGridViewRow row = dgvSectores.SelectedRows[0];
                bool estaActivo = Convert.ToBoolean(row.Cells["Activo"].Value);
                if (!estaActivo)
                {
                    MessageBox.Show("No se pueden modificar sectores que han sido dados de baja.", "Operación Denegada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            try
            {
                double.TryParse(txtSecTarifa.Text, out double tarifa);
                double.TryParse(txtSecCaudal.Text, out double caudal);
                int idCultivo = Convert.ToInt32(cboSecCultivo.SelectedValue);

                using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                {
                    using (SqlCommand cmd = new SqlCommand("dbo.sp_ModificarSector", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IdSector", selectedSecId);
                        cmd.Parameters.AddWithValue("@NombreSector", txtSecNombre.Text.Trim());
                        cmd.Parameters.AddWithValue("@NombreEncargado", txtSecEncargado.Text.Trim());
                        cmd.Parameters.AddWithValue("@IdCultivo", idCultivo);
                        cmd.Parameters.AddWithValue("@PuertoSerial", cboSecPuerto.Text.Trim());
                        cmd.Parameters.AddWithValue("@TarifaAguaReferencial", tarifa);
                        cmd.Parameters.AddWithValue("@CaudalBombaLPM", caudal);
                        cmd.Parameters.AddWithValue("@Activo", chkSecActivo.Checked);

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Sector de cultivo modificado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ListarSectores(estadoFiltradoActual);
                CargarSectoresCombo();
                CargarSectoresOpCombo();
                CargarSectoresMonitoreoGrid();
                LimpiarFormularioSector();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al modificar el sector: " + ex.Message, "Fallo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSecEliminar_Click(object sender, EventArgs e)
        {
            if (selectedSecId == -1)
            {
                MessageBox.Show("Seleccione un sector para darle de baja.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Validar si el sector ya está dado de baja
            if (dgvSectores.SelectedRows.Count > 0)
            {
                DataGridViewRow row = dgvSectores.SelectedRows[0];
                bool estaActivo = Convert.ToBoolean(row.Cells["Activo"].Value);
                if (!estaActivo)
                {
                    MessageBox.Show("Este sector ya se encuentra dado de baja.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            var confirmResult = MessageBox.Show("¿Está seguro de dar de baja el sector seleccionado?", "Confirmar Baja Lógica", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirmResult == DialogResult.No) return;

            try
            {
                using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                {
                    using (SqlCommand cmd = new SqlCommand("dbo.sp_EliminarSector", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@IdSector", selectedSecId);

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Sector inhabilitado exitosamente (Baja Lógica).", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ListarSectores(estadoFiltradoActual);
                CargarSectoresCombo();
                CargarSectoresOpCombo();
                CargarSectoresMonitoreoGrid();
                LimpiarFormularioSector();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al inhabilitar sector: " + ex.Message, "Fallo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSecLimpiar_Click(object sender, EventArgs e)
        {
            LimpiarFormularioSector();
        }

        private void BtnSecFisicoEliminar_Click(object sender, EventArgs e)
        {
            if (selectedSecId == -1)
            {
                MessageBox.Show("Seleccione un sector para eliminar permanentemente.", "Información", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirmResult = MessageBox.Show("¿Está seguro de eliminar PERMANENTEMENTE el sector seleccionado de la base de datos?\nEsta acción no se puede deshacer y eliminará también el dispositivo por defecto asociado si no tiene lecturas.", "Confirmar Eliminación Física", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirmResult == DialogResult.No) return;

            try
            {
                using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                {
                    conn.Open();
                    using (SqlTransaction trans = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Eliminar dispositivos asociados
                            using (SqlCommand cmdDisp = new SqlCommand("DELETE FROM dbo.Dispositivos WHERE IdSector = @IdSector", conn, trans))
                            {
                                cmdDisp.Parameters.AddWithValue("@IdSector", selectedSecId);
                                cmdDisp.ExecuteNonQuery();
                            }

                            // 2. Eliminar el sector
                            using (SqlCommand cmdSec = new SqlCommand("DELETE FROM dbo.Sectores WHERE IdSector = @IdSector", conn, trans))
                            {
                                cmdSec.Parameters.AddWithValue("@IdSector", selectedSecId);
                                cmdSec.ExecuteNonQuery();
                            }

                            trans.Commit();
                        }
                        catch (SqlException ex) when (ex.Number == 547)
                        {
                            trans.Rollback();
                            MessageBox.Show("No se puede eliminar físicamente el sector porque cuenta con lecturas, alertas o historial de riego vinculados.\n\nPor favor, use la opción 'Baja' (inhabilitar) en su lugar para mantener la integridad de los datos.", "Restricción de Integridad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        catch (Exception)
                        {
                            trans.Rollback();
                            throw;
                        }
                    }
                }
                MessageBox.Show("Sector eliminado permanentemente de la base de datos.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ListarSectores(estadoFiltradoActual);
                CargarSectoresCombo();
                CargarSectoresOpCombo();
                CargarSectoresMonitoreoGrid();
                LimpiarFormularioSector();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al eliminar físicamente el sector: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LimpiarFormularioSector()
        {
            selectedSecId = -1;
            txtSecNombre.Text = "";
            txtSecEncargado.Text = "";
            if (cboSecPuerto.Items.Count > 0) cboSecPuerto.SelectedIndex = 0;
            txtSecTarifa.Text = "";
            txtSecCaudal.Text = "";
            chkSecActivo.Checked = true;
        }

        // =================================================================================
        // EJECUCIÓN DE REPORTES MULTIFILTRO
        // =================================================================================

        private void BtnEjecutarReporte_Click(object sender, EventArgs e)
        {
            if (cboReportes.SelectedIndex == -1) return;

            string reporte = cboReportes.SelectedItem.ToString();
            DateTime fInicio = dtpFechaInicio.Value;
            DateTime fFin = dtpFechaFin.Value;
            
            int? idSector = null;
            if (cboSectores.SelectedValue != null && cboSectores.SelectedValue is int secId && secId > 0)
            {
                idSector = secId;
            }

            string spName = "";
            if (reporte.Contains("Agro-Climático"))
                spName = "dbo.sp_ReporteAgroClimatico";
            else if (reporte.Contains("Costos"))
                spName = "dbo.sp_ReporteCostosYConsumo";
            else if (reporte.Contains("Anomalías"))
                spName = "dbo.sp_ReporteAnomaliasYFugas";

            try
            {
                using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                {
                    using (SqlCommand cmd = new SqlCommand(spName, conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        if (spName == "dbo.sp_ReporteAgroClimatico")
                        {
                            cmd.Parameters.AddWithValue("@IdSector", (object)idSector ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@FechaInicio", fInicio);
                            cmd.Parameters.AddWithValue("@FechaFin", fFin);
                        }
                        else if (spName == "dbo.sp_ReporteCostosYConsumo")
                        {
                            cmd.Parameters.AddWithValue("@FechaInicio", fInicio);
                            cmd.Parameters.AddWithValue("@FechaFin", fFin);
                        }
                        else if (spName == "dbo.sp_ReporteAnomaliasYFugas")
                        {
                            cmd.Parameters.AddWithValue("@IdSector", (object)idSector ?? DBNull.Value);
                        }

                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);
                        dgvReportes.DataSource = dt;

                        if (dgvReportes.Columns.Contains("CodigoMAC"))
                        {
                            dgvReportes.Columns["CodigoMAC"].Visible = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fallo al ejecutar reporte: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnImprimirPDF_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog sfd = new SaveFileDialog
                {
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    FileName = "Reporte_Sectores_HydroSense.pdf",
                    Title = "Guardar Reporte de Sectores en PDF"
                };

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    riegoController.GenerarReporteSectoresPDF(sfd.FileName);
                    MessageBox.Show("Reporte PDF generado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    // Abrir el PDF automáticamente
                    System.Diagnostics.Process.Start(sfd.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar el reporte PDF: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnCerrarSesion_Click(object sender, EventArgs e)
        {
            riegoController.DesconectarTodos();
            this.Hide();
            FrmLogin login = new FrmLogin();
            login.Show();
        }

        private void PanelConexionStatus_Paint(object sender, PaintEventArgs e)
        {
            Panel p = (Panel)sender;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Brush b = new SolidBrush(p.BackColor))
            {
                e.Graphics.FillEllipse(b, 0, 0, p.Width - 1, p.Height - 1);
            }
        }

        private void BtnRiegoManual_Click(object sender, EventArgs e)
        {
            if (dgvMonitoreoSectores.SelectedRows.Count > 0)
            {
                int idSector = Convert.ToInt32(dgvMonitoreoSectores.SelectedRows[0].Cells["IdSector"].Value);
                var dict = riegoController.ObtenerSectoresMonitoreados();
                foreach (var kvp in dict)
                {
                    if (kvp.Value.IdSector == idSector)
                    {
                        riegoController.ConmutarRiegoManual(kvp.Value);
                        break;
                    }
                }
            }
        }

        private Panel CrearTarjetaMetrica(string titulo, Color colorTexto, int posX, int posY, out Label valorLabel)
        {
            Panel panel = new Panel
            {
                Location = new Point(posX, posY),
                Size = new Size(240, 55),
                BackColor = Color.FromArgb(245, 247, 245),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblTitulo = new Label
            {
                Text = titulo,
                Location = new Point(10, 8),
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.DarkGray,
                AutoSize = true
            };

            valorLabel = new Label
            {
                Text = "-- °C",
                Location = new Point(10, 23),
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = colorTexto,
                AutoSize = true
            };

            panel.Controls.Add(lblTitulo);
            panel.Controls.Add(valorLabel);
            return panel;
        }

        private void CargarSectoresComboEstadisticas()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                {
                    using (SqlCommand cmd = new SqlCommand("SELECT IdSector, NombreSector FROM dbo.Sectores WHERE Activo = 1", conn))
                    {
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        DataRow dr = dt.NewRow();
                        dr["IdSector"] = 0;
                        dr["NombreSector"] = "-- Todos los Sectores --";
                        dt.Rows.InsertAt(dr, 0);

                        cboEstSectores.DataSource = dt;
                        cboEstSectores.DisplayMember = "NombreSector";
                        cboEstSectores.ValueMember = "IdSector";
                    }
                }
            }
            catch
            {
                cboEstSectores.Items.Clear();
                cboEstSectores.Items.Add("-- Todos los Sectores --");
                cboEstSectores.SelectedIndex = 0;
            }
        }

        private void BtnProcesarEst_Click(object sender, EventArgs e)
        {
            if (cboEstSectores.SelectedValue == null) return;

            int idSector = Convert.ToInt32(cboEstSectores.SelectedValue);
            DateTime inicio = dtpEstInicio.Value.Date;
            DateTime fin = dtpEstFin.Value.Date.AddDays(1).AddSeconds(-1);

            // 1. Obtener métricas de temperatura
            var metricas = riegoController.ObtenerMetricasTemperatura(idSector, inicio, fin);
            lblEstTempMax.Text = metricas["Max"].ToString("0.0") + " °C";
            lblEstTempMin.Text = metricas["Min"].ToString("0.0") + " °C";
            lblEstTempAvg.Text = metricas["Avg"].ToString("0.0") + " °C";

            // 2. Obtener y graficar consumo de agua
            DataTable dtConsumo = riegoController.ObtenerConsumoAguaHistorico(idSector, inicio, fin);
            
            chartConsumo.Series["Consumo Hídrico"].Points.Clear();

            foreach (DataRow row in dtConsumo.Rows)
            {
                DateTime fecha = Convert.ToDateTime(row["Fecha"]);
                double litros = Convert.ToDouble(row["TotalLitros"]);
                
                chartConsumo.Series["Consumo Hídrico"].Points.AddXY(fecha, litros);
            }

            if (dtConsumo.Rows.Count == 0)
            {
                MessageBox.Show("No se encontraron registros de riego para el sector y rango de fechas seleccionados.", 
                                "Sin Datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void TxtOpBuscar_TextChanged(object sender, EventArgs e)
        {
            if (dgvOperadores.DataSource is DataTable dt)
            {
                string filtro = txtOpBuscar.Text.Trim().Replace("'", "''");
                if (string.IsNullOrEmpty(filtro))
                {
                    dt.DefaultView.RowFilter = "";
                }
                else
                {
                    dt.DefaultView.RowFilter = string.Format("Username LIKE '%{0}%' OR Nombre LIKE '%{0}%'", filtro);
                }
            }
        }

        private void TxtSecBuscar_TextChanged(object sender, EventArgs e)
        {
            if (dgvSectores.DataSource is DataTable dt)
            {
                string filtro = txtSecBuscar.Text.Trim().Replace("'", "''");
                if (string.IsNullOrEmpty(filtro))
                {
                    dt.DefaultView.RowFilter = "";
                }
                else
                {
                    dt.DefaultView.RowFilter = string.Format("NombreSector LIKE '%{0}%' OR NombreEncargado LIKE '%{0}%'", filtro);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            riegoController.DesconectarTodos();
            base.OnFormClosing(e);
            Application.Exit();
        }
    }
}
