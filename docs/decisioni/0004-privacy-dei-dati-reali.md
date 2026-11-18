# 0004 — Privacy dei dati reali

- **Stato:** accettata
- **Data:** 2026-11-18

## Contesto

I file reali che rendono prezioso il museo degli orrori possono contenere dati personali di clienti (nomi, codici fiscali, IBAN, indirizzi). Non devono finire in un repository pubblico né essere inviati a servizi esterni.

## Decisione

- I file reali stanno solo in `private-data/`, esclusa da git.
- Claude Code non ne legge il contenuto: è vietato in `CLAUDE.md` e bloccato in `.claude/settings.json`.
- Lo strumento `tools/Fedele.Anonymizer`, eseguito in locale, produce copie in cui i valori personali sono sostituiti da valori finti ma validi, preservando tutto ciò che rende il file "difficile": encoding, BOM, separatore, virgolette, fine riga, formati di numeri e date.
- Ogni copia anonimizzata entra nel museo solo dopo l'approvazione del proprietario.

## Conseguenze

Il museo resta pubblico e utile senza esporre nessuno. L'anonimizzatore diventa anche una funzionalità utile agli utenti di Fedele.
