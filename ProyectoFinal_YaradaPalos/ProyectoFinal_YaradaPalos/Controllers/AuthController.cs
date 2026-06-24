using System;
using System.Data;
using System.Data.SqlClient;
using ProyectoFinal_YaradaPalos.Models;

namespace ProyectoFinal_YaradaPalos.Controllers
{
    /// <summary>
    /// Controlador responsable de gestionar la autenticación y las credenciales de los usuarios en la BD.
    /// </summary>
    public class AuthController
    {
        private readonly string connectionString;

        public AuthController()
        {
            // Cadena de conexión predeterminada (modificar según la instancia de SQL Server local)
            connectionString = "Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;";
        }

        public AuthController(string customConnectionString)
        {
            connectionString = customConnectionString;
        }

        /// <summary>
        /// Valida las credenciales de acceso invocando al procedimiento almacenado sp_AutenticarUsuario.
        /// </summary>
        /// <param name="username">Nombre de usuario.</param>
        /// <param name="passwordTextoPlano">Contraseña en texto plano escrita en la UI.</param>
        /// <returns>Objeto UsuarioModel con los detalles del perfil si es exitoso; de lo contrario, null.</returns>
        public UsuarioModel Autenticar(string username, string passwordTextoPlano)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(passwordTextoPlano))
            {
                return null;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("dbo.sp_AutenticarUsuario", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        
                        // Enviamos los datos solicitados
                        cmd.Parameters.Add("@Username", SqlDbType.VarChar, 50).Value = username.Trim();
                        cmd.Parameters.Add("@PasswordTextoPlano", SqlDbType.VarChar, 100).Value = passwordTextoPlano;

                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new UsuarioModel
                                {
                                    IdUsuario = Convert.ToInt32(reader["IdUsuario"]),
                                    Username = reader["Username"].ToString(),
                                    Nombre = reader["Nombre"].ToString(),
                                    NombreRol = reader["NombreRol"].ToString()
                                };
                            }
                        }
                    }
                }
            }
            catch (SqlException sqlex)
            {
                throw new Exception("Error crítico en el servidor de Base de Datos: " + sqlex.Message, sqlex);
            }
            catch (Exception ex)
            {
                throw new Exception("Error inesperado en el sistema de autenticación: " + ex.Message, ex);
            }

            return null; // Credenciales inválidas o usuario inactivo
        }
    }
}
