# Stato del progetto

## Dove siamo

- **Fase 1 — Il lettore onesto**, fine della settimana 1: struttura, anonimizzatore, primi tre casi del museo e test che li scoprono da soli sono pronti; build e 122 test verdi in locale. Manca solo la verifica della CI su GitHub.
- **Primi utenti da coinvolgere**: software house e sviluppatori di gestionali .NET in Italia.

## Settimana 1 — obiettivi

- [x] Struttura del repository e soluzione .NET 10 secondo `docs/architettura.md`
- [ ] Build e test verdi in locale e su GitHub Actions (in locale sì; su GitHub Actions da verificare al primo push)
- [x] `tools/Fedele.Anonymizer`, prima versione: il proprietario indica le colonne da anonimizzare; i valori vengono sostituiti preservando encoding, BOM, separatore, virgolette e fine riga (comandi `analyze` e `anonymize`; i caratteri di controllo di codice fiscale, partita IVA e IBAN non sono ancora ricalcolati)
- [x] Primi casi nel museo degli orrori, da `private-data/`, anonimizzati e approvati (0001–0003; dati sintetici, copiati identici)
- [x] `Fedele.Museum.Tests` che scopre i casi da solo (controlla completezza dei casi, `expected.json` secondo ADR 0006, encoding, BOM, fine riga, record e intestazione; il confronto di tipi e problemi arriverà con il lettore CSV)

## Prossimo passo

Creare il repository su GitHub e fare il primo push, per vedere la CI verde su Windows e Linux (prima verificare che i nomi `Fedele.Core` e `fedele` siano liberi su NuGet e GitHub, come in `AVVIO.md`).

## Da fare più avanti

- Settimana 2: progettare l'API di `Fedele.Core` (`IFormatReader`, `RawRecord`, `Issue`) e il lettore di `Fedele.Formats.Csv`.
- Spostare rilevamento di encoding/separatore e classificazione dei valori da `tools/Fedele.Anonymizer` a `Fedele.Formats.Csv` e `Fedele.Semantics.Italy`, quando si progettano le loro API.
- Ricalcolare i caratteri di controllo (codice fiscale, partita IVA, IBAN) nei valori anonimizzati, tramite `GenerateSample` dei tipi semantici.
- `analyze` su una cartella analizza anche i file non CSV (README, JSON): filtrarli o segnalarli.
- Il file intero `dataset_1000000x20.csv` (136 MB, in `private-data/`) è un buon candidato per i benchmark della settimana 5.

## Domande aperte

- `PackageId` del dotnet tool: resta `Fedele.Cli` oppure `fedele`? Da decidere quando si verifica la disponibilità dei nomi su NuGet.
- Copyright: per ora «The Fedele Authors» in `NOTICE` e nei pacchetti.
- `AVVIO.md`: tenerlo nella radice, spostarlo in `docs/` o eliminarlo?

## Diario

Una riga per sessione, la più recente in alto: data, cosa è stato fatto, cosa resta.

- 2026-11-18 — Prima sessione di lavoro: repository inizializzato (soluzione `.slnx`, progetti, test di architettura, CI, LICENSE, NOTICE, README, ADR 0005); anonimizzatore con `analyze`, `anonymize` e `--max-records`; primi tre casi nel museo (riga di campi vuoti e decimali col punto; solo interi; telefoni che sembrano numeri); `Fedele.Museum.Tests` che scopre i casi da solo (ADR 0006); regola dell'italiano resa esplicita in `CLAUDE.md`. Resta: CI su GitHub, poi la settimana 2.
- 2026-11-18 — Progetto pianificato, kit di avvio pronto.
