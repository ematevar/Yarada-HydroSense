/*
  =================================================================================
  Yarada HydroSense - Firmware ESP32 para Riego de Precisión IoT
  Hardware Engineer & Firmware Specialist
  
  Descripción:
  Este firmware inicializa el bus serial del ESP32 a 115200 baudios, simula las 
  variaciones físicas de humedad y temperatura del suelo en La Yarada Los Palos,
  y responde activando/desactivando el Relé de la bomba hídrica en base a las 
  instrucciones seriales de la aplicación C# WinForms.
  =================================================================================
*/

// Usaremos el Pin GPIO 2 (LED integrado en la mayoría de placas de desarrollo ESP32 NodeMCU)
#define PIN_RELE 2 

// Variables globales para simulación física de suelo
float humedadActual = 32.5;     // Humedad inicial en porcentaje (%)
float temperaturaActual = 28.0; // Temperatura inicial en grados Celsius (°C)
unsigned long tiempoUltimoEnvio = 0;
const long intervaloEnvio = 2000; // Frecuencia de envío de telemetría (cada 2 segundos)

void setup() {
  // Inicialización del canal de comunicación USB-Serial a 115200 bps
  Serial.begin(115200);
  
  // Configuración del pin del relé como salida digital
  pinMode(PIN_RELE, OUTPUT);
  digitalWrite(PIN_RELE, LOW); // Apagado por defecto al arrancar el hardware
}

void loop() {
  unsigned long tiempoActual = millis();

  // Enviar telemetría periódica al puerto serie
  if (tiempoActual - tiempoUltimoEnvio >= intervaloEnvio) {
    tiempoUltimoEnvio = tiempoActual;

    // Simulación del comportamiento físico de la parcela
    if (digitalRead(PIN_RELE) == HIGH) {
      // Si la bomba está encendida, la humedad aumenta
      humedadActual += random(10, 25) / 10.0; // Sube entre +1.0% y +2.5% por ciclo
      if (humedadActual > 100.0) humedadActual = 100.0;
      
      // El agua enfría sutilmente el suelo
      temperaturaActual -= random(1, 4) / 10.0; // Baja entre -0.1°C y -0.4°C
      if (temperaturaActual < 15.0) temperaturaActual = 15.0;
    } 
    else {
      // Si la bomba está apagada, la humedad cae lentamente debido al calor árido de Tacna
      humedadActual -= random(5, 15) / 10.0;  // Baja entre -0.5% y -1.5%
      if (humedadActual < 5.0) humedadActual = 5.0;

      // El sol tacneño calienta el suelo
      temperaturaActual += random(1, 4) / 10.0; // Sube entre +0.1°C y +0.4°C
      if (temperaturaActual > 45.0) temperaturaActual = 45.0;
    }

    // Transmitir cadena formateada esperada por RiegoController.cs: MAC|Humedad|Temperatura
    // Dirección MAC ficticia registrada para pruebas en Script 1: AA:BB:CC:11:22:33
    Serial.print("AA:BB:CC:11:22:33|");
    Serial.print(humedadActual, 1);
    Serial.print("|");
    Serial.println(temperaturaActual, 1);
  }

  // Escucha activa de comandos serie procedentes de la aplicación WinForms (PC)
  if (Serial.available() > 0) {
    char comando = Serial.read();

    // Toma de acción sobre el hardware
    if (comando == 'E') {
      digitalWrite(PIN_RELE, HIGH); // Cierra el contacto del Relé (Enciende el LED integrado)
    } 
    else if (comando == 'A') {
      digitalWrite(PIN_RELE, LOW);  // Abre el contacto del Relé (Apaga el LED integrado)
    }
  }
}
