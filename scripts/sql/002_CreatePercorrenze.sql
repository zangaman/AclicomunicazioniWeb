USE [aclicomunicazioni_sviluppo];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.Percorrenze', N'U') IS NOT NULL
BEGIN
    PRINT 'La tabella dbo.Percorrenze esiste già. Nessuna modifica eseguita.';
    RETURN;
END;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    CREATE TABLE dbo.Percorrenze
    (
        Id INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Percorrenze PRIMARY KEY,

        IdUtente INT NOT NULL,
        DataPercorrenza DATE NOT NULL,
        KmPartenza INT NOT NULL,
        KmArrivo INT NOT NULL,

        KmPercorsi AS (KmArrivo - KmPartenza) PERSISTED,

        Tragitto NVARCHAR(200) NOT NULL,
        Descrizione NVARCHAR(500) NULL,

        DataCreazione DATETIME2(0) NOT NULL
            CONSTRAINT DF_Percorrenze_DataCreazione
            DEFAULT SYSUTCDATETIME(),

        CONSTRAINT FK_Percorrenze_Utenti
            FOREIGN KEY (IdUtente)
            REFERENCES dbo.Utenti(ID_utente),

        CONSTRAINT CK_Percorrenze_KmPartenza
            CHECK (KmPartenza >= 0),

        CONSTRAINT CK_Percorrenze_KmArrivo
            CHECK (KmArrivo >= KmPartenza),

        CONSTRAINT CK_Percorrenze_Tragitto
            CHECK (LEN(LTRIM(RTRIM(Tragitto))) > 0)
    );

    CREATE INDEX IX_Percorrenze_IdUtente_DataPercorrenza
        ON dbo.Percorrenze
        (
            IdUtente,
            DataPercorrenza DESC,
            Id DESC
        );

    COMMIT TRANSACTION;

    PRINT 'Tabella dbo.Percorrenze creata correttamente.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
GO
