USE [aclicomunicazioni_sviluppo];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.RilevazioniChilometriche', N'U') IS NOT NULL
BEGIN
    PRINT 'La tabella dbo.RilevazioniChilometriche esiste già. Nessuna modifica eseguita.';
    RETURN;
END;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    CREATE TABLE dbo.RilevazioniChilometriche
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_RilevazioniChilometriche PRIMARY KEY,

        IdUtente INT NOT NULL,

        Chilometri INT NOT NULL,

        DataRilevazione DATETIME2(0) NOT NULL,

        DataCreazione DATETIME2(0) NOT NULL
            CONSTRAINT DF_RilevazioniChilometriche_DataCreazione
            DEFAULT SYSUTCDATETIME(),

        CONSTRAINT CK_RilevazioniChilometriche_Chilometri
            CHECK (Chilometri > 0),

        CONSTRAINT FK_RilevazioniChilometriche_Utenti
            FOREIGN KEY (IdUtente)
            REFERENCES dbo.Utenti(ID_utente)
    );

    CREATE INDEX IX_RilevazioniChilometriche_IdUtente_DataRilevazione
        ON dbo.RilevazioniChilometriche
        (
            IdUtente,
            DataRilevazione DESC,
            Id DESC
        );

    COMMIT TRANSACTION;

    PRINT 'Tabella dbo.RilevazioniChilometriche creata correttamente.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO
