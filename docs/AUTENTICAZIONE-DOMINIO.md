# Autenticazione configurabile

AcliComunicazioni supporta due modalità alternative:

- `Local`: form con nome utente e password di `dbo.Utenti`.
- `Domain`: Windows Authentication e associazione a `dbo.Utenti`.

La modalità si imposta con:

```json
"Authentication": {
  "Mode": "Local"
}
```

Valori ammessi: `Local` e `Domain`. L'applicazione interrompe l'avvio se
il valore manca o non è valido.

Configurazione predefinita:

| Ambiente | Modalità |
|---|---|
| Development | Local |
| Test | Local |
| Production | Domain |

Le due modalità non vengono esposte contemporaneamente. In questo modo
il login locale non diventa un accesso alternativo involontario quando è
attivo il dominio.

## Provare l'autenticazione di dominio sul PC locale

Il PC deve appartenere al dominio e l'account Windows deve essere
presente in `dbo.Utenti`.

Da PowerShell, nella cartella del progetto Web:

```powershell
$env:Authentication__Mode = "Domain"
dotnet run --launch-profile "AcliComunicazioni.Web - Sviluppo"
```

Aprire l'indirizzo HTTPS con il nome host, per esempio:

```text
https://localhost:61528
```

Evitare l'indirizzo IP durante la prova: Kerberos/Negotiate lavora meglio
con un nome host e il browser deve riconoscere l'indirizzo come intranet.

Per tornare al login locale:

```powershell
Remove-Item Env:Authentication__Mode
dotnet run --launch-profile "AcliComunicazioni.Web - Sviluppo"
```

In alternativa si può modificare temporaneamente
`appsettings.Development.json`, impostando `Mode` a `Domain`.

La prova continua a usare `AcliComunicazioni_Sviluppo`: cambia soltanto
il metodo di autenticazione.

## Funzionamento della modalità Domain

1. Windows autentica l'identità Active Directory.
2. L'applicazione riceve un nome come `DOMINIO\nome.cognome`.
3. Il nome breve `nome.cognome` viene cercato in
   `dbo.Utenti.Utente`.
4. ID, nome visualizzato e permessi vengono caricati da `dbo.Utenti`.
5. Un account assente o con `Bloccato = 1` non può accedere.

La password di dominio non viene acquisita, salvata o confrontata
dall'applicazione.

## Configurazione IIS

Quando `Authentication:Mode` è `Domain`:

- **Autenticazione Windows: Abilitata**
- **Autenticazione anonima: Disabilitata**
- provider: `Negotiate`, poi `NTLM`

Quando `Authentication:Mode` è `Local`:

- **Autenticazione Windows: Disabilitata**
- **Autenticazione anonima: Abilitata**

La configurazione dell'applicazione e quella di IIS devono quindi
corrispondere.

Non è necessario abilitare l'impersonificazione. L'accesso a SQL Server
continua a usare l'identità dell'applicazione o la connection string
configurata.

## Preparazione degli account

Esempio:

- identità Windows: `DOMINIO\mario.rossi`
- `dbo.Utenti.Utente`: `mario.rossi`
- `dbo.Utenti.Bloccato`: `0`

## Verifiche

1. Modalità Local: il form di login viene visualizzato.
2. Modalità Domain: il form non viene utilizzato.
3. Account di dominio presente in `dbo.Utenti`: accesso consentito.
4. Account assente: accesso negato.
5. Account con `Bloccato = 1`: accesso negato.
6. Le pagine protette richiedono l'associazione a `dbo.Utenti`.
