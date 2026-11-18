---
name: fine-sessione
description: Chiude una sessione di lavoro su Fedele aggiornando il diario di progetto e facendo il commit. Da lanciare a mano alla fine di ogni sessione.
disable-model-invocation: true
---

# Fine sessione

1. Esegui `dotnet build` e `dotnet test`. Se qualcosa fallisce, dillo al proprietario e chiedi se correggere ora o annotarlo.
2. Aggiorna `docs/stato.md`:
   - "Dove siamo": fase e settimana correnti;
   - spunta gli obiettivi raggiunti;
   - "Prossimo passo": una sola azione concreta da cui ripartire;
   - "Domande aperte": decisioni che aspettano il proprietario;
   - "Diario": una nuova riga in alto con la data di oggi, cosa è stato fatto e cosa resta.
3. Se la struttura è cambiata, controlla che `docs/architettura.md` sia allineato.
4. Mostra al proprietario cosa entrerà nel commit (`git status`, riassunto delle modifiche) e, dopo il suo ok, fai il commit con un messaggio in inglese in stile Conventional Commits.
5. Chiudi con un riepilogo in italiano di massimo 5 righe: cosa è stato fatto, cosa ha imparato il progetto, da dove si riparte.
