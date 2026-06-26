# -*- coding: utf-8 -*-
"""
=================================================================================
Yarada HydroSense - Simulador Multidispositivo de Telemetría y Riego
=================================================================================
Este script simula N cantidad de Arduinos físicos transmitiendo datos a la app
WinForms y respondiendo a comandos de control de bomba ('E' = ON, 'A' = OFF).

Requisitos:
1. Python 3.x
2. Librería pyserial: instálala ejecutando: pip install pyserial
3. Puertos COM Virtuales (Ej: usando com0com en Windows para crear pares de puertos:
   COM10 <-> COM11, COM12 <-> COM13, etc.)
   El simulador se conecta a un extremo del par (Ej: COM11, COM13) y la aplicación
   WinForms se conecta al otro extremo (Ej: COM10, COM12).
=================================================================================
"""

import time
import sys
import random
import threading

# Intentar importar pyserial
try:
    import serial
except ImportError:
    print("Error: La librería 'pyserial' no está instalada.")
    print("Por favor, instálala ejecutando: pip install pyserial")
    sys.exit(1)

# ==========================================
# CONFIGURACIÓN DE DISPOSITIVOS A SIMULAR
# ==========================================
# Agrega o modifica esta lista para simular la cantidad de dispositivos que desees.
DISPOSITIVOS = [
    {
        "nombre": "Sector 3 - Granada Real (Pozo 14)",
        "puerto": "COM13",          # Puerto COM virtual donde se conectará el simulador (par con COM14 en C#)
        "mac": "AA:BB:CC:11:22:33", # MAC real configurada en el firmware de Arduino
        "humedad_inicial": 45.0,    # Porcentaje de humedad inicial
        "temp_inicial": 24.5,       # Temperatura inicial
        "tasa_secado": 0.05,        # Disminución de humedad por segundo
        "tasa_riego": 1.2,          # Incremento de humedad por segundo cuando la bomba está activa
    }
]

INTERVALO_ENVIO_SEGUNDOS = 2.0  # Telemetría cada 2 segundos, igual al Arduino
lock_pantalla = threading.Lock()

class DispositivoSimulado(threading.Thread):
    def __init__(self, config):
        super().__init__()
        self.nombre = config["nombre"]
        self.puerto = config["puerto"]
        self.mac = config["mac"]
        self.humedad = config["humedad_inicial"]
        self.temperatura = config["temp_inicial"]
        self.tasa_secado = config["tasa_secado"]
        self.tasa_riego = config["tasa_riego"]
        
        self.bomba_activa = False
        self.activo = True
        self.ser = None

    def log(self, mensaje):
        with lock_pantalla:
            timestamp = time.strftime("%Y-%m-%d %H:%M:%S")
            print(f"[{timestamp}] [{self.nombre} en {self.puerto}]: {mensaje}")

    def run(self):
        self.log(f"Iniciando simulación en puerto {self.puerto} con MAC {self.mac}...")
        
        # Conectar al puerto serial
        try:
            self.ser = serial.Serial(self.puerto, 115200, timeout=0.1)
            self.log("Puerto serial abierto con éxito.")
        except Exception as e:
            self.log(f"ERROR: No se pudo abrir el puerto serial {self.puerto}. Asegúrate de crear el puerto virtual. Detalles: {e}")
            self.activo = False
            return

        tiempo_ultimo_envio = 0
        tiempo_ultimo_calculo = time.time()

        while self.activo:
            tiempo_actual = time.time()
            delta_tiempo = tiempo_actual - tiempo_ultimo_calculo
            tiempo_ultimo_calculo = tiempo_actual

            # --- Simular Dinámica del Suelo y Temperatura ---
            if self.bomba_activa:
                # La humedad sube si la bomba está activa
                self.humedad += self.tasa_riego * delta_tiempo
                if self.humedad > 100.0:
                    self.humedad = 100.0
            else:
                # La humedad baja lentamente si la bomba está apagada
                self.humedad -= self.tasa_secado * delta_tiempo
                if self.humedad < 15.0:  # Límite mínimo realista
                    self.humedad = 15.0

            # Fluctuación de temperatura aleatoria mínima
            self.temperatura += random.uniform(-0.05, 0.05)
            if self.temperatura < 10.0: self.temperatura = 10.0
            if self.temperatura > 45.0: self.temperatura = 45.0

            # --- Escuchar Comandos Seriales (Entrada) ---
            try:
                if self.ser.in_waiting > 0:
                    comando = self.ser.read().decode('utf-8', errors='ignore')
                    for char in comando:
                        if char == 'E':
                            if not self.bomba_activa:
                                self.bomba_activa = True
                                self.log("Comando 'E' recibido -> BOMBA ENCENDIDA")
                        elif char == 'A':
                            if self.bomba_activa:
                                self.bomba_activa = False
                                self.log("Comando 'A' recibido -> BOMBA APAGADA")
            except Exception as e:
                self.log(f"Error leyendo comandos seriales: {e}")
                self.activo = False
                break

            # --- Enviar Telemetría (Salida) ---
            if (tiempo_actual - tiempo_ultimo_envio) >= INTERVALO_ENVIO_SEGUNDOS:
                tiempo_ultimo_envio = tiempo_actual
                # Trama formato: MAC|Humedad|Temperatura
                trama = f"{self.mac}|{self.humedad:.1f}|{self.temperatura:.1f}\n"
                try:
                    self.ser.write(trama.encode('utf-8'))
                    self.ser.flush()
                    # Log opcional para monitorear telemetría
                    # self.log(f"Telemetría enviada: {trama.strip()}")
                except Exception as e:
                    self.log(f"Error escribiendo telemetría serial: {e}")
                    self.activo = False
                    break

            time.sleep(0.05)  # Latencia del ciclo de simulación

        # Cerrar puerto al finalizar
        if self.ser and self.ser.is_open:
            self.ser.close()
        self.log("Conexión serial cerrada. Simulación terminada.")

    def detener(self):
        self.activo = False

def main():
    print("=====================================================================")
    print("        Yarada HydroSense - Simulador de Dispositivos Hardware       ")
    print("=====================================================================")
    print(f"Dispositivos configurados para simulación: {len(DISPOSITIVOS)}")
    print("Presiona Ctrl+C para detener el simulador.\n")

    hilos = []
    for config in DISPOSITIVOS:
        hilo = DispositivoSimulado(config)
        hilo.daemon = True
        hilos.append(hilo)
        hilo.start()

    try:
        while any(hilo.is_alive() for hilo in hilos):
            time.sleep(0.5)
    except KeyboardInterrupt:
        print("\n\nDeteniendo simulación de dispositivos...")
        for hilo in hilos:
            hilo.detener()
        for hilo in hilos:
            hilo.join()
        print("Simulador finalizado correctamente.")

if __name__ == "__main__":
    main()
