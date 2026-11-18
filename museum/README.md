# Museum of horrors / Museo degli orrori

Real-world data files that are hard to read correctly, anonymized and kept as regression tests.
`Fedele.Museum.Tests` runs Fedele on every case and compares the result with `expected.json`.

File di dati reali difficili da leggere correttamente, anonimizzati e conservati come test di regressione.

## Structure / Struttura

```text
museum/
└── 0001-<short-name>/
    ├── input.csv       the anonymized file, stored byte for byte (see .gitattributes)
    ├── expected.json   what Fedele must detect: encoding, separator, header, column types, issues
    └── README.md       what makes this file hard
```

## Rules / Regole

- Only anonymized files, approved by the owner, enter the museum. See `docs/decisioni/0004-privacy-dei-dati-reali.md`.
- Input files are never normalized: encoding, BOM, line endings and whitespace are part of each case.
- Add a case with the `/aggiungi-al-museo` skill. No test code is needed.
