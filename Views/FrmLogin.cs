using System;
using System.Drawing;
using System.Windows.Forms;
using YaradaHydroSense.Controllers;
using YaradaHydroSense.Models;

namespace YaradaHydroSense.Views
{
    /// <summary>
    /// Formulario de Login diseñado con estética plana, moderna y agrícola.
    /// Creado programáticamente para evitar dependencias de archivos de diseño XML de Visual Studio.
    /// </summary>
    public class FrmLogin : Form
    {
        private Panel panelHeader;
        private Panel panelMain;
        private Label lblTitulo;
        private Label lblSubtitulo;
        private Label lblUsuario;
        private Label lblPassword;
        private TextBox txtUsuario;
        private TextBox txtPassword;
        private Button btnIngresar;
        private Button btnCerrar;
        private Label lblInfo;

        private AuthController authController;

        // Variables para mover el formulario borderless
        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        public FrmLogin()
        {
            authController = new AuthController();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Propiedades Básicas de la Ventana
            this.Size = new Size(400, 480);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 244, 241); // Fondo gris claro ecológico
            this.Font = new Font("Segoe UI", 10F, FontStyle.Regular);

            // 1. Panel Superior (Header / Barra de Título Personalizada)
            panelHeader = new Panel
            {
                Size = new Size(400, 45),
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(30, 86, 49) // Verde Bosque Oscuro
            };
            panelHeader.MouseDown += Header_MouseDown;
            panelHeader.MouseMove += Header_MouseMove;
            panelHeader.MouseUp += Header_MouseUp;

            lblTitulo = new Label
            {
                Text = "Yarada HydroSense",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Location = new Point(15, 11),
                AutoSize = true
            };
            lblTitulo.MouseDown += (s, e) => Header_MouseDown(s, e); // Mover desde el texto también
            lblTitulo.MouseMove += (s, e) => Header_MouseMove(s, e);
            lblTitulo.MouseUp += (s, e) => Header_MouseUp(s, e);

            btnCerrar = new Button
            {
                Text = "✕",
                Size = new Size(40, 45),
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCerrar.FlatAppearance.BorderSize = 0;
            btnCerrar.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 50, 50); // Rojo al pasar cursor
            btnCerrar.Click += (s, e) => Application.Exit();

            panelHeader.Controls.Add(lblTitulo);
            panelHeader.Controls.Add(btnCerrar);

            // 2. Panel Central (Contenedor Principal de Elementos)
            panelMain = new Panel
            {
                Location = new Point(25, 65),
                Size = new Size(350, 390),
                BackColor = Color.White
            };

            lblSubtitulo = new Label
            {
                Text = "Iniciar Sesión",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(46, 125, 50), // Verde Agrícola
                Location = new Point(20, 20),
                AutoSize = true
            };

            lblUsuario = new Label
            {
                Text = "Usuario",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.DimGray,
                Location = new Point(20, 80),
                AutoSize = true
            };

            txtUsuario = new TextBox
            {
                Location = new Point(20, 105),
                Size = new Size(310, 30),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11F)
            };

            lblPassword = new Label
            {
                Text = "Contraseña",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.DimGray,
                Location = new Point(20, 160),
                AutoSize = true
            };

            txtPassword = new TextBox
            {
                Location = new Point(20, 185),
                Size = new Size(310, 30),
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = true, // Ocultar caracteres
                Font = new Font("Segoe UI", 11F)
            };

            btnIngresar = new Button
            {
                Text = "Ingresar al Sistema",
                Location = new Point(20, 255),
                Size = new Size(310, 45),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 125, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnIngresar.FlatAppearance.BorderSize = 0;
            btnIngresar.FlatAppearance.MouseOverBackColor = Color.FromArgb(56, 142, 60);
            btnIngresar.Click += BtnIngresar_Click;

            lblInfo = new Label
            {
                Text = "Precisión IoT para el Agro de Tacna",
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.DarkGray,
                Location = new Point(20, 330),
                Size = new Size(310, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            panelMain.Controls.Add(lblSubtitulo);
            panelMain.Controls.Add(lblUsuario);
            panelMain.Controls.Add(txtUsuario);
            panelMain.Controls.Add(lblPassword);
            panelMain.Controls.Add(txtPassword);
            panelMain.Controls.Add(btnIngresar);
            panelMain.Controls.Add(lblInfo);

            // Agregar Paneles al Formulario
            this.Controls.Add(panelMain);
            this.Controls.Add(panelHeader);
        }

        private void BtnIngresar_Click(object sender, EventArgs e)
        {
            string user = txtUsuario.Text.Trim();
            string pass = txtPassword.Text;

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("Por favor, ingrese el usuario y la contraseña.", "Campos vacíos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Invocamos al controlador de Autenticación
                UsuarioModel usuario = authController.Autenticar(user, pass);

                if (usuario != null)
                {
                    // Éxito: Ocultar login y abrir Dashboard pasándole los datos del perfil
                    this.Hide();
                    FrmDashboard mainDash = new FrmDashboard(usuario);
                    mainDash.Show();
                }
                else
                {
                    MessageBox.Show("Credenciales incorrectas o usuario inactivo.", "Error de Acceso", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fallo en la comunicación: " + ex.Message, "Error del Servidor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // =================================================================================
        // HILOS Y EVENTOS DE ARRASTRE DE FORMULARIO SIN BORDES
        // =================================================================================
        private void Header_MouseDown(object sender, MouseEventArgs e)
        {
            dragging = true;
            dragCursorPoint = Cursor.Position;
            dragFormPoint = this.Location;
        }

        private void Header_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(dragFormPoint, new Size(dif));
            }
        }

        private void Header_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }
    }
}
