# Architettura

## Struttura delle cartelle

Le voci segnate *(futuro)* non vanno create ora: indicano dove andranno quando servono, così la struttura di oggi è già pronta ad accoglierle.

```text
Fedele/
├── CLAUDE.md                     istruzioni per Claude Code
├── README.md                     in inglese, con una sezione in italiano
├── LICENSE                       Apache-2.0, testo ufficiale
├── NOTICE
├── Fedele.slnx                   soluzione (formato .slnx, predefinito in .NET 10)
├── global.json                   fissa l'SDK .NET 10 e il motore di test (Microsoft.Testing.Platform)
├── Directory.Build.props         impostazioni comuni a tutti i progetti
├── Directory.Packages.props      versioni centralizzate dei pacchetti NuGet
├── .editorconfig                 stile del codice; non tocca museum/
├── .gitattributes                museum/: git non converte mai fine riga, BOM o encoding
├── .gitignore                    include private-data/
├── .github/workflows/ci.yml      build + test su Windows e Linux
├── .claude/                      impostazioni e skill di Claude Code
│
├── src/
│   ├── Directory.Build.props     pacchetti NuGet: metadati, README, commenti XML obbligatori, AOT
│   ├── Fedele.Core/              modello dei dati, inferenza dei tipi, problemi, punti di estensione
│   ├── Fedele.Formats.Csv/       lettura (e poi scrittura) CSV
│   ├── Fedele.Semantics.Italy/   codice fiscale, partita IVA, IBAN, CAP, province
│   ├── Fedele.Cli/               lo strumento `fedele`
│   ├── Fedele.Contracts/         (futuro, fase 2) schema portabile e validazione
│   ├── Fedele.Formats.Excel/     (futuro, fase 3)
│   ├── Fedele.Formats.Json/      (futuro, fase 3)
│   ├── Fedele.Formats.Xml/       (futuro, fase 3)
│   ├── Fedele.Semantics.Europe/  (futuro) tipi semantici di altri paesi
│   ├── Fedele.FatturaPA/         (futuro, fase 5)
│   ├── Fedele.Import.Api/        (futuro, fase 4) API ASP.NET Core dell'importatore
│   └── Fedele.Import.Web/        (futuro, fase 4) interfaccia Blazor dell'importatore
│
├── tests/
│   ├── Directory.Build.props     xUnit v3, progetti non pubblicabili, include Shared/
│   ├── Shared/                   codice comune ai test (es. verifiche delle dipendenze)
│   ├── Fedele.Core.Tests/
│   ├── Fedele.Formats.Csv.Tests/
│   ├── Fedele.Semantics.Italy.Tests/
│   ├── Fedele.Cli.Tests/
│   ├── Fedele.Anonymizer.Tests/  test dello strumento: copia identica byte per byte, forma dei valori, privacy
│   └── Fedele.Museum.Tests/      esegue Fedele su ogni caso del museo e confronta con il risultato atteso
│
├── museum/                       il museo degli orrori: file anonimizzati
│   └── 0001-<nome-breve>/
│       ├── input.csv             il file (anonimizzato)
│       ├── expected.json         cosa Fedele deve rilevare
│       └── README.md             cosa rende difficile questo file
│
├── tools/
│   ├── Directory.Build.props     strumenti interni, non pubblicabili
│   └── Fedele.Anonymizer/        anonimizza i file reali preservandone le stranezze
│
├── benchmarks/
│   └── Fedele.Benchmarks/        (dalla fase 1, settimana 5) BenchmarkDotNet
│
├── docs/
│   ├── architettura.md           questo file
│   ├── stato.md                  diario: dove siamo, prossimo passo
│   ├── avvio.md                  il kit di avvio originale (documento storico)
│   └── decisioni/                ADR: una decisione per file
│
└── private-data/                 CSV reali, MAI su git, MAI letti da Claude
```

## Regole di dipendenza

```text
Fedele.Cli ──────────┬──> Fedele.Formats.Csv ─────┐
(e in futuro         ├──> Fedele.Semantics.Italy ─┼──> Fedele.Core
 Fedele.Import.*)    └──> (altri moduli) ─────────┘
```

