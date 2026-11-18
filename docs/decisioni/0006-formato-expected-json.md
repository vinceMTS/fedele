# 0006 — Formato di `expected.json` nel museo

- **Stato:** proposta
- **Data:** 2026-11-18

## Contesto

Ogni caso del museo (`museum/NNNN-slug/`) dichiara cosa Fedele deve rilevare. `Fedele.Museum.Tests` leggerà questi file per scoprire i casi da soli e confrontarli con il risultato di Fedele. Il formato diventa un contratto stabile tra il museo e i test: cambiarlo dopo significa riscrivere tutti i casi.

## Decisione

Un file JSON per caso, in inglese (è un artefatto tecnico, come il codice), con quattro sezioni:

```json
{
  "description": "One sentence: what makes this file hard.",
  "source": { "kind": "full-copy", "note": "optional" },
  "file": {
    "encoding": "ascii",
    "bom": false,
    "lineEnding": "crlf",
    "finalLineEnding": true,
    "separator": ",",
    "quote": "\"",
    "header": true,
    "records": 10000,
    "fieldsPerRecord": 5
  },
  "columns": [
    { "name": "Importo", "type": "decimal", "decimalSeparator": ",", "thousandsSeparator": "." },
    { "name": "Data", "type": "date", "format": "dd/MM/yyyy" },
    { "name": "Telefono", "type": "text", "semantic": "phone" }
  ],
  "issues": [
    { "code": "empty-record", "record": 10002, "column": null }
  ]
}
```

Regole:

- **`source.kind`**: `full-copy` (file intero) oppure `prefix` (prime righe di un file più grande, con `--max-records`).
- **`file.encoding`**: `ascii`, `utf-8`, `utf-16le`, `utf-16be`, `windows-1252`. **`lineEnding`**: `crlf`, `lf`, `cr`, `mixed`.
- **`file.records`**: record di dati, intestazione esclusa, righe completamente vuote escluse; un record di soli separatori (`,,,,`) **conta**.
- **`columns[].type`**: `text`, `integer`, `decimal`, `date`, `datetime`, `boolean`. È il tipo che Fedele deve dedurre, **non** quello che "sembra": un numero di telefono è `text`.
- **`columns[].semantic`** (facoltativo): id del tipo semantico atteso (`phone`, `email`, `it.tax-code`, `it.vat-number`, `iban`, ...).
- Per `decimal`: `decimalSeparator` e `thousandsSeparator` (`null` se assente). Per `date` e `datetime`: `format` con la sintassi .NET (`yyyy-MM-dd`).
- **`issues[]`**: i problemi che Fedele deve segnalare. `record` è il numero del record contando l'intestazione come 1 (come `analyze`); `column` è il nome della colonna o `null`. I codici si definiscono man mano che Fedele li implementa; il primo è `empty-record`.
- Campi sconosciuti sono un errore: i test devono fallire su un refuso, non ignorarlo.

## Conseguenze

- `Fedele.Museum.Tests` può verificare subito la sezione `file` per i fatti leggibili dai byte (encoding, BOM, fine riga); il resto quando esisterà il lettore CSV.
- Aggiungere un campo è compatibile; rinominarne o cambiarne il significato richiede un nuovo ADR e l'aggiornamento di tutti i casi.
