# Lições reutilizáveis do Supermarket-OS — crash-safety, durabilidade, sync multi-terminal e hardware

> Fonte: `/Users/igorgewehr/Documents/GitHub/Supermarket-OS` (Node/TypeScript, Electron + Fastify + Postgres + SQLite).
> Objetivo: extrair PRINCÍPIOS (independentes de linguagem) para reimplementação em C#/.NET de um ERP de varejo BR
> que precisa sobreviver a quedas de luz/internet/PC sem corromper dados, com ~10 PCs por loja (9 PDV + 1 admin)
> compartilhando estado em tempo real.

---

## 1. Recuperação de crash de venda em andamento

### O que existe de fato (dois mecanismos, só um está realmente ligado)

**Mecanismo A — "Transaction Journal" (existe no repo, mas está MORTO):**
`apps/desktop/src/main/transaction-journal.ts:1-121` implementa exatamente o padrão clássico de write-ahead
journal de aplicação: antes de cada mutação crítica, grava um JSON num arquivo temporário, `fsync`, depois
`rename` atômico (`persistSnapshot()`, linhas 51-68); no boot, `recoverFromJournal()` (linhas 74-95) leria o
arquivo de volta. **Evidência de que está morto**: nenhum arquivo do repo importa
`transaction-journal` (`grep -rn "transaction-journal" .` não retorna nenhum caller fora do próprio arquivo).
O roadmap interno (`FUTUREMARKET.md:29-34`) descreve esse arquivo como item "(novo)" a ser criado — ele foi
escrito conforme o roadmap, mas nunca foi conectado ao `saleStore` nem ao boot do `main/index.ts`.

**Mecanismo B — Auto-save de rascunho em SQLite (este É o que funciona de verdade):**
`apps/desktop/src/renderer/stores/saleStore.ts:1341-1374` — um `subscribe()` no Zustand store dispara,
a cada mudança de estado, um `setTimeout` **debounced em 2000ms** que serializa o carrinho inteiro
(itens, pagamentos, totais, operador) e grava via IPC `api.drafts.save(saleId, json)` na tabela
`sale_drafts` do SQLite local (`packages/db-local/src/schema.ts:597-601`).

No boot, `PosLayout.tsx:138-155` chama `drafts.loadLatest()`; se existir rascunho com itens, mostra um
**diálogo de confirmação** ("Venda em andamento encontrada — Recuperar/Descartar",
`PosLayout.tsx:403-469`) — nunca restaura silenciosamente. Ao escolher "Recuperar",
`saleStore.recoverFromDraft()` (linhas 1277-1332) repõe o estado exato do carrinho.//
Ao finalizar a venda com sucesso, o draft é apagado (`saleStore.ts:862`).

Há ainda um terceiro caminho, `recoverSale()` (linhas 1112-1152), que tenta ler uma venda `OPEN` direto
da tabela `sales` via `api.sales.getOpen()` — mas esse método **não existe no preload/IPC**
(`grep -n "getOpen" apps/desktop/src/preload/index.ts` só mostra `cash.getOpen`, não `sales.getOpen`).
Chamado dentro de um `try/catch` que ignora tudo, então falha silenciosamente sem quebrar o app — mas é
código morto disfarçado de mecanismo funcional.

### O verdadeiro "cofre" de crash-safety: a transação SQL atômica no fechamento da venda

