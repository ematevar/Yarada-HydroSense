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
    public partial class FrmDashboard : Form
    {
        private UsuarioModel usuarioSesion;
        private RiegoController riegoController;

        // Sectores con alerta crítica activa para evitar spam de MessageBox
        private HashSet<int> sectoresAlertados = new HashSet<int>();
        private int selectedOpId = -1;
        private int selectedSecId = -1;
        private bool? estadoFiltradoActual = true; // Por defecto Activos

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

                                // Enviar alerta por WhatsApp y mostrar MessageBox diferenciado por déficit o exceso
                                string msg;
                                if (bombaEncendida)
                                {
                                    msg = $"🚨 *ALERTA CRÍTICA EN {secName.ToUpper()}* 🚨\n\nLa salud del cultivo ha caído a {salud:0.0}%. Humedad: {humedad:0.0}%, Temperatura: {temperatura:0.0}°C. El sistema ha iniciado automáticamente el riego de emergencia.";
                                    System.Threading.Tasks.Task.Run(() => GestionNotificaciones.EnviarAlertaWhatsAppPorPuerto(puerto, msg));

                                    MessageBox.Show($"¡ALERTA CRÍTICA EN {secName.ToUpper()}!\n\nLa salud del cultivo ha caído a {salud:0.0}%. El sistema ha iniciado automáticamente el riego de emergencia.",
                                                    "Estrés de Cultivo Detectado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                                else
                                {
                                    msg = $"⚠️ *ALERTA DE SATURACIÓN EN {secName.ToUpper()}* ⚠️\n\nLa salud del cultivo ha caído a {salud:0.0}% debido a un exceso crítico de humedad ({humedad:0.0}%). El riego automático ha sido suspendido.";
                                    System.Threading.Tasks.Task.Run(() => GestionNotificaciones.EnviarAlertaWhatsAppPorPuerto(puerto, msg));

                                    MessageBox.Show($"¡ALERTA DE SATURACIÓN EN {secName.ToUpper()}!\n\nLa salud del cultivo ha caído a {salud:0.0}% debido a un exceso crítico de humedad ({humedad:0.0}%).\n\nEl sistema ha suspendido el riego automático para proteger las raíces del cultivo.",
                                                    "Saturación de Suelo Detectada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }
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
