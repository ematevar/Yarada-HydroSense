using System;

namespace YaradaHydroSense.Models
{
    /// <summary>
    /// Representa el modelo de datos de un usuario autenticado en el sistema.
    /// </summary>
    public class UsuarioModel
    {
        public int IdUsuario { get; set; }
        public string Username { get; set; }
        public string Nombre { get; set; }
        public string NombreRol { get; set; }

        public UsuarioModel()
        {
        }

        public UsuarioModel(int idUsuario, string username, string nombre, string nombreRol)
        {
            IdUsuario = idUsuario;
            Username = username;
            Nombre = nombre;
            NombreRol = nombreRol;
        }

        /// <summary>
        /// Comprueba si el usuario tiene privilegios de Administrador.
        /// </summary>
        public bool EsAdministrador()
        {
            return NombreRol != null && NombreRol.Equals("Administrador", StringComparison.OrdinalIgnoreCase);
        }
    }
}
