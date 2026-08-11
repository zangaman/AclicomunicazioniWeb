# Autenticazione configurabile

AcliComunicazioni supporta due modalità:

- `Local`: login applicativo riservato a sviluppo e test.
- `Domain`: accesso Windows integrato e, se abilitato, credenziali
  Active Directory verificate tramite LDAPS.

Microsoft 365 / Entra ID non è attualmente configurato.

## Modalità per ambiente

| Ambiente | Modalità predefinita |
|---|---|
| Development | Local |
| Test | Local |
| Production | Domain |

La modalità può essere sovrascritta con la variabile
`Authentication__Mode`.

## Modalità Domain

La pagina di accesso mostra:

1. **Accedi con Windows**, che avvia Negotiate/Kerberos su un endpoint
   dedicato.
2. **Accedi con le credenziali aziendali**, soltanto quando LDAPS è
   abilitato.

Entrambi i metodi producono lo stesso cookie applicativo e verificano
sempre che l'account esista in `dbo.Utenti` e non sia bloccato.

L'endpoint Windows è separato dalla pagina di login: un eventuale
`401` di Negotiate non impedisce di utilizzare il modulo LDAPS.

## Configurazione LDAPS

Esempio:

```json
"Authentication": {
  "Mode": "Domain",
  "ActiveDirectory": {
    "Enabled": true,
    "Server": "dc01.dominio.local",
    "Port": 636,
    "UseSsl": true,
    "UserPrincipalSuffix": "dominio.local"
  }
}
```

Non abilitare questa sezione finché i valori reali non sono stati
verificati.

Requisiti:

- certificato LDAPS valido sul Domain Controller;
- certificato della CA considerato attendibile dal server Web;
- porta TCP 636 raggiungibile dal server Web;
- DNS del Domain Controller funzionante;
- `UseSsl` obbligatoriamente impostato a `true`.

L'applicazione non accetta LDAP non cifrato e non memorizza la password
di dominio. La password viene utilizzata esclusivamente per il bind
LDAPS e non deve essere registrata nei log.

Se viene inserito solo `nome.cognome`, l'applicazione costruisce
`nome.cognome@dominio.local` usando `UserPrincipalSuffix`. Sono
accettati anche UPN completi e nomi `DOMINIO\nome.cognome`.

## Associazione a dbo.Utenti

Dopo l'autenticazione, il nome viene normalizzato e cercato in
`dbo.Utenti.Utente`.

Esempio:

- identità Windows: `DOMINIO\mario.rossi`;
- UPN: `mario.rossi@dominio.local`;
- valore consigliato in `dbo.Utenti.Utente`: `mario.rossi`;
- `dbo.Utenti.Bloccato`: `0`.

Un account di dominio valido ma assente dal database applicativo non
può accedere.

## Prove in sviluppo

Per provare Windows mantenendo il database di sviluppo:

```powershell
$env:Authentication__Mode = "Domain"
dotnet run --launch-profile "AcliComunicazioni.Web - Sviluppo"
```

Aprire `https://localhost:61528` e scegliere **Accedi con Windows**.

Per provare LDAPS, valorizzare la sezione
`Authentication:ActiveDirectory` in
`appsettings.Development.json` e impostare `Enabled` a `true`.

Per tornare al login locale:

```powershell
Remove-Item Env:Authentication__Mode
```

## Pubblicazione con IIS

L'applicazione deve mantenere l'accesso anonimo alla pagina di login,
per consentire il metodo LDAPS. Windows Authentication deve essere
abilitata per l'endpoint Negotiate.

- **Anonymous Authentication:** Enabled
- **Windows Authentication:** Enabled
- provider Windows: `Negotiate`, poi `NTLM`

Non abilitare l'impersonificazione. SQL Server continua a utilizzare
l'identità del processo applicativo o quella specificata nella
connection string.
