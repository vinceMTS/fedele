# 0001 — Struttura modulare

- **Stato:** accettata
- **Data:** 2026-11-18

## Contesto

Fedele partirà dai CSV ma dovrà supportare Excel, JSON, XML, FatturaPA, un importatore web e tipi semantici di più paesi. Un unico progetto che cresce diventerebbe difficile da capire, testare e distribuire.

## Decisione

Un nucleo (`Fedele.Core`) senza dipendenze esterne definisce il modello dei dati e i punti di estensione. Ogni formato e ogni insieme di tipi semantici è un progetto separato che dipende solo da `Core`. Le applicazioni mettono insieme i moduli che servono.

## Conseguenze

- Aggiungere un formato significa aggiungere un progetto, senza toccare quelli esistenti.
- Chi usa la libreria installa solo i pacchetti che gli servono.
- Serve disciplina: ciò che due moduli condividono va in `Core`, mai in un modulo che l'altro referenzia.
