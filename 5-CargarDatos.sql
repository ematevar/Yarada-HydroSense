-- =================================================================================
-- SCRIPT DE GENERACIÓN MASIVA DE HISTORIAL (DATA SEEDING)
-- Simulación de 30 días de telemetría, riegos y alertas para pruebas de carga y reportes
-- =================================================================================

USE RiegoPrecisionDB;
GO

SET NOCOUNT ON;
PRINT 'Iniciando generación masiva de datos... Esto puede tomar unos segundos.';

-- 1. IDENTIFICAR DISPOSITIVOS Y SECTORES EXISTENTES
DECLARE @IdDispOlivo INT, @IdDispGranada INT;
DECLARE @IdSectOlivo INT, @IdSectGranada INT;
DECLARE @CaudalOlivo DECIMAL(5,2), @CaudalGranada DECIMAL(5,2);

SELECT TOP 1 @IdDispOlivo = IdDispositivo, @IdSectOlivo = IdSector FROM dbo.Dispositivos WHERE CodigoMAC = 'AA:BB:CC:11:22:33';
SELECT TOP 1 @IdDispGranada = IdDispositivo, @IdSectGranada = IdSector FROM dbo.Dispositivos WHERE CodigoMAC = 'DD:EE:FF:44:55:66';

SELECT @CaudalOlivo = CaudalBombaLPM FROM dbo.Sectores WHERE IdSector = @IdSectOlivo;
SELECT @CaudalGranada = CaudalBombaLPM FROM dbo.Sectores WHERE IdSector = @IdSectGranada;

-- Validar que existan los dispositivos semilla
IF @IdDispOlivo IS NULL OR @IdDispGranada IS NULL
BEGIN
    PRINT 'ERROR: No se encontraron los dispositivos base (Ejecuta el Script 1 primero).';
    RETURN;
END

-- 2. CONFIGURACIÓN DEL BUCLE DE TIEMPO
DECLARE @FechaInicio DATETIME = DATEADD(DAY, -30, GETDATE()); -- Hace 30 días
DECLARE @FechaFin DATETIME = GETDATE();
DECLARE @FechaActual DATETIME = @FechaInicio;

-- Variables de simulación ambiental
DECLARE @HumedadOlivo DECIMAL(5,2) = 65.00;
DECLARE @HumedadGranada DECIMAL(5,2) = 70.00;
DECLARE @TempActual DECIMAL(5,2);
DECLARE @Hora INT;
DECLARE @Pendiente DECIMAL(10,5);
DECLARE @EstadoSuelo VARCHAR(50);
DECLARE @MinutosRiego INT;

