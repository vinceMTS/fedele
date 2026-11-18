# 0002 — Solo interi, ventuno colonne

**Origine:** `dataset_predittivo_10000x20.csv`, copia identica (SHA-256 `5b404714c808892539b12fbc1b9e4d83458b888f455772a5b39f0af8e224758e`). Dati sintetici, approvati dal proprietario il 2026-11-18.

**Contenuto:** `ID` e 20 colonne `C1`…`C20`; nella riga *n* la colonna `Ck` vale `n × 1000 + k`. ASCII, virgola, CRLF, nessuna virgoletta, nessun valore vuoto.

## Cosa lo rende difficile

È soprattutto un **caso di controllo**: un file pulito su cui Fedele non deve inventare problemi.

- **I valori crescono lungo il file**: `C1` va da `1001` (4 cifre) a `10000001` (8 cifre). Un lettore che deduce il tipo, o la larghezza, solo dalle prime righe sbaglia sulle ultime.
- **Nessun separatore delle migliaia**: `10000001` è un intero, non `10.000.001`. Fedele non deve aggiungere né togliere nulla.
- **Nessuna colonna di testo** tranne l'intestazione: la prima riga va riconosciuta come intestazione perché i nomi (`C1`, `C2`, …) non sono numeri, anche se sono brevi e "tecnici".
- **Larghezza**: 21 colonne, utile per misurare le prestazioni quando ci saranno i benchmark.
