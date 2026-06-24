-- =================================================================================
-- SCRIPT 3: MIGRACIÓN DE DATOS E INTELIGENCIA AGRÍCOLA
-- =================================================================================

USE RiegoPrecisionDB;
GO

-- 1. Modificar tabla Cultivos para agregar CoeficienteKc
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Cultivos') AND name = 'CoeficienteKc')
BEGIN
    ALTER TABLE dbo.Cultivos ADD CoeficienteKc DECIMAL(5,2) NOT NULL CONSTRAINT DF_Cultivos_Kc DEFAULT 0.60;
END
GO

-- Actualizar coeficientes de cultivo y agregar Zapallo
UPDATE dbo.Cultivos SET CoeficienteKc = 0.60 WHERE NombreCultivo = 'Olivo';
UPDATE dbo.Cultivos SET CoeficienteKc = 0.75 WHERE NombreCultivo = 'Granada';

IF NOT EXISTS (SELECT 1 FROM dbo.Cultivos WHERE NombreCultivo = 'Zapallo')
BEGIN
    INSERT INTO dbo.Cultivos (NombreCultivo, HumedadMinima, TemperaturaMaxima, CoeficienteKc)
    VALUES ('Zapallo', 30.00, 32.00, 0.85);
END
GO

-- 2. Modificar tabla HistorialLecturas para agregar PendienteEvaporacion y TiempoLimiteEstimado
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.HistorialLecturas') AND name = 'PendienteEvaporacion')
BEGIN
    ALTER TABLE dbo.HistorialLecturas ADD PendienteEvaporacion DECIMAL(10,5) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.HistorialLecturas') AND name = 'TiempoLimiteEstimado')
BEGIN
    ALTER TABLE dbo.HistorialLecturas ADD TiempoLimiteEstimado INT NULL;
END
GO

-- 3. Modificar Stored Procedure de registro de lectura por puerto
IF OBJECT_ID('dbo.sp_RegistrarLecturaPorPuerto', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.sp_RegistrarLecturaPorPuerto;
END
GO

CREATE PROCEDURE dbo.sp_RegistrarLecturaPorPuerto
    @PuertoSerial VARCHAR(20),
    @Humedad DECIMAL(5,2),
    @Temperatura DECIMAL(5,2),
    @EstadoSuelo VARCHAR(50),
    @PendienteEvaporacion DECIMAL(10,5) = NULL,
    @TiempoLimiteEstimado INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdDispositivo INT;

    -- Intentar buscar dispositivo activo para el sector con este puerto serial
    SELECT TOP 1 @IdDispositivo = d.IdDispositivo
    FROM dbo.Dispositivos d
    INNER JOIN dbo.Sectores s ON d.IdSector = s.IdSector
    WHERE s.PuertoSerial = @PuertoSerial
      AND d.Estado = 'Activo'
      AND s.Activo = 1;

    -- Si no existe, buscar cualquier dispositivo del sector
    IF @IdDispositivo IS NULL
    BEGIN
        SELECT TOP 1 @IdDispositivo = d.IdDispositivo
        FROM dbo.Dispositivos d
        INNER JOIN dbo.Sectores s ON d.IdSector = s.IdSector
        WHERE s.PuertoSerial = @PuertoSerial;
    END

    -- Si aún no existe, registrar el dispositivo dinámicamente
    IF @IdDispositivo IS NULL
    BEGIN
        DECLARE @IdSector INT;
        SELECT TOP 1 @IdSector = IdSector FROM dbo.Sectores WHERE PuertoSerial = @PuertoSerial AND Activo = 1;
        
        IF @IdSector IS NOT NULL
        BEGIN
            INSERT INTO dbo.Dispositivos (CodigoMAC, IdSector, Estado)
            VALUES ('DEV-' + @PuertoSerial, @IdSector, 'Activo');
            SET @IdDispositivo = SCOPE_IDENTITY();
        END
    END

    -- Registrar la lectura física
    IF @IdDispositivo IS NOT NULL
    BEGIN
        INSERT INTO dbo.HistorialLecturas (IdDispositivo, Humedad, Temperatura, FechaHora, EstadoSuelo, PendienteEvaporacion, TiempoLimiteEstimado)
        VALUES (@IdDispositivo, @Humedad, @Temperatura, GETDATE(), @EstadoSuelo, @PendienteEvaporacion, @TiempoLimiteEstimado);
        
        SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS IdLecturaRegistrada;
    END
    ELSE
    BEGIN
        THROW 50006, 'No se pudo asociar la lectura a ningún sector activo para el puerto serial dado.', 1;
    END
END
GO
