# Fedele

**Read data files without betraying them.**

Fedele is an open source .NET library that reads data files (CSV today, then Excel, JSON and XML)
without ever altering what they contain: no lost leading zeros, no invented dates, no wrong decimals.
It speaks Italian and European formats natively.

> **Status:** early development (phase 1, "the honest reader"). Nothing is published on NuGet yet.

## Principles

1. **No silent conversions.** When in doubt, a value stays text and Fedele reports an issue with row, column and explanation.
2. **The original is always recoverable.** Every typed value keeps the raw text it came from.
3. **Every inference has a confidence and a reason** that a person can read.
4. **Streaming by default.** Files of several gigabytes work without being loaded into memory.
5. **Italian and European formats are first class:** `;` as separator, `1.234,56`, `dd/mm/yyyy` dates,
   Windows-1252, codice fiscale, partita IVA, IBAN.

## Packages

| Package | Purpose |
| --- | --- |
| `Fedele.Core` | Data model, type inference, issues and extension points. No dependencies. |
| `Fedele.Formats.Csv` | Streaming CSV reader. |
| `Fedele.Semantics.Italy` | Codice fiscale, partita IVA, IBAN, CAP, provinces. |
| `Fedele.Cli` | The `fedele` command-line tool. |

Each module depends only on `Fedele.Core`: install just what you need.

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet test
dotnet run --project src/Fedele.Cli
```

## The museum of horrors

`museum/` collects real-world files that are hard to read correctly, anonymized and kept as
regression tests. Every case states what Fedele must detect.

## License

[Apache License 2.0](LICENSE). See also [NOTICE](NOTICE).

---

## In italiano

**Fedele legge i file di dati senza tradirli.**

Fedele è una libreria .NET open source che legge file di dati (oggi CSV, poi Excel, JSON e XML)
senza mai alterarne il contenuto: nessuno zero iniziale perso, nessuna data inventata, nessun
decimale sbagliato. Parla nativamente i formati italiani ed europei: `;` come separatore, importi
come `1.234,56`, date `gg/mm/aaaa`, Windows-1252, codice fiscale, partita IVA e IBAN.

Nel dubbio un valore resta testo e Fedele segnala il problema con riga, colonna e spiegazione;
il testo originale di ogni valore è sempre recuperabile.

Il progetto è nelle prime fasi di sviluppo. Segnalazioni e file "difficili" (anonimizzati!) sono
benvenuti tramite le issue di GitHub.
