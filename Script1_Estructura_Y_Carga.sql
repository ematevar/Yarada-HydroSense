-- =================================================================================
-- SCRIPT 1: ESTRUCTURA DE DATOS, RELACIONES Y CARGA INICIAL
-- Sistema Comercial de Riego de Precisión IoT (Tacna - La Yarada Los Palos)
-- DBA & Database Architect: Versión Comercial Avanzada con Control de Bomba y Volumen
-- =================================================================================

-- 1. CREACIÓN DE LA BASE DE DATOS
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'RiegoPrecisionDB')
BEGIN
    CREATE DATABASE RiegoPrecisionDB;
END
GO

USE RiegoPrecisionDB;
GO

-- Limpieza de tablas previas en caso de re-ejecución (orden inverso de dependencias)
IF OBJECT_ID('dbo.AlertasAnomalias', 'U') IS NOT NULL DROP TABLE dbo.AlertasAnomalias;
IF OBJECT_ID('dbo.HistorialRiego', 'U') IS NOT NULL DROP TABLE dbo.HistorialRiego;
IF OBJECT_ID('dbo.HistorialLecturas', 'U') IS NOT NULL DROP TABLE dbo.HistorialLecturas;
IF OBJECT_ID('dbo.Dispositivos', 'U') IS NOT NULL DROP TABLE dbo.Dispositivos;
IF OBJECT_ID('dbo.Sectores', 'U') IS NOT NULL DROP TABLE dbo.Sectores;
IF OBJECT_ID('dbo.Cultivos', 'U') IS NOT NULL DROP TABLE dbo.Cultivos;
IF OBJECT_ID('dbo.Usuarios', 'U') IS NOT NULL DROP TABLE dbo.Usuarios;
IF OBJECT_ID('dbo.Roles', 'U') IS NOT NULL DROP TABLE dbo.Roles;
GO

-- =================================================================================
-- 2. CREACIÓN DE TABLAS (Normalización Comercial y Escalable)
-- =================================================================================

-- MÓDULO DE SEGURIDAD Y USUARIOS
CREATE TABLE dbo.Roles (
    IdRol INT IDENTITY(1,1) NOT NULL,
    NombreRol VARCHAR(50) NOT NULL,
    CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (IdRol),
    CONSTRAINT UQ_Roles_Nombre UNIQUE (NombreRol)
);
GO

CREATE TABLE dbo.Usuarios (
    IdUsuario INT IDENTITY(1,1) NOT NULL,
    Username VARCHAR(50) NOT NULL,
    PasswordHash VARCHAR(256) NOT NULL, -- SHA-256 en formato Hexadecimal (64 caracteres)
    Nombre VARCHAR(100) NOT NULL,
    IdRol INT NOT NULL,
    Activo BIT NOT NULL CONSTRAINT DF_Usuarios_Activo DEFAULT 1,
    FechaCreacion DATETIME NOT NULL CONSTRAINT DF_Usuarios_FechaCreacion DEFAULT GETDATE(),
    CONSTRAINT PK_Usuarios PRIMARY KEY CLUSTERED (IdUsuario),
    CONSTRAINT UQ_Usuarios_Username UNIQUE (Username),
    CONSTRAINT FK_Usuarios_Roles FOREIGN KEY (IdRol) REFERENCES dbo.Roles (IdRol)
);
GO

-- MÓDULO AGRÍCOLA E IOT
CREATE TABLE dbo.Cultivos (
    IdCultivo INT IDENTITY(1,1) NOT NULL,
    NombreCultivo VARCHAR(100) NOT NULL,
    HumedadMinima DECIMAL(5,2) NOT NULL,    -- Porcentaje de humedad ideal
    TemperaturaMaxima DECIMAL(5,2) NOT NULL, -- Límite de estrés térmico
    CONSTRAINT PK_Cultivos PRIMARY KEY CLUSTERED (IdCultivo),
    CONSTRAINT UQ_Cultivos_Nombre UNIQUE (NombreCultivo)
);
GO

