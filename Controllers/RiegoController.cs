using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO.Ports;
using ProyectoFinal_YaradaPalos.Models;
using PdfSharp.Pdf;
using PdfSharp.Drawing;

namespace ProyectoFinal_YaradaPalos.Controllers
{
    /// <summary>
    /// Controlador principal encargado de gestionar la comunicación Serial con el ESP32,
    /// aplicar las reglas de negocio en tiempo real y persistir los eventos en la BD.
    /// Soporta múltiples puertos en paralelo para varios sectores de cultivo.
    /// </summary>
    public class RiegoController
    {
        private readonly string connectionString;

        // Estado y conexiones multipuerto
        private Dictionary<string, SectorState> sectoresMonitoreados = new Dictionary<string, SectorState>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, SerialPort> activeSerialPorts = new Dictionary<string, SerialPort>(StringComparer.OrdinalIgnoreCase);
        private System.Windows.Forms.Timer simTimer;
        private System.Windows.Forms.Timer connectionCheckTimer;

        // Delegados y Eventos para comunicación asíncrona hacia la Vista (UI)
        public delegate void DatosRecibidosEventHandler(double humedad, double temperatura, string estadoSuelo);
        public delegate void EstadoBombaChangedEventHandler(bool encendida);
        public delegate void AlertaRegistradaEventHandler(string mensaje);
        public delegate void ConexionEstadoChangedEventHandler(bool conectado, string mensaje);
        public delegate void DatosSectorRecibidosEventHandler(int idSector, string puerto, double humedad, double temperatura, string estadoSuelo, double salud, bool bombaEncendida, bool isOnline);

        public event DatosRecibidosEventHandler OnDatosRecibidos;
        public event EstadoBombaChangedEventHandler OnEstadoBombaChanged;
        public event AlertaRegistradaEventHandler OnAlertaRegistrada;
        public event ConexionEstadoChangedEventHandler OnConexionEstadoChanged;
        public event DatosSectorRecibidosEventHandler OnDatosSectorRecibidos;

        public class SectorState
        {
            public int IdSector { get; set; }
            public string NombreSector { get; set; }
            public string NombreEncargado { get; set; }
            public string PuertoSerial { get; set; }
            public string NombreCultivo { get; set; }
            public double HumedadMinima { get; set; }
            public double TemperaturaMaxima { get; set; }
            public double CaudalBombaLPM { get; set; }
            public double TarifaAgua { get; set; }

            // Estado de telemetría en tiempo real
            public double Humedad { get; set; } = -1;
            public double Temperatura { get; set; } = -1;
            public string EstadoSuelo { get; set; } = "Desconectado";
            public double Salud { get; set; } = 100;

            // Estado de la bomba
            public bool BombaEncendida { get; set; } = false;
            public DateTime FechaEncendidoBomba { get; set; }
            public double HumedadInicialEncendido { get; set; }

            // Estado de conexión y control manual
            public DateTime LastTelemetryTime { get; set; } = DateTime.MinValue;
            public bool IsOnline { get; set; } = false;
            public bool ModoManual { get; set; } = false;

            // Variables de Inteligencia Agrícola - "Hydrosense Analytics"
            public double CoeficienteKc { get; set; } = 0.60;
            public double PendienteEvaporacion { get; set; } = 0.0;
            public int TiempoLimiteEstimado { get; set; } = 9999;
            public double TiempoRiegoLimiteMinutos { get; set; } = 0.0;
            public double LitrosAReponerEstimados { get; set; } = 0.0;
            public DateTime LastPreventativeAlertTime { get; set; } = DateTime.MinValue;
        }

        public RiegoController()
        {
            connectionString = "Server=.;Database=RiegoPrecisionDB;Trusted_Connection=True;";
        }

        public RiegoController(string customConnectionString)
        {
            connectionString = customConnectionString;
        }

        /// <summary>
        /// Obtiene los puertos COM disponibles en la PC.
        /// </summary>
        public string[] ObtenerPuertosDisponibles()
        {
            return SerialPort.GetPortNames();
        }

        /// <summary>
        /// Obtiene el estado actual de todos los sectores monitoreados.
        /// </summary>
        public Dictionary<string, SectorState> ObtenerSectoresMonitoreados()
        {
            return sectoresMonitoreados;
        }

        /// <summary>
        /// Carga todos los sectores activos desde la base de datos.
        /// </summary>
        public List<SectorState> ObtenerSectoresActivos()
        {
            List<SectorState> lista = new List<SectorState>();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT s.IdSector, s.NombreSector, s.NombreEncargado, s.PuertoSerial, 
                                c.NombreCultivo, c.HumedadMinima, c.TemperaturaMaxima, c.CoeficienteKc,
                                s.CaudalBombaLPM, s.TarifaAguaReferencial
                        FROM dbo.Sectores s
                        INNER JOIN dbo.Cultivos c ON s.IdCultivo = c.IdCultivo
                        WHERE s.Activo = 1;";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                SectorState s = new SectorState
                                {
                                    IdSector = Convert.ToInt32(reader["IdSector"]),
                                    NombreSector = reader["NombreSector"].ToString(),
                                    NombreEncargado = reader["NombreEncargado"].ToString(),
                                    PuertoSerial = reader["PuertoSerial"].ToString(),
                                    NombreCultivo = reader["NombreCultivo"].ToString(),
                                    HumedadMinima = Convert.ToDouble(reader["HumedadMinima"]),
                                    TemperaturaMaxima = Convert.ToDouble(reader["TemperaturaMaxima"]),
                                    CoeficienteKc = Convert.ToDouble(reader["CoeficienteKc"]),
                                    CaudalBombaLPM = Convert.ToDouble(reader["CaudalBombaLPM"]),
                                    TarifaAgua = Convert.ToDouble(reader["TarifaAguaReferencial"])
                                };
                                lista.Add(s);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnAlertaRegistrada?.Invoke("Error al cargar sectores activos de la base de datos: " + ex.Message);
            }
            return lista;
        }

