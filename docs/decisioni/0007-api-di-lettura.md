# 0007 — API di lettura: prima si apre, poi si legge

- **Stato:** accettata
- **Data:** 2026-11-18

## Contesto

Lo schizzo iniziale di `docs/architettura.md` prevedeva un unico metodo `IFormatReader.ReadAsync` che restituisce subito i record. Scrivendo il lettore CSV sono emersi tre bisogni che quello schizzo non copre:

- chi usa la libreria vuole vedere **cosa Fedele ha dedotto** (encoding, separatore, intestazione) e con quale confidenza **prima** di leggere un file da gigabyte, per fermarsi o correggere;
- i **problemi** (`Issue`) nascono durante la lettura e devono avere un posto dove finire;
- alcuni fatti si conoscono solo **alla fine** del file: un file che nei primi 64 KB è ASCII può contenere UTF-8 più avanti, il numero di record e i fine riga si sanno solo dopo l'ultimo byte.

## Decisione

La lettura avviene in due passi.

```csharp
public interface IFormatReader
{
    string FormatId { get; }
    FormatDetection Detect(ReadOnlySpan<byte> sample);
    ValueTask<IRecordReader> OpenAsync(Stream input, ReadOptions? options = null, CancellationToken ct = default);
}

public interface IRecordReader : IAsyncDisposable
{
    FormatLayout Layout { get; }                  // dedotto all'apertura, ogni fatto con confidenza e motivo
    IAsyncEnumerable<RawRecord> ReadRecordsAsync(CancellationToken ct = default);
    IReadOnlyList<Issue> Issues { get; }          // cresce durante la lettura, con un limite
    long DroppedIssueCount { get; }               // problemi oltre il limite: contati, non conservati
    ReadSummary? Summary { get; }                 // disponibile quando la lettura è finita
}
```

- **`OpenAsync`** legge solo un campione iniziale (64 KB per il CSV) e deduce il layout. Non richiede uno stream riavvolgibile: il campione resta in memoria e viene riletto come inizio del file. Funziona quindi anche con un upload web.
- **`ReadRecordsAsync`** si può chiamare una sola volta: lo stream si consuma leggendolo.
- **`RawRecord`** ha il numero del record (l'intestazione conta come 1, come in ADR 0006; le righe completamente vuote non contano), la riga fisica e la posizione in byte del suo inizio: l'originale è sempre recuperabile. I campi sono testo, mai tipizzati: i tipi sono il compito di un passo successivo.
- **`Issue`** ha un codice stabile in inglese (`empty-record`), una gravità, un messaggio leggibile e la posizione (record, riga, colonna). I codici sono un contratto: si aggiungono, non si rinominano.
- **`Detected<T>`** accompagna ogni deduzione con una confidenza da 0 a 1 e un motivo leggibile da una persona.
- **`FormatLayout`** e **`ReadSummary`** sono classi base in `Core`; ogni formato le estende (`CsvLayout`, `CsvReadSummary`) e le descrive come elenco di fatti generici, così la CLI le può mostrare senza conoscere il formato.
- Il **riconoscimento dell'encoding** sta in `Core`, perché servirà anche a JSON e XML.
- Un byte non valido nell'encoding scelto non viene corretto in silenzio: il testo del campo contiene il carattere di sostituzione `U+FFFD` e Fedele segnala `invalid-byte` con la posizione.

## Conseguenze

- L'API è asincrona. Una variante sincrona si può aggiungere più avanti senza rompere nulla.
- Le classi base `FormatLayout` e `ReadSummary` non sono `sealed`: è un'eccezione voluta alla convenzione.
- Il riconoscimento dell'intestazione arriva con l'inferenza dei tipi; finché non c'è, `ReadOptions.Header` dice se la prima riga è un'intestazione (per default sì).
- Le ottimizzazioni (campi come `ReadOnlyMemory<char>`, meno allocazioni) restano possibili dietro la stessa API e si faranno solo con i benchmark.
