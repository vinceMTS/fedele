---
name: nuovo-formato
description: Aggiunge a Fedele il supporto per un nuovo formato di file (Excel, JSON, XML, ...) come modulo separato. Usala quando si chiede di leggere o scrivere un formato non ancora supportato.
argument-hint: "[nome del formato, es. Excel]"
---

# Aggiungere un nuovo formato: $ARGUMENTS

Segui questi passi in ordine. Prima di iniziare, mostra al proprietario il piano e aspetta l'ok.

1. **Nome.** Il progetto si chiama `Fedele.Formats.<Nome>` (inglese, PascalCase: `Excel`, `Json`, `Xml`). Verifica che non esista già.
2. **Dipendenze.** Se serve una libreria esterna (es. per leggere `.xlsx`), proponi 1–2 alternative con licenza, ultima versione stabile su NuGet e stato di manutenzione. Aspetta la scelta del proprietario e registrala in un ADR (`docs/decisioni/NNNN-<titolo>.md`).
3. **Progetti.** Crea `src/Fedele.Formats.<Nome>/` (riferimento solo a `Fedele.Core`) e `tests/Fedele.Formats.<Nome>.Tests/`. Aggiungili a `Fedele.slnx`.
4. **Implementazione.** Implementa `IFormatReader` (e `IFormatWriter` se richiesto) rispettando i principi di `CLAUDE.md`: streaming, nessuna conversione silenziosa, testo originale conservato.
5. **Registrazione.** Aggiungi il metodo di estensione `Add<Nome>()` su `FedeleBuilder` e chiamalo nel punto di composizione di `Fedele.Cli`.
6. **Test.** Test unitari per rilevamento e lettura, inclusi file vuoti, file troncati, encoding diversi. Aggiungi almeno un caso nel museo (`museum/`) per il nuovo formato.
7. **Documentazione.** Aggiorna `docs/architettura.md` (togli *(futuro)* se c'era) e il README.
8. **Verifica.** `dotnet build` e `dotnet test` verdi.
9. **Riepilogo.** Spiega al proprietario in italiano cosa è stato aggiunto e come provarlo da riga di comando.
