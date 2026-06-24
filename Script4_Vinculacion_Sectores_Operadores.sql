-- =================================================================================
-- SCRIPT DE MODIFICACIÓN DE BASE DE DATOS: VINCULACIÓN DINÁMICA DE OPERADORES A SECTORES
-- =================================================================================

USE RiegoPrecisionDB;
GO

-- 1. AGREGAR COLUMNA IdSector Y RELACIÓN DE LLAVE FORÁNEA A LA TABLA Usuarios
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID('dbo.Usuarios') AND name = 'IdSector'
)
BEGIN
    ALTER TABLE dbo.Usuarios ADD IdSector INT NULL;
    ALTER TABLE dbo.Usuarios ADD CONSTRAINT FK_Usuarios_Sectores FOREIGN KEY (IdSector) REFERENCES dbo.Sectores(IdSector);
    PRINT 'Columna IdSector y relacion de FK creadas en dbo.Usuarios.';
END
ELSE
BEGIN
    PRINT 'La columna IdSector ya existe en dbo.Usuarios.';
END
GO

-- 2. MODIFICAR PROCEDIMIENTO ALMACENADO sp_ListarOperadores
-- Retorna el IdSector y el NombreSector asignado mediante LEFT JOIN
CREATE OR ALTER PROCEDURE dbo.sp_ListarOperadores
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdRolOperador INT;
    SELECT @IdRolOperador = IdRol FROM dbo.Roles WHERE NombreRol = 'Operador';

    SELECT 
        u.IdUsuario,
        u.Username,
        u.Nombre,
        u.Celular,
        u.IdSector,
        s.NombreSector,
        u.Activo,
        u.FechaCreacion
    FROM dbo.Usuarios u
    LEFT JOIN dbo.Sectores s ON u.IdSector = s.IdSector
    WHERE u.IdRol = @IdRolOperador
    ORDER BY u.Nombre ASC;
END;
GO

-- 3. MODIFICAR PROCEDIMIENTO ALMACENADO sp_CrearOperador
-- Permite guardar la asignacion de sector al crear un operador
CREATE OR ALTER PROCEDURE dbo.sp_CrearOperador
    @Username VARCHAR(50),
    @PasswordTextoPlano VARCHAR(100),
    @Nombre VARCHAR(100),
    @Celular VARCHAR(20) = NULL,
    @IdSector INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdRolOperador INT;
    SELECT @IdRolOperador = IdRol FROM dbo.Roles WHERE NombreRol = 'Operador';

    IF EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Username = @Username)
    BEGIN
        THROW 50002, 'El nombre de usuario ya esta registrado en el sistema.', 1;
    END

    DECLARE @PasswordHashHex VARCHAR(256);
    SET @PasswordHashHex = CONVERT(VARCHAR(256), HASHBYTES('SHA2_256', @PasswordTextoPlano), 2);

    INSERT INTO dbo.Usuarios (Username, PasswordHash, Nombre, Celular, IdSector, IdRol, Activo, FechaCreacion)
    VALUES (@Username, @PasswordHashHex, @Nombre, @Celular, @IdSector, @IdRolOperador, 1, GETDATE());

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS IdUsuarioCreado;
END;
GO

-- 4. MODIFICAR PROCEDIMIENTO ALMACENADO sp_ModificarOperador
-- Permite modificar la asignacion de sector y el resto de datos del operador
CREATE OR ALTER PROCEDURE dbo.sp_ModificarOperador
    @IdUsuario INT,
    @Username VARCHAR(50),
    @Nombre VARCHAR(100),
    @Celular VARCHAR(20) = NULL,
    @IdSector INT = NULL,
    @PasswordTextoPlano VARCHAR(100) = NULL,
    @Activo BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdRolOperador INT;
    SELECT @IdRolOperador = IdRol FROM dbo.Roles WHERE NombreRol = 'Operador';

    IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE IdUsuario = @IdUsuario AND IdRol = @IdRolOperador)
    BEGIN
        THROW 50003, 'El usuario no existe o no cuenta con los privilegios de Operador.', 1;
    END

    IF EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Username = @Username AND IdUsuario <> @IdUsuario)
    BEGIN
        THROW 50004, 'El nombre de usuario ya esta asignado a otra cuenta.', 1;
    END

    UPDATE dbo.Usuarios
    SET Username = @Username,
        Nombre = @Nombre,
        Celular = @Celular,
        IdSector = @IdSector,
        Activo = ISNULL(@Activo, Activo)
    WHERE IdUsuario = @IdUsuario;

    IF @PasswordTextoPlano IS NOT NULL AND LTRIM(RTRIM(@PasswordTextoPlano)) <> ''
    BEGIN
        DECLARE @PasswordHashHex VARCHAR(256);
        SET @PasswordHashHex = CONVERT(VARCHAR(256), HASHBYTES('SHA2_256', @PasswordTextoPlano), 2);

        UPDATE dbo.Usuarios
        SET PasswordHash = @PasswordHashHex
        WHERE IdUsuario = @IdUsuario;
    END
END;
GO
