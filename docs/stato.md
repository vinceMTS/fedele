# Stato del progetto

## Dove siamo

- **Fase 1 — Il lettore onesto**, settimana 1: repository inizializzato, build e test verdi in locale.
- **Primi utenti da coinvolgere**: software house e sviluppatori di gestionali .NET in Italia.

## Settimana 1 — obiettivi

- [x] Struttura del repository e soluzione .NET 10 secondo `docs/architettura.md`
- [ ] Build e test verdi in locale e su GitHub Actions (in locale sì; su GitHub Actions da verificare al primo push)
- [ ] `tools/Fedele.Anonymizer`, prima versione: il proprietario indica le colonne da anonimizzare; i valori vengono sostituiti preservando encoding, BOM, separatore, virgolette e fine riga (per ora c'è solo lo scheletro)
- [ ] Primi casi nel museo degli orrori, da `private-data/`, anonimizzati e approvati
- [ ] `Fedele.Museum.Tests` che scopre i casi da solo (per ora verifica solo che `museum/` esista)

## Prossimo passo

Costruire `tools/Fedele.Anonymizer` con i comandi `analyze` e `anonymize` (requisiti nella skill `/aggiungi-al-museo`), insieme al progetto di test `tests/Fedele.Anonymizer.Tests`.

## Domande aperte

- `PackageId` del dotnet tool: resta `Fedele.Cli` oppure `fedele`? Da decidere quando si verifica la disponibilità dei nomi su NuGet.
- Copyright: per ora «The Fedele Authors» in `NOTICE` e nei pacchetti.

## Diario

Una riga per sessione, la più recente in alto: data, cosa è stato fatto, cosa resta.

- 2026-11-18 — Repository inizializzato: soluzione `.slnx`, 4 progetti in `src/`, 5 progetti di test con test di architettura, scheletro dell'anonimizzatore, CI Windows + Linux, LICENSE, NOTICE, README, ADR 0005 (xUnit v3). Resta: anonimizzatore, primi casi del museo, CI da verificare su GitHub.
- 2026-11-18 — Progetto pianificato, kit di avvio pronto.