-- 3. BUCLE PRINCIPAL (Avanza hora por hora)
WHILE @FechaActual <= @FechaFin
BEGIN
    SET @Hora = DATEPART(HOUR, @FechaActual);

    -- Simular ciclo de temperatura día/noche (La Yarada: Frío de noche, calor de día)
    IF @Hora BETWEEN 0 AND 5
        SET @TempActual = 14.0 + (RAND(CHECKSUM(NEWID())) * 4.0); -- 14°C a 18°C
    ELSE IF @Hora BETWEEN 6 AND 11
        SET @TempActual = 18.0 + (RAND(CHECKSUM(NEWID())) * 10.0); -- 18°C a 28°C
    ELSE IF @Hora BETWEEN 12 AND 16
        SET @TempActual = 25.0 + (RAND(CHECKSUM(NEWID())) * 8.0); -- 25°C a 33°C
    ELSE
        SET @TempActual = 16.0 + (RAND(CHECKSUM(NEWID())) * 8.0); -- 16°C a 24°C

    -- Evaporación: Baja más rápido si hace más calor
    SET @Pendiente = (@TempActual / 100.0) + (RAND(CHECKSUM(NEWID())) * 0.5);
    SET @HumedadOlivo = @HumedadOlivo - @Pendiente;
    SET @HumedadGranada = @HumedadGranada - (@Pendiente * 1.2); -- La granada evapora un poco distinto

    -- ====================================================
    -- LOGICA SECTOR 1: OLIVO
    -- ====================================================
    -- Determinar estado del suelo
    IF @HumedadOlivo > 45.0 SET @EstadoSuelo = 'Óptimo';
    ELSE IF @HumedadOlivo > 25.0 SET @EstadoSuelo = 'Atención - Disminuyendo';
    ELSE SET @EstadoSuelo = 'Crítico - Seco';

    -- Insertar Lectura Olivo
    INSERT INTO dbo.HistorialLecturas (IdDispositivo, Humedad, Temperatura, FechaHora, EstadoSuelo, PendienteEvaporacion, TiempoLimiteEstimado)
    VALUES (@IdDispOlivo, @HumedadOlivo, @TempActual, @FechaActual, @EstadoSuelo, @Pendiente, CASE WHEN @HumedadOlivo < 30 THEN 120 ELSE 500 END);

    -- Simular Riego si baja de la humedad mínima (25%)
    IF @HumedadOlivo <= 25.0
    BEGIN
        SET @MinutosRiego = 120 + CAST(RAND(CHECKSUM(NEWID())) * 60 AS INT); -- Riego de 2 a 3 horas
        
        INSERT INTO dbo.HistorialRiego (IdSector, FechaHoraInicio, FechaHoraFin, LitrosConsumidos)
        VALUES (@IdSectOlivo, @FechaActual, DATEADD(MINUTE, @MinutosRiego, @FechaActual), @MinutosRiego * @CaudalOlivo);
        
        -- Recuperar humedad después del riego
        SET @HumedadOlivo = 75.0 + (RAND(CHECKSUM(NEWID())) * 10.0);
    END

    -- ====================================================
    -- LOGICA SECTOR 2: GRANADA
    -- ====================================================
    IF @HumedadGranada > 50.0 SET @EstadoSuelo = 'Óptimo';
    ELSE IF @HumedadGranada > 30.0 SET @EstadoSuelo = 'Atención - Disminuyendo';
    ELSE SET @EstadoSuelo = 'Crítico - Seco';

    -- Insertar Lectura Granada
    INSERT INTO dbo.HistorialLecturas (IdDispositivo, Humedad, Temperatura, FechaHora, EstadoSuelo, PendienteEvaporacion, TiempoLimiteEstimado)
    VALUES (@IdDispGranada, @HumedadGranada, @TempActual + 1.5, @FechaActual, @EstadoSuelo, @Pendiente * 1.1, CASE WHEN @HumedadGranada < 35 THEN 90 ELSE 400 END);

    -- Simular Riego si baja de la humedad mínima (30%)
    IF @HumedadGranada <= 30.0
    BEGIN
        SET @MinutosRiego = 90 + CAST(RAND(CHECKSUM(NEWID())) * 45 AS INT); -- Riego de 1.5 a 2 horas aprox
        
        INSERT INTO dbo.HistorialRiego (IdSector, FechaHoraInicio, FechaHoraFin, LitrosConsumidos)
        VALUES (@IdSectGranada, @FechaActual, DATEADD(MINUTE, @MinutosRiego, @FechaActual), @MinutosRiego * @CaudalGranada);
        
        -- Recuperar humedad
        SET @HumedadGranada = 80.0 + (RAND(CHECKSUM(NEWID())) * 5.0);
    END

    -- ====================================================
    -- GENERACIÓN DE ANOMALÍAS ALEATORIAS (1% de probabilidad por hora)
    -- ====================================================
    DECLARE @RandomAlert INT = CAST(RAND(CHECKSUM(NEWID())) * 100 AS INT);
    IF @RandomAlert = 1 
    BEGIN
        INSERT INTO dbo.AlertasAnomalias (IdDispositivo, TipoAnomalia, Descripcion, FechaHora, Solucionado)
        VALUES (@IdDispOlivo, 'Estrés Térmico', 'Temperatura superior a los umbrales seguros durante exposición solar prolongada.', @FechaActual, 1);
    END
    ELSE IF @RandomAlert = 2
    BEGIN
        INSERT INTO dbo.AlertasAnomalias (IdDispositivo, TipoAnomalia, Descripcion, FechaHora, Solucionado)
        VALUES (@IdDispGranada, 'Posible Fuga', 'Caída inusual de presión o humedad anómala detectada en sector periférico.', @FechaActual, 0);
    END

    -- Avanzar 1 hora
    SET @FechaActual = DATEADD(HOUR, 1, @FechaActual);
END

PRINT 'Generación completada.';
PRINT 'Se han insertado aproximadamente 1440 lecturas por sensor, simulando 30 días de operación.';

-- 4. VERIFICAR RESULTADOS RÁPIDAMENTE
SELECT 'Lecturas Generadas' AS Tabla, COUNT(*) AS TotalRegistros FROM dbo.HistorialLecturas;
SELECT 'Riegos Ejecutados' AS Tabla, COUNT(*) AS TotalRegistros FROM dbo.HistorialRiego;
SELECT 'Anomalías Detectadas' AS Tabla, COUNT(*) AS TotalRegistros FROM dbo.AlertasAnomalias;
GO