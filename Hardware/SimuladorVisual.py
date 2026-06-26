# -*- coding: utf-8 -*-
"""
=================================================================================
Yarada HydroSense - Panel de Control y Simulador Visual de Dispositivos (GUI)
=================================================================================
Este script proporciona una interfaz gráfica (GUI) en Tkinter para simular N
dispositivos sensores/bombas de Yarada HydroSense en tiempo real.

Requisitos:
1. Python 3.x
2. Librería pyserial: pip install pyserial
3. Puertos seriales virtuales (com0com) configurados.
=================================================================================
"""

import tkinter as tk
from tkinter import ttk, messagebox, scrolledtext
import threading
import time
import random
import sys

# Intentar importar pyserial
try:
    import serial
except ImportError:
    # Mostrar un mensaje gráfico de error si Tkinter está disponible
    root = tk.Tk()
    root.withdraw()
    messagebox.showerror(
        "Error de dependencias",
        "La librería 'pyserial' no está instalada.\n\nPor favor, instálala ejecutando:\npip install pyserial"
    )
    sys.exit(1)

# ==========================================
# CONFIGURACIÓN DE DISPOSITIVOS A SIMULAR
# ==========================================
DISPOSITIVOS = [
    {
        "nombre": "Sector 3 - Granada Real (Pozo 14)",
        "puerto": "COM13",          # Conectado a COM14 en la app WinForms (puerto virtual par)
        "mac": "AA:BB:CC:11:22:33", # MAC real configurada en el firmware de Arduino
        "humedad_inicial": 45.0,
        "temp_inicial": 24.5,
        "tasa_secado": 0.05,
        "tasa_riego": 1.2,
    }
]

INTERVALO_ENVIO_SEGUNDOS = 2.0

class DispositivoSimulado:
    def __init__(self, config):
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
        self.logs_recientes = []

    def procesar(self, delta_tiempo, log_callback):
        # 1. Simular Dinámica de Humedad
        if self.bomba_activa:
            self.humedad += self.tasa_riego * delta_tiempo
            if self.humedad > 100.0:
                self.humedad = 100.0
        else:
            self.humedad -= self.tasa_secado * delta_tiempo
            if self.humedad < 15.0:
                self.humedad = 15.0

        # Simular variación de temperatura
        self.temperatura += random.uniform(-0.05, 0.05)
        if self.temperatura < 10.0: self.temperatura = 10.0
        if self.temperatura > 45.0: self.temperatura = 45.0

        # 2. Leer comandos del puerto
        if self.ser and self.ser.is_open:
            try:
                if self.ser.in_waiting > 0:
                    comando = self.ser.read().decode('utf-8', errors='ignore')
                    for char in comando:
                        if char == 'E':
                            if not self.bomba_activa:
                                self.bomba_activa = True
                                log_callback(self, "Comando 'E' -> BOMBA ENCENDIDA")
                        elif char == 'A':
                            if self.bomba_activa:
                                self.bomba_activa = False
                                log_callback(self, "Comando 'A' -> BOMBA APAGADA")
            except Exception as e:
                log_callback(self, f"Error leyendo: {e}")


