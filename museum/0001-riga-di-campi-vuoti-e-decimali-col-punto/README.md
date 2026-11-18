# 0001 — Riga di campi vuoti e decimali col punto

**Origine:** `dati_casuali.csv`, copia identica (SHA-256 `d75014533a345614860b1dce6d3a45357de804db94989ccc44969f1ba4eeec12`). Dati sintetici, approvati dal proprietario il 2026-11-18.

**Contenuto:** 10.000 persone con `ID`, `Nome`, `Eta`, `Punteggio`, `Data`. ASCII, virgola come separatore, CRLF, nessuna virgoletta.

## Cosa lo rende difficile

- **L'ultimo record è `,,,,`**: cinque campi vuoti. Non è una riga vuota (ha i separatori), quindi è un record vero, ma quasi certamente è un residuo dell'esportazione. Fedele deve contarlo, lasciare i valori vuoti (mai `0` o una data inventata) e segnalarlo con `empty-record`.
- **Nomi italiani, decimali all'inglese**: `25.99`, non `25,99`. Fedele non deve dedurre il formato dei numeri dalla lingua dei dati, ma dai numeri stessi.
- **Precisione variabile**: circa 1.000 valori hanno una sola cifra decimale (`75.0`, `19.2`), perché chi ha generato il file ha tolto lo zero finale. La colonna resta `decimal`, e il testo originale di ogni valore deve restare recuperabile.
- **Nessun valore ambiguo** del tipo `1.234`: qui il punto è sempre decimale, ma un lettore che non controlla tutta la colonna non lo sa.
