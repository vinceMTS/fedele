# 0005 — Test con xUnit v3 e Microsoft.Testing.Platform

- **Stato:** accettata
- **Data:** 2026-11-18

## Contesto

`CLAUDE.md` prevede xUnit. Con .NET 10 esistono due motori per eseguire i test: il vecchio VSTest (che richiede `Microsoft.NET.Test.Sdk` e `xunit.runner.visualstudio`) e il nuovo Microsoft.Testing.Platform (MTP), integrato in `dotnet test` dal .NET 10 SDK. xUnit v3 dalla versione 4 usa MTP per default.

## Decisione

- Unico pacchetto di test: `xunit.v3` (4.0.3 all'avvio), versione in `Directory.Packages.props`.
- `global.json` imposta `"test": { "runner": "Microsoft.Testing.Platform" }`, così `dotnet test` usa MTP.
- I progetti di test sono eseguibili (`OutputType Exe`) e ricevono le impostazioni comuni da `tests/Directory.Build.props`.
- Ogni progetto di test contiene almeno un test: MTP considera un errore un progetto senza test. I primi test sono **test di architettura** che verificano le regole di dipendenza di `docs/architettura.md`.

## Conseguenze

- Meno pacchetti da mantenere; i test si possono anche lanciare direttamente come eseguibili.
- Gli strumenti che conoscono solo VSTest (IDE molto vecchi) non vedono i test; Visual Studio 2022 17.14+, Visual Studio 2026 e VS Code con C# Dev Kit li supportano.
- La copertura del codice, quando servirà, userà un'estensione MTP (`Microsoft.Testing.Extensions.CodeCoverage`), da aggiungere con un nuovo ok.