O que realmente impede corrupção não é nenhum dos rascunhos acima — é que **o fechamento da venda inteiro
é UMA ÚNICA transação SQLite local**, feita no processo principal (não no renderer):
`apps/desktop/src/main/database.ts:398-578` (`db:sales:finishBatch`). Dentro de
`database.transaction(() => { ... })` (linha 437, que por baixo é `better-sqlite3`'s `db.transaction()`):

1. aloca o próximo número de sequência local (`sequences` table, linhas 440-462) — **sem depender do servidor**;
2. `INSERT INTO sales (... status='FINISHED' ...)` (linha 473);
3. `INSERT` em lote de todos os itens (linhas 487-504);
4. `INSERT` em lote de todos os pagamentos, incluindo dados de TEF (linhas 506-522);
5. `UPDATE` de estoque + `INSERT` de `stock_movements` para cada item ativo (linhas 530-559);
6. `INSERT` na fila de sync (`sync_queue`) com uma `idempotency_key` ULID (linhas 561-569).

Como é uma única transação SQLite (BEGIN...COMMIT via WAL), **ou tudo isso é gravado, ou nada é** — mesmo
que o processo morra no meio (queda de luz, `kill -9`, BSOD). Não existe "venda meio-finalizada": ao reiniciar,
o SQLite descarta a transação incompleta (rollback automático do WAL) e o pior caso é o draft (mecanismo B)
continuar existindo, provocando o diálogo de recuperação.

### PRINCÍPIO a reimplementar em .NET

- **A unidade de crash-safety é a transação do motor de banco local, não a lógica da aplicação.** Em vez de
  orquestrar 25 passos via chamadas de app (o comentário em `saleStore.ts:794-803` até documenta que essa
  era a versão antiga — "Replaces 25+ sequential IPC round-trips"), o fechamento de venda deve ser **um único
  batch/stored procedure/transação local** (SQLite local em .NET: `Microsoft.Data.Sqlite` + `SqliteTransaction`,
  ou LiteDB/SQLite embarcado). Nunca faça "criar venda" → "adicionar item" → "adicionar pagamento" como
  chamadas HTTP/IPC separadas e independentes: qualquer uma pode falhar isoladamente e deixar estado órfão.
- **Numeração de sequência é local e não depende do servidor estar de pé** — crítico para operar 100% offline.
  A reconciliação de duplicidade (dois PDVs gerando o mesmo número enquanto offline) é resolvida **depois**,
  no servidor (ver seção 3).
- **Auto-save de rascunho é FEATURE DE UX (evitar redigitar o carrinho), não é o mecanismo de integridade
  transacional.** Trate os dois como camadas independentes: (a) draft best-effort para não perder digitação
  de carrinho ainda não pago; (b) transação atômica indivisível no momento em que dinheiro/pagamento é
  confirmado. Nunca prometa "crash-safety" baseado só em (a).
- **Recuperação nunca deve ser automática e silenciosa quando dinheiro real pode estar envolvido** — o padrão
  do Supermarket-OS de perguntar ao operador "Recuperar ou Descartar" (em vez de re-aplicar silenciosamente)
  evita duplo lançamento ou re-cobrança acidental quando o estado é ambíguo (ex: pagamento TEF pode ter
  sido aprovado no adquirente mas a resposta não chegou antes do crash).

### FRAQUEZAS a corrigir na reimplementação

1. **Debounce de 2s no auto-save é resetado a cada mudança de estado** (`saleStore.ts:1343`:
   `if (draftSaveTimer) clearTimeout(draftSaveTimer)`). Se o operador estiver escaneando itens rapidamente
   (menos de 2s entre scans), **o rascunho nunca é persistido** até haver uma pausa — ou seja, atividade
   contínua atrasa indefinidamente a durabilidade do "melhor esforço". Em .NET, use um agendador que
   também dispara por um **teto de tempo absoluto** (ex.: salvar no máximo a cada 2s *desde o último save*,
   não desde a última mudança — via `throttle`, não `debounce`).
2. **Código morto duplicado e concorrente confunde manutenção**: dois mecanismos de crash-recovery
   (`transaction-journal.ts` nunca chamado + `sale_drafts` que funciona) convivem no mesmo repositório, e
   um terceiro (`recoverSale()`) chama uma API que não existe. Ao portar, escolha **um único mecanismo**
   de recuperação e remova os concorrentes — não deixe "provas de conceito" no código de produção sem
   marcação clara ou remoção.
3. **Nenhum fsync explícito no caminho quente do SQLite** além do que o WAL fornece por padrão
   (ver §2) — está OK porque `synchronous=NORMAL` + WAL é o padrão recomendado, mas vale registrar
   explicitamente essa escolha (ver trade-off na próxima seção).

---

## 2. Durabilidade local — SQLite WAL, backup, auto-recuperação de corrupção

Arquivo-fonte: `packages/db-local/src/database.ts` (usado no `db-local`; o app desktop tem uma classe
paralela em `apps/desktop/src/main/database.ts` com a mesma lógica de pragmas).

### Mecanismo exato

**Pragmas no construtor** (`database.ts:16-26`):
```
journal_mode = WAL          // grava mudanças num arquivo -wal separado, commit é append-only
synchronous = NORMAL        // fsync no checkpoint do WAL, não em cada transação (trade-off perf/durabilidade)
foreign_keys = ON
busy_timeout = 5000         // evita erro imediato "database is locked" sob contenção
cache_size = -64000         // 64MB cache
temp_store = MEMORY
mmap_size = 268435456       // 256MB mmap I/O
journal_size_limit = 16777216
```
`synchronous=NORMAL` (em vez de `FULL`) é uma escolha deliberada: com WAL, `NORMAL` garante que a base
nunca corrompe mesmo em crash do processo (a pior perda é "perder as últimas transações committed que
ainda não foram fsync'adas para o arquivo principal", não corrupção) — só `synchronous=OFF` arriscaria
corrupção real. Esse é o trade-off correto para um PDV: performance de escrita alta, e a garantia de
"não corrompe" é preservada.

**Integrity check condicional** (`database.ts:29-99`): `PRAGMA integrity_check` + `PRAGMA quick_check`
rodam **no máximo uma vez por dia** (guarda por timestamp salvo em `config` table, linha 37) — evita
pagar ~500ms+ de verificação toda vez que o app abre, mas ainda detecta corrupção periodicamente. Se
falhar, dispara `attemptRecovery()`.

**Backup rotativo** (`database.ts:109-156`): `createBackup()` faz `PRAGMA wal_checkpoint(TRUNCATE)`
(garante que todas as mudanças do WAL foram levadas ao arquivo principal antes de copiar) e depois
`fs.copyFileSync()` para `backups/db-<timestamp>.sqlite`. Mantém só os `MAX_BACKUPS = 7` mais recentes
(`cleanupOldBackups`, linhas 137-156). O comentário no código diz que deveria ser chamado "before each
cash session opening" — ou seja, o gatilho é um evento de negócio (abertura de caixa), não um timer puro.

**Auto-recuperação de corrupção** (`attemptRecovery()`, linhas 165-232) — sequência exata:
1. Fecha a conexão corrompida (`this.db.close()`, ignorando erro se já estiver em estado ruim).
2. Renomeia o arquivo `.sqlite` corrompido para `.sqlite.corrupted-<timestamp>` — **preserva para forense**,
   nunca deleta. Move também os arquivos `-wal` e `-shm` junto.
3. Procura o backup mais recente em `backups/` e faz `fs.copyFileSync()` de volta para o path original.
4. Se não houver backup nenhum, loga aviso e segue com banco **novo/vazio** (fail-open, não fail-closed —
   decisão consciente de manter o PDV vendendo mesmo sem histórico, em vez de travar a loja).
5. Reabre o banco com os mesmos pragmas.

### PRINCÍPIO a reimplementar em .NET

- **WAL (ou equivalente) + `synchronous=NORMAL`** é o ponto ótimo entre performance e "nunca corrompe".
  Em SQLite via `Microsoft.Data.Sqlite`, isso é literalmente `PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;`
  — mesma API. Se for outro motor embarcado (LiteDB, DuckDB local), procure o equivalente (LiteDB usa seu
  próprio WAL desde v5).
- **Integrity check é caro — rode-o raramente e de forma assíncrona/idempotente**, gatilhado por tempo
  (1x/dia) e por evento de negócio relevante (abertura de caixa), não em todo boot.
- **Backup = checkpoint + cópia de arquivo, não dump lógico** — muito mais barato e rápido que exportar
  dados; e a rotação com N cópias mais um `corrupted-<timestamp>` preservado (nunca sobrescrito
  automaticamente) dá tanto recuperação automática quanto rastro forense para auditoria.
- **Recovery é fail-open**: prefira reabrir com banco vazio/restaurado a travar o PDV indefinidamente.
  Registre isso como incidente (log + alerta ao admin), mas não pare a venda.

### FRAQUEZAS a corrigir

1. **Backup síncrono na UI thread do processo principal via `fs.copyFileSync`** — para bases grandes,
   isso pode travar o event loop do Electron por um tempo perceptível. Em .NET, use I/O assíncrono
   (`File.CopyAsync`/stream com `await`) para não bloquear a thread que atende IPC/UI.
2. **A checagem de corrupção só roda no boot** — não há verificação periódica *durante* a operação do dia
   (ex.: a cada N horas, ou após uma janela de picos de erro de I/O). Um disco degradando ao longo do dia
   só seria pego no próximo restart. Vale adicionar um checkpoint de integridade leve periódico (ex.:
   `PRAGMA quick_check` a cada X horas em background, fora do caminho de vendas).
2. **Nenhuma verificação de espaço em disco antes do backup** — se o disco estiver cheio, `copyFileSync`
   falha silenciosamente (capturado por `try/catch` genérico) e o usuário não é avisado que está sem
   backup válido. Adicionar checagem de espaço livre + alerta visível.

---

## 3. Modelo de sync — push/pull, idempotência, conflito por entidade, cursores

### Outbox transacional via TRIGGERS do banco (o principal ensinamento do repo)

`packages/db-local/src/schema.ts:634-800+` define, para cada tabela relevante (`products`, `sales`,
`sale_items`, `sale_payments`, `cash_sessions`, `cash_movements`, `stock_movements`, `customers`, etc.),
três **triggers SQL** (`AFTER INSERT`, `AFTER UPDATE`, `AFTER DELETE`) que inserem automaticamente uma
linha em `sync_queue` com o payload serializado em JSON e uma `idempotency_key`
(ex.: `packages/db-local/src/schema.ts:638-667` para `products`).

Isso implementa o **padrão "transactional outbox" no nível do motor de banco**, não na camada de
aplicação: é estruturalmente impossível gravar uma venda/pagamento/movimento de estoque **sem** enfileirar
o evento de sync correspondente, porque o trigger roda **dentro da mesma transação** do `INSERT`/`UPDATE`
que a disparou. Não existe caminho de código onde um dev esqueceu de chamar "enfileirar para sync" depois
de escrever dado local — o banco garante isso por construção.

### Fila local → push em lote → pull com cursor

- **Storage adapter** (`apps/desktop/src/main/sync.ts:23-57`): lê linhas `status='pending'` de
  `sync_queue`, marca como `confirmed` após ACK do servidor, incrementa `attempts` em falha.
- **Transport adapter** (`sync.ts:63-146`): `POST /api/sync/batch` (envia lote de até 50 por vez, definido
  em `sync-engine.ts:90`) e `GET /api/sync/pull?since=cursor` para trazer mudanças de outros terminais.
- **`SyncEngine.flush()`** (`packages/sync-engine/src/sync-engine.ts:78-131`): checa conectividade (`ping()`),
  envia lote, marca sucesso/falha por item, e **sempre puxa mudanças remotas logo depois de empurrar as
  locais** (linha 119) — push e pull são round-trips separados dentro do mesmo ciclo, não uma única
  chamada bidirecional.
- **Backoff exponencial** (`sync-engine.ts:176-180`): `base * 2^tentativa`, capado em 5 minutos; itens que
  excedem `maxRetries` são simplesmente ignorados no próximo flush (ficam "presos" com status `pending`
  mas fora do lote elegível — nenhum alerta automático ao operador/admin é dado nesse ponto, só
  side-effect de status `ERROR` no engine).
- **Idempotência no servidor** (`apps/server/src/routes/sync.routes.ts:24-115`): cada mudança chega com
  um `id` (o `idempotencyKey` ULID gerado no terminal). Antes de aplicar, o servidor checa se já existe
  uma linha `sync_queue` com esse `id` e `status='synced'` — se sim, responde `already_synced` sem
  reaplicar (linhas 48-58). Isso é o que torna seguro o terminal **reenviar o mesmo lote** depois de uma
  falha de rede no meio do envio (o cliente não sabe se o servidor recebeu ou não, então reenviar é seguro
  por construção).
- **Cursor de pull** (`sync.routes.ts:117-166`): usa `createdAt` (timestamp) como cursor, filtra
  `terminalId != meu_terminal` (não recebo minhas próprias mudanças de volta) e `status IN ('synced','processed')`.
  Cliente salva o cursor localmente (`sync_cursor` table, `sync.ts:241-253`) e retoma dali na próxima sessão.
- **Terminal que fica offline e volta**: nada de especial é necessário — a fila `sync_queue` local
  simplesmente acumula enquanto `ping()` falha (`SyncEngine` fica em status `OFFLINE`), e ao voltar,
  o próximo ciclo de `flush()` (rodando a cada `flushInterval`, padrão 15s, `sync.ts:348`) drena tudo
  em lotes de 50. Não há limite de tamanho de fila documentado — teoricamente pode crescer sem bound
  se o terminal ficar offline por dias.

### Resolução de conflito por entidade (política explícita, não "last write wins" genérico)

`apps/server/src/sync/conflict-resolver.ts:21-141` define uma tabela **por tipo de entidade**:

| Entidade | Estratégia | Racional |
|---|---|---|
| `products`, `categories`, `settings`, `employees`, `customers`, `promotions` | `server_wins` (com checagem de `version`: se terminal tiver `version` MAIOR, aceita terminal) | Admin é autoridade — preço/cadastro não deve divergir por terminal |
| `sales`, `sale_items`, `sale_payments`, `cash_sessions`, `cash_movements`, `stock_movements` | `terminal_wins` | **Nunca descartar uma venda ou movimento de caixa** — dado transacional de dinheiro real sempre vence |
| `inventory`/estoque | `reconcile` (soma de deltas, não substituição de valor absoluto) | Concorrência: dois terminais vendendo o mesmo produto ao mesmo tempo — a forma correta é somar as **variações**, não sobrescrever o valor total |

O `reconcileStock()` (linhas 123-141) calcula `delta = quantidadeTerminal - quantidadeOriginalConhecida`
e aplica esse delta sobre o valor atual do servidor — é uma forma simplificada de CRDT (contador
"soma de deltas assinados"), evitando o clássico bug de "dois PDVs decrementam estoque, servidor
só aplica o último e perde uma das duas vendas".

`change-applier.ts:79-213` aplica a estratégia: em `insert`, sempre aceita (novo registro do terminal);
em `update`, busca o registro atual do servidor, chama `resolveConflict()`, e só grava se o terminal
"ganhou"; em `delete`, sempre aplica. **Todo payload passa por validação Zod antes de tocar o banco**
(`validateChangePayload`, linha 100) — payloads inválidos são rejeitados e logados em auditoria
(`sanitizePayloadForAudit` remove XML/senha/PIN antes de logar, linhas 413-428) em vez de derrubar o
processamento do lote inteiro.

**Caso especial: duplicidade de número de venda** (`change-applier.ts:223-293`,
`insertSaleWithDuplicateHandling`) — como a numeração é local por registrador
(`sale_seq_<registerId>`), dois terminais offline podem, em teoria, gerar o mesmo `sale_number` se
reconfigurados incorretamente (ou depois de restaurar um backup antigo). O servidor detecta a violação de
unique constraint Postgres (`23505`), automaticamente renumera para o próximo disponível
(`getNextAvailableSaleNumber`, linhas 320-357) e **loga a renumeração em auditoria** em vez de rejeitar
a venda. Isso é uma decisão de produto correta: nunca perder uma venda por colisão de número — resolva
a colisão, não descarte o dado financeiro.

### PRINCÍPIO a reimplementar em .NET

1. **Outbox transacional via triggers ou, na falta de triggers no motor escolhido, dentro da MESMA
   transação de escrita da aplicação** (ex.: em EF Core local com SQLite, um `SaveChanges()` que escreve
   tanto a entidade de negócio quanto uma linha em `OutboxEvents` na mesma `DbContext.SaveChanges` —
   nunca dois `SaveChanges()` separados). O objetivo é: **não pode existir mudança de dado sem evento de
   sync correspondente**, e vice-versa não pode existir evento de sync "fantasma" sem dado.
2. **Idempotência ponta a ponta via chave estável gerada no cliente** (ULID/GUID), verificada no servidor
   antes de aplicar — não confie em "o cliente não vai reenviar", confie em "o cliente VAI reenviar
   depois de qualquer falha de rede, e isso tem que ser seguro por design".
3. **Conflito resolvido por POLÍTICA EXPLÍCITA POR ENTIDADE**, nunca um único algoritmo genérico de
   "last write wins" para tudo. Dados financeiros (vendas, pagamentos, caixa) = terminal sempre vence.
   Dados mestres (preço, cadastro) = servidor sempre vence. Contadores (estoque) = reconciliação por soma
   de delta, nunca substituição do valor absoluto.
4. **Cursor de pull baseado em timestamp monotônico do SERVIDOR** (não do cliente) evita problema de
   relógio de PDV desincronizado causar buracos ou repetição no pull.
5. **Numeração fiscal/sequencial é local-first com renumeração server-side em caso de colisão**, nunca
   bloqueando a venda. Detecte, renumere, audite — não rejeite dinheiro real.

### FRAQUEZAS a corrigir

1. **Bug latente na geração de `idempotency_key` dos triggers**: a chave é
   `<entidade>:<id>:<segundo-unix>:<ACAO>` (`schema.ts:647`, `:659`, etc. — usa `strftime('%s','now')`,
   granularidade de **1 segundo**, não um valor aleatório/monotônico). Há um `UNIQUE INDEX` sobre
   `idempotency_key` (`schema.ts:172`). Se **duas escritas na mesma linha, com a mesma ação (ex.: dois
   `UPDATE` na mesma entidade), ocorrerem dentro do mesmo segundo de relógio dentro da MESMA transação**
   (perfeitamente plausível — updates em lote rodam em microssegundos), a segunda `INSERT INTO sync_queue`
   colide com a `UNIQUE INDEX`, lança `SqliteError` de constraint, e como isso acontece **dentro de uma
   transação `better-sqlite3`, a transação inteira sofre ROLLBACK** — inclusive a venda/pagamento que
   disparou o trigger. No fluxo atual isso é mitigado incidentalmente porque `finishBatch`
   (`database.ts:398-578`) mescla itens do mesmo produto por `productId` antes de chegar à transação
   (`saleStore.ts:421-446`, `existingIndex` por `productId`) — mas é uma proteção acidental de UI, não uma
   garantia estrutural do trigger. **Na reimplementação, gere a chave de idempotência com um valor
   verdadeiramente único por escrita** (GUID/ULID por linha do outbox, nunca timestamp de baixa
   resolução), e nunca deixe uma falha de log/outbox abortar a transação de negócio principal — trate o
   outbox como side-effect que não pode fazer o "caminho feliz" falhar (ex.: outbox com sua própria PK
   auto-incremento, sem UNIQUE em cima de um campo derivado de timestamp).
2. **Fila local sem limite/alerta de tamanho**: se um terminal ficar offline por dias, `sync_queue` cresce
   indefinidamente sem sinalização proativa ao admin ("terminal X está com 40.000 mudanças pendentes há
   3 dias"). Adicionar um limite de alerta (não de bloqueio — nunca pare de vender) e uma tela de saúde
   de sync no painel admin.
3. **Itens que excedem `maxRetries` ficam mudos**: o `SyncEngine` marca status `ERROR` mas não há
   mecanismo claro de re-enfileirar/investigar manualmente esses itens "presos" (ficam com
   `status='pending'` e `attempts >= maxRetries`, fora do lote elegível, mas sem rota de purgatório /
   dead-letter explícita). Implementar uma "dead-letter queue" visível no admin, com ação manual de
   retry/descarte auditado.
4. **`onConflictDoUpdate` como fallback genérico em `change-applier.ts:147-157`** — em caso de erro no
   upsert, cai para `INSERT` puro sem tratamento específico; isso mascara erros de schema/tipo em vez de
   falhar alto e alertar. Prefira falhar explicitamente e registrar em auditoria/observabilidade a
   silenciar com fallback genérico.

---

## 4. Tempo real multi-terminal — via NUVEM/servidor único, NÃO via hub local (ponto crítico)

### Resposta direta e honesta

**O modelo do Supermarket-OS é estritamente de 2 camadas: PDV ↔ UM servidor central (Fastify + Postgres +
Redis).** Não existe hub local, não existe comunicação terminal-a-terminal direta, não existe protocolo
LAN (mDNS, broadcast UDP, WebRTC local) para os PDVs se descobrirem entre si. Toda visibilidade em tempo
real passa por:

1. PDV envia mudança → `POST /api/sync/batch` no servidor (`sync.routes.ts:24`);
2. Servidor aplica no Postgres (`applyChange`) e publica em **Redis Pub/Sub**
   (`publishSyncChange`, `apps/server/src/sync/broadcast.ts:61-65`) no canal `sync:{storeId}`;
3. Todas as instâncias do servidor (se houver mais de uma, atrás de load balancer) recebem via
   `getSubscriber().on('message', ...)` (`broadcast.ts:28-59`) e repassam para os terminais conectados
   via **WebSocket** (`broadcastToStore`, `apps/server/src/websocket/ws-server.ts:294-301`).
4. O terminal desktop conecta nesse WS (`apps/desktop/src/main/sync.ts:276-332`) usando
   `wsUrl = process.env['WS_URL'] ?? 'ws://localhost:3333/ws'` — ou seja, **por padrão aponta para
   `localhost:3333`, e em produção aponta para o que estiver configurado em `WS_URL`/`API_URL`**, que
   pode ser um IP de LAN (deploy on-premise) ou um domínio na nuvem (deploy central) — **isso é 100%
   decisão de infraestrutura/deploy, não está resolvido na arquitetura do software**.

`docker/docker-compose.yml:1-74` define um único serviço `server` com Postgres+Redis+Fastify juntos —
não há separação entre "servidor da loja" e "servidor central". `docs/MARKET-RESEARCH.md:119-206`
compara o produto com concorrentes descrevendo-se como concorrente de arquiteturas "100% cloud-based" e
frisa "Offline Operation... Core feature" — ou seja, o offline-first é vendido como diferencial *apesar*
do modelo ser terminal↔servidor único, não *por causa* de uma camada local dedicada.

### O que isso significa na prática para "queda de internet"

- Se o servidor único estiver hospedado **na nuvem**: uma queda de internet da loja mata toda visibilidade
  em tempo real entre os 9 PDVs simultaneamente — cada terminal continua vendendo sozinho (graças ao
  SQLite local + outbox), mas **nenhum PDV vê o que o outro está fazendo até a internet voltar**. Isso
  inclui coisas operacionalmente importantes na hora: reserva de estoque entre caixas, abertura/fechamento
  de caixa visível ao admin, alerta de preço/promoção mudando ao vivo.
- Se o "servidor único" for implantado **dentro da própria loja** (on-premise, num PC/NAS local rodando
  o `docker-compose`), então o comportamento sobrevive à queda de internet — mas isso é **uma escolha de
  deploy que o código permite, não um recurso que ele garante ou documenta como padrão**. Não há:
  - service discovery automático (os PDVs têm `WS_URL`/`API_URL` fixos via env var/config,
    `sync.ts:340-349`) — trocar de "servidor na nuvem" para "servidor local" exige reconfigurar todos
    os 10 PCs manualmente;
  - failover: se o único processo servidor (mesmo local) cair, os PDVs voltam ao modo "cada um por si",
    idêntico ao caso de internet cair — não existe promoção automática de um PDV a "hub temporário", nem
    fallback de um segundo servidor.
- **Conclusão honesta para o novo ERP**: o padrão terminal↔servidor-único do Supermarket-OS **não resolve
  sozinho** o requisito "loja continua com tempo real na LAN mesmo sem internet" — ele resolve "loja
  continua VENDENDO offline" (o que já é valioso), mas tempo real entre PDVs é refém de UM processo
  estar de pé E acessível pela rede que os PDVs estão configurados para usar. Para o cenário de 9 PDV +
  1 admin descrito, **uma arquitetura de 3 camadas (PDV ↔ hub-de-loja-local ↔ nuvem) é estritamente mais
  robusta** do que replicar este modelo — o hub-de-loja pode rodar no próprio PC do admin ou num NUC
  dedicado na loja, falar com os PDVs por LAN (latência ~0, sem depender do ISP) e só ele precisa
  falar com a nuvem para consolidar dados entre lojas/backup remoto/relatórios centrais.

### PRINCÍPIO a reimplementar em .NET (o que aproveitar do design, mesmo trocando a topologia)

- O **padrão de mensageria** (outbox local → push em lote → pub/sub → WebSocket fanout) é reaproveitável
  1:1 dentro da camada "PDV ↔ hub de loja": troque só o "servidor" por "hub local" e ganhe tudo isso —
  outbox transacional, idempotência, backoff, conflito por entidade — rodando dentro da própria loja.
  Depois, o hub de loja replica para a nuvem usando **exatamente o mesmo padrão de sync** (outbox +
  idempotência + cursor), agora como um único "terminal lógico" visto da nuvem.
  Não precisa reinventar o motor de sync para a camada hub↔nuvem — é o mesmo motor, mais um salto.
- **Redis Pub/Sub é dispensável para 10 conexões** — foi usado aqui para permitir múltiplas instâncias do
  servidor atrás de load balancer (escala horizontal de SaaS multi-loja), não é necessário assumir isso
  para um hub de loja de processo único. Simplifique: broadcast in-process para WebSocket já resolve o
  caso de 9 PDVs conectados num hub local.
- O `WsClient` reconecta e faz "catch-up pull" ao reconectar (`sync.ts:314-319`) — esse padrão
  ("ao reconectar, sempre puxar mudanças perdidas antes de confiar no WS ao vivo") é essencial e deve ser
  preservado na camada PDV↔hub: nunca assuma que o WS entregou tudo, sempre reconcilie via pull/cursor
  no reconnect.

---

## 5. Drivers de hardware — impressora, balança, TEF, gaveta, scanner

Diretório: `apps/desktop/src/main/hardware/`.

### Arquitetura: Adapter pattern + Null Object + orquestrador central

`apps/desktop/src/main/hardware/types.ts:1-9` declara a regra do módulo inteiro em comentário:
> "Errors are NEVER thrown to the caller — they are reported via status/events. The PDV MUST continue
> operating even if all hardware is offline."

Cada dispositivo (`ScaleAdapter`, `PrinterAdapter`, `TefAdapter`, `CashDrawerAdapter`) é uma **interface**
com `connect()/disconnect()`, `status: 'disconnected'|'connecting'|'connected'|'error'` e `lastError`.
Toda implementação real (`SerialScaleAdapter`, `EscPosPrinterAdapter`, `PayGoTefAdapter`, etc.) tem uma
contraparte **Null Object** (`NullScaleAdapter`, `NullPrinterAdapter`, `NullTefAdapter`,
`NullCashDrawerAdapter`) que nunca lança exceção e retorna default seguro — usada como valor inicial
até o operador configurar hardware de verdade (`manager.ts:70-74`).

`HardwareManager` (`manager.ts:49-352`) é o orquestrador central:
- **`safeXxx()` wrappers** (`safePrint`, `safeGetWeight`, `safeStartTef`, `safeOpenDrawer`, etc., linhas
  248-352) — cada operação de hardware exposta ao resto do app é encapsulada num wrapper que nunca
  propaga exceção, sempre retorna um resultado tipado com `success`/`error`.
- **Health check + reconexão com backoff** (`startHealthCheck`, linhas 153-246): a cada 30s, checa status
  de cada dispositivo; se um que estava conectado cair, agenda reconexão com backoff exponencial
  (5s, 10s, 20s, 40s, 60s máx — `calculateBackoff`, linhas 44-47), resetando o contador de tentativas ao
  reconectar com sucesso.
- **Troca de adapter em runtime sem reiniciar o app** (`setTefAdapter`, linhas 88-97) — permite o operador
  mudar de provedor de TEF nas Configurações sem reiniciar o PDV.

### Fila persistente de impressão (mesmo padrão de outbox aplicado a hardware)

`apps/desktop/src/main/hardware/printer.ts:683-853` (`PrintQueueManager`) — se a impressora estiver
offline ou sem papel, o job de impressão é gravado na tabela `print_queue` do SQLite
(`schema.ts:558-570`) com status `pending/printing/failed/completed` e `attempts`/`max_attempts` (default
5). Quando o health check detecta a impressora reconectada, dispara `processQueue()` automaticamente
(`hardware-ipc.ts:44-48`); há também um timer de auto-retry a cada 10s (`printer.ts:763-778`). **O mesmo
princípio de outbox durável usado para sync de dados de negócio é reaplicado aqui para hardware** — cupom
fiscal não se perde só porque a impressora estava sem papel no momento da venda.

### Abstração de fornecedor (TEF) com fallback em cadeia

`apps/desktop/src/main/hardware/tef-factory.ts:17-38` — uma factory simples que, dado um `provider`
(`paygo|sitef|stone|cappta|connecttef|mock`), instancia o adapter concreto certo. O resto do app só
conhece a interface `TefAdapter` — trocar de adquirente é configuração, não código.

`TefProviderManager` (`tef.ts:1043-1115`) orquestra fallback entre provedores em cadeia: tenta o provider
primário, se falhar/expirar (`Promise.race` contra timeout por provider, linha 1090-1095), tenta o
próximo da lista, registrando quais foram tentados.

### O que é específico de Node/Electron vs. portável para .NET

| Específico de Node/Electron (repensar em .NET) | Portável (o princípio, não o código) |
|---|---|
| `serialport` npm package via `import()` dinâmico para RS-232 da balança (`scale.ts:200`) — em .NET usar `System.IO.Ports.SerialPort` | Parsers de protocolo por fabricante como funções puras `Buffer → Reading\|null` (`PROTOCOL_PARSERS`, `scale.ts:17-114`) — mapeia 1:1 para um `Dictionary<string, Func<byte[], Reading?>>` em C# |
| `node:net` para impressora de rede (ESC/POS via socket TCP, `printer.ts:13`) — em .NET usar `System.Net.Sockets.TcpClient` | Comando de impressão como lista de `PrintCommand` tipados (`text\|line\|barcode\|qrcode\|feed\|cut\|image\|drawer`, `types.ts:86-94`) — um DTO/union type portável, desacoplado do transporte físico |
| Import dinâmico condicional de `serialport` (peer dependency opcional — se não instalado, degrada para `disconnected`, comentário em `scale.ts:8-9`) | O padrão em si (dependência de hardware é opcional/plugável, nunca hard-crash no startup se driver nativo faltar) — em .NET, equivalente é carregar drivers de porta serial/USB via plugin/DI opcional, com fallback Null Object se o assembly não estiver presente |
| Gaveta de dinheiro acionada via comando ESC/POS através da MESMA conexão da impressora (`cash-drawer.ts:36-45`) — não é um dispositivo próprio | Princípio: nem todo "dispositivo" no diagrama de hardware é uma conexão física separada; mapeie a topologia real (gaveta = comando pela impressora) em vez de modelar 1 adapter = 1 dispositivo físico |
| `EventEmitter` do Node para eventos de status (`cash-drawer.ts:45`) | Em .NET, `event`/`IObservable<T>` — mesmo padrão de pub/sub local para status change |
| IPC do Electron (`ipcMain.handle`) como fronteira processo-UI (`hardware-ipc.ts`) | Em .NET desktop (WPF/MAUI/Avalonia), a fronteira equivalente é injeção de serviço via DI entre camada de UI e camada de hardware rodando no mesmo processo — ou um serviço Windows separado se quiser isolar hardware da UI |

### PRINCÍPIO a reimplementar em .NET

1. **Toda operação de hardware é fire-and-forget do ponto de vista do fluxo de venda — nunca pode
   lançar exceção que interrompa uma venda.** Envolva cada chamada de driver físico num wrapper que
   captura qualquer exceção e devolve um resultado tipado (`Result<T>`/`success+error`), nunca deixe o
   driver de balança/impressora derrubar o processo principal.
2. **Interface + Null Object para cada categoria de hardware**, com um "modo sem hardware nenhum
   configurado" que é o estado padrão até o operador configurar — nunca crashe no boot por falta de
   hardware físico conectado.
3. **Health check periódico com reconexão exponencial e reset de contador ao suceder** — não fique
   tentando reconectar a cada 1s indefinidamente (satura porta serial/rede), nem desista de vez após
   uma falha (loja fica sem impressora até reiniciar o PDV manualmente).
4. **Fila de impressão (e de qualquer output físico crítico) é persistida em disco, não em memória** —
   mesmo princípio de outbox durável usado para dados de negócio.
5. **Fábrica de adapters por fornecedor + fallback em cadeia para pagamento** — a lógica de negócio
   (venda, PDV) nunca deve conhecer detalhes de PayGo/SiTef/Stone/Cappta, só a interface `TefAdapter`.

### FRAQUEZAS a corrigir na reimplementação

1. **Fallback de TEF entre provedores em caso de timeout é PERIGOSO sem reconciliação de estado
   ambíguo.** `TefProviderManager.fallbackTransaction()` (`tef.ts:1089-1105`) corre `startTransaction()`
   contra um timeout e, se o provider A estourar o tempo (`Promise.race` rejeita), **tenta o provider B
   em seguida sem antes consultar se a transação em A realmente falhou ou apenas demorou para
   responder.** Em pagamento eletrônico real, um timeout local não implica que o adquirente não processou
   a cobrança — isso é a receita clássica para **cobrança duplicada no cartão do cliente**. Na
   reimplementação, antes de tentar um segundo provedor/nova tentativa de autorização, é obrigatório: (a)
   consultar o status da transação original pelo `nsu`/`idempotencyKey` junto ao mesmo adquirente antes
   de desistir dela, e (b) se o status permanecer indeterminado, **exigir confirmação manual do operador/
   gerente** antes de permitir uma segunda tentativa de cobrança — nunca decidir isso automaticamente.
2. **`TefTransactionRequest.idempotencyKey` existe no tipo (`types.ts:169`) mas o adapter PayGo não o
   envia no corpo da requisição** (`tef.ts:86-93` monta `body` com `externalId: req.saleId` mas nunca usa
   `req.idempotencyKey`) — o `saleId` sozinho não é suficiente se a mesma venda tentar autorizar duas
   vezes (ex.: reenvio após timeout) e o adquirente não tratar `externalId` como chave de dedupe. Garanta
   que a chave de idempotência realmente viaje até o adquirente em todo request de autorização.
3. **Balança: parsers de protocolo são heurísticas de regex sobre string ASCII** (`scale.ts:24-113`) sem
   checksum/CRC validado para os protocolos que o têm (Balmak, por exemplo, comentário na linha 92 cita
   checksum mas o parser real não valida BCC) — leituras corrompidas por ruído de linha serial podem
   passar como válidas. Adicionar validação de checksum onde o protocolo do fabricante define um.
4. **Gaveta sem sensor de estado real** (`cash-drawer.ts:171-175`: "Most drawers don't report their state
   back... return based on tracked status") — o sistema não sabe de verdade se a gaveta está fisicamente
   aberta ou fechada, só infere pelo último comando enviado. Se houver hardware com sensor (porta DK),
   vale integrar leitura real em vez de inferência.

---

## Resumo executivo (uma frase por seção)

1. **Crash recovery real** = transação SQL local atômica no commit financeiro + rascunho best-effort
   (throttled, não debounced) para UX, com confirmação manual do operador antes de reaplicar qualquer
   estado ambíguo.
2. **Durabilidade local** = WAL + `synchronous=NORMAL` (nunca corrompe, mesmo em crash) + backup por
   checkpoint-e-cópia rotativo + auto-recovery fail-open que preserva o arquivo corrompido para forense.
3. **Sync** = outbox transacional (idealmente via trigger de banco) + idempotência por chave verdadeiramente
   única + política de conflito explícita por entidade (terminal vence em dinheiro, servidor vence em
   cadastro, soma-de-delta em contadores) + cursor monotônico do servidor.
4. **Tempo real multi-terminal DEPENDE de um único processo servidor estar de pé e acessível pela rede
   configurada — o Supermarket-OS não tem hub local nativo; se esse servidor único rodar na nuvem, uma
   queda de internet mata o tempo real entre PDVs da loja (mas não a venda offline).** Para o cenário de
   9 PDV + 1 admin, uma arquitetura de 3 camadas (PDV ↔ hub-de-loja-local ↔ nuvem) é necessária e não é
   algo que o modelo deste repo dá de graça — precisa ser desenhada à parte, reaproveitando o MESMO motor
   de sync (outbox+idempotência+conflito) em cada salto.
5. **Hardware** = adapter + null-object por dispositivo, orquestrador central com wrappers "never-throw",
   health-check com backoff exponencial, fila de impressão persistida como outbox — mas o fallback entre
   provedores de TEF por timeout, sem reconciliar o estado no adquirente antes de tentar de novo, é uma
   fraqueza real que pode gerar cobrança duplicada e não deve ser copiada como está.