class SimuladorGUI:
    def __init__(self, root):
        self.root = root
        self.root.title("Yarada HydroSense - Simulador de Hardware Multidispositivo")
        self.root.geometry("850x600")
        self.root.minsize(750, 500)
        
        # Paleta de colores Premium Dark Mode
        self.bg_color = "#1E1E24"
        self.card_color = "#2A2A35"
        self.text_color = "#EAEAEA"
        self.accent_color = "#4E8CFF"
        self.green_color = "#00E676"
        self.red_color = "#FF1744"
        
        self.root.configure(bg=self.bg_color)
        
        # Estilos generales
        self.style = ttk.Style()
        self.style.theme_use("clam")
        self.style.configure(".", background=self.bg_color, foreground=self.text_color)
        
        # Instanciar dispositivos
        self.dispositivos = [DispositivoSimulado(conf) for conf in DISPOSITIVOS]
        self.threads = []
        
        self.crear_interfaz()
        self.iniciar_simulacion()
        self.actualizar_gui_periodico()

    def crear_interfaz(self):
        # Header principal
        header_frame = tk.Frame(self.root, bg=self.bg_color, pady=15)
        header_frame.pack(fill=tk.X)
        
        titulo_label = tk.Label(
            header_frame, 
            text="Simulador de Hardware - Yarada HydroSense", 
            font=("Segoe UI", 16, "bold"), 
            bg=self.bg_color, 
            fg=self.accent_color
        )
        titulo_label.pack(side=tk.LEFT, padx=20)
        
        estado_general = tk.Label(
            header_frame,
            text=f"Simulando {len(self.dispositivos)} dispositivos concurrentes",
            font=("Segoe UI", 10, "italic"),
            bg=self.bg_color,
            fg="#8A8A9F"
        )
        estado_general.pack(side=tk.RIGHT, padx=20, pady=5)
        
        # Tabla de dispositivos
        table_frame = tk.Frame(self.root, bg=self.card_color, bd=1, relief=tk.FLAT, padx=10, pady=10)
        table_frame.pack(fill=tk.BOTH, expand=True, padx=20, pady=10)
        
        # Cabecera de la tabla
        headers = ["Nombre del Sector", "Puerto COM", "MAC del Dispositivo", "Humedad Suelo", "Temperatura", "Bomba/Relé"]
        col_widths = [22, 12, 18, 15, 12, 12]
        
        for idx, (header, width) in enumerate(zip(headers, col_widths)):
            lbl = tk.Label(
                table_frame,
                text=header,
                font=("Segoe UI", 10, "bold"),
                bg=self.bg_color,
                fg="#B0B0C0",
                width=width,
                anchor="w",
                padx=5,
                pady=5
            )
            lbl.grid(row=0, column=idx, sticky="nsew", pady=5)
            
        # Filas de la tabla (guardamos los widgets que cambian dinámicamente)
        self.widgets_filas = {}
        for row_idx, disp in enumerate(self.dispositivos, start=1):
            widgets_disp = {}
            
            # Nombre
            lbl_name = tk.Label(table_frame, text=disp.nombre, font=("Segoe UI", 10), bg=self.card_color, fg=self.text_color, anchor="w", padx=5)
            lbl_name.grid(row=row_idx, column=0, sticky="w", pady=6)
            
            # Puerto
            lbl_port = tk.Label(table_frame, text=disp.puerto, font=("Consolas", 10, "bold"), bg=self.card_color, fg="#FFB300", anchor="w", padx=5)
            lbl_port.grid(row=row_idx, column=1, sticky="w", pady=6)
            
            # MAC
            lbl_mac = tk.Label(table_frame, text=disp.mac, font=("Consolas", 10), bg=self.card_color, fg="#8A8A9F", anchor="w", padx=5)
            lbl_mac.grid(row=row_idx, column=2, sticky="w", pady=6)
            
            # Humedad
            lbl_hum = tk.Label(table_frame, text="0.0 %", font=("Segoe UI", 10, "bold"), bg=self.card_color, fg=self.text_color, anchor="w", padx=5)
            lbl_hum.grid(row=row_idx, column=3, sticky="w", pady=6)
            widgets_disp["humedad"] = lbl_hum
            
            # Temperatura
            lbl_temp = tk.Label(table_frame, text="0.0 °C", font=("Segoe UI", 10), bg=self.card_color, fg=self.text_color, anchor="w", padx=5)
            lbl_temp.grid(row=row_idx, column=4, sticky="w", pady=6)
            widgets_disp["temperatura"] = lbl_temp
            
            # Estado Bomba
            lbl_pump = tk.Label(
                table_frame, 
                text="APAGADO", 
                font=("Segoe UI", 9, "bold"), 
                bg="#424242", 
                fg="#BDBDBD", 
                width=10, 
                pady=3,
                relief=tk.RIDGE
            )
            lbl_pump.grid(row=row_idx, column=5, sticky="w", pady=6)
            widgets_disp["bomba"] = lbl_pump
            
            self.widgets_filas[disp.puerto] = widgets_disp

        # Consola de Eventos / Log
        console_frame = tk.Frame(self.root, bg=self.bg_color)
        console_frame.pack(fill=tk.BOTH, expand=True, padx=20, pady=10)
        
        console_title = tk.Label(console_frame, text="Consola de eventos en tiempo real:", font=("Segoe UI", 9, "bold"), bg=self.bg_color, fg="#8A8A9F")
        console_title.pack(anchor="w", pady=3)
        
        self.consola = scrolledtext.ScrolledText(
            console_frame, 
            bg="#111116", 
            fg="#00FF66", 
            insertbackground="#00FF66",
            font=("Consolas", 9), 
            relief=tk.FLAT, 
            bd=0
        )
        self.consola.pack(fill=tk.BOTH, expand=True)
        self.consola.insert(tk.END, "[SISTEMA] Cargando puertos virtuales y servicios de telemetría...\n")
        self.consola.configure(state=tk.DISABLED)

    def log_evento(self, dispositivo, mensaje):
        timestamp = time.strftime("%H:%M:%S")
        texto = f"[{timestamp}] [{dispositivo.puerto} - {dispositivo.nombre}]: {mensaje}\n"
        
        self.consola.configure(state=tk.NORMAL)
        self.consola.insert(tk.END, texto)
        self.consola.see(tk.END)
        self.consola.configure(state=tk.DISABLED)

    def iniciar_simulacion(self):
        # Lanzar la simulación serial en hilos independientes para no colgar la GUI
        for disp in self.dispositivos:
            t = threading.Thread(target=self.loop_serial_dispositivo, args=(disp,), daemon=True)
            self.threads.append(t)
            t.start()

    def loop_serial_dispositivo(self, disp):
        # Inicializar el puerto serial
        try:
            disp.ser = serial.Serial(disp.puerto, 115200, timeout=0.1)
            self.log_evento(disp, f"Puerto {disp.puerto} abierto exitosamente.")
        except Exception as e:
            self.log_evento(disp, f"ERROR al abrir puerto {disp.puerto}. ¿Está creado com0com? {e}")
            disp.activo = False
            return

        tiempo_ultimo_envio = 0
        tiempo_ultimo_calculo = time.time()

        while disp.activo:
            tiempo_actual = time.time()
            delta = tiempo_actual - tiempo_ultimo_calculo
            tiempo_ultimo_calculo = tiempo_actual

            # Ejecutar lógica física interna
            disp.procesar(delta, self.log_evento)

            # Enviar telemetría según intervalo
            if (tiempo_actual - tiempo_ultimo_envio) >= INTERVALO_ENVIO_SEGUNDOS:
                tiempo_ultimo_envio = tiempo_actual
                trama = f"{disp.mac}|{disp.humedad:.1f}|{disp.temperatura:.1f}\n"
                try:
                    disp.ser.write(trama.encode('utf-8'))
                    disp.ser.flush()
                except Exception as e:
                    self.log_evento(disp, f"Error de transmisión: {e}")
                    disp.activo = False
                    break

            time.sleep(0.05)

        if disp.ser and disp.ser.is_open:
            disp.ser.close()
        self.log_evento(disp, "Puerto serial cerrado.")

    def actualizar_gui_periodico(self):
        # Actualiza las etiquetas de la tabla desde la memoria de los hilos de simulación
        for disp in self.dispositivos:
            widgets = self.widgets_filas.get(disp.puerto)
            if not widgets: continue

            # Humedad
            hum_txt = f"{disp.humedad:.1f} %"
            widgets["humedad"].config(text=hum_txt)
            # Cambiar color según humedad
            if disp.humedad < 30.0:
                widgets["humedad"].config(fg="#FF8F00") # Seco / Alerta
            elif disp.humedad < 70.0:
                widgets["humedad"].config(fg="#00E676") # Óptimo
            else:
                widgets["humedad"].config(fg="#2979FF") # Exceso / Húmedo

            # Temperatura
            widgets["temperatura"].config(text=f"{disp.temperatura:.1f} °C")

            # Estado Bomba
            if disp.bomba_activa:
                widgets["bomba"].config(text="ACTIVO", bg=self.green_color, fg="#121212")
            else:
                widgets["bomba"].config(text="APAGADO", bg="#424242", fg="#BDBDBD")

        # Programar siguiente actualización
        if any(disp.activo for disp in self.dispositivos):
            self.root.after(100, self.actualizar_gui_periodico)

    def cerrar(self):
        # Detener hilos
        for disp in self.dispositivos:
            disp.activo = False
        self.root.destroy()


def main():
    root = tk.Tk()
    app = SimuladorGUI(root)
    root.protocol("WM_DELETE_WINDOW", app.cerrar)
    root.mainloop()

if __name__ == "__main__":
    main()
