---
name: nuovo-tipo-semantico
description: Aggiunge un tipo di dato con significato (codice fiscale, partita IVA, IBAN, CAP, codice SDI, targa, ...) che Fedele sa riconoscere, validare e generare. Usala quando si chiede di riconoscere o validare un nuovo tipo di valore.
argument-hint: "[nome del tipo, es. codice SDI]"
---

# Aggiungere un tipo semantico: $ARGUMENTS

1. **Dove va.** Tipi italiani in `Fedele.Semantics.Italy`; tipi internazionali (IBAN, email) nel modulo indicato in `docs/architettura.md`; tipi di un altro paese in `Fedele.Semantics.<Paese>` (se non esiste, crealo seguendo le regole di `CLAUDE.md` e aggiorna l'architettura).
2. **Identificativo.** `Id` stabile e in minuscolo: `<codice-paese>.<nome-in-inglese>` (es. `it.tax-code`, `it.vat-number`), oppure solo il nome se è internazionale (`iban`).
3. **Fonte ufficiale.** Prima di scrivere l'algoritmo di validazione, trova la specifica ufficiale (es. decreto, Agenzia delle Entrate, ISO) e citala in un commento XML con il link.
4. **Implementazione** di `ISemanticType`:
   - `Match` restituisce `Valid`, `Invalid` con un motivo leggibile (es. "carattere di controllo errato"), oppure `NotApplicable`;
   - `GenerateSample` produce valori finti ma validi: servono all'anonimizzatore;
   - tollera spazi e maiuscole/minuscole solo se la specifica lo consente, e segnalalo.
5. **Test.** Almeno: valori validi reali di esempio pubblici, valori con errore di controllo, lunghezze sbagliate, caratteri non ammessi, e un test che verifica che 1.000 valori generati da `GenerateSample` siano tutti validi.
6. **Registrazione** nel metodo di estensione del modulo (es. `AddItalianSemantics()`).
7. **Verifica** con `dotnet build` e `dotnet test`, poi riepilogo in italiano per il proprietario.
