/*
  =================================================================================
  Yarada HydroSense - Firmware ESP32 con DHT22 (PRODUCCIÓN REAL)
  Hardware Engineer & Firmware Specialist
  =================================================================================
*/

#include "DHT.h"

// ==========================================
// ASIGNACIÓN DE PINES SEGUROS
// ==========================================
#define PIN_RELE    14  // Salida Digital (Lado izquierdo, seguro para relé)
#define PIN_DHT     27  // Entrada Digital para DHT22 (Lado derecho de la placa)

// Definimos el tipo de sensor
#define DHTTYPE DHT22   

// Inicialización de la librería DHT
DHT dht(PIN_DHT, DHTTYPE);

unsigned long tiempoUltimoEnvio = 0;
const long intervaloEnvio = 2000; // Telemetría cada 2 segundos

void setup() {
  Serial.begin(115200);
  
  // Configuración segura del Relé
  pinMode(PIN_RELE, OUTPUT);
  digitalWrite(PIN_RELE, LOW); // Bomba apagada por defecto
  
  // Inicializar el sensor DHT22
  dht.begin();
}

void loop() {
  unsigned long tiempoActual = millis();

  // 1. TRANSMISIÓN DE TELEMETRÍA REAL
  if (tiempoActual - tiempoUltimoEnvio >= intervaloEnvio) {
    tiempoUltimoEnvio = tiempoActual;

    // Lectura de los parámetros reales del DHT22
    float humedadAire = dht.readHumidity();
    float temperaturaAire = dht.readTemperature(); // En grados Celsius

    // Control de fallas: Si el sensor se desconecta o falla la lectura
    if (isnan(humedadAire) || isnan(temperaturaAire)) {
      // Enviamos flags de error controlados para que C# sepa que el hardware falló
      humedadAire = -99.9;
      temperaturaAire = -99.9;
    }

    // --- Envío de la Trama Estricta a WinForms ---
    // Formato requerido: MAC|Humedad|Temperatura
    Serial.print("AA:BB:CC:11:22:33|");
    Serial.print(humedadAire, 1);
    Serial.print("|");
    Serial.println(temperaturaAire, 1);
  }

  // 2. ESCUCHA ACTIVA DE COMANDOS SERIALES
  if (Serial.available() > 0) {
    char comando = Serial.read();

    if (comando == 'E') {
      digitalWrite(PIN_RELE, HIGH); // Enciende Bomba
    } 
    else if (comando == 'A') {
      digitalWrite(PIN_RELE, LOW);  // Apaga Bomba
    }
  }
}