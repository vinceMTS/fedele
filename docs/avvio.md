# Come avviare Fedele con Claude Code

Questa guida è per te, non per Claude Code. Dopo l'avvio puoi cancellarla o tenerla in `docs/`.

## 1. Cosa installare

| Cosa | Come verificare | Dove trovarlo |
| --- | --- | --- |
| .NET 10 SDK | `dotnet --version` mostra `10.x` | https://dotnet.microsoft.com/download |
| Git | `git --version` | https://git-scm.com (su Windows: Git for Windows, che serve anche a Claude Code) |
| GitHub CLI (consigliata) | `gh --version`, poi `gh auth login` | https://cli.github.com |
| Claude Code | `claude --version` | vedi sotto |

**Claude Code**, da terminale:

- Windows (PowerShell): `irm https://claude.ai/install.ps1 | iex`
- macOS / Linux: `curl -fsSL https://claude.ai/install.sh | bash`

In alternativa usa la scheda **Code** dell'app desktop di Claude, che include già Claude Code. Serve un abbonamento a pagamento. Guida ufficiale: https://code.claude.com/docs/en/overview

## 2. Prepara la cartella

1. Crea una cartella `Fedele` dove tieni i tuoi progetti.
2. Copia dentro **tutto** il contenuto di questo kit. Attenzione: la cartella `.claude` è nascosta; su Windows attiva "Mostra elementi nascosti", su macOS premi Cmd+Shift+punto.
3. Crea la cartella `Fedele/private-data/` e metti lì i tuoi CSV reali. Claude Code non li leggerà mai direttamente: li userà solo tramite l'anonimizzatore che costruirà.

## 3. Primo avvio

Apri un terminale nella cartella `Fedele` e lancia `claude`. Alla domanda se ti fidi della cartella, rispondi sì: così valgono le impostazioni di `.claude/settings.json`.

Poi incolla questo primo messaggio:

> Leggi CLAUDE.md e i file in docs/. Inizializza il repository seguendo docs/architettura.md, creando solo le parti non segnate come "futuro": git, soluzione .NET 10, progetti in src/ e tests/, tools/Fedele.Anonymizer, Directory.Build.props, Directory.Packages.props, global.json, .editorconfig, .gitignore (con private-data/), LICENSE Apache-2.0 con il testo ufficiale, NOTICE, README in inglese con una sezione in italiano, workflow GitHub Actions che compila e testa su Windows e Linux. Verifica su NuGet le ultime versioni stabili dei pacchetti che usi. Prima di creare qualsiasi file mostrami il piano completo e aspetta il mio ok. Alla fine build e test devono essere verdi: fai il primo commit e aggiorna docs/stato.md.

Leggi il piano che ti propone, chiedi spiegazioni su ciò che non ti è chiaro, poi dai l'ok.

## 4. I tuoi file reali nel museo

Quando la struttura è pronta, per ogni CSV reale:

> /aggiungi-al-museo nome-del-file.csv

La prima volta Claude Code costruirà l'anonimizzatore. Ti chiederà sempre quali colonne anonimizzare e ti farà controllare il risultato prima del commit.

## 5. Su GitHub

Quando vuoi pubblicare il repository (con la GitHub CLI già autenticata):

> Verifica che i nomi Fedele.Core e fedele siano liberi su NuGet e su GitHub, poi crea il repository pubblico su GitHub e fai il push.

## 6. Ogni sessione di lavoro

- **Inizio:** apri il terminale in `Fedele`, lancia `claude` e scrivi "riprendiamo". `CLAUDE.md` e `docs/stato.md` vengono letti da soli: Claude Code sa dove eravate rimasti.
- **Durante:** chiedi una cosa alla volta e lascia che ti mostri il piano per i lavori più grandi.
- **Fine:** `/fine-sessione`. Aggiorna il diario e fa il commit.

## 7. Le skill del progetto

Sono in `.claude/skills/` e fanno crescere il progetto sempre nello stesso modo, rispettando la struttura:

| Comando | Quando usarlo |
| --- | --- |
| `/nuovo-formato Excel` | Aggiungere un formato di file (fase 3) |
| `/nuovo-tipo-semantico codice SDI` | Riconoscere un nuovo tipo di valore |
| `/aggiungi-al-museo file.csv` | Trasformare un file reale in un caso di test anonimo |
| `/fine-sessione` | Chiudere la giornata di lavoro |

Le prime due Claude Code le usa anche da solo, quando capisce che servono.
