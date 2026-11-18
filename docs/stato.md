# Stato del progetto

## Dove siamo

- **Fase 1 — Il lettore onesto**, settimana 1: repository inizializzato, build e test verdi in locale.
- **Primi utenti da coinvolgere**: software house e sviluppatori di gestionali .NET in Italia.

## Settimana 1 — obiettivi

- [x] Struttura del repository e soluzione .NET 10 secondo `docs/architettura.md`
- [ ] Build e test verdi in locale e su GitHub Actions (in locale sì; su GitHub Actions da verificare al primo push)
- [x] `tools/Fedele.Anonymizer`, prima versione: il proprietario indica le colonne da anonimizzare; i valori vengono sostituiti preservando encoding, BOM, separatore, virgolette e fine riga (comandi `analyze` e `anonymize`; i caratteri di controllo di codice fiscale, partita IVA e IBAN non sono ancora ricalcolati)
- [ ] Primi casi nel museo degli orrori, da `private-data/`, anonimizzati e approvati
- [ ] `Fedele.Museum.Tests` che scopre i casi da solo (per ora verifica solo che `museum/` esista)

## Prossimo passo

Fase 2 di `/aggiungi-al-museo`: eseguire `analyze private-data/`, mostrare il riepilogo al proprietario e, con la sua conferma (nessuna colonna da anonimizzare), copiare i file nel museo con `anonymize --columns none`.

## Da fare più avanti

- Spostare rilevamento di encoding/separatore e classificazione dei valori da `tools/Fedele.Anonymizer` a `Fedele.Formats.Csv` e `Fedele.Semantics.Italy`, quando si progettano le loro API.
- Ricalcolare i caratteri di controllo (codice fiscale, partita IVA, IBAN) nei valori anonimizzati, tramite `GenerateSample` dei tipi semantici.

## Domande aperte

- `PackageId` del dotnet tool: resta `Fedele.Cli` oppure `fedele`? Da decidere quando si verifica la disponibilità dei nomi su NuGet.
- Copyright: per ora «The Fedele Authors» in `NOTICE` e nei pacchetti.

## Diario

Una riga per sessione, la più recente in alto: data, cosa è stato fatto, cosa resta.

- 2026-11-18 — Anonimizzatore, prima versione: `analyze` (solo struttura, mai valori) e `anonymize` (stessa forma, copia identica verificata con `--columns none`), 86 nuovi test in `Fedele.Anonymizer.Tests`. Resta: portare i file di `private-data/` nel museo.
- 2026-11-18 — Repository inizializzato: soluzione `.slnx`, 4 progetti in `src/`, 5 progetti di test con test di architettura, scheletro dell'anonimizzatore, CI Windows + Linux, LICENSE, NOTICE, README, ADR 0005 (xUnit v3). Resta: anonimizzatore, primi casi del museo, CI da verificare su GitHub.
- 2026-11-18 — Progetto pianificato, kit di avvio pronto.