- `Fedele.Core` non dipende da nulla: niente pacchetti esterni.
- I moduli (`Formats.*`, `Semantics.*`, `Contracts`, `FatturaPA`) dipendono **solo** da `Fedele.Core`, mai l'uno dall'altro. Se due moduli devono condividere qualcosa, quel qualcosa va in `Core`.
- Le applicazioni (`Fedele.Cli`, `Fedele.Import.*`) sono l'unico punto in cui i moduli vengono messi insieme.
- `tools/` e `benchmarks/` possono dipendere da tutto, ma nulla in `src/` dipende da loro.
- Queste regole sono verificate da **test di architettura** (`ArchitectureTests` in ogni progetto di test, con l'aiuto di `tests/Shared/AssemblyReferences.cs`): una dipendenza sbagliata fa fallire la build in CI.

## Impostazioni a livelli

Ogni `Directory.Build.props` di cartella importa quello della radice e aggiunge solo ciò che è specifico:

- **radice**: .NET 10, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, analizzatori e stile del codice applicati in compilazione;
- **`src/`**: metadati dei pacchetti NuGet, commenti XML obbligatori su ogni API pubblica, `IsAotCompatible`;
- **`tests/`**: xUnit v3 su Microsoft.Testing.Platform (vedi ADR 0005), progetti non pubblicabili;
- **`tools/`**: progetti non pubblicabili.

## Punti di estensione (in `Fedele.Core`)

Lo schizzo qui sotto è un punto di partenza: i dettagli si raffinano scrivendo il codice, ma il principio resta. Ogni modulo aggiunge capacità implementando un'interfaccia di `Core` e registrandosi con un metodo di estensione.

```csharp
namespace Fedele.Core;

/// Legge un formato di file in streaming.
public interface IFormatReader
{
    string FormatId { get; }                                   // "csv"
    FormatDetection Detect(ReadOnlySpan<byte> sample);         // quanto è probabile che sia questo formato
    IAsyncEnumerable<RawRecord> ReadAsync(Stream input, ReadOptions options, CancellationToken ct = default);
}

/// Riconosce e valida un tipo di dato con un significato (codice fiscale, IBAN, ...).
public interface ISemanticType
{
    string Id { get; }                                         // "it.tax-code", "iban"
    SemanticMatch Match(ReadOnlySpan<char> value);             // Valid / Invalid(motivo) / NotApplicable
    string GenerateSample(Random random);                      // valore finto ma valido: serve all'anonimizzatore
}

/// Punto di composizione: le applicazioni scelgono i moduli.
var fedele = new FedeleBuilder()
    .AddCsv()                  // da Fedele.Formats.Csv
    .AddItalianSemantics()     // da Fedele.Semantics.Italy
    .Build();
```

Ogni tipo semantico sa anche **generare valori finti ma validi** (codici fiscali con il carattere di controllo giusto, IBAN con il modulo 97 corretto). È ciò che permette all'anonimizzatore di sostituire i dati reali senza distruggere le caratteristiche che rendono interessante un file.

## L'anonimizzatore (`tools/Fedele.Anonymizer`)

- `analyze <file|cartella>` stampa solo la struttura: encoding, BOM, fine riga, separatore, virgolette, record, nomi delle colonne (solo se la prima riga sembra un'intestazione) e il tipo probabile di ogni colonna, dedotto dalla forma. Mai i valori.
- `anonymize <input> <output> --columns <elenco|none>` sostituisce i valori delle colonne scelte con valori finti della stessa forma; tutti gli altri byte sono copiati identici. `--columns none` produce una copia identica verificata con SHA-256.
- Legge il file come sequenza di "unità" (byte per UTF-8 e Windows-1252, coppie di byte per UTF-16) che si riconvertono esattamente nei byte originali: è ciò che garantisce la copia fedele.
- Il riconoscimento di encoding e separatore e la classificazione dei valori sono per ora **interni allo strumento**. Quando progetteremo `Fedele.Formats.Csv` e `Fedele.Semantics.Italy`, quella logica si sposterà lì e lo strumento la userà dai moduli.

## Pacchetti e distribuzione

- Ogni progetto in `src/` (tranne le applicazioni) diventa un pacchetto NuGet con lo stesso nome.
- `Fedele.Cli` si distribuisce in due modi: come `dotnet tool` (comando `fedele`) e come eseguibile unico Native AOT per Windows, macOS e Linux nelle GitHub Releases.
- I comandi della CLI sono in inglese (`inspect`, `infer`, `validate`, `convert`); i messaggi all'utente sono localizzabili, con l'italiano e l'inglese supportati dall'inizio.

## Il museo degli orrori

Ogni caso in `museum/` è una cartella con un file di input, il risultato atteso (`expected.json`: encoding, separatore, intestazione, tipo di ogni colonna, problemi attesi) e un `README.md` che spiega cosa lo rende difficile. `Fedele.Museum.Tests` scopre i casi da solo: aggiungere un caso non richiede di scrivere codice di test. Il formato di `expected.json` è fissato dall'ADR 0006.