        /// <summary>
        /// Conecta todos los sectores activos en paralelo según sus puertos configurados.
        /// </summary>
        public bool ConectarTodosLosSectores()
        {
            DesconectarTodos();

            List<SectorState> activos = ObtenerSectoresActivos();
            sectoresMonitoreados.Clear();

            bool algunPuertoConectado = false;
            bool haySimulado = false;

            foreach (var sec in activos)
            {
                if (string.IsNullOrWhiteSpace(sec.PuertoSerial)) continue;

                sectoresMonitoreados[sec.PuertoSerial] = sec;

                // Generar datos históricos si no existen para probar regresión
                GenerarDatosHistoricosDemostracion(sec);

                if (sec.PuertoSerial.Contains("Simulado"))
                {
                    haySimulado = true;
                    algunPuertoConectado = true;
                    
                    // Inicializar valores de simulación base
                    if (sec.Humedad < 0)
                    {
                        sec.Humedad = 32.5;
                        sec.Temperatura = 28.0;
                    }
                    sec.Salud = CalcularSaludCultivo(sec.Humedad, sec.HumedadMinima, sec.Temperatura, sec.TemperaturaMaxima);
                    sec.EstadoSuelo = EvaluarEstadoSuelo(sec.Humedad, sec.Temperatura, sec.HumedadMinima);
                    
                    OnAlertaRegistrada?.Invoke($"Sector '{sec.NombreSector}' cargado en puerto simulado {sec.PuertoSerial}.");
                }
                else
                {
                    try
                    {
                        SerialPort sp = new SerialPort(sec.PuertoSerial, 115200, Parity.None, 8, StopBits.One);
                        sp.DtrEnable = true;
                        sp.RtsEnable = true;
                        sp.DataReceived += SerialPort_DataReceived;
                        sp.Open();

                        activeSerialPorts[sec.PuertoSerial] = sp;
                        algunPuertoConectado = true;
                        OnAlertaRegistrada?.Invoke($"Conectado al puerto físico {sec.PuertoSerial} para '{sec.NombreSector}'.");
                    }
                    catch (Exception ex)
                    {
                        OnAlertaRegistrada?.Invoke($"Error al conectar al puerto {sec.PuertoSerial} ({sec.NombreSector}): {ex.Message}");
                    }
                }
            }

            if (haySimulado)
            {
                IniciarSimulador();
            }

            if (connectionCheckTimer == null)
            {
                connectionCheckTimer = new System.Windows.Forms.Timer();
                connectionCheckTimer.Interval = 1000; // Check every 1 second
                connectionCheckTimer.Tick += ConnectionCheckTimer_Tick;
            }
            connectionCheckTimer.Start();

            OnConexionEstadoChanged?.Invoke(algunPuertoConectado, algunPuertoConectado ? "Monitoreo multipuerto iniciado con éxito." : "No se pudo conectar a ningún puerto.");
            return algunPuertoConectado;
        }

        /// <summary>
        /// Desconecta todos los puertos seriales activos y detiene la simulación.
        /// </summary>
        public void DesconectarTodos()
        {
            DetenerSimulador();

            if (connectionCheckTimer != null)
            {
                connectionCheckTimer.Stop();
            }

            foreach (var kvp in activeSerialPorts)
            {
                try
                {
                    SerialPort sp = kvp.Value;
                    string puerto = kvp.Key;

                    if (sectoresMonitoreados.TryGetValue(puerto, out SectorState sec) && sec.BombaEncendida)
                    {
                        try { sp.Write("A"); } catch { }
                        ActualizarCierreRiego(sec);
                    }

                    if (sp.IsOpen)
                    {
                        sp.DataReceived -= SerialPort_DataReceived;
                        sp.Close();
                    }
                    sp.Dispose();
                }
                catch (Exception ex)
                {
                    OnAlertaRegistrada?.Invoke($"Error al cerrar puerto {kvp.Key}: {ex.Message}");
                }
            }

            activeSerialPorts.Clear();
            sectoresMonitoreados.Clear();
            OnConexionEstadoChanged?.Invoke(false, "Desconectado");
        }

        // Métodos legados para mantener compatibilidad
        public bool Conectar(string puertoCOM, int baudios = 115200)
        {
            return ConectarTodosLosSectores();
        }

        public void Desconectar()
        {
            DesconectarTodos();
        }

        /// <summary>
        /// Calibra el valor analógico bruto obtenido del sensor capacitivo v1.2 a un porcentaje de humedad.
        /// </summary>
        private double CalibrarHumedad(int valorBruto)
        {
            const double seco = 3100.0;
            const double humedo = 1400.0;

            if (valorBruto >= seco) return 0.0;
            if (valorBruto <= humedo) return 100.0;

            double porcentaje = ((seco - valorBruto) / (seco - humedo)) * 100.0;
            return Math.Round(porcentaje, 2);
        }

