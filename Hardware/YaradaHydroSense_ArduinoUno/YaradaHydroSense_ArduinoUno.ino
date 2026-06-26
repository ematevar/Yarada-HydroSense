/*
  =================================================================================
  Yarada HydroSense - Firmware Arduino Uno con Sensor Capacitivo v1.2 y DHT22
  Hardware Engineer & Firmware Specialist
  =================================================================================
*/

#include "DHT.h"

// ==========================================
// ASIGNACIÓN DE PINES EN ARDUINO UNO
// ==========================================
#define PIN_RELE           8   // Salida Digital para el módulo de Relé (Bomba)
#define PIN_DHT            2   // Entrada Digital para el sensor DHT22 (Temperatura ambiente)
#define PIN_HUMEDAD_SUELO  A0  // Entrada Analógica para el Sensor Capacitivo de Humedad de Suelo v1.2

// Definimos el tipo de sensor DHT
#define DHTTYPE DHT22   

// Inicialización de la librería DHT
DHT dht(PIN_DHT, DHTTYPE);

// ==========================================
// IDENTIFICADOR ÚNICO DE DISPOSITIVO (MAC EMULADA)
// ==========================================
// IMPORTANTE: Cada Arduino Uno físico debe programarse con su propia MAC única
// para evitar colisiones e inconsistencias de datos en la base de datos SQL.
// Ejemplos:
// Sector 1 - Olivo Joven (Pozo 5): "AA:BB:CC:11:22:33"
// Sector 2 - Granada Exportación (Pozo 9): "DD:EE:FF:44:55:66"
#define MAC_DISPOSITIVO "AA:BB:CC:11:22:33"

// ==========================================
// CONSTANTES DE CALIBRACIÓN Y DIAGNÓSTICO
// (Ajustadas para el ADC de 10 bits de Arduino Uno: 0 - 1023)
// ==========================================
const int VALOR_SECO = 550;   // Lectura analógica en aire seco (0% humedad)
const int VALOR_HUMEDO = 250; // Lectura analógica sumergido en agua (100% humedad)

// Rango esperado del sensor capacitivo v1.2 conectado (evita lecturas espurias/desconexión)
const int RANGO_MIN_VALIDO = 200;
const int RANGO_MAX_VALIDO = 800;

// ==========================================
// CONFIGURACIÓN DE LÓGICA DEL RELÉ (BOMBA) Y WATCHDOG
// ==========================================
// La mayoría de módulos de relé comerciales para Arduino son Active Low (se activan con LOW).
// Si tu módulo de relé físico es Active High, invierte estos valores (ENCENDIDO = HIGH, APAGADO = LOW).
#define RELE_ENCENDIDO  LOW
#define RELE_APAGADO    HIGH

// Watchdog por hardware local: tiempo máximo de riego continuo (seguridad)
const unsigned long MAX_RIEGO_MS = 30UL * 60 * 1000; // 30 minutos máximo
unsigned long tiempoRelayOn = 0;

unsigned long tiempoUltimoEnvio = 0;
const long intervaloEnvio = 2000; // Telemetría cada 2 segundos

void setup() {
  // Inicialización del puerto serial a 115200 baudios
  Serial.begin(115200);
  
  // Configuración del Relé
  pinMode(PIN_RELE, OUTPUT);
  digitalWrite(PIN_RELE, RELE_APAGADO); // Bomba apagada por defecto
  
  // Inicializar el sensor DHT22
  dht.begin();
}

void loop() {
  unsigned long tiempoActual = millis();

  // 1. TRANSMISIÓN DE TELEMETRÍA REAL
  if (tiempoActual - tiempoUltimoEnvio >= intervaloEnvio) {
    tiempoUltimoEnvio = tiempoActual;

    // A. Lectura de temperatura ambiente desde DHT22
    float temperaturaAire = dht.readTemperature(); // En grados Celsius

    // B. Lectura del sensor capacitivo v1.2 y calibración a porcentaje
    int valorAnalogo = analogRead(PIN_HUMEDAD_SUELO);
    
    // Acotar el valor analógico a los rangos de calibración
    int valorAcotado = valorAnalogo;
    if (valorAcotado > VALOR_SECO) valorAcotado = VALOR_SECO;
    if (valorAcotado < VALOR_HUMEDO) valorAcotado = VALOR_HUMEDO;

    // Calcular el porcentaje de humedad del suelo (relación inversa: menor voltaje = mayor humedad)
    float humedadSuelo = ((float)(VALOR_SECO - valorAcotado) / (VALOR_SECO - VALOR_HUMEDO)) * 100.0;

    // Control de fallas: Si el DHT22 falla
    if (isnan(temperaturaAire)) {
      temperaturaAire = -99.9;
    }
    // Control de fallas robusto para el sensor analógico (ej. desconexión, cable suelto o flotante)
    bool sensorFallido = (valorAnalogo < RANGO_MIN_VALIDO || valorAnalogo > RANGO_MAX_VALIDO);
    if (sensorFallido) {
      humedadSuelo = -99.9;
    }

    // --- Envío de la Trama Estricta a WinForms ---
    // Formato requerido por RiegoController: MAC|Humedad|Temperatura
    Serial.print(MAC_DISPOSITIVO);
    Serial.print("|");
    Serial.print(humedadSuelo, 1);
    Serial.print("|");
    Serial.println(temperaturaAire, 1);
  }

  // 2. ESCUCHA ACTIVA DE COMANDOS SERIALES (Bucle para procesar buffer de entrada sin pérdidas)
  while (Serial.available() > 0) {
    char comando = Serial.read();

    if (comando == 'E') {
      digitalWrite(PIN_RELE, RELE_ENCENDIDO); // Enciende Bomba (Relé)
      tiempoRelayOn = millis();               // Registrar inicio para watchdog local
    } 
    else if (comando == 'A') {
      digitalWrite(PIN_RELE, RELE_APAGADO);  // Apaga Bomba (Relé)
    }
  }

  // 3. WATCHDOG LOCAL DE SEGURIDAD (Apagado por tiempo límite continuo)
  if (digitalRead(PIN_RELE) == RELE_ENCENDIDO && (tiempoActual - tiempoRelayOn) >= MAX_RIEGO_MS) {
    digitalWrite(PIN_RELE, RELE_APAGADO); // Apagado forzado por seguridad
    Serial.print(MAC_DISPOSITIVO);
    Serial.println("|TIMEOUT|RELAY_FORCED_OFF");
  }
}
