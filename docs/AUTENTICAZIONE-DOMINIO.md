# Autenticazione di dominio

In **Development** e **Test** AcliComunicazioni mantiene il login applicativo
basato su `dbo.Utenti`.

In **Production** l'applicazione usa Windows Authentication:

1. IIS autentica l'identità Active Directory.
2. L'applicazione riceve un nome come `DOMINIO\nome.cognome`.
3. Il nome breve `nome.cognome` viene cercato nella colonna
   `dbo.Utenti.Utente`.
4. ID, nome visualizzato e permessi vengono caricati da `dbo.Utenti`.
5. Un account assente o con `Bloccato = 1` non può accedere.

La password di dominio non viene acquisita, salvata o confrontata
dall'applicazione.

## Configurazione IIS per Production

Sul server IIS:

1. Installare il servizio ruolo **Windows Authentication**.
2. Verificare che il server appartenga al dominio.
3. Nel sito o nell'applicazione AcliComunicazioni aprire
   **Autenticazione**.
4. Impostare:
   - **Autenticazione Windows: Abilitata**
   - **Autenticazione anonima: Disabilitata**
5. Nei provider di Windows Authentication mantenere:
   - `Negotiate`
   - `NTLM`
6. Configurare l'ambiente:
   `ASPNETCORE_ENVIRONMENT=Production`.
7. Verificare che la connection string punti esclusivamente a
   `AcliComunicazioni_Produzione`.

Non è necessario abilitare l'impersonificazione dell'utente. L'accesso a
SQL Server continua a usare l'identità configurata per l'applicazione o
la connection string di produzione.

## Preparazione degli account

Ogni persona autorizzata deve avere una riga attiva in `dbo.Utenti`.

Esempio:

- identità Windows: `DOMINIO\mario.rossi`
- `dbo.Utenti.Utente`: `mario.rossi`
- `dbo.Utenti.Bloccato`: `0`

Il confronto del nome utente non usa la password presente in
`dbo.Utenti`.

## Verifiche

1. Persona del dominio presente in `dbo.Utenti`: accesso consentito.
2. Persona del dominio assente da `dbo.Utenti`: accesso negato.
3. Persona con `Bloccato = 1`: accesso negato.
4. Ambiente Development/Test: il form di login continua a funzionare.
5. Ambiente Production: il form password non è utilizzato.
6. Le pagine protette richiedono sia l'identità di dominio sia
   l'associazione a `dbo.Utenti`.

Per l'accesso automatico dai PC aziendali, l'indirizzo del sito deve
essere riconosciuto dal browser come sito intranet.
