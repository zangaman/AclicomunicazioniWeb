USE [aclicomunicazioni_sviluppo];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.Percorrenze', N'IsDeleted') IS NULL
    BEGIN
        ALTER TABLE dbo.Percorrenze
        ADD IsDeleted BIT NOT NULL
            CONSTRAINT DF_Percorrenze_IsDeleted DEFAULT (0);
    END;

    IF COL_LENGTH(N'dbo.Percorrenze', N'DeletedAt') IS NULL
    BEGIN
        ALTER TABLE dbo.Percorrenze
        ADD DeletedAt DATETIME2(0) NULL;
    END;

    IF COL_LENGTH(N'dbo.Percorrenze', N'DeletedBy') IS NULL
    BEGIN
        ALTER TABLE dbo.Percorrenze
        ADD DeletedBy NVARCHAR(150) NULL;
    END;

    COMMIT TRANSACTION;
    PRINT 'Soft delete aggiunto a dbo.Percorrenze.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO
