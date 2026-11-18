# Fedele — istruzioni per Claude Code

> **Regola fissa: con il proprietario si parla sempre in italiano**, in ogni messaggio, riepilogo, domanda e spiegazione, per tutta la sessione, anche quando i file, i log o gli strumenti sono in inglese. In inglese restano solo codice, nomi, commenti nel codice e messaggi di commit.

Fedele è una libreria .NET open source (Apache-2.0) che legge file di dati — CSV oggi, poi Excel, JSON, XML — **senza mai tradirne il contenuto**: nessuno zero perso, nessuna data inventata, nessun decimale sbagliato. Parla nativamente i formati italiani ed europei.

Il progetto è pensato per crescere a strati: libreria (`Fedele.*` su NuGet) → strumento a riga di comando (`fedele`) → prodotto (importatore web). Ogni scelta di struttura deve tenerne conto.

@docs/architettura.md
@docs/stato.md

## Come lavorare con il proprietario

- Il proprietario conosce C# a livello base-intermedio e ha scelto che **Claude scriva la maggior parte del codice**. Lui decide, prova con i file reali e parla con gli utenti.
- Parla con lui **in italiano**. Codice, nomi, commenti nel codice e messaggi di commit **in inglese** (vedi `docs/decisioni/0002-lingua-del-codice.md`).
- Dopo ogni modifica significativa spiega in 3–5 righe cosa hai fatto e perché. Se hai usato un concetto nuovo per lui (es. `Span<T>`, `IAsyncEnumerable`, Native AOT), spiegalo in una frase.
- **Chiedi conferma prima di**: aggiungere una dipendenza NuGet, cambiare un'API pubblica, rinominare o eliminare progetti, pubblicare qualcosa (NuGet, GitHub Releases), fare push.
- Per lavori più grandi di un paio di file, mostra prima il piano e aspetta l'ok.

## Principi del prodotto (non negoziabili)

1. **Nessuna conversione silenziosa.** Nel dubbio il valore resta testo e Fedele segnala un problema (`Issue`) con riga, colonna e spiegazione.
2. **L'originale è sempre recuperabile.** Ogni valore tipizzato conserva il testo grezzo da cui viene.
3. **Ogni deduzione ha una confidenza e un perché** leggibile da una persona.
4. **Streaming per default.** Mai caricare un file intero in memoria se non serve: deve funzionare su file da gigabyte.
5. **I formati italiani ed europei sono di prima classe**: `;` come separatore, `1.234,56`, date `gg/mm/aaaa`, Windows-1252, codice fiscale, partita IVA, IBAN.

## Regole per ampliare il progetto

La struttura è modulare: **si cresce aggiungendo progetti, non gonfiando quelli esistenti.**

- Nuovo formato di file → usa la skill `/nuovo-formato`.
- Nuovo tipo semantico (codice fiscale, IBAN, ...) → usa `/nuovo-tipo-semantico`.
- Nuovo file nel museo degli orrori → usa `/aggiungi-al-museo`.
- Nuova area non prevista: crea `src/Fedele.<Area>/` e `tests/Fedele.<Area>.Tests/`, aggiungili alla soluzione, aggiorna `docs/architettura.md` e scrivi un ADR in `docs/decisioni/`.
- `Fedele.Core` **non dipende da nessun altro progetto né da pacchetti esterni**. I moduli dipendono solo da `Fedele.Core`. Solo le applicazioni (`Fedele.Cli`, in futuro il web) mettono insieme i moduli.
- Quando la struttura cambia, aggiorna `docs/architettura.md` **nello stesso commit**.
- Una decisione che non si cambia facilmente (dipendenze, formati di file, API pubbliche) va registrata come ADR in `docs/decisioni/NNNN-titolo.md`.

## Convenzioni di codice

- .NET 10 (LTS), ultima versione di C#, `Nullable` e `ImplicitUsings` attivi, `TreatWarningsAsErrors` attivo. Impostazioni comuni in `Directory.Build.props`.
- Versioni dei pacchetti centralizzate in `Directory.Packages.props`. Prima di aggiungere un pacchetto verifica su NuGet l'ultima versione **stabile**.
- Namespace a livello di file, uguali al nome del progetto. Classi `sealed` per default, `record` per i modelli immutabili.
- Ogni API pubblica ha commenti XML (`///`).
- Test con xUnit. Nomi dei test: `Method_Scenario_ExpectedResult`. Ogni bug corretto ha un test che lo riproduce.
- Nei percorsi critici per le prestazioni usa `Span<T>`/`ReadOnlySpan<char>`; non dichiarare un'ottimizzazione senza un benchmark in `benchmarks/`.

## Comandi

```bash
dotnet build                     # compila tutto
dotnet test                      # esegue tutti i test
dotnet run --project src/Fedele.Cli -- inspect museum/<caso>/input.csv
```

Prima di dire che un lavoro è finito: `dotnet build` e `dotnet test` devono essere verdi.

## Privacy dei dati reali (IMPORTANTE)

- `private-data/` contiene CSV reali, che possono avere dati personali di clienti. È in `.gitignore` e **non va mai letto, stampato o copiato da Claude**: né con lo strumento di lettura, né con comandi come `cat`, `head`, `type`, `Get-Content`. La lettura è bloccata anche in `.claude/settings.json`.
- L'unico modo di usarli è lo strumento `tools/Fedele.Anonymizer`, eseguito in modo che stampi solo statistiche strutturali (encoding, separatore, numero di righe e colonne, nomi delle colonne), mai i valori.
- Nel museo (`museum/`) entrano solo file anonimizzati, e solo dopo l'approvazione esplicita del proprietario.

## Flusso di lavoro con git

- Finché il proprietario lavora da solo: commit piccoli direttamente su `main`, messaggi in inglese in stile Conventional Commits (`feat:`, `fix:`, `test:`, `docs:`, `refactor:`, `chore:`).
- Il push su GitHub solo quando il proprietario lo chiede.
- A fine sessione il proprietario lancia `/fine-sessione`, che aggiorna `docs/stato.md` e fa il commit.
