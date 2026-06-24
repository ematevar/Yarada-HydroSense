using System;

namespace ProyectoFinal_YaradaPalos.Models
{
    /// <summary>
    /// Representa una lectura física obtenida desde los sensores del ESP32.
    /// Contiene la lógica agronómica para evaluar las condiciones del suelo de La Yarada.
    /// </summary>
    public class LecturaModel
    {
        public double Humedad { get; set; }
        public double Temperatura { get; set; }
        public DateTime FechaHora { get; set; }

        public LecturaModel()
        {
            FechaHora = DateTime.Now;
        }

        public LecturaModel(double humedad, double temperatura)
        {
            Humedad = humedad;
            Temperatura = temperatura;
            FechaHora = DateTime.Now;
        }

        /// <summary>
        /// Evalúa agronómicamente el estado del suelo hídrico y térmico de La Yarada.
        /// </summary>
        /// <returns>Descripción textual del estado para la interfaz del usuario.</returns>
        public string EvaluarEstadoSuelo()
        {
            // Umbral crítico de humedad por debajo del 25% para inicio de riego
            if (Humedad < 25.0)
            {
                return "Crítico - Seco (Estrés Hídrico)";
            }
            // Humedad óptima entre 25% y 50%
            else if (Humedad >= 25.0 && Humedad <= 50.0)
            {
                // Estrés por evaporación si la temperatura supera la máxima (35.0 °C)
                if (Temperatura > 35.0)
                {
                    return "Evaporación Crítica (Temperatura Excesiva)";
                }
                return "Óptimo (Conservación de Recursos)";
            }
            // Humedad excesiva (puede exacerbar problemas de salinidad y saturación radicular)
            else
            {
                return "Crítico - Saturado (Exceso / Riesgo de Salinidad)";
            }
        }
    }
}