CREATE TABLE dbo.Sectores (
    IdSector INT IDENTITY(1,1) NOT NULL,
    NombreSector VARCHAR(100) NOT NULL,
    NombreEncargado VARCHAR(100) NOT NULL CONSTRAINT DF_Sectores_Encargado DEFAULT '',
    IdCultivo INT NOT NULL,
    PuertoSerial VARCHAR(20) NOT NULL CONSTRAINT DF_Sectores_Puerto DEFAULT '',
    TarifaAguaReferencial DECIMAL(10,2) NOT NULL CONSTRAINT DF_Sectores_Tarifa DEFAULT 0.00, -- Tarifa por hora de riego (PEN/hora)
    CaudalBombaLPM DECIMAL(5,2) NOT NULL CONSTRAINT DF_Sectores_Caudal DEFAULT 0.00,         -- Caudal de la bomba en Litros Por Minuto (LPM)
    Activo BIT NOT NULL CONSTRAINT DF_Sectores_Activo DEFAULT 1,
    CONSTRAINT PK_Sectores PRIMARY KEY CLUSTERED (IdSector),
    CONSTRAINT FK_Sectores_Cultivos FOREIGN KEY (IdCultivo) REFERENCES dbo.Cultivos (IdCultivo)
);
GO

CREATE TABLE dbo.Dispositivos (
    IdDispositivo INT IDENTITY(1,1) NOT NULL,
    CodigoMAC VARCHAR(17) NOT NULL,
    IdSector INT NOT NULL,
    FechaInstalacion DATETIME NOT NULL CONSTRAINT DF_Dispositivos_Instalacion DEFAULT GETDATE(),
    Estado VARCHAR(20) NOT NULL CONSTRAINT DF_Dispositivos_Estado DEFAULT 'Activo', -- 'Activo', 'Inactivo', 'Mantenimiento'
    CONSTRAINT PK_Dispositivos PRIMARY KEY CLUSTERED (IdDispositivo),
    CONSTRAINT UQ_Dispositivos_MAC UNIQUE (CodigoMAC),
    CONSTRAINT FK_Dispositivos_Sectores FOREIGN KEY (IdSector) REFERENCES dbo.Sectores (IdSector),
    CONSTRAINT CK_Dispositivos_Estado CHECK (Estado IN ('Activo', 'Inactivo', 'Mantenimiento'))
);
GO

CREATE TABLE dbo.HistorialLecturas (
    IdLectura BIGINT IDENTITY(1,1) NOT NULL,
    IdDispositivo INT NOT NULL,
    Humedad DECIMAL(5,2) NOT NULL,
    Temperatura DECIMAL(5,2) NOT NULL,
    FechaHora DATETIME NOT NULL CONSTRAINT DF_HistorialLecturas_Fecha DEFAULT GETDATE(),
    EstadoSuelo VARCHAR(50) NOT NULL, -- 'Óptimo', 'Humedad Crítica', 'Salinidad Alta', etc.
    CONSTRAINT PK_HistorialLecturas PRIMARY KEY CLUSTERED (IdLectura),
    CONSTRAINT FK_HistorialLecturas_Dispositivos FOREIGN KEY (IdDispositivo) REFERENCES dbo.Dispositivos (IdDispositivo)
);
GO

