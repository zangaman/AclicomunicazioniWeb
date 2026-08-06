/* =============================================================
   ACLI COMUNICAZIONI — RIMOZIONE AUDIT TESTUALE PERCORRENZE
   Prerequisito: eseguire prima 20260806_percorrenze_utente_ids.sql.

   Elimina definitivamente:
   - dbo.Percorrenze.InseritoDa
   - dbo.Percorrenze.DeletedBy

   L'audit rimane basato esclusivamente su InseritoDaUserId
   e DeletedByUserId.
   ============================================================= */

SET NOCOUNT ON;
SET XACT_ABORT ON;

/* Nessuna riga deve perdere l'identità di chi l'ha inserita. */
IF COL_LENGTH('dbo.Percorrenze', 'InseritoDaUserId') IS NULL
    THROW 51000, 'Manca InseritoDaUserId. Eseguire prima la migrazione degli ID utente.', 1;

IF COL_LENGTH('dbo.Percorrenze', 'DeletedByUserId') IS NULL
    THROW 51001, 'Manca DeletedByUserId. Eseguire prima la migrazione degli ID utente.', 1;

IF EXISTS (SELECT 1 FROM dbo.Percorrenze WHERE InseritoDaUserId IS NULL)
    THROW 51002, 'Sono presenti percorrenze senza InseritoDaUserId: rimozione annullata.', 1;

IF EXISTS
(
    SELECT 1
    FROM dbo.Percorrenze
    WHERE IsDeleted = 1
      AND DeletedByUserId IS NULL
)
    THROW 51003, 'Sono presenti eliminazioni senza DeletedByUserId: completare la mappatura prima di rimuovere DeletedBy.', 1;
GO

/* La vista non usa più le colonne testuali. */
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
        CONCAT('Utente ', CONVERT(NVARCHAR(12), p.InseritoDaUserId))
    ) AS InseritoDa,
    CASE
        WHEN p.DeletedByUserId IS NULL THEN NULL
        ELSE COALESCE(
            NULLIF(LTRIM(RTRIM(CONCAT(deleteUser.Nome, ' ', deleteUser.Cognome))), ''),
            CONCAT('Utente ', CONVERT(NVARCHAR(12), p.DeletedByUserId))
        )
    END AS DeletedBy
FROM dbo.Percorrenze AS p
LEFT JOIN dbo.Utenti AS ownerUser ON ownerUser.ID_utente = p.IdUtente
LEFT JOIN dbo.Utenti AS insertUser ON insertUser.ID_utente = p.InseritoDaUserId
LEFT JOIN dbo.Utenti AS deleteUser ON deleteUser.ID_utente = p.DeletedByUserId;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH('dbo.Percorrenze', 'InseritoDa') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.Percorrenze DROP COLUMN InseritoDa;
    END;

    IF COL_LENGTH('dbo.Percorrenze', 'DeletedBy') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.Percorrenze DROP COLUMN DeletedBy;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT TOP (100)
    Id, IdUtente, InseritoDaUserId, DeletedByUserId,
    UtenteProprietario, InseritoDa, DeletedBy,
    DataPercorrenza, KmPartenza, KmArrivo, IsDeleted
FROM dbo.vw_PercorrenzeConUtenti
ORDER BY Id DESC;
