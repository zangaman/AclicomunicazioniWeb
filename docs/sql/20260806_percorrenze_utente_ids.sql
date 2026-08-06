/* =============================================================
   ACLI COMUNICAZIONI — TRACCIAMENTO UTENTI PERCORRENZE
   Eseguire una sola volta sul database aclicomunicazioni_sviluppo.

   Obiettivo:
   - memorizzare gli ID utente, non nomi e cognomi, nelle nuove righe;
   - conservare i campi testuali esistenti solo come storico/fallback;
   - esporre i nomi tramite dbo.vw_PercorrenzeConUtenti.
   ============================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH('dbo.Percorrenze', 'InseritoDaUserId') IS NULL
    BEGIN
        ALTER TABLE dbo.Percorrenze
            ADD InseritoDaUserId INT NULL;
    END;

    IF COL_LENGTH('dbo.Percorrenze', 'DeletedByUserId') IS NULL
    BEGIN
        ALTER TABLE dbo.Percorrenze
            ADD DeletedByUserId INT NULL;
    END;

    /* Le colonne testo restano per non perdere lo storico preesistente.
       Le nuove registrazioni useranno gli ID. */
    ALTER TABLE dbo.Percorrenze
        ALTER COLUMN InseritoDa NVARCHAR(150) NULL;

    ALTER TABLE dbo.Percorrenze
        ALTER COLUMN DeletedBy NVARCHAR(150) NULL;

    /* Per le righe storiche l'utente proprietario è la fonte più affidabile. */
    UPDATE dbo.Percorrenze
    SET InseritoDaUserId = IdUtente
    WHERE InseritoDaUserId IS NULL
      AND IdUtente IS NOT NULL;

    /* Recupera l'autore dell'eliminazione solo se nome+cognome identifica
       un unico utente. Le righe ambigue restano NULL e non vengono inventate. */
    ;WITH DeletedByMatches AS
    (
        SELECT
            p.Id,
            MIN(u.ID_utente) AS UserId
        FROM dbo.Percorrenze AS p
        INNER JOIN dbo.Utenti AS u
            ON LTRIM(RTRIM(CONCAT(u.Nome, ' ', u.Cognome)))
             = LTRIM(RTRIM(p.DeletedBy))
        WHERE p.DeletedByUserId IS NULL
          AND NULLIF(LTRIM(RTRIM(p.DeletedBy)), '') IS NOT NULL
        GROUP BY p.Id
        HAVING COUNT(*) = 1
    )
    UPDATE p
    SET DeletedByUserId = matches.UserId
    FROM dbo.Percorrenze AS p
    INNER JOIN DeletedByMatches AS matches ON matches.Id = p.Id;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.Percorrenze')
          AND name = 'IX_Percorrenze_InseritoDaUserId'
    )
    BEGIN
        CREATE INDEX IX_Percorrenze_InseritoDaUserId
            ON dbo.Percorrenze (InseritoDaUserId);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID('dbo.Percorrenze')
          AND name = 'IX_Percorrenze_DeletedByUserId'
    )
    BEGIN
        CREATE INDEX IX_Percorrenze_DeletedByUserId
            ON dbo.Percorrenze (DeletedByUserId);
    END;

    /* NOCHECK mantiene operativi eventuali dati legacy non più presenti
       in Utenti, ma il vincolo protegge tutte le nuove scritture. */
    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = 'FK_Percorrenze_InseritoDaUtente'
    )
    BEGIN
        ALTER TABLE dbo.Percorrenze WITH NOCHECK
            ADD CONSTRAINT FK_Percorrenze_InseritoDaUtente
            FOREIGN KEY (InseritoDaUserId)
            REFERENCES dbo.Utenti (ID_utente);
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.foreign_keys
        WHERE name = 'FK_Percorrenze_DeletedByUtente'
    )
    BEGIN
        ALTER TABLE dbo.Percorrenze WITH NOCHECK
            ADD CONSTRAINT FK_Percorrenze_DeletedByUtente
            FOREIGN KEY (DeletedByUserId)
            REFERENCES dbo.Utenti (ID_utente);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

CREATE OR ALTER VIEW dbo.vw_PercorrenzeConUtenti
AS
SELECT
    p.Id,
    p.IdUtente,
    p.DataPercorrenza,
    p.KmPartenza,
    p.KmArrivo,
    p.KmPercorsi,
    p.Tragitto,
    p.Descrizione,
    p.DataCreazione,
    p.IsDeleted,
    p.DeletedAt,
    p.InseritoDaUserId,
    p.DeletedByUserId,
    LTRIM(RTRIM(CONCAT(ownerUser.Nome, ' ', ownerUser.Cognome))) AS UtenteProprietario,
    COALESCE(
        NULLIF(LTRIM(RTRIM(CONCAT(insertUser.Nome, ' ', insertUser.Cognome))), ''),
        NULLIF(LTRIM(RTRIM(p.InseritoDa)), ''),
        CONCAT('Utente ', COALESCE(CONVERT(NVARCHAR(12), p.InseritoDaUserId), CONVERT(NVARCHAR(12), p.IdUtente)))
    ) AS InseritoDa,
    COALESCE(
        NULLIF(LTRIM(RTRIM(CONCAT(deleteUser.Nome, ' ', deleteUser.Cognome))), ''),
        NULLIF(LTRIM(RTRIM(p.DeletedBy)), '')
    ) AS DeletedBy
FROM dbo.Percorrenze AS p
LEFT JOIN dbo.Utenti AS ownerUser ON ownerUser.ID_utente = p.IdUtente
LEFT JOIN dbo.Utenti AS insertUser ON insertUser.ID_utente = p.InseritoDaUserId
LEFT JOIN dbo.Utenti AS deleteUser ON deleteUser.ID_utente = p.DeletedByUserId;
GO

/* Verifica finale */
SELECT TOP (100)
    Id, IdUtente, InseritoDaUserId, DeletedByUserId,
    UtenteProprietario, InseritoDa, DeletedBy,
    DataPercorrenza, KmPartenza, KmArrivo, IsDeleted
FROM dbo.vw_PercorrenzeConUtenti
ORDER BY Id DESC;
