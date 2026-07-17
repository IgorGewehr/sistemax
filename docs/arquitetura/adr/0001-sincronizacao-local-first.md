# ADR-0001 — Sincronização Local-First: log de eventos append-only (CRDT onde é são) + reconciliação autoritativa para invariantes

**Status:** Aceito · **Data:** 2026-07-16 · **Contexto do produto:** ERP de bancada, 3 camadas (PDV ↔ servidor de loja LAN ↔ nuvem), offline-first, dados de **estoque / financeiro / fiscal** — que têm invariantes legais e de negócio duras.

## Pergunta que este ADR responde
> "A arquitetura precisa ser Local-First (Offline-First) com sincronização baseada em CRDTs?"

**Resposta curta:** **Local-First, sim — é o núcleo.** **CRDT, seletivamente — onde ele é são; NÃO como mecanismo universal de sync.** Para um ERP financeiro, "CRDT em tudo" é uma armadilha conhecida. Abaixo o porquê e o desenho.

## Decisão

1. **Local-First é o núcleo (não-negociável).** Cada nó tem **SQLite (WAL, ACID)** como fonte da verdade local. O sistema funciona **100% offline**; a rede é uma otimização, não um requisito. Cai a internet no meio de uma venda → a loja segue na LAN, o PDV persiste local e recupera no boot.

2. **Primitivo de sincronização = log de eventos _append-only_ por nó (outbox).** Cada evento carrega um **ULID** (ordenável no tempo + globalmente único). O merge entre nós é **união + dedup por ULID**. Isso **é** um CRDT — um **G-Set (grow-only set)**: a operação de merge é comutativa, associativa e idempotente, então ganhamos **convergência forte eventual de graça**, sem coordenação.

3. **Read-models derivados por _fold_ determinístico do log (event-sourcing).** O estado exibido (saldos, estoque, KPIs) é uma projeção reprocessável dos eventos. Reprocessar é seguro (idempotente por ULID/SourceRef).

4. **CRDT onde é são (merge natural, sem invariante frágil):**
   - o **log de eventos** (G-Set);
   - a **razão contábil** — lançamentos são **imutáveis**; estorno é um novo lançamento, nunca um update → append-only puro;
   - **config/preferências** → LWW-Register (last-write-wins por campo);
   - presença/sinais efêmeros.

5. **Reconciliação AUTORITATIVA (não merge cego) onde há invariante dura:**
   - **Estoque não-negativo** → reserva/**escrow**: o servidor de loja particiona o disponível entre os PDVs (ou reconcilia por regra). Dois PDVs vendendo a última unidade offline **não podem** convergir para "estoque = −1".
   - **Numeração fiscal (NF-e/NFC-e gapless e única)** → alocação autoritativa de faixas por nó (ou central). Um contador CRDT criaria buracos/duplicatas — **inaceitável legalmente**.
   - **Postagens financeiras que dependem de saldo** → regra de negócio na reconciliação, não união cega.
   - Toda operação é **idempotente** (SourceRef/ULID) → replay seguro.

## Por que NÃO "CRDT em tudo"
CRDT garante **convergência** (todos os nós chegam ao mesmo estado), **não preservação de invariante**. Para dados financeiros/fiscais/estoque, **convergir para um estado inválido** (estoque negativo, NF-e duplicada, caixa que não fecha) é **pior** do que ter um conflito explícito para resolver. A escolha madura — usada por sistemas offline-first sérios — é: **CRDT para o que merge sozinho (logs, ledger, config); reserva/autoridade para o que tem lei.**

## Consequências
- **(+)** Offline de verdade; sync simples e robusto (união de logs); **auditável** por construção (event-sourced); escala bem (append-only, sem locks distribuídos).
- **(+)** O **financeiro fica fácil de deixar inteligente**: tudo é evento → uma nova análise/tool é um novo _fold_/read-model, **sem tocar no passado nem quebrar o que existe** (ver [[ADR sobre módulos e eventos]]).
- **(−)** Exige a camada de **reconciliação autoritativa** para invariantes (mais engenharia que merge cego — mas é o que torna o sistema correto, não só convergente).

## Estado atual no repo
`src/Infrastructure/Sync/` está em esqueleto; `src/Infrastructure/Local/` já tem SQLite + transação atômica de venda + backup/recovery. Este ADR fixa os princípios que o motor de sync vai implementar. Detalhes de robustez em `docs/robustez/`.
