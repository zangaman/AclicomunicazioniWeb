USE [aclicomunicazioni_sviluppo];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'dbo.Percorrenze', N'InseritoDa') IS NULL
BEGIN
    ALTER TABLE dbo.Percorrenze
        ADD InseritoDa NVARCHAR(150) NULL;

    PRINT 'Colonna dbo.Percorrenze.InseritoDa aggiunta correttamente.';
END
ELSE
BEGIN
    PRINT 'La colonna dbo.Percorrenze.InseritoDa esiste già.';
END;
GO