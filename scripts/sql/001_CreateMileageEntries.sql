USE [aclicomunicazioni_sviluppo];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.MileageEntries', N'U') IS NOT NULL
BEGIN
    PRINT 'La tabella dbo.MileageEntries esiste già. Nessuna modifica eseguita.';
    RETURN;
END;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    CREATE TABLE dbo.MileageEntries
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_MileageEntries PRIMARY KEY,

        UserId INT NOT NULL,

        Kilometers INT NOT NULL,

        ReadingDate DATETIME2(0) NOT NULL,

        CreatedAt DATETIME2(0) NOT NULL
            CONSTRAINT DF_MileageEntries_CreatedAt
            DEFAULT SYSUTCDATETIME(),

        CONSTRAINT CK_MileageEntries_Kilometers
            CHECK (Kilometers > 0),

        CONSTRAINT FK_MileageEntries_Utenti
            FOREIGN KEY (UserId)
            REFERENCES dbo.Utenti(ID_utente)
    );

    CREATE INDEX IX_MileageEntries_UserId_ReadingDate
        ON dbo.MileageEntries
        (
            UserId,
            ReadingDate DESC,
            Id DESC
        );

    COMMIT TRANSACTION;

    PRINT 'Tabella dbo.MileageEntries creata correttamente.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO
