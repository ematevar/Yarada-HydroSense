using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using QRCoder; // Instalado desde NuGet

namespace ProyectoFinal_YaradaPalos
{
    public class GestionNotificaciones
    {
        // Credenciales fijas de tu cuenta de pruebas
        private const string AccountSid = "ACac19977fc48641f19085adecc9e9e98f";
        private const string AuthToken = "57de6fcfad2b8fb75e41a3038c931540";
        private const string NumRemitente = "whatsapp:+14155238886";
        private const string NumDestinatario = "whatsapp:+51990877875";
        
        // Cadena de conexion por defecto
        private const string ConnectionString = "Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;";

        /// <summary>
        /// Valida y normaliza un número de celular al formato internacional E.164 (ej: +51990877875).
        /// </summary>
        public static string NormalizarCelular(string celular)
        {
            if (string.IsNullOrWhiteSpace(celular)) return null;
            string cleaned = celular.Trim().Replace(" ", "");
            if (!cleaned.StartsWith("+"))
            {
                cleaned = "+" + cleaned;
            }
            return cleaned;
        }

        /// <summary>
        /// Envía un mensaje de texto libre a tu WhatsApp de pruebas (destinatario fijo)
        /// </summary>
        public static void EnviarAlertaWhatsApp(string mensajeTexto)
        {
            try
            {
                TwilioClient.Init(AccountSid, AuthToken);

                var messageOptions = new CreateMessageOptions(new PhoneNumber(NumDestinatario));
                messageOptions.From = new PhoneNumber(NumRemitente);
                messageOptions.Body = mensajeTexto; // Texto libre dinámico

                var message = MessageResource.Create(messageOptions);
                Console.WriteLine($"[Twilio] Mensaje enviado al remitente fijo. SID: {message.Sid}");
            }
            catch (Exception ex)
            {
                // Evita que la aplicación se cierre si falla el internet durante la exposición
                MessageBox.Show($"No se pudo enviar la alerta de WhatsApp de prueba: {ex.Message}", 
                                "Aviso del Sistema", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Envía un mensaje de texto libre a todos los operadores registrados y activos en el sistema
        /// </summary>
        public static void EnviarAlertaWhatsAppAOperadores(string mensajeTexto)
        {
            List<string> celulares = ObtenerCelularesOperadoresActivos();
            if (celulares.Count == 0)
            {
                // Si no hay operadores con celular, enviar al destinatario por defecto
                EnviarAlertaWhatsApp(mensajeTexto);
                return;
            }

            try
            {
                TwilioClient.Init(AccountSid, AuthToken);
                foreach (string cel in celulares)
                {
                    string celFormateado = NormalizarCelular(cel);
                    if (string.IsNullOrEmpty(celFormateado)) continue;

                    try
                    {
                        var messageOptions = new CreateMessageOptions(new PhoneNumber($"whatsapp:{celFormateado}"));
                        messageOptions.From = new PhoneNumber(NumRemitente);
                        messageOptions.Body = mensajeTexto;

                        var message = MessageResource.Create(messageOptions);
                        Console.WriteLine($"[Twilio] Mensaje enviado a operador ({celFormateado}). SID: {message.Sid}");
                    }
                    catch (Exception exInner)
                    {
                        Console.WriteLine($"Error al enviar a {celFormateado}: {exInner.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo enviar la alerta de WhatsApp a los operadores: {ex.Message}", 
                                "Aviso del Sistema", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Obtiene los números de celular de todos los operadores activos de la base de datos
        /// </summary>
        private static List<string> ObtenerCelularesOperadoresActivos()
        {
            List<string> lista = new List<string>();
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    string query = @"
                        SELECT u.Celular 
                        FROM dbo.Usuarios u
                        INNER JOIN dbo.Roles r ON u.IdRol = r.IdRol
                        WHERE u.Activo = 1 
                          AND r.NombreRol = 'Operador' 
                          AND u.Celular IS NOT NULL 
                          AND u.Celular <> ''";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                lista.Add(reader["Celular"].ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener celulares de operadores: " + ex.Message);
            }
            return lista;
        }

        /// <summary>
        /// Envía una alerta de WhatsApp específicamente a los operadores vinculados al sector que tiene asignado un puerto COM.
        /// </summary>
        public static void EnviarAlertaWhatsAppPorPuerto(string puertoCOM, string mensajeTexto)
        {
            List<string> celulares = ObtenerCelularesOperadoresPorPuerto(puertoCOM);
            if (celulares.Count == 0)
            {
                // Si no hay operadores asignados a ese sector, enviar al destinatario por defecto (respaldo)
                EnviarAlertaWhatsApp(mensajeTexto);
                return;
            }

            try
            {
                TwilioClient.Init(AccountSid, AuthToken);
                foreach (string cel in celulares)
                {
                    string celFormateado = NormalizarCelular(cel);
                    if (string.IsNullOrEmpty(celFormateado)) continue;

                    try
                    {
                        var messageOptions = new CreateMessageOptions(new PhoneNumber($"whatsapp:{celFormateado}"));
                        messageOptions.From = new PhoneNumber(NumRemitente);
                        messageOptions.Body = mensajeTexto;

                        var message = MessageResource.Create(messageOptions);
                        Console.WriteLine($"[Twilio] Mensaje segmentado enviado a operador ({celFormateado}). SID: {message.Sid}");
                    }
                    catch (Exception exInner)
                    {
                        Console.WriteLine($"Error al enviar a {celFormateado}: {exInner.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo enviar la alerta segmentada de WhatsApp: {ex.Message}", 
                                "Aviso del Sistema", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Busca el sector asociado al puerto COM y obtiene los celulares de todos los operadores vinculados a ese sector.
        /// </summary>
        private static List<string> ObtenerCelularesOperadoresPorPuerto(string puertoCOM)
        {
            List<string> lista = new List<string>();
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    // Query con JOIN para relacionar Usuarios (Operadores) con Sectores mediante IdSector y filtrar por PuertoSerial
                    string query = @"
                        SELECT u.Celular 
                        FROM dbo.Usuarios u
                        INNER JOIN dbo.Roles r ON u.IdRol = r.IdRol
                        INNER JOIN dbo.Sectores s ON u.IdSector = s.IdSector
                        WHERE u.Activo = 1 
                          AND r.NombreRol = 'Operador' 
                          AND s.PuertoSerial = @PuertoCOM
                          AND u.Celular IS NOT NULL 
                          AND u.Celular <> ''";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@PuertoCOM", puertoCOM);
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                lista.Add(reader["Celular"].ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener celulares por puerto COM: " + ex.Message);
            }
            return lista;
        }

        /// <summary>
        /// Genera el código QR oficial de tu Sandbox directamente en la interfaz
        /// </summary>
        public static void GenerarQrVinculacion(PictureBox pictureBoxQR)
        {
            try
            {
                // Enlace directo que abre el chat y escribe el comando automáticamente
                string urlWhatsApp = "https://wa.me/14155238886?text=join%20particles-bridge";

                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(urlWhatsApp, QRCodeGenerator.ECCLevel.Q))
                using (QRCode qrCode = new QRCode(qrCodeData))
                {
                    Bitmap qrCodeImage = qrCode.GetGraphic(10);
                    
                    // Asigna la imagen generada al PictureBox de tu interfaz
                    pictureBoxQR.Image = qrCodeImage;
                    pictureBoxQR.SizeMode = PictureBoxSizeMode.Zoom;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el código QR: {ex.Message}", 
                                "Error Gráfico", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
