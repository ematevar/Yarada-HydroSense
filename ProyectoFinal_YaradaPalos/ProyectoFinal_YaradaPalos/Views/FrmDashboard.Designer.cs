using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace ProyectoFinal_YaradaPalos.Views
{
    partial class FrmDashboard
    {
        /// <summary>
        /// Variable del diseñador necesaria.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpiar los recursos que se estén usando.
        /// </summary>
        /// <param name="disposing">true si los recursos administrados se deben desechar; false en caso contrario.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código generado por el Diseñador de Windows Forms

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
                string puerto = "COM13"; // Default fallback
                bool encontrado = false;
                var dict = riegoController.ObtenerSectoresMonitoreados();
                foreach (var kvp in dict)
                {
                    if (kvp.Value.NombreSector.IndexOf("Olivo", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        puerto = kvp.Key;
                        encontrado = true;
                        break;
                    }
                }

                if (!encontrado)
                {
                    try
                    {
                        using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                        {
                            using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 PuertoSerial FROM dbo.Sectores WHERE NombreSector LIKE '%Olivo%' AND Activo = 1", conn))
                            {
                                conn.Open();
                                object res = cmd.ExecuteScalar();
                                if (res != null && res != DBNull.Value) puerto = res.ToString();
                            }
                        }
                    }
                    catch { }
                }

                // Activar la telemetría simulada a nivel del controlador para provocar la alerta crítica y el riego automático de emergencia
                riegoController.SimularTelemetria(puerto, 15.2, 26.5);
                lstLogAlertas.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] [Simulador] Inyectada telemetría de estrés (H: 15.2%, T: 26.5°C) en {puerto}.");
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
                string puerto = "COM13"; // Default fallback
                bool encontrado = false;
                var dict = riegoController.ObtenerSectoresMonitoreados();
                foreach (var kvp in dict)
                {
                    if (kvp.Value.NombreSector.IndexOf("Olivo", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        puerto = kvp.Key;
                        encontrado = true;
                        break;
                    }
                }

                if (!encontrado)
                {
                    try
                    {
                        using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                        {
                            using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 PuertoSerial FROM dbo.Sectores WHERE NombreSector LIKE '%Olivo%' AND Activo = 1", conn))
                            {
                                conn.Open();
                                object res = cmd.ExecuteScalar();
                                if (res != null && res != DBNull.Value) puerto = res.ToString();
                            }
                        }
                    }
                    catch { }
                }

                ProyectoFinal_YaradaPalos.Controllers.RiegoController.SectorState sec = null;
                foreach (var kvp in dict)
                {
                    if (kvp.Key.Equals(puerto, StringComparison.OrdinalIgnoreCase))
                    {
                        sec = kvp.Value;
                        break;
                    }
                }

                if (sec != null && sec.BombaEncendida)
                {
                    // Forzar el estado de la simulación de fuga a nivel del controlador
                    sec.FechaEncendidoBomba = DateTime.Now.AddMinutes(-6); // Exceder límite de 5 minutos
                    sec.Humedad = sec.HumedadInicialEncendido - 0.5; // Simular que la humedad no aumentó
                    riegoController.SimularTelemetria(puerto, sec.Humedad, sec.Temperatura);
                    lstLogAlertas.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] [Simulador] Forzada fuga en {puerto} (tiempo transcurrido sin aumento de humedad).");
                }
                else
                {
                    MessageBox.Show("El sistema debe estar regando el sector (Bomba Encendida) para poder simular una fuga en la bomba.", "Aviso del Simulador", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
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
                string puerto = "COM4"; // Default fallback
                bool encontrado = false;
                var dict = riegoController.ObtenerSectoresMonitoreados();
                foreach (var kvp in dict)
                {
                    if (kvp.Value.NombreSector.IndexOf("Granada", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        puerto = kvp.Key;
                        encontrado = true;
                        break;
                    }
                }

                if (!encontrado)
                {
                    try
                    {
                        using (SqlConnection conn = new SqlConnection("Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;"))
                        {
                            using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 PuertoSerial FROM dbo.Sectores WHERE NombreSector LIKE '%Granada%' AND Activo = 1", conn))
                            {
                                conn.Open();
                                object res = cmd.ExecuteScalar();
                                if (res != null && res != DBNull.Value) puerto = res.ToString();
                            }
                        }
                    }
                    catch { }
                }

                ProyectoFinal_YaradaPalos.Controllers.RiegoController.SectorState sec = null;
                foreach (var kvp in dict)
                {
                    if (kvp.Key.Equals(puerto, StringComparison.OrdinalIgnoreCase))
                    {
                        sec = kvp.Value;
                        break;
                    }
                }

                if (sec != null && sec.IsOnline)
                {
                    // Forzar el timeout de conexión a nivel del controlador (se detectará en el siguiente segundo)
                    sec.LastTelemetryTime = DateTime.Now.AddSeconds(-20);
                    lstLogAlertas.Items.Insert(0, $"[{DateTime.Now:HH:mm:ss}] [Simulador] Forzado timeout de conexión en {puerto}.");
                }
                else
                {
                    MessageBox.Show("El sector debe estar en línea (Online) para poder simular la pérdida de conexión.", "Aviso del Simulador", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
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
            
            // Populación dinámica y robusta de puertos COM
            var listaPuertos = new List<string>();
            try
            {
                listaPuertos.AddRange(System.IO.Ports.SerialPort.GetPortNames());
            }
            catch { }

            // Agregar puertos estándar si no fueron detectados por hardware, asegurando que COM13 y otros estén disponibles
            for (int i = 1; i <= 20; i++)
            {
                string portName = "COM" + i;
                if (!listaPuertos.Contains(portName))
                {
                    listaPuertos.Add(portName);
                }
            }

            cboSecPuerto.Items.AddRange(listaPuertos.ToArray());
            
            if (cboSecPuerto.Items.Contains("COM13"))
            {
                cboSecPuerto.SelectedItem = "COM13";
            }
            else if (cboSecPuerto.Items.Count > 0)
            {
                cboSecPuerto.SelectedIndex = 0;
            }

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

        #endregion

        // Contenedores Principales
        private Panel panelSidebar;
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

        // Filtros de Sectores
        private ComboBox cboFiltroSecEstado;
        private DateTimePicker dtpSecDesde;
        private DateTimePicker dtpSecHasta;
        private Button btnFiltroSecAplicar;

        // Elementos de Reportes
        private ComboBox cboReportes;
        private ComboBox cboSectores;
        private DateTimePicker dtpFechaInicio;
        private DateTimePicker dtpFechaFin;
        private Button btnEjecutarReporte;
        private Button btnImprimirPDF;
        private DataGridView dgvReportes;
    }
}