CREATE TABLE dbo.HistorialRiego (
    IdHistorialRiego INT IDENTITY(1,1) NOT NULL,
    IdSector INT NOT NULL,
    FechaHoraInicio DATETIME NOT NULL,
    FechaHoraFin DATETIME NULL,
    -- Columna calculada para saber los minutos exactos que el relé estuvo encendido
    MinutosRiego AS (DATEDIFF(MINUTE, FechaHoraInicio, FechaHoraFin)),
    -- Columna para almacenar los litros consumidos (calculado en C# o mediante un Stored Procedure)
    LitrosConsumidos DECIMAL(10,2) NOT NULL CONSTRAINT DF_HistorialRiego_Litros DEFAULT 0.00,
    CONSTRAINT PK_HistorialRiego PRIMARY KEY (IdHistorialRiego),
    CONSTRAINT FK_HistorialRiego_Sectores FOREIGN KEY (IdSector) REFERENCES dbo.Sectores (IdSector)
);
GO

-- MÓDULO DE ALERTAS Y ANOMALÍAS
CREATE TABLE dbo.AlertasAnomalias (
    IdAlerta INT IDENTITY(1,1) NOT NULL,
    IdDispositivo INT NOT NULL,
    TipoAnomalia VARCHAR(100) NOT NULL, -- 'Posible Fuga', 'Estrés Hídrico Crítico', 'Salinidad Extrema'
    Descripcion VARCHAR(500) NOT NULL,
    FechaHora DATETIME NOT NULL CONSTRAINT DF_Alertas_Fecha DEFAULT GETDATE(),
    Solucionado BIT NOT NULL CONSTRAINT DF_Alertas_Solucionado DEFAULT 0,
    CONSTRAINT PK_AlertasAnomalias PRIMARY KEY CLUSTERED (IdAlerta),
    CONSTRAINT FK_AlertasAnomalias_Dispositivos FOREIGN KEY (IdDispositivo) REFERENCES dbo.Dispositivos (IdDispositivo)
);
GO

-- =================================================================================
-- 3. CREACIÓN DE ÍNDICES (Optimización DBA de Reportes y Consultas de Telemetría)
-- =================================================================================

-- Optimización de búsqueda de usuario en Login
CREATE NONCLUSTERED INDEX IX_Usuarios_Username ON dbo.Usuarios (Username) INCLUDE (PasswordHash, Activo, Nombre, IdRol);
GO

-- Búsquedas del ESP32 por dirección MAC
CREATE NONCLUSTERED INDEX IX_Dispositivos_MAC ON dbo.Dispositivos (CodigoMAC) INCLUDE (IdSector, Estado);
GO

-- Búsqueda de lecturas para dashboard y análisis temporal de sectores
CREATE NONCLUSTERED INDEX IX_HistorialLecturas_Dispositivo_Fecha ON dbo.HistorialLecturas (IdDispositivo, FechaHora DESC) INCLUDE (Humedad, Temperatura, EstadoSuelo);
GO

-- Búsqueda e informes de costos por fechas y sectores
CREATE NONCLUSTERED INDEX IX_HistorialRiego_Sector_Fechas ON dbo.HistorialRiego (IdSector, FechaHoraInicio, FechaHoraFin) INCLUDE (LitrosConsumidos);
GO

-- Búsqueda rápida de anomalías pendientes de solución por dispositivo
CREATE NONCLUSTERED INDEX IX_AlertasAnomalias_Dispositivo_Solucionado ON dbo.AlertasAnomalias (IdDispositivo, Solucionado) INCLUDE (TipoAnomalia, FechaHora);
GO


-- =================================================================================
-- 4. CARGA DE DATOS INICIALES (Semilla del Entorno Beta)
-- =================================================================================

-- A. Roles Comerciales
INSERT INTO dbo.Roles (NombreRol) VALUES ('Administrador');
INSERT INTO dbo.Roles (NombreRol) VALUES ('Operador');
GO

-- B. Usuario Administrador de Prueba
DECLARE @IdRolAdmin INT;
SELECT @IdRolAdmin = IdRol FROM dbo.Roles WHERE NombreRol = 'Administrador';

-- Contraseña '123' encriptada nativamente con SHA-256
INSERT INTO dbo.Usuarios (Username, PasswordHash, Nombre, IdRol, Activo, FechaCreacion)
VALUES (
    'elvis.mamani', 
    CONVERT(VARCHAR(256), HASHBYTES('SHA2_256', '123'), 2), 
    'Elvis Mamani', 
    @IdRolAdmin, 
    1, 
    GETDATE()
);
GO

-- C. Cultivos Comunes en Tacna (La Yarada Los Palos)
INSERT INTO dbo.Cultivos (NombreCultivo, HumedadMinima, TemperaturaMaxima) VALUES ('Olivo', 25.00, 35.00);
INSERT INTO dbo.Cultivos (NombreCultivo, HumedadMinima, TemperaturaMaxima) VALUES ('Granada', 30.00, 38.00);
GO

-- E. Sectores con Tarifas por Hora de Riego y Caudales de Bomba en LPM
DECLARE @IdCultivoOlivo INT, @IdCultivoGranada INT;
SELECT @IdCultivoOlivo = IdCultivo FROM dbo.Cultivos WHERE NombreCultivo = 'Olivo';
SELECT @IdCultivoGranada = IdCultivo FROM dbo.Cultivos WHERE NombreCultivo = 'Granada';

-- Caudal: 4.50 Litros por minuto para Olivo, y 6.00 Litros por minuto para Granada
INSERT INTO dbo.Sectores (NombreSector, NombreEncargado, IdCultivo, PuertoSerial, TarifaAguaReferencial, CaudalBombaLPM, Activo) 
VALUES ('Sector 1 - Olivo Joven (Pozo 5)', 'Elvis Mamani', @IdCultivoOlivo, 'COM13', 15.50, 4.50, 1);

INSERT INTO dbo.Sectores (NombreSector, NombreEncargado, IdCultivo, PuertoSerial, TarifaAguaReferencial, CaudalBombaLPM, Activo) 
VALUES ('Sector 2 - Granada Exportación (Pozo 9)', 'Juan Palos', @IdCultivoGranada, 'COM4', 18.00, 6.00, 1);
GO

-- F. Dispositivos (ESP32 de Prueba enlazados a Sectores)
DECLARE @IdSectorOlivo INT, @IdSectorGranada INT;
SELECT @IdSectorOlivo = IdSector FROM dbo.Sectores WHERE NombreSector = 'Sector 1 - Olivo Joven (Pozo 5)';
SELECT @IdSectorGranada = IdSector FROM dbo.Sectores WHERE NombreSector = 'Sector 2 - Granada Exportación (Pozo 9)';

INSERT INTO dbo.Dispositivos (CodigoMAC, IdSector, FechaInstalacion, Estado)
VALUES ('AA:BB:CC:11:22:33', @IdSectorOlivo, GETDATE(), 'Activo');

INSERT INTO dbo.Dispositivos (CodigoMAC, IdSector, FechaInstalacion, Estado)
VALUES ('DD:EE:FF:44:55:66', @IdSectorGranada, GETDATE(), 'Activo');
GO

-- G. Datos Históricos Básicos para validación de reportes
DECLARE @IdDispositivoOlivo INT, @IdDispositivoGranada INT;
DECLARE @IdSectorOlivo INT, @IdSectorGranada INT;

SELECT @IdDispositivoOlivo = IdDispositivo FROM dbo.Dispositivos WHERE CodigoMAC = 'AA:BB:CC:11:22:33';
SELECT @IdDispositivoGranada = IdDispositivo FROM dbo.Dispositivos WHERE CodigoMAC = 'DD:EE:FF:44:55:66';

SELECT @IdSectorOlivo = IdSector FROM dbo.Sectores WHERE NombreSector = 'Sector 1 - Olivo Joven (Pozo 5)';
SELECT @IdSectorGranada = IdSector FROM dbo.Sectores WHERE NombreSector = 'Sector 2 - Granada Exportación (Pozo 9)';

-- Telemetría de ejemplo
INSERT INTO dbo.HistorialLecturas (IdDispositivo, Humedad, Temperatura, FechaHora, EstadoSuelo)
VALUES (@IdDispositivoOlivo, 22.50, 31.20, DATEADD(HOUR, -5, GETDATE()), 'Crítico - Seco');

INSERT INTO dbo.HistorialLecturas (IdDispositivo, Humedad, Temperatura, FechaHora, EstadoSuelo)
VALUES (@IdDispositivoOlivo, 28.10, 29.80, DATEADD(HOUR, -2, GETDATE()), 'Óptimo');

INSERT INTO dbo.HistorialLecturas (IdDispositivo, Humedad, Temperatura, FechaHora, EstadoSuelo)
VALUES (@IdDispositivoGranada, 32.40, 30.50, DATEADD(HOUR, -1, GETDATE()), 'Óptimo');

-- Riegos de ejemplo:
-- Sector 1: 180 minutos (3 horas). Consumo: 180 min * 4.5 LPM = 810 litros.
INSERT INTO dbo.HistorialRiego (IdSector, FechaHoraInicio, FechaHoraFin, LitrosConsumidos)
VALUES (@IdSectorOlivo, DATEADD(HOUR, -6, GETDATE()), DATEADD(MINUTE, -180, DATEADD(HOUR, -6, GETDATE())), 810.00);

-- Sector 2: 150 minutos (2.5 horas). Consumo: 150 min * 6.0 LPM = 900 litros.
INSERT INTO dbo.HistorialRiego (IdSector, FechaHoraInicio, FechaHoraFin, LitrosConsumidos)
VALUES (@IdSectorGranada, DATEADD(HOUR, -4, GETDATE()), DATEADD(MINUTE, -150, DATEADD(HOUR, -4, GETDATE())), 900.00);

-- Alerta de anomalía activa de ejemplo
INSERT INTO dbo.AlertasAnomalias (IdDispositivo, TipoAnomalia, Descripcion, FechaHora, Solucionado)
VALUES (@IdDispositivoOlivo, 'Posible Fuga', 'Humedad subió anormalmente un 15% en 5 minutos sin riego activo en la zona.', DATEADD(HOUR, -3, GETDATE()), 0);
GO

PRINT 'SCRIPT 1: Arquitectura avanzada con cálculo de volumen de agua y bomba instalada exitosamente.';
GO
