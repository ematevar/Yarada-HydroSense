-- =================================================================================
-- SCRIPT 2: PROCEDIMIENTOS ALMACENADOS AVANZADOS (Reportes y Operaciones)
-- Sistema Comercial de Riego de Precisión IoT (Tacna - La Yarada Los Palos)
-- DBA & Database Architect: Versión Comercial Avanzada con Control de Bomba y Volumen
-- =================================================================================

USE RiegoPrecisionDB;
GO

-- Limpieza de procedimientos previos en caso de re-ejecución
IF OBJECT_ID('dbo.sp_AutenticarUsuario', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_AutenticarUsuario;
IF OBJECT_ID('dbo.sp_RegistrarLectura', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_RegistrarLectura;
IF OBJECT_ID('dbo.sp_RegistrarMonitoreo', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_RegistrarMonitoreo;
IF OBJECT_ID('dbo.sp_CrearOperador', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_CrearOperador;
IF OBJECT_ID('dbo.sp_ModificarOperador', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ModificarOperador;
IF OBJECT_ID('dbo.sp_EliminarOperador', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_EliminarOperador;
IF OBJECT_ID('dbo.sp_ListarOperadores', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ListarOperadores;
IF OBJECT_ID('dbo.sp_ReporteAgroClimatico', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ReporteAgroClimatico;
IF OBJECT_ID('dbo.sp_ReporteCostosYConsumo', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ReporteCostosYConsumo;
IF OBJECT_ID('dbo.sp_ReporteAnomaliasYFugas', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ReporteAnomaliasYFugas;
IF OBJECT_ID('dbo.sp_ListarSectores', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ListarSectores;
IF OBJECT_ID('dbo.sp_ListarTodosLosSectores', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ListarTodosLosSectores;
IF OBJECT_ID('dbo.sp_CrearSector', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_CrearSector;
IF OBJECT_ID('dbo.sp_ModificarSector', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ModificarSector;
IF OBJECT_ID('dbo.sp_EliminarSector', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_EliminarSector;
IF OBJECT_ID('dbo.sp_RegistrarLecturaPorPuerto', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_RegistrarLecturaPorPuerto;
GO

-- =================================================================================
-- 1. MÓDULO DE AUTENTICACIÓN
-- =================================================================================
CREATE PROCEDURE dbo.sp_AutenticarUsuario
    @Username VARCHAR(50),
    @PasswordTextoPlano VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PasswordHashHex VARCHAR(256);
    -- Generar SHA-256 en formato hex compatible con lo guardado en la base de datos
    SET @PasswordHashHex = CONVERT(VARCHAR(256), HASHBYTES('SHA2_256', @PasswordTextoPlano), 2);

    SELECT 
        u.IdUsuario,
        u.Username,
        u.Nombre,
        r.NombreRol
    FROM dbo.Usuarios u
    INNER JOIN dbo.Roles r ON u.IdRol = r.IdRol
    WHERE u.Username = @Username
      AND u.PasswordHash = @PasswordHashHex
      AND u.Activo = 1;
END
GO

-- =================================================================================
-- 2. MÓDULO DE TELEMETRÍA IOT (Llamado por el microcontrolador o pasarela IoT)
-- =================================================================================
CREATE PROCEDURE dbo.sp_RegistrarLectura
    @CodigoMAC VARCHAR(17),
    @Humedad DECIMAL(5,2),
    @Temperatura DECIMAL(5,2),
    @EstadoSuelo VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdDispositivo INT;

    SELECT @IdDispositivo = IdDispositivo
    FROM dbo.Dispositivos
    WHERE CodigoMAC = @CodigoMAC
      AND Estado = 'Activo';

    IF @IdDispositivo IS NOT NULL
    BEGIN
        INSERT INTO dbo.HistorialLecturas (IdDispositivo, Humedad, Temperatura, FechaHora, EstadoSuelo)
        VALUES (@IdDispositivo, @Humedad, @Temperatura, GETDATE(), @EstadoSuelo);
        
        -- Retorna confirmación con el identificador generado
        SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS IdLecturaRegistrada;
    END
    ELSE
    BEGIN
        DECLARE @ErrorMsg VARCHAR(200) = 'El dispositivo con dirección MAC ' + @CodigoMAC + ' no está registrado o se encuentra inactivo.';
        THROW 50001, @ErrorMsg, 1;
    END
END
GO

-- =================================================================================
-- 2.1. REGISTRO DE MONITOREO DIRECTO (Valor Bruto del ESP32)
-- =================================================================================
CREATE PROCEDURE dbo.sp_RegistrarMonitoreo
    @ValorBruto INT,
    @PorcentajeHumedad DECIMAL(5,2),
    @EstadoSuelo VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdDispositivo INT;

    -- Seleccionar el primer dispositivo activo registrado para asociar la lectura
    SELECT TOP 1 @IdDispositivo = IdDispositivo
    FROM dbo.Dispositivos
    WHERE Estado = 'Activo';

    IF @IdDispositivo IS NULL
    BEGIN
        SET @IdDispositivo = 1; -- Valor por defecto
    END

    -- Insertar en la tabla principal de telemetría (humedad calibrada y temperatura simulada a 0.0)
    INSERT INTO dbo.HistorialLecturas (IdDispositivo, Humedad, Temperatura, FechaHora, EstadoSuelo)
    VALUES (@IdDispositivo, @PorcentajeHumedad, 0.00, GETDATE(), @EstadoSuelo);
    
    -- Retorna confirmación con el identificador generado
    SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS IdLecturaRegistrada;
END
GO

-- =================================================================================
-- 3. MÓDULO CRUD OPERADORES (Gestión exclusiva para Administradores)
-- =================================================================================

-- 3.1. sp_CrearOperador
CREATE PROCEDURE dbo.sp_CrearOperador
    @Username VARCHAR(50),
    @PasswordTextoPlano VARCHAR(100),
    @Nombre VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    -- Obtener Id de Rol 'Operador'
    DECLARE @IdRolOperador INT;
    SELECT @IdRolOperador = IdRol FROM dbo.Roles WHERE NombreRol = 'Operador';

    -- Validar unicidad del Username
    IF EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Username = @Username)
    BEGIN
        THROW 50002, 'El nombre de usuario ya está registrado en el sistema.', 1;
    END

    -- Generar Hash de Contraseña
    DECLARE @PasswordHashHex VARCHAR(256);
    SET @PasswordHashHex = CONVERT(VARCHAR(256), HASHBYTES('SHA2_256', @PasswordTextoPlano), 2);

    -- Insertar operador activo por defecto
    INSERT INTO dbo.Usuarios (Username, PasswordHash, Nombre, IdRol, Activo, FechaCreacion)
    VALUES (@Username, @PasswordHashHex, @Nombre, @IdRolOperador, 1, GETDATE());

    -- Retorna el ID asignado al nuevo operador
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS IdUsuarioCreado;
END
GO

-- 3.2. sp_ModificarOperador
CREATE PROCEDURE dbo.sp_ModificarOperador
    @IdUsuario INT,
    @Username VARCHAR(50),
    @Nombre VARCHAR(100),
    @PasswordTextoPlano VARCHAR(100) = NULL, -- Parámetro opcional. Si se envía NULL, mantiene la contraseña actual.
    @Activo BIT = NULL                      -- Parámetro opcional. Si se envía NULL, mantiene el estado actual.
AS
BEGIN
    SET NOCOUNT ON;

    -- Validar que el usuario exista y sea Operador
    DECLARE @IdRolOperador INT;
    SELECT @IdRolOperador = IdRol FROM dbo.Roles WHERE NombreRol = 'Operador';

    IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE IdUsuario = @IdUsuario AND IdRol = @IdRolOperador)
    BEGIN
        THROW 50003, 'El usuario no existe o no cuenta con los privilegios de Operador.', 1;
    END

    -- Validar unicidad de username con otros usuarios
    IF EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Username = @Username AND IdUsuario <> @IdUsuario)
    BEGIN
        THROW 50004, 'El nombre de usuario ya está asignado a otra cuenta.', 1;
    END

    -- Actualización de datos generales
    UPDATE dbo.Usuarios
    SET Username = @Username,
        Nombre = @Nombre,
        Activo = ISNULL(@Activo, Activo)
    WHERE IdUsuario = @IdUsuario;

    -- Actualización condicional de contraseña si es provista
    IF @PasswordTextoPlano IS NOT NULL AND LTRIM(RTRIM(@PasswordTextoPlano)) <> ''
    BEGIN
        DECLARE @PasswordHashHex VARCHAR(256);
        SET @PasswordHashHex = CONVERT(VARCHAR(256), HASHBYTES('SHA2_256', @PasswordTextoPlano), 2);

        UPDATE dbo.Usuarios
        SET PasswordHash = @PasswordHashHex
        WHERE IdUsuario = @IdUsuario;
    END
END
GO

-- 3.3. sp_EliminarOperador (Baja Lógica para mantener consistencia referencial histórica)
CREATE PROCEDURE dbo.sp_EliminarOperador
    @IdUsuario INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdRolOperador INT;
    SELECT @IdRolOperador = IdRol FROM dbo.Roles WHERE NombreRol = 'Operador';

    IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE IdUsuario = @IdUsuario AND IdRol = @IdRolOperador)
    BEGIN
        THROW 50005, 'El usuario no existe o no tiene el rol de Operador.', 1;
    END

    -- Baja lógica desactivando la cuenta
    UPDATE dbo.Usuarios
    SET Activo = 0
    WHERE IdUsuario = @IdUsuario;
END
GO

-- 3.4. sp_ListarOperadores (Muestra todos los operadores registrados para administración)
CREATE PROCEDURE dbo.sp_ListarOperadores
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdRolOperador INT;
    SELECT @IdRolOperador = IdRol FROM dbo.Roles WHERE NombreRol = 'Operador';

    SELECT 
        IdUsuario,
        Username,
        Nombre,
        Activo,
        FechaCreacion
    FROM dbo.Usuarios
    WHERE IdRol = @IdRolOperador
    ORDER BY Nombre ASC;
END
GO


-- =================================================================================
-- 4. MÓDULO DE REPORTES MULTIFILTRO
-- =================================================================================

-- 4.1. sp_ReporteAgroClimatico (Telemetría de suelo con filtros opcionales de sector y fechas)
CREATE PROCEDURE dbo.sp_ReporteAgroClimatico
    @IdSector INT = NULL,
    @FechaInicio DATETIME = NULL,
    @FechaFin DATETIME = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        hl.IdLectura,
        s.NombreSector,
        c.NombreCultivo,
        d.CodigoMAC,
        hl.Humedad,
        hl.Temperatura,
        hl.FechaHora,
        hl.EstadoSuelo
    FROM dbo.HistorialLecturas hl
    INNER JOIN dbo.Dispositivos d ON hl.IdDispositivo = d.IdDispositivo
    INNER JOIN dbo.Sectores s ON d.IdSector = s.IdSector
    INNER JOIN dbo.Cultivos c ON s.IdCultivo = c.IdCultivo
    WHERE (@IdSector IS NULL OR s.IdSector = @IdSector)
      AND (@FechaInicio IS NULL OR hl.FechaHora >= @FechaInicio)
      AND (@FechaFin IS NULL OR hl.FechaHora <= @FechaFin)
    ORDER BY hl.FechaHora DESC;
END
GO

-- 4.2. sp_ReporteCostosYConsumo (Cálculo de horas y costos de riego por sector y tarifa)
CREATE PROCEDURE dbo.sp_ReporteCostosYConsumo
    @FechaInicio DATETIME = NULL,
    @FechaFin DATETIME = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        s.IdSector,
        s.NombreSector,
        c.NombreCultivo,
        s.TarifaAguaReferencial AS TarifaSolesHora,
        s.CaudalBombaLPM,
        SUM(ISNULL(hr.MinutosRiego, 0)) AS TotalMinutosRiego,
        CAST(SUM(ISNULL(hr.MinutosRiego, 0) / 60.0) AS DECIMAL(10,2)) AS TotalHorasRiego,
        -- Cálculo dinámico solicitado: Minutos de riego multiplicado por el CaudalBombaLPM del sector
        CAST(SUM(ISNULL(hr.MinutosRiego, 0) * s.CaudalBombaLPM) AS DECIMAL(10,2)) AS TotalLitrosConsumidos,
        -- Costo total en Soles basado en la tarifa horaria
        CAST(SUM((ISNULL(hr.MinutosRiego, 0) / 60.0) * s.TarifaAguaReferencial) AS DECIMAL(10,2)) AS CostoTotalAguaPEN
    FROM dbo.HistorialRiego hr
    INNER JOIN dbo.Sectores s ON hr.IdSector = s.IdSector
    INNER JOIN dbo.Cultivos c ON s.IdCultivo = c.IdCultivo
    WHERE (@FechaInicio IS NULL OR hr.FechaHoraInicio >= @FechaInicio)
      AND (@FechaFin IS NULL OR hr.FechaHoraFin <= @FechaFin OR (hr.FechaHoraFin IS NULL AND hr.FechaHoraInicio <= @FechaFin))
    GROUP BY s.IdSector, s.NombreSector, c.NombreCultivo, s.TarifaAguaReferencial, s.CaudalBombaLPM
    ORDER BY CostoTotalAguaPEN DESC;
END
GO

-- 4.3. sp_ReporteAnomaliasYFugas (Listado de anomalías IoT filtrado opcionalmente por Sector)
CREATE PROCEDURE dbo.sp_ReporteAnomaliasYFugas
    @IdSector INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        aa.IdAlerta,
        d.CodigoMAC,
        s.NombreSector,
        aa.TipoAnomalia,
        aa.Descripcion,
        aa.FechaHora,
        aa.Solucionado
    FROM dbo.AlertasAnomalias aa
    INNER JOIN dbo.Dispositivos d ON aa.IdDispositivo = d.IdDispositivo
    INNER JOIN dbo.Sectores s ON d.IdSector = s.IdSector
    WHERE (@IdSector IS NULL OR s.IdSector = @IdSector)
    ORDER BY aa.FechaHora DESC;
END
GO

PRINT 'SCRIPT 2: Procedimientos almacenados avanzados creados con éxito.';
GO
USE RiegoPrecisionDB;
GO

-- Eliminar el procedimiento si ya existiera
IF OBJECT_ID('dbo.sp_RegistrarMonitoreo', 'P') IS NOT NULL 
    DROP PROCEDURE dbo.sp_RegistrarMonitoreo;
GO

-- Crear el procedimiento para registrar las lecturas brutos calibradas
CREATE PROCEDURE dbo.sp_RegistrarMonitoreo
    @ValorBruto INT,
    @PorcentajeHumedad DECIMAL(5,2),
    @EstadoSuelo VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdDispositivo INT;

    -- Buscar el primer dispositivo activo registrado en la base de datos
    SELECT TOP 1 @IdDispositivo = IdDispositivo
    FROM dbo.Dispositivos
    WHERE Estado = 'Activo';

    -- Si no existe ninguno asigna el ID 1 por defecto
    IF @IdDispositivo IS NULL
    BEGIN
        SET @IdDispositivo = 1; 
    END

    -- Insertar la lectura en la tabla con temperatura en 0.0
    INSERT INTO dbo.HistorialLecturas (IdDispositivo, Humedad, Temperatura, FechaHora, EstadoSuelo)
    VALUES (@IdDispositivo, @PorcentajeHumedad, 0.00, GETDATE(), @EstadoSuelo);
    
    -- Retornar el ID del registro creado
    SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS IdLecturaRegistrada;
END
GO

-- =================================================================================
-- 5. MÓDULO CRUD SECTORES Y TELEMETRÍA MULTIPUERTO
-- =================================================================================

-- 5.1. sp_ListarSectores (Solo sectores activos)
CREATE PROCEDURE dbo.sp_ListarSectores
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        s.IdSector,
        s.NombreSector,
        s.NombreEncargado,
        s.IdCultivo,
        c.NombreCultivo,
        s.PuertoSerial,
        s.TarifaAguaReferencial,
        s.CaudalBombaLPM,
        s.Activo
    FROM dbo.Sectores s
    INNER JOIN dbo.Cultivos c ON s.IdCultivo = c.IdCultivo
    WHERE s.Activo = 1
    ORDER BY s.NombreSector ASC;
END
GO

-- 5.2. sp_ListarTodosLosSectores (Para el CRUD del Administrador, incluye inactivos)
CREATE PROCEDURE dbo.sp_ListarTodosLosSectores
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        s.IdSector,
        s.NombreSector,
        s.NombreEncargado,
        s.IdCultivo,
        c.NombreCultivo,
        s.PuertoSerial,
        s.TarifaAguaReferencial,
        s.CaudalBombaLPM,
        s.Activo
    FROM dbo.Sectores s
    INNER JOIN dbo.Cultivos c ON s.IdCultivo = c.IdCultivo
    ORDER BY s.NombreSector ASC;
END
GO

-- 5.3. sp_CrearSector
CREATE PROCEDURE dbo.sp_CrearSector
    @NombreSector VARCHAR(100),
    @NombreEncargado VARCHAR(100),
    @IdCultivo INT,
    @PuertoSerial VARCHAR(20),
    @TarifaAguaReferencial DECIMAL(10,2),
    @CaudalBombaLPM DECIMAL(5,2)
AS
BEGIN
    SET NOCOUNT ON;
    
    INSERT INTO dbo.Sectores (NombreSector, NombreEncargado, IdCultivo, PuertoSerial, TarifaAguaReferencial, CaudalBombaLPM, Activo)
    VALUES (@NombreSector, @NombreEncargado, @IdCultivo, @PuertoSerial, @TarifaAguaReferencial, @CaudalBombaLPM, 1);
    
    DECLARE @NewIdSector INT = SCOPE_IDENTITY();
    
    -- Insertar un dispositivo por defecto asociado al sector
    INSERT INTO dbo.Dispositivos (CodigoMAC, IdSector, Estado)
    VALUES ('DEV-' + @PuertoSerial, @NewIdSector, 'Activo');

    SELECT @NewIdSector AS IdSectorCreado;
END
GO

-- 5.4. sp_ModificarSector
CREATE PROCEDURE dbo.sp_ModificarSector
    @IdSector INT,
    @NombreSector VARCHAR(100),
    @NombreEncargado VARCHAR(100),
    @IdCultivo INT,
    @PuertoSerial VARCHAR(20),
    @TarifaAguaReferencial DECIMAL(10,2),
    @CaudalBombaLPM DECIMAL(5,2),
    @Activo BIT
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE dbo.Sectores
    SET NombreSector = @NombreSector,
        NombreEncargado = @NombreEncargado,
        IdCultivo = @IdCultivo,
        PuertoSerial = @PuertoSerial,
        TarifaAguaReferencial = @TarifaAguaReferencial,
        CaudalBombaLPM = @CaudalBombaLPM,
        Activo = @Activo
    WHERE IdSector = @IdSector;
END
GO

-- 5.5. sp_EliminarSector (Baja lógica)
CREATE PROCEDURE dbo.sp_EliminarSector
    @IdSector INT
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE dbo.Sectores
    SET Activo = 0
    WHERE IdSector = @IdSector;
END
GO

-- 5.6. sp_RegistrarLecturaPorPuerto (Registra lectura por el nombre del puerto)
CREATE PROCEDURE dbo.sp_RegistrarLecturaPorPuerto
    @PuertoSerial VARCHAR(20),
    @Humedad DECIMAL(5,2),
    @Temperatura DECIMAL(5,2),
    @EstadoSuelo VARCHAR(50)
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
        INSERT INTO dbo.HistorialLecturas (IdDispositivo, Humedad, Temperatura, FechaHora, EstadoSuelo)
        VALUES (@IdDispositivo, @Humedad, @Temperatura, GETDATE(), @EstadoSuelo);
        
        SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS IdLecturaRegistrada;
    END
    ELSE
    BEGIN
        THROW 50006, 'No se pudo asociar la lectura a ningún sector activo para el puerto serial dado.', 1;
    END
END
GO