        private static bool SafeTryParseDouble(string input, out double result)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                result = 0;
                return false;
            }
            input = input.Trim();
            // Try with InvariantCulture (dot decimal separator)
            if (double.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result))
            {
                return true;
            }
            // Try with current culture (comma decimal separator)
            if (double.TryParse(input, out result))
            {
                return true;
            }
            // Fallback: replace dot with comma or vice versa
            string modified = input.Contains(".") ? input.Replace(".", ",") : input.Replace(",", ".");
            if (double.TryParse(modified, out result))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Evento asíncrono disparado cuando llegan datos del ESP32 por USB.
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string puerto = sp.PortName;

            if (!sectoresMonitoreados.TryGetValue(puerto, out SectorState sec)) return;

            try
            {
                string linea = sp.ReadLine();
                if (string.IsNullOrWhiteSpace(linea)) return;
                linea = linea.Trim();

                OnAlertaRegistrada?.Invoke($"[{puerto}] Datos brutos recibidos: '{linea}'");

                double humedad = -1;
                double temperatura = 20.0;

                // Intentar parsear el formato MAC|Humedad|Temperatura
                string[] partes = linea.Split('|');
                if (partes.Length == 3)
                {
                    if (SafeTryParseDouble(partes[1], out double humVal) && SafeTryParseDouble(partes[2], out double tempVal))
                    {
                        // Si el valor es mayor a 100, asumimos que es el valor analógico sin calibrar
                        humedad = (humVal > 100.0) ? CalibrarHumedad((int)humVal) : humVal;
                        temperatura = tempVal;
                    }
                }
                else if (linea.Contains(","))
                {
                    // Formato alternativo: "2523 , 18.31 C" o "2523, 18.31" o "2523, 18.31, 0.00"
                    string[] partesComa = linea.Split(',');
                    if (partesComa.Length >= 2)
                    {
                        string humStr = partesComa[0].Trim();
                        string tempStr = partesComa[1].Replace("C", "").Replace("c", "").Trim();

                        if (SafeTryParseDouble(humStr, out double humVal))
                        {
                            humedad = (humVal > 100.0) ? CalibrarHumedad((int)humVal) : humVal;
                        }
                        if (SafeTryParseDouble(tempStr, out double tempVal))
                        {
                            temperatura = tempVal;
                        }
                    }
                }
                else
                {
                    // Fallback a valor analógico bruto
                    if (int.TryParse(linea, out int valorBruto))
                    {
                        humedad = CalibrarHumedad(valorBruto);
                        temperatura = 22.0; // Valor simulado
                    }
                }

                if (humedad >= 0)
                {
                    sec.LastTelemetryTime = DateTime.Now;
                    sec.IsOnline = true;
                    ProcesarLecturaSector(sec, humedad, temperatura);
                }
            }
            catch (Exception ex)
            {
                OnAlertaRegistrada?.Invoke($"Error procesando telemetría serial en puerto {puerto}: " + ex.Message);
            }
        }

        /// <summary>
        /// Procesa la telemetría leída, calcula la salud, evalúa reglas y persiste en DB.
        /// </summary>
        private void ProcesarLecturaSector(SectorState sec, double humedad, double temperatura)
        {
            sec.Humedad = Math.Round(humedad, 1);
            sec.Temperatura = Math.Round(temperatura, 1);
            sec.Salud = CalcularSaludCultivo(sec.Humedad, sec.HumedadMinima, sec.Temperatura, sec.TemperaturaMaxima);
            sec.EstadoSuelo = EvaluarEstadoSuelo(sec.Humedad, sec.Temperatura, sec.HumedadMinima);

            // Calcular tendencia y estimar tiempo de evaporación
            CalcularTendenciaHumedad(sec);

            // Guardar lectura en BD por puerto serial
            RegistrarLecturaCompletaDB(sec.PuertoSerial, sec.Humedad, sec.Temperatura, sec.EstadoSuelo, sec.PendienteEvaporacion, sec.TiempoLimiteEstimado);

            // Aplicar reglas de negocio para activación/desactivación de la bomba
            EvaluarReglasDeNegocioSector(sec);

            // Disparar eventos
            OnDatosSectorRecibidos?.Invoke(sec.IdSector, sec.PuertoSerial, sec.Humedad, sec.Temperatura, sec.EstadoSuelo, sec.Salud, sec.BombaEncendida, sec.IsOnline);
            
            // Compatibilidad
            OnDatosRecibidos?.Invoke(sec.Humedad, sec.Temperatura, sec.EstadoSuelo);
        }

        /// <summary>
        /// Calcula el porcentaje de salud del cultivo (0% - 100%) basado en parámetros agronómicos.
        /// </summary>
        public double CalcularSaludCultivo(double humedadActual, double humedadMinima, double temperaturaActual, double temperaturaMaxima)
        {
            double salud = 100.0;

            // 1. Evaluación por Humedad
            if (humedadActual < humedadMinima)
            {
                double deficit = humedadMinima - humedadActual;
                if (deficit <= 5.0)
                {
                    salud = 75.0 - (deficit * 5.0); // 75% a 50%
                }
                else
                {
                    salud = Math.Max(0.0, 50.0 - (deficit * 2.0)); // < 50%
                }
            }
            else if (humedadActual >= humedadMinima && humedadActual <= humedadMinima + 20.0)
            {
                salud = 100.0; // Rango óptimo
            }
            else // Exceso de humedad
            {
                double exceso = humedadActual - (humedadMinima + 20.0);
                if (exceso <= 15.0)
                {
                    salud = 80.0 - (exceso * 2.0); // 80% a 50%
                }
                else
                {
                    salud = Math.Max(0.0, 50.0 - (exceso * 1.5)); // < 50%
                }
            }

            // 2. Evaluación por Temperatura (Estrés Térmico)
            if (temperaturaActual > temperaturaMaxima)
            {
                double excesoTermico = temperaturaActual - temperaturaMaxima;
                salud = Math.Max(0.0, salud - (excesoTermico * 3.0)); // Restar 3% por cada grado de exceso
            }

            return Math.Round(salud, 2);
        }

        /// <summary>
        /// Evalúa el estado del suelo agronómico para el dashboard.
        /// </summary>
        public string EvaluarEstadoSuelo(double humedad, double temperatura, double humedadMinima)
        {
            if (humedad < humedadMinima)
            {
                return "Crítico - Seco (Estrés Hídrico)";
            }
            else if (humedad >= humedadMinima && humedad <= humedadMinima + 20.0)
            {
                if (temperatura > 35.0)
                {
                    return "Evaporación Crítica (Temperatura Excesiva)";
                }
                return "Óptimo (Conservación)";
            }
            else
            {
                return "Crítico - Saturado (Exceso)";
            }
        }

        /// <summary>
        /// Evalúa las condiciones para automatizar el encendido, apagado o detección de fallas en la bomba del sector.
        /// </summary>
        private void EvaluarReglasDeNegocioSector(SectorState sec)
        {
            if (sec.ModoManual || !sec.IsOnline) return;

            // REGLA 1: Encendido Automático por Humedad Crítica (< HumedadMinima)
            if (sec.Humedad < sec.HumedadMinima && !sec.BombaEncendida)
            {
                sec.BombaEncendida = true;
                sec.FechaEncendidoBomba = DateTime.Now;
                sec.HumedadInicialEncendido = sec.Humedad;

                // Calcular balance hídrico y tiempo óptimo de riego
                CalcularTiempoRiegoReposicion(sec);

                EnviarComandoSerial(sec.PuertoSerial, "E");
                RegistrarInicioRiegoSectorDB(sec);

                OnEstadoBombaChanged?.Invoke(true);
                OnAlertaRegistrada?.Invoke($"[{sec.NombreSector}] Riego iniciado automáticamente por estrés hídrico (< {sec.HumedadMinima}%).");
            }
            // SI LA BOMBA YA ESTÁ TRABAJANDO:
            else if (sec.BombaEncendida)
            {
                double minutosTranscurridos = (DateTime.Now - sec.FechaEncendidoBomba).TotalMinutes;
                
                // Si es un puerto simulado, acortamos el tiempo de detección de fugas para facilitar la demo
                double limiteTiempoMinutos = sec.PuertoSerial.Contains("Simulado") ? 0.3 : 5.0;

                // REGLA 2: Algoritmo de Detección de Fugas (Regla del tiempo límite)
                if (minutosTranscurridos >= limiteTiempoMinutos)
                {
                    // Si el suelo no aumentó humedad o incluso bajó a pesar de tener la bomba prendida
                    if (sec.Humedad <= sec.HumedadInicialEncendido)
                    {
                        sec.BombaEncendida = false;
                        
                        EnviarComandoSerial(sec.PuertoSerial, "A");
                        RegistrarFugaSectorDB(sec, $"Falla: Bomba encendida en {sec.NombreSector} por más de {limiteTiempoMinutos} minutos y la humedad no subió. Posible fuga o rotura.");
                        ActualizarCierreRiego(sec);

                        OnEstadoBombaChanged?.Invoke(false);
                        OnAlertaRegistrada?.Invoke($"¡ALERTA DE SEGURIDAD [{sec.NombreSector}]: Bomba apagada por sospecha de fuga de agua.");
                        return;
                    }
                }

                // REGLA 3: Apagado Automático por Reposición Inteligente (Evapotranspiración)
                double limiteRiego = sec.TiempoRiegoLimiteMinutos > 0 ? sec.TiempoRiegoLimiteMinutos : 15.0;
                if (sec.PuertoSerial.Contains("Simulado"))
                {
                    limiteRiego = 0.5; // 30 segundos para demostración rápida en vivo
                }

                if (minutosTranscurridos >= limiteRiego)
                {
                    sec.BombaEncendida = false;
                    
                    EnviarComandoSerial(sec.PuertoSerial, "A");
                    ActualizarCierreRiego(sec);

                    OnEstadoBombaChanged?.Invoke(false);
                    OnAlertaRegistrada?.Invoke($"[{sec.NombreSector}] Riego finalizado: Reposición hídrica inteligente completada.");
                }
            }
        }

        private void EnviarComandoSerial(string puerto, string comando)
        {
            try
            {
                if (activeSerialPorts.TryGetValue(puerto, out SerialPort sp) && sp.IsOpen)
                {
                    sp.WriteLine(comando);
                }
            }
            catch (Exception ex)
            {
                OnAlertaRegistrada?.Invoke($"Error enviando comando '{comando}' a {puerto}: " + ex.Message);
            }
        }

        private void ActualizarCierreRiego(SectorState sec)
        {
            DateTime fechaFin = DateTime.Now;
            double minutos = (fechaFin - sec.FechaEncendidoBomba).TotalMinutes;
            if (minutos < 0) minutos = 0;

            // Volumen = Minutos * Caudal por Minuto
            double litros = minutos * sec.CaudalBombaLPM;

            RegistrarFinRiegoSectorDB(sec, fechaFin, litros);
        }

        // =================================================================================
        // SIMULACIÓN MULTIPUERTO
        // =================================================================================

        private void IniciarSimulador()
        {
            if (simTimer == null)
            {
                simTimer = new System.Windows.Forms.Timer();
                simTimer.Interval = 2000; // Tick cada 2 segundos
                simTimer.Tick += SimTimer_Tick;
            }
            simTimer.Start();
        }

        private void DetenerSimulador()
        {
            if (simTimer != null)
            {
                simTimer.Stop();
            }
        }

        private void SimTimer_Tick(object sender, EventArgs e)
        {
            Random r = new Random();
            foreach (var kvp in sectoresMonitoreados)
            {
                var sec = kvp.Value;
                if (!sec.PuertoSerial.Contains("Simulado")) continue;

                if (sec.BombaEncendida)
                {
                    // Humedad sube, temperatura baja ligeramente
                    sec.Humedad += r.Next(10, 25) / 10.0;
                    if (sec.Humedad > 100.0) sec.Humedad = 100.0;

                    sec.Temperatura -= r.Next(1, 3) / 10.0;
                    if (sec.Temperatura < 15.0) sec.Temperatura = 15.0;
                }
                else
                {
                    // Humedad cae, temperatura sube
                    sec.Humedad -= r.Next(5, 15) / 10.0;
                    if (sec.Humedad < 5.0) sec.Humedad = 5.0;

                    sec.Temperatura += r.Next(1, 3) / 10.0;
                    if (sec.Temperatura > 45.0) sec.Temperatura = 45.0;
                }

                ProcesarLecturaSector(sec, sec.Humedad, sec.Temperatura);
            }
        }

        // =================================================================================
        // ACCESO A DATOS ADO.NET (Conexión con Stored Procedures)
        // =================================================================================

        private void RegistrarLecturaCompletaDB(string puerto, double humedad, double temperatura, string estadoSuelo, double? pendiente, int? tiempoEstimado)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("dbo.sp_RegistrarLecturaPorPuerto", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.Add("@PuertoSerial", SqlDbType.VarChar, 20).Value = puerto;
                        cmd.Parameters.Add("@Humedad", SqlDbType.Decimal).Value = (decimal)humedad;
                        cmd.Parameters.Add("@Temperatura", SqlDbType.Decimal).Value = (decimal)temperatura;
                        cmd.Parameters.Add("@EstadoSuelo", SqlDbType.VarChar, 50).Value = estadoSuelo;
                        cmd.Parameters.Add("@PendienteEvaporacion", SqlDbType.Decimal).Value = (object)pendiente ?? DBNull.Value;
                        cmd.Parameters.Add("@TiempoLimiteEstimado", SqlDbType.Int).Value = (object)tiempoEstimado ?? DBNull.Value;

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                OnAlertaRegistrada?.Invoke($"Error al persistir telemetría en BD para puerto {puerto}: {ex.Message}");
            }
        }

        private void RegistrarInicioRiegoSectorDB(SectorState sec)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        INSERT INTO dbo.HistorialRiego (IdSector, FechaHoraInicio, FechaHoraFin, LitrosConsumidos)
                        VALUES (@IdSector, @FechaHoraInicio, NULL, 0.00);";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@IdSector", SqlDbType.Int).Value = sec.IdSector;
                        cmd.Parameters.Add("@FechaHoraInicio", SqlDbType.DateTime).Value = sec.FechaEncendidoBomba;

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                OnAlertaRegistrada?.Invoke($"[{sec.NombreSector}] Error al registrar inicio de riego: {ex.Message}");
            }
        }

        private void RegistrarFinRiegoSectorDB(SectorState sec, DateTime fechaFin, double litrosConsumidos)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        UPDATE dbo.HistorialRiego
                        SET FechaHoraFin = @FechaHoraFin,
                            LitrosConsumidos = @LitrosConsumidos
                        WHERE IdSector = @IdSector AND FechaHoraFin IS NULL;";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@IdSector", SqlDbType.Int).Value = sec.IdSector;
                        cmd.Parameters.Add("@FechaHoraFin", SqlDbType.DateTime).Value = fechaFin;
                        cmd.Parameters.Add("@LitrosConsumidos", SqlDbType.Decimal).Value = (decimal)litrosConsumidos;

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                OnAlertaRegistrada?.Invoke($"[{sec.NombreSector}] Error al registrar fin de riego: {ex.Message}");
            }
        }

        private void RegistrarFugaSectorDB(SectorState sec, string descripcion)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    // Buscar el IdDispositivo para este sector
                    string getDevQuery = "SELECT TOP 1 IdDispositivo FROM dbo.Dispositivos WHERE IdSector = @IdSector;";
                    int idDispositivo = 1;
                    
                    using (SqlCommand cmdGet = new SqlCommand(getDevQuery, conn))
                    {
                        cmdGet.Parameters.Add("@IdSector", SqlDbType.Int).Value = sec.IdSector;
                        conn.Open();
                        object res = cmdGet.ExecuteScalar();
                        if (res != null && res != DBNull.Value)
                        {
                            idDispositivo = Convert.ToInt32(res);
                        }
                    }

                    string query = @"
                        INSERT INTO dbo.AlertasAnomalias (IdDispositivo, TipoAnomalia, Descripcion, FechaHora, Solucionado)
                        VALUES (@IdDispositivo, 'Posible Fuga', @Descripcion, GETDATE(), 0);";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.Add("@IdDispositivo", SqlDbType.Int).Value = idDispositivo;
                        cmd.Parameters.Add("@Descripcion", SqlDbType.VarChar, 500).Value = descripcion;

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                OnAlertaRegistrada?.Invoke($"[{sec.NombreSector}] Error al guardar alerta de fuga: {ex.Message}");
            }
        }

        /// <summary>
        /// Genera un documento PDF formal, membretado y estructurado con los sectores activos de cultivo.
        /// </summary>
        public void GenerarReporteSectoresPDF(string outputFilePath)
        {
            PdfDocument document = new PdfDocument();
            document.Info.Title = "Reporte de Sectores Activos - HydroSense";
            PdfPage page = document.AddPage();
            XGraphics gfx = XGraphics.FromPdfPage(page);

            // 1. Diseño del Membrete
            XFont titleFont = new XFont("Arial", 22, XFontStyle.Bold);
            gfx.DrawString("HYDROSENSE", titleFont, XBrushes.DarkGreen, new XPoint(40, 50));

            XFont subtitleFont = new XFont("Arial", 10, XFontStyle.Italic);
            gfx.DrawString("Sistema Comercial de Riego de Precisión IoT", subtitleFont, XBrushes.Gray, new XPoint(40, 68));

            // Ubicación regional y Fecha
            XFont infoFont = new XFont("Arial", 9, XFontStyle.Regular);
            gfx.DrawString("Ubicación: La Yarada los Palos, Tacna", infoFont, XBrushes.DarkSlateGray, new XPoint(360, 48));
            gfx.DrawString("Fecha de generación: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), infoFont, XBrushes.DarkSlateGray, new XPoint(360, 63));

            // Línea estética divisoria
            XPen linePen = new XPen(XColor.FromArgb(30, 86, 49), 2); // Verde bosque
            gfx.DrawLine(linePen, 40, 80, 570, 80);

            // Título del Reporte
            XFont reportTitleFont = new XFont("Arial", 14, XFontStyle.Bold);
            gfx.DrawString("REPORTE DE SECTORES ACTIVOS DE CULTIVO", reportTitleFont, XBrushes.Black, new XPoint(40, 110));

            // 2. Tabla estructurada de Sectores
            XFont tableHeaderFont = new XFont("Arial", 10, XFontStyle.Bold);
            XFont tableBodyFont = new XFont("Arial", 9, XFontStyle.Regular);

            int startY = 140;
            int rowHeight = 22;

            // Dibujar encabezado de la tabla con fondo verde claro
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(230, 245, 235)), 40, startY, 530, rowHeight);
            gfx.DrawRectangle(linePen, 40, startY, 530, rowHeight);

            gfx.DrawString("Sector", tableHeaderFont, XBrushes.Black, new XPoint(45, startY + 15));
            gfx.DrawString("Responsable/Dueño", tableHeaderFont, XBrushes.Black, new XPoint(200, startY + 15));
            gfx.DrawString("Tipo de Planta", tableHeaderFont, XBrushes.Black, new XPoint(360, startY + 15));
            gfx.DrawString("Puerto COM", tableHeaderFont, XBrushes.Black, new XPoint(485, startY + 15));

            // Obtener sectores activos de la DB
            List<SectorState> sectores = ObtenerSectoresActivos();

            int currentY = startY + rowHeight;
            XPen gridPen = new XPen(XColor.FromArgb(220, 220, 220), 1);

            foreach (var sec in sectores)
            {
                gfx.DrawRectangle(gridPen, 40, currentY, 530, rowHeight);

                gfx.DrawString(sec.NombreSector, tableBodyFont, XBrushes.Black, new XPoint(45, currentY + 14));
                gfx.DrawString(sec.NombreEncargado, tableBodyFont, XBrushes.Black, new XPoint(200, currentY + 14));
                gfx.DrawString(sec.NombreCultivo, tableBodyFont, XBrushes.Black, new XPoint(360, currentY + 14));
                gfx.DrawString(sec.PuertoSerial, tableBodyFont, XBrushes.Black, new XPoint(485, currentY + 14));

                currentY += rowHeight;
            }

            // 3. Cierre del Documento (Firma membretada)
            int signatureY = currentY + 80;
            if (signatureY > 700)
            {
                page = document.AddPage();
                gfx = XGraphics.FromPdfPage(page);
                signatureY = 100;
            }

            XPen signaturePen = new XPen(XColor.FromArgb(150, 150, 150), 1);
            gfx.DrawLine(signaturePen, 200, signatureY, 400, signatureY);

            XFont signatureFont = new XFont("Arial", 10, XFontStyle.Bold);
            XFont signatureSubFont = new XFont("Arial", 9, XFontStyle.Regular);
            
            gfx.DrawString("ADMINISTRADOR - HYDROSENSE", signatureFont, XBrushes.Black, new XRect(0, signatureY + 5, page.Width, 20), XStringFormats.TopCenter);
            gfx.DrawString("Sistema de Riego de Precisión", signatureSubFont, XBrushes.Gray, new XRect(0, signatureY + 20, page.Width, 20), XStringFormats.TopCenter);

            document.Save(outputFilePath);
        }

        private void ConnectionCheckTimer_Tick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            List<SectorState> list;
            lock (sectoresMonitoreados)
            {
                list = new List<SectorState>(sectoresMonitoreados.Values);
            }

            foreach (var sec in list)
            {
                if (sec.IsOnline && (now - sec.LastTelemetryTime).TotalSeconds >= 15.0)
                {
                    sec.IsOnline = false;
                    sec.EstadoSuelo = "Sector Fuera de Línea";
                    sec.BombaEncendida = false;

                    // Notificar UI de pérdida de conexión
                    OnDatosSectorRecibidos?.Invoke(sec.IdSector, sec.PuertoSerial, sec.Humedad, sec.Temperatura, sec.EstadoSuelo, sec.Salud, sec.BombaEncendida, sec.IsOnline);
                    OnAlertaRegistrada?.Invoke($"¡CONEXIÓN PERDIDA! El sector '{sec.NombreSector}' en {sec.PuertoSerial} no responde (timeout 15s).");
                }
            }
        }

        public void ConmutarRiegoManual(SectorState sec)
        {
            if (!sec.IsOnline)
            {
                OnAlertaRegistrada?.Invoke($"[{sec.NombreSector}] No se puede activar el riego manual: Sector Fuera de Línea.");
                return;
            }

            if (!sec.ModoManual)
            {
                // Activar Riego Manual
                sec.ModoManual = true;
                sec.BombaEncendida = true;
                sec.FechaEncendidoBomba = DateTime.Now;
                sec.HumedadInicialEncendido = sec.Humedad;

                // Calcular balance hídrico y tiempo óptimo de riego
                CalcularTiempoRiegoReposicion(sec);

                EnviarComandoSerial(sec.PuertoSerial, "E");
                RegistrarInicioRiegoSectorDB(sec);

                OnAlertaRegistrada?.Invoke($"[{sec.NombreSector}] Riego manual ACTIVADO (Bomba Encendida - Modo Automático Suspendido).");
            }
            else
            {
                // Detener Riego Manual y volver a Auto
                sec.ModoManual = false;
                sec.BombaEncendida = false;

                EnviarComandoSerial(sec.PuertoSerial, "A");
                ActualizarCierreRiego(sec);

                OnAlertaRegistrada?.Invoke($"[{sec.NombreSector}] Riego manual DETENIDO (Bomba Apagada - Modo Automático Reanudado).");
            }

            // Notificar a la vista
            OnDatosSectorRecibidos?.Invoke(sec.IdSector, sec.PuertoSerial, sec.Humedad, sec.Temperatura, sec.EstadoSuelo, sec.Salud, sec.BombaEncendida, sec.IsOnline);
        }

        public Dictionary<string, double> ObtenerMetricasTemperatura(int idSector, DateTime inicio, DateTime fin)
        {
            Dictionary<string, double> metricas = new Dictionary<string, double>();
            metricas["Max"] = 0;
            metricas["Min"] = 0;
            metricas["Avg"] = 0;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query;
                    if (idSector > 0)
                    {
                        query = @"
                            SELECT 
                                ISNULL(MAX(hl.Temperatura), 0) AS MaxTemp,
                                ISNULL(MIN(hl.Temperatura), 0) AS MinTemp,
                                ISNULL(AVG(hl.Temperatura), 0) AS AvgTemp
                            FROM dbo.HistorialLecturas hl
                            INNER JOIN dbo.Dispositivos d ON hl.IdDispositivo = d.IdDispositivo
                            WHERE d.IdSector = @IdSector
                              AND hl.FechaHora BETWEEN @FechaInicio AND @FechaFin;";
                    }
                    else
                    {
                        query = @"
                            SELECT 
                                ISNULL(MAX(hl.Temperatura), 0) AS MaxTemp,
                                ISNULL(MIN(hl.Temperatura), 0) AS MinTemp,
                                ISNULL(AVG(hl.Temperatura), 0) AS AvgTemp
                            FROM dbo.HistorialLecturas hl
                            INNER JOIN dbo.Dispositivos d ON hl.IdDispositivo = d.IdDispositivo
                            WHERE hl.FechaHora BETWEEN @FechaInicio AND @FechaFin;";
                    }

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (idSector > 0)
                        {
                            cmd.Parameters.AddWithValue("@IdSector", idSector);
                        }
                        cmd.Parameters.AddWithValue("@FechaInicio", inicio);
                        cmd.Parameters.AddWithValue("@FechaFin", fin);

                        conn.Open();
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                metricas["Max"] = Convert.ToDouble(rdr["MaxTemp"]);
                                metricas["Min"] = Convert.ToDouble(rdr["MinTemp"]);
                                metricas["Avg"] = Convert.ToDouble(rdr["AvgTemp"]);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnAlertaRegistrada?.Invoke("Error al obtener métricas de temperatura de la BD: " + ex.Message);
            }
            return metricas;
        }

        public DataTable ObtenerConsumoAguaHistorico(int idSector, DateTime inicio, DateTime fin)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Fecha", typeof(DateTime));
            dt.Columns.Add("Litros", typeof(double));

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query;
                    if (idSector > 0)
                    {
                        query = @"
                            SELECT 
                                CAST(FechaHoraInicio AS DATE) AS Fecha,
                                SUM(LitrosConsumidos) AS TotalLitros
                            FROM dbo.HistorialRiego
                            WHERE IdSector = @IdSector
                              AND FechaHoraInicio BETWEEN @FechaInicio AND @FechaFin
                            GROUP BY CAST(FechaHoraInicio AS DATE)
                            ORDER BY Fecha;";
                    }
                    else
                    {
                        query = @"
                            SELECT 
                                CAST(FechaHoraInicio AS DATE) AS Fecha,
                                SUM(LitrosConsumidos) AS TotalLitros
                            FROM dbo.HistorialRiego
                            WHERE FechaHoraInicio BETWEEN @FechaInicio AND @FechaFin
                            GROUP BY CAST(FechaHoraInicio AS DATE)
                            ORDER BY Fecha;";
                    }

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (idSector > 0)
                        {
                            cmd.Parameters.AddWithValue("@IdSector", idSector);
                        }
                        cmd.Parameters.AddWithValue("@FechaInicio", inicio);
                        cmd.Parameters.AddWithValue("@FechaFin", fin);

                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        da.Fill(dt);
                    }
                }
            }
            catch (Exception ex)
            {
                OnAlertaRegistrada?.Invoke("Error al obtener consumo de agua de la BD: " + ex.Message);
            }
            return dt;
        }

        /// <summary>
        /// Motor Estadístico: Realiza una regresión lineal simple (y = mx + b) sobre los datos de las últimas 4 horas
        /// de humedad del suelo para estimar la tendencia y calcular el tiempo estimado para llegar al mínimo crítico.
        /// </summary>
        private void CalcularTendenciaHumedad(SectorState sec)
        {
            try
            {
                List<Tuple<DateTime, double>> readings = new List<Tuple<DateTime, double>>();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT hl.FechaHora, hl.Humedad
                        FROM dbo.HistorialLecturas hl
                        INNER JOIN dbo.Dispositivos d ON hl.IdDispositivo = d.IdDispositivo
                        WHERE d.IdSector = @IdSector
                          AND hl.FechaHora >= DATEADD(HOUR, -4, GETDATE())
                        ORDER BY hl.FechaHora ASC;";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@IdSector", sec.IdSector);
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DateTime dt = Convert.ToDateTime(reader["FechaHora"]);
                                double hum = Convert.ToDouble(reader["Humedad"]);
                                readings.Add(new Tuple<DateTime, double>(dt, hum));
                            }
                        }
                    }
                }

                if (readings.Count < 2)
                {
                    sec.PendienteEvaporacion = 0.0;
                    sec.TiempoLimiteEstimado = 9999;
                    return;
                }

                // Calcular regresión lineal simple y = mx + b
                // x_i representa las horas transcurridas desde la primera lectura
                DateTime t0 = readings[0].Item1;
                int n = readings.Count;
                double sumX = 0.0;
                double sumY = 0.0;

                double[] xVals = new double[n];
                double[] yVals = new double[n];

                for (int i = 0; i < n; i++)
                {
                    xVals[i] = (readings[i].Item1 - t0).TotalHours;
                    yVals[i] = readings[i].Item2;
                    sumX += xVals[i];
                    sumY += yVals[i];
                }

                double meanX = sumX / n;
                double meanY = sumY / n;

                double sumNumerator = 0.0;
                double sumDenominator = 0.0;

                for (int i = 0; i < n; i++)
                {
                    sumNumerator += (xVals[i] - meanX) * (yVals[i] - meanY);
                    sumDenominator += (xVals[i] - meanX) * (xVals[i] - meanX);
                }

                double m = sumDenominator != 0.0 ? sumNumerator / sumDenominator : 0.0;

                sec.PendienteEvaporacion = Math.Round(m, 4);

                if (m < 0.0)
                {
                    double currentHum = sec.Humedad;
                    if (currentHum <= sec.HumedadMinima)
                    {
                        sec.TiempoLimiteEstimado = 0;
                    }
                    else
                    {
                        // horas restantes = (HumedadMinima - HumedadActual) / m
                        double hoursLeft = (sec.HumedadMinima - currentHum) / m;
                        sec.TiempoLimiteEstimado = (int)Math.Round(hoursLeft * 60.0);
                        if (sec.TiempoLimiteEstimado < 0) sec.TiempoLimiteEstimado = 0;
                    }

                    // Disparar Alerta Preventiva si el límite es menor a 60 minutos
                    if (sec.TiempoLimiteEstimado < 60)
                    {
                        if ((DateTime.Now - sec.LastPreventativeAlertTime).TotalMinutes >= 5.0)
                        {
                            sec.LastPreventativeAlertTime = DateTime.Now;
                            OnAlertaRegistrada?.Invoke($"[ALERTA PREVENTIVA] Sector '{sec.NombreSector}': Riego Inminente por Tendencia. Se estima que alcanzará el límite crítico ({sec.HumedadMinima}%) en {sec.TiempoLimiteEstimado} minutos debido a una evaporación de {Math.Abs(sec.PendienteEvaporacion):F2}%/hora.");
                        }
                    }
                }
                else
                {
                    sec.TiempoLimiteEstimado = 9999;
                }
            }
            catch (Exception ex)
            {
                OnAlertaRegistrada?.Invoke($"[{sec.NombreSector}] Error al calcular tendencia de humedad: " + ex.Message);
                sec.PendienteEvaporacion = 0.0;
                sec.TiempoLimiteEstimado = 9999;
            }
        }

        /// <summary>
        /// Si hay menos de 10 lecturas en las últimas 4 horas, siembra 24 lecturas de declive controlado
        /// de humedad (de 38% a 33%) para que la predicción y regresión funcionen de forma inmediata en la demo.
        /// </summary>
        private void GenerarDatosHistoricosDemostracion(SectorState sec)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // 1. Contar lecturas recientes
                    string countQuery = @"
                        SELECT COUNT(*)
                        FROM dbo.HistorialLecturas hl
                        INNER JOIN dbo.Dispositivos d ON hl.IdDispositivo = d.IdDispositivo
                        WHERE d.IdSector = @IdSector
                          AND hl.FechaHora >= DATEADD(HOUR, -4, GETDATE());";

                    int count = 0;
                    using (SqlCommand cmdCount = new SqlCommand(countQuery, conn))
                    {
                        cmdCount.Parameters.AddWithValue("@IdSector", sec.IdSector);
                        count = Convert.ToInt32(cmdCount.ExecuteScalar());
                    }

                    if (count >= 10)
                    {
                        return; // Ya hay suficientes datos reales o históricos
                    }

                    OnAlertaRegistrada?.Invoke($"[{sec.NombreSector}] Generando 24 puntos de datos históricos para demostración inmediata de regresión (38% a 33%)...");

                    // 2. Obtener o crear IdDispositivo
                    int idDispositivo = 0;
                    string getDevQuery = @"
                        SELECT TOP 1 IdDispositivo FROM dbo.Dispositivos WHERE IdSector = @IdSector AND Estado = 'Activo';";
                    using (SqlCommand cmdDev = new SqlCommand(getDevQuery, conn))
                    {
                        cmdDev.Parameters.AddWithValue("@IdSector", sec.IdSector);
                        object res = cmdDev.ExecuteScalar();
                        if (res != null && res != DBNull.Value)
                        {
                            idDispositivo = Convert.ToInt32(res);
                        }
                    }

                    if (idDispositivo == 0)
                    {
                        string getDevQuery2 = @"
                            SELECT TOP 1 IdDispositivo FROM dbo.Dispositivos WHERE IdSector = @IdSector;";
                        using (SqlCommand cmdDev2 = new SqlCommand(getDevQuery2, conn))
                        {
                            cmdDev2.Parameters.AddWithValue("@IdSector", sec.IdSector);
                            object res = cmdDev2.ExecuteScalar();
                            if (res != null && res != DBNull.Value)
                            {
                                idDispositivo = Convert.ToInt32(res);
                            }
                        }
                    }

                    if (idDispositivo == 0)
                    {
                        string insertDev = @"
                            INSERT INTO dbo.Dispositivos (CodigoMAC, IdSector, Estado)
                            VALUES (@CodigoMAC, @IdSector, 'Activo');
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";
                        using (SqlCommand cmdIns = new SqlCommand(insertDev, conn))
                        {
                            cmdIns.Parameters.AddWithValue("@CodigoMAC", "DEV-" + sec.PuertoSerial);
                            cmdIns.Parameters.AddWithValue("@IdSector", sec.IdSector);
                            idDispositivo = Convert.ToInt32(cmdIns.ExecuteScalar());
                        }
                    }

                    // 3. Sembrar 24 puntos históricos descendentes en los últimos 240 minutos
                    DateTime now = DateTime.Now;
                    using (SqlTransaction trans = conn.BeginTransaction())
                    {
                        string insertLectura = @"
                            INSERT INTO dbo.HistorialLecturas (IdDispositivo, Humedad, Temperatura, FechaHora, EstadoSuelo, PendienteEvaporacion, TiempoLimiteEstimado)
                            VALUES (@IdDispositivo, @Humedad, @Temperatura, @FechaHora, @EstadoSuelo, NULL, NULL);";

                        for (int i = 0; i < 24; i++)
                        {
                            // Hace (240 - i * 10) minutos
                            int minutesAgo = 240 - (i * 10);
                            DateTime fecha = now.AddMinutes(-minutesAgo);

                            // Declive lineal de 38% a 33%
                            double humedad = 38.0 - (5.0 / 23.0) * i;
                            humedad = Math.Round(humedad, 2);

                            // Temperatura con pequeña variación sobre 25°C
                            double temperatura = 25.0 + (new Random(i).Next(-10, 10) / 10.0);
                            string estadoSuelo = EvaluarEstadoSuelo(humedad, temperatura, sec.HumedadMinima);

                            using (SqlCommand cmdInsLect = new SqlCommand(insertLectura, conn, trans))
                            {
                                cmdInsLect.Parameters.AddWithValue("@IdDispositivo", idDispositivo);
                                cmdInsLect.Parameters.AddWithValue("@Humedad", (decimal)humedad);
                                cmdInsLect.Parameters.AddWithValue("@Temperatura", (decimal)temperatura);
                                cmdInsLect.Parameters.AddWithValue("@FechaHora", fecha);
                                cmdInsLect.Parameters.AddWithValue("@EstadoSuelo", estadoSuelo);

                                cmdInsLect.ExecuteNonQuery();
                            }
                        }
                        trans.Commit();
                    }

                    OnAlertaRegistrada?.Invoke($"[{sec.NombreSector}] Sembrado completado. Humedad actual del simulador: {sec.Humedad}%.");
                }
            }
            catch (Exception ex)
            {
                OnAlertaRegistrada?.Invoke($"[{sec.NombreSector}] Error al sembrar datos de demostración: " + ex.Message);
            }
        }

        /// <summary>
        /// Calcula el tiempo de riego óptimo y los litros a reponer por Evapotranspiración (ET)
        /// cruzando Kc y las temperaturas de las últimas 24 horas.
        /// </summary>
        private void CalcularTiempoRiegoReposicion(SectorState sec)
        {
            try
            {
                double tempMedia = 22.0;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT AVG(CAST(hl.Temperatura AS DOUBLE PRECISION))
                        FROM dbo.HistorialLecturas hl
                        INNER JOIN dbo.Dispositivos d ON hl.IdDispositivo = d.IdDispositivo
                        WHERE d.IdSector = @IdSector
                          AND hl.FechaHora >= DATEADD(HOUR, -24, GETDATE());";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@IdSector", sec.IdSector);
                        conn.Open();
                        object res = cmd.ExecuteScalar();
                        if (res != null && res != DBNull.Value)
                        {
                            tempMedia = Convert.ToDouble(res);
                        }
                    }
                }

                double kc = sec.CoeficienteKc;
                if (kc <= 0) kc = 0.60;

                double et0 = 0.18 * tempMedia;
                double etc = et0 * kc;
                double litros = etc * 100.0; // Parcela estándar de 100 m2

                double caudal = sec.CaudalBombaLPM;
                if (caudal <= 0) caudal = 10.0;

                double runtimeMinutos = litros / caudal;

                sec.TiempoRiegoLimiteMinutos = Math.Round(runtimeMinutos, 2);
                sec.LitrosAReponerEstimados = Math.Round(litros, 2);

                OnAlertaRegistrada?.Invoke($"[{sec.NombreSector}] Balance Hídrico: T_media(24h) = {tempMedia:F1}°C, ET_0 = {et0:F2} mm/día, ET_c = {etc:F2} mm/día (Kc = {kc:F2}). Reposición estimada = {sec.LitrosAReponerEstimados} Litros. Tiempo de Riego Óptimo = {sec.TiempoRiegoLimiteMinutos} min.");
            }
            catch (Exception ex)
            {
                OnAlertaRegistrada?.Invoke($"[{sec.NombreSector}] Error al calcular balance hídrico: " + ex.Message);
                sec.TiempoRiegoLimiteMinutos = 5.0; // Fallback
                sec.LitrosAReponerEstimados = 50.0; // Fallback
            }
        }
    }
}
