using System;
using System.Windows.Forms;
using ProyectoFinal_YaradaPalos.Views;

namespace ProyectoFinal_YaradaPalos
{
    internal static class Program
    {
        /// <summary>
        /// Punto de entrada principal para la aplicación.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Iniciar directamente con la pantalla de Login del sistema comercial
            Application.Run(new FrmLogin());
        }
    }
}
