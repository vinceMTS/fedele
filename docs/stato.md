# Stato del progetto

## Dove siamo

- **Fase 1 — Il lettore onesto**, settimana 2 completata in locale: API di lettura in `Fedele.Core` (ADR 0007), lettore CSV in streaming, museo confrontato con il risultato di Fedele, comando `fedele inspect`; build e 211 test verdi in locale. Gli ultimi commit non sono ancora su GitHub.
- **Repository pubblico**: https://github.com/vinceMTS/fedele
- **Primi utenti da coinvolgere**: software house e sviluppatori di gestionali .NET in Italia.

## Settimana 1 — obiettivi

- [x] Struttura del repository e soluzione .NET 10 secondo `docs/architettura.md`
- [x] Build e test verdi in locale e su GitHub Actions (Windows e Linux, primo push del 2026-11-18)
- [x] `tools/Fedele.Anonymizer`, prima versione: il proprietario indica le colonne da anonimizzare; i valori vengono sostituiti preservando encoding, BOM, separatore, virgolette e fine riga (comandi `analyze` e `anonymize`; i caratteri di controllo di codice fiscale, partita IVA e IBAN non sono ancora ricalcolati)
- [x] Primi casi nel museo degli orrori, da `private-data/`, anonimizzati e approvati (0001–0003; dati sintetici, copiati identici)
- [x] `Fedele.Museum.Tests` che scopre i casi da solo (controlla completezza dei casi, `expected.json` secondo ADR 0006, encoding, BOM, fine riga, record e intestazione; il confronto di tipi e problemi arriverà con il lettore CSV)

## Settimana 2 — obiettivi

- [x] ADR 0007: lettura in due passi (`OpenAsync` → `ReadRecordsAsync`), con `Detected<T>`, `Issue`, `RawRecord`, `FormatLayout`, `ReadSummary`
- [x] `Fedele.Core`: modello, `FedeleBuilder`/`FedeleEngine` (riconosce il formato anche da stream non riavvolgibili), `TextEncodingDetector` su un campione di 64 KB
- [x] `Fedele.Formats.Csv`: lettore in streaming, separatore dedotto con confidenza e motivo, posizione di ogni record (riga e byte), problemi `empty-record`, `field-count-mismatch`, `unterminated-quote`, `text-after-closing-quote`, `stray-quote`, `ambiguous-separator`, `invalid-byte`
- [x] `Fedele.Museum.Tests` confronta la sezione `file` e i problemi di `expected.json` con ciò che legge Fedele (tutti e tre i casi coincidono)
- [x] `fedele inspect <file>`: layout, colonne, riepilogo e problemi, mai i valori

## Prossimo passo

Settimana 3: inferenza dei tipi delle colonne (intero, decimale con separatori italiani, data, testo) e riconoscimento automatico dell'intestazione; definire `ISemanticType` e spostare la classificazione dei valori dall'anonimizzatore a `Fedele.Semantics.Italy`; far usare all'anonimizzatore encoding, separatore e lettore dei moduli. Prima mostrare il piano al proprietario. Il museo potrà allora confrontare anche la sezione `columns`.

## Da fare più avanti

- Provare `fedele inspect` sui file reali di `private-data/` (lo fa il proprietario: stampa solo la struttura) e, se emergono stranezze, aggiungerle al museo.
- Messaggi della CLI e dei problemi: oggi solo in inglese; l'italiano è previsto dall'inizio (`docs/architettura.md`), da fare quando i messaggi si stabilizzano.
- Un campo molto lungo (per esempio una virgoletta mai chiusa in un file da gigabyte) finisce tutto in memoria: valutare un limite alla lunghezza dei campi.
- `tools/Fedele.Anonymizer` ha ancora una sua copia di lexer e riconoscimento di encoding/separatore: si toglie nella settimana 3.
- Ricalcolare i caratteri di controllo (codice fiscale, partita IVA, IBAN) nei valori anonimizzati, tramite `GenerateSample` dei tipi semantici.
- `analyze` su una cartella analizza anche i file non CSV (README, JSON): filtrarli o segnalarli.
- Il file intero `dataset_1000000x20.csv` (136 MB, in `private-data/`) è un buon candidato per i benchmark della settimana 5.

## Domande aperte

- Copyright: per ora «The Fedele Authors» in `NOTICE` e nei pacchetti.

## Decisioni prese

- `PackageId` del dotnet tool: resta `Fedele.Cli`, coerente con gli altri pacchetti `Fedele.*`; il comando resta `fedele`.
- `AVVIO.md` spostato in `docs/avvio.md` come documento storico.

## Diario

Una riga per sessione, la più recente in alto: data, cosa è stato fatto, cosa resta.

- 2026-11-18 — Prima sessione di lavoro: repository inizializzato (soluzione `.slnx`, progetti, test di architettura, CI, LICENSE, NOTICE, README, ADR 0005); anonimizzatore con `analyze`, `anonymize` e `--max-records`; primi tre casi nel museo (riga di campi vuoti e decimali col punto; solo interi; telefoni che sembrano numeri); `Fedele.Museum.Tests` che scopre i casi da solo (ADR 0006); regola dell'italiano resa esplicita in `CLAUDE.md`. Resta: CI su GitHub, poi la settimana 2.
- 2026-11-18 — Progetto pianificato, kit di avvio pronto.
