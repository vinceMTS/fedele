# 0003 — Telefoni che sembrano numeri

**Origine:** `dataset_1000000x20.csv` (136 MB), solo intestazione e primi 10.000 record, copiati byte per byte con `anonymize --columns none --max-records 10000`. Il file intero non entra nel museo perché GitHub rifiuta file oltre i 100 MB; resta in `private-data/` per i benchmark futuri. Dati sintetici, approvati dal proprietario il 2026-11-18.

**Contenuto:** anagrafica di personale, 20 colonne (`ID`, `Nome`, `Cognome`, `Eta`, date, città, ruolo, `Email`, `Telefono`, …). ASCII, virgola, CRLF, nessuna virgoletta, nessun valore vuoto.

## Cosa lo rende difficile

- **`Telefono` sembra un intero**: tutti i valori sono 10 cifre che iniziano con `3` (cellulari italiani, es. `3152831299`). Un lettore ingenuo li trasforma in numeri: si perdono gli eventuali prefissi `+39` o zeri in altri file, si aprono a somme e medie senza senso, in Excel diventano `3,15E+09`. Fedele deve riconoscerli come **telefono** e tenerli come testo. Qui il nome della colonna aiuta, ma la forma da sola (10 cifre, inizio `3`) è già un indizio.
- **`ID` ha zeri dopo un prefisso**: `ID0000001`. È testo; se qualcuno togliesse il prefisso, gli zeri iniziali andrebbero conservati.
- **Tre colonne di date ISO** (`yyyy-MM-dd`), da riconoscere come date senza ambiguità giorno/mese.
- **Colonne a pochi valori** (`Livello`, `Stato`, `Priorita`, `Categoria`): candidate a enumerazioni, ma restano `text`.

## Cosa non è compito del lettore

I dati sono generati a caso e **incoerenti tra colonne**: età 54 con data di nascita 2011, Bologna in Sicilia, email con un nome diverso dalla persona. Fedele legge e tipizza le colonne una per una; la coerenza tra colonne sarà compito dei contratti (fase 2), non del lettore.

## Curiosità del file originale

Il nome dice 1.000.000 righe, ma il file ne contiene 784.988 di dati: forse fu generato o copiato interrotto. `analyze` lo ha rilevato senza problemi (ultima riga terminata da CRLF, nessun record irregolare).
