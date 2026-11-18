---
name: aggiungi-al-museo
description: Trasforma un file reale di private-data/ in un caso anonimizzato del museo degli orrori, senza mai leggerne il contenuto. Usala quando il proprietario vuole aggiungere un file reale ai test.
argument-hint: "[nome del file in private-data/]"
disable-model-invocation: true
---

# Aggiungere al museo: $ARGUMENTS

**Regola assoluta:** non leggere, stampare o copiare mai il contenuto di `private-data/`. Nessun `Read`, `cat`, `head`, `type`, `Get-Content`, e nessuno script che stampi valori. Solo lo strumento `tools/Fedele.Anonymizer` tocca quei file.

1. **Lo strumento esiste?** Se `tools/Fedele.Anonymizer` non esiste ancora o non supporta ciò che serve, costruiscilo o estendilo prima. Requisiti minimi:
   - comando `analyze <file>`: stampa solo encoding, BOM, separatore, virgolette, fine riga, numero di righe e colonne, nomi delle colonne e, per ogni colonna, il tipo semantico probabile. **Mai i valori.**
   - comando `anonymize <file> <output> --columns <elenco>`: sostituisce i valori delle colonne indicate con valori finti ma validi (usando `GenerateSample` dei tipi semantici, oppure valori casuali della stessa forma), preservando byte per byte encoding, BOM, separatore, virgolette, fine riga e formati di numeri e date;
   - deterministico con un seme (`--seed`), così il risultato è riproducibile.
2. **Analisi.** Esegui `analyze` sul file e mostra al proprietario il riepilogo strutturale.
3. **Scelta delle colonne.** Proponi quali colonne anonimizzare (nomi, codici fiscali, IBAN, indirizzi, email, telefoni, note libere...) e **aspetta la conferma del proprietario**. Nel dubbio, anonimizza.
4. **Anonimizzazione.** Scegli il prossimo numero libero in `museum/` e uno slug breve che descriva la difficoltà (es. `0003-decimali-con-virgola-e-bom`). Esegui `anonymize` verso `museum/<numero>-<slug>/input.csv`.
5. **Revisione del proprietario.** Chiedigli di aprire il file anonimizzato e confermare che non contiene dati personali. **Non procedere senza il suo ok.**
6. **Risultato atteso.** Ora puoi leggere il file anonimizzato. Scrivi `expected.json` (encoding, separatore, intestazione, tipo di ogni colonna, problemi attesi) e `README.md` (cosa rende difficile questo file, in italiano).
7. **Verifica.** Esegui `dotnet test`. Se un test del museo fallisce perché Fedele non gestisce ancora quel caso, segnalalo come lavoro da fare (in `docs/stato.md`), non correggere `expected.json` per farlo passare.
8. **Commit** del nuovo caso, solo dopo l'ok del proprietario.
