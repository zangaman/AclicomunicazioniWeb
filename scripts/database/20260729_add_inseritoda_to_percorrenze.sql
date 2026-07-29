SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Percorrenze', N'U') IS NULL
    BEGIN
        THROW 50001, 'La tabella dbo.Percorrenze non esiste.', 1;
    END;

    IF COL_LENGTH(N'dbo.Percorrenze', N'InseritoDa') IS NULL
    BEGIN
        ALTER TABLE dbo.Percorrenze
        ADD InseritoDa NVARCHAR(150) NULL;
    END;

    UPDATE dbo.Percorrenze
    SET InseritoDa = CONCAT(N'Utente ', IdUtente)
    WHERE InseritoDa IS NULL
       OR LTRIM(RTRIM(InseritoDa)) = N'';

    COMMIT TRANSACTION;

    PRINT 'Colonna dbo.Percorrenze.InseritoDa verificata correttamente.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
