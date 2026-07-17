# Infrastructure — robustez offline, sync 3 camadas, hardware

> Especificação-fonte: `docs/robustez/robustez-hardware-licoes.md` (lições extraídas do
> Supermarket-OS). Este README documenta o que foi construído aqui, o que é real vs. stub, e
> quais fraquezas do SMOS (Supermarket-OS) foram projetadas pra fora.

Três projetos, cada um compilando de forma independente, referenciados hoje por:
`SharedKernel ← Local ← Sync` e `SharedKernel ← Hardware` (Hardware não referencia Local —
decisão deliberada, ver §4).

---

## 1. `SistemaX.Infrastructure.Local`

SQLite (`Microsoft.Data.Sqlite`) como o "cofre" de crash-safety de UM terminal.

### Real (funcional, testado em runtime — ver §5)

- **`SqlitePragmas`** — `journal_mode=WAL` + `synchronous=NORMAL` + `busy_timeout` + `cache_size`
  + `temp_store=MEMORY` + `mmap_size` + `journal_size_limit`, reaplicados em TODA conexão nova
  (pragmas por-conexão nunca "vazam" de uma conexão fechada para a próxima).
- **`LocalUnitOfWork` / `ILocalUnitOfWorkFactory`** — a materialização da lição central do
  Supermarket-OS: **a unidade de crash-safety é a transação do motor de banco, não a lógica da
  aplicação**. Um único `BEGIN...COMMIT` agrupa N inserts/updates de negócio + o enqueue do
  outbox. `CreateCommand()` já vem com `Connection`/`Transaction` vinculados; `EnqueueOutboxAsync`
  grava na MESMA transação — nunca uma segunda transação separada. Testado: commit grava os dois
  lados juntos, rollback não grava nenhum dos dois (nunca "outbox fantasma" nem "venda órfã").
- **`ILocalSequenceAllocator`** — aloca número de sequência local (ex.: número de venda por
  registrador) via `UPSERT ... RETURNING` dentro da MESMA transação do Unit-of-Work — sem
  depender do servidor estar de pé (requisito duro de operação 100% offline).
- **`IOutboxStore` / `OutboxMessage`** — outbox transacional. `Id` é **ULID** (ver
  `Ids.UlidGenerator`), nunca timestamp de baixa resolução.
- **`OutboxTriggerFactory`** — opt-in: gera DDL de triggers SQL (`AFTER INSERT/UPDATE/DELETE`)
  para quem quiser a garantia estrutural MÁXIMA (outbox populado pelo próprio motor de banco, não
  por disciplina de código de app) em uma tabela de negócio específica. Usa
  `lower(hex(randomblob(16)))` como chave — puro SQL, sem função customizada registrada.
- **`ICorruptionRecoveryService`** — `integrity_check` completo (throttlado 1x/dia via `app_kv`)
  no boot + `quick_check` leve periódico em background (`PeriodicIntegrityCheckService`, corrige a
  fraqueza do SMOS de só checar no boot). `AttemptRecoveryAsync` faz exatamente a sequência da
  lição: `ClearAllPools()` → renomeia `.corrupted-{timestamp}` (+ `-wal`/`-shm`, NUNCA apaga) →
  restaura o backup mais recente → se não houver, segue **fail-open** com banco novo/vazio →
  reabre e garante schema. Testado em runtime: preserva o arquivo corrompido, dispara o evento
  `CorruptionRecovered`, e o banco volta a funcionar.
- **`IBackupManager`** — `PRAGMA wal_checkpoint(TRUNCATE)` + cópia via `FileStream` assíncrono
  (nunca `File.Copy` síncrono bloqueando a thread de UI/IPC), checagem de espaço livre em disco
  antes de aceitar o backup (recusa + loga `Critical` se abaixo do mínimo — nunca falha
  silenciosamente), rotação mantendo `MaxBackups` (7) mais recentes.
- **`ICrashRecoveryHook` / `CrashRecoveryRunner`** — o gancho de boot pedido pela tarefa. Local
  não conhece "venda" ou "rascunho de carrinho" — módulos de negócio (ainda não construídos)
  registram seus próprios hooks via `AddCrashRecoveryHook<T>()`; um hook que falha é logado e
  pulado, nunca impede os demais nem trava o boot do terminal.
- **`LocalDatabaseBootstrapper`** — `IHostedService` que orquestra: schema → integrity check →
  crash-recovery hooks. Também exposto como método público (`BootstrapAsync`) para hosts sem
  Generic Host.
- **`ITerminalIdentity`** — ULID gerado uma vez no primeiro boot, persistido em `app_kv`. Usado
  por Sync para namespacing de sequência e prevenção de eco.

### Stub / não construído

- Nenhum. Este projeto está completo para o escopo pedido. O que falta é o que **não é dele**:
  schema de tabelas de negócio (Vendas, Estoque) — migradas pelos módulos donos.

---

## 2. `SistemaX.Infrastructure.Sync`

Motor de sync de 3 camadas: **PDV ↔ ServidorDeLoja ↔ Nuvem**. A MESMA classe (`SyncEngine`) serve
os 2 saltos — só muda a `SyncOptions` injetada.

### Onde cada salto se pluga

```
┌─────────┐   AddSistemaXSyncClient()   ┌──────────────────┐   AddSistemaXSyncClient()   ┌────────┐
│   PDV   │ ──────────────────────────► │  ServidorDeLoja  │ ──────────────────────────► │ Nuvem  │
│ (Host.  │ ◄────────────────────────── │  (Store.Server)  │ ◄────────────────────────── │(Cloud. │
│ Desktop)│   pull por cursor + WS      │  TAMBÉM roda      │   pull por cursor + WS      │  Api)  │
└─────────┘                             │  AddSistemaXSync  │                             └────────┘
                                        │  Inbound() pro    │
                                        │  salto 1           │
                                        └──────────────────┘
```

- **Salto 1** (PDV → Loja): `Host.Desktop` chama `AddSistemaXSyncClient(o => o.UpstreamBaseAddress
  = <servidor de loja>)`. `Store.Server` chama `AddSistemaXSyncInbound()` e expõe
  `POST /api/sync/batch` / `GET /api/sync/pull` chamando `SyncInboundService.ApplyBatchAsync` /
  `BuildPullResponseAsync` diretamente (código de rota fica no Host, fora desta partição).
- **Salto 2** (Loja → Nuvem): `Store.Server` chama `AddSistemaXSyncClient` DE NOVO (outra
  instância de `SyncOptions`, apontando pra nuvem) — o mesmo processo é cliente do salto 2 e
  receptor do salto 1 simultaneamente. `Cloud.Api` expõe as mesmas 2 rotas sobre o MESMO contrato
  de wire (`SyncPushRequest`/`SyncPullResponse` em `Model/PushModel.cs`), podendo reusar
  `SyncInboundService` (se .NET) ou reimplementar o mesmo contrato sobre Postgres.

### Real (funcional, testado em runtime — ver §5)

- **Idempotência por ULID** — reaproveita `Ids.UlidGenerator` de Local (a MESMA correção do bug
  SMOS de chave de timestamp de 1 segundo). `IProcessedMessageStore` dedupe no receptor:
  reenviar o mesmo lote depois de falha de rede é seguro por construção (`AlreadySynced`).
- **Push em lote com backoff exponencial** — `BackoffCalculator` (base×2^tentativa, capado),
  distingue falha de TRANSPORTE (lote inteiro fica pendente, não consome tentativa) de rejeição
  POR ITEM (consome tentativa, vai a dead-letter explícito após `MaxRetries` — nunca "muda"
  silenciosamente como no SMOS).
- **Pull por cursor monotônico do RECEPTOR** — `server_sequence` (autoincrement), não timestamp:
  mais robusto que a recomendação original do SMOS (zero risco residual de clock-skew, mesmo do
  lado do servidor). Prevenção de eco por `excludeTerminalId`.
- **Conflito por entidade** — `DefaultConflictResolutionPolicy` (venda/pagamento/caixa/estoque-
  lançamento = `TerminalWins`; cadastro = `ServerWinsWithVersion`; estoque agregado =
  `ReconcileDelta`). `ConflictMath.ReconcileByDelta` testado: preserva mudança concorrente do
  servidor em vez de sobrescrever (o bug clássico "dois PDVs vendem o mesmo produto, servidor
  aplica só o último e perde uma venda").
- **`IRemoteChangeApplier`** — mesmo padrão de plugin do `IIntegrationEventHandler` já existente
  em `Modules.Abstractions`: Sync não conhece schema de negócio, módulos registram appliers por
  tipo de entidade.
- **`SyncWebSocketClient`** — heartbeat periódico + reconexão com backoff + **catch-up pull
  obrigatório ao conectar/reconectar** (nunca assume que o WS entregou tudo enquanto esteve
  desconectado — a lição preservada do SMOS). Mensagem recebida = gatilho de flush imediato, sem
  decodificar o payload (o pull subsequente traz o dado real via cursor).
- **Alerta de fila sem bloquear venda** — `PendingQueueAlertThreshold` loga `Critical` se o outbox
  crescer demais, mas nunca impede o terminal de continuar vendendo (corrige a fraqueza do SMOS de
  fila sem sinalização proativa).

### Stub / não construído

- **Adapters reais de fabricante/adquirente** não se aplicam aqui — Sync é 100% transporte HTTP +
  WS genérico, já funcional ponta a ponta contra qualquer host que implemente o contrato de wire.
- **As rotas HTTP em si** (`POST /api/sync/batch`, `GET /api/sync/pull`) não existem — são
  responsabilidade dos Hosts (fora da partição). `SyncInboundService`/`HttpSyncTransportAdapter`
  já têm toda a lógica; falta só o `app.MapPost(...)`/`app.MapGet(...)` do lado de cada Host.
- **Nuvem (Postgres)** — o contrato de wire é agnóstico de storage; `Cloud.Api` pode reusar
  `SyncInboundService` (se rodar como serviço .NET) ou implementar o mesmo contrato sobre Postgres
  do zero. Nenhuma linha de código Postgres foi escrita aqui (fora de escopo/partição).

---

## 3. `SistemaX.Infrastructure.Hardware`

Adapter + Null Object por dispositivo, orquestrador central, fila de impressão persistida.

### Decisão de partição: Hardware NÃO referencia Local

Diferente de Sync, Hardware não tem `ProjectReference` para Local (grafo de referências
preexistente no esqueleto, preservado). A fila de impressão (`SqlitePrintQueueStore`) usa
`Microsoft.Data.Sqlite` diretamente, num arquivo próprio (`hardware-print-queue.db`,
configurável), com seu próprio `CREATE TABLE IF NOT EXISTS` e pragmas mínimos — pequena duplicação
consciente em troca de zero acoplamento entre as duas partições.

### Real (funcional, testado em runtime — ver §5)

- **`IPrinterAdapter` / `NullPrinterAdapter` / `TcpEscPosPrinterAdapter`** — impressora de rede
  ESC/POS via `TcpClient` (mapeamento direto do `node:net` do SMOS). `EscPosBuilder` converte
  `PrintCommand` (union type com `[JsonDerivedType]` pra serialização polimórfica) em bytes reais:
  texto (com reset defensivo de negrito/largura dupla — evita vazar estado pro próximo cupom, a
  gotcha de `GS!` conhecida em impressão térmica), linha separadora, código de barras (Code128/
  EAN13/Code39, function B), **QR code completo** (sequência de 4 comandos `GS ( k`: modelo,
  tamanho, correção de erro, armazena, imprime), avanço, corte, imagem raster, abrir gaveta.
  Testado: gera bytes não-vazios para um cupom com todos os tipos de comando, incluindo acentuação
  PT-BR via CP860 (`System.Text.Encoding.CodePages`).
- **`ICashDrawerAdapter` / `PrinterDrivenCashDrawerAdapter`** — a gaveta NÃO é uma conexão física
  própria: aciona via `PrintCommand.AbrirGaveta` pela MESMA conexão da impressora (mapeamento
  correto da topologia real, corrige o "1 adapter = 1 dispositivo" do SMOS). `DrawerState` é
  honesto sobre confiança da informação (`InferidoAberta` vs `ConfirmadoPorSensor*`) — nunca finge
  saber o estado físico real sem sensor (fraqueza corrigida, não só documentada).
- **`IScaleAdapter` / `SerialScaleAdapter` / `ScaleProtocolParsers`** — balança serial via
  `System.IO.Ports.SerialPort`. Parser de referência (`GenericAsciiBcc`) **valida checksum (BCC)
  antes de aceitar a leitura** — a fraqueza corrigida do SMOS (lá o comentário citava checksum mas
  o código não validava, deixando ruído de linha passar como leitura real). Novos fabricantes
  entram como novas entradas no dicionário `ScaleProtocolParsers.Todos`, sem tocar no adapter.
- **`ITefAdapter` / `TefFallbackCoordinator`** — **A PEÇA CRÍTICA da tarefa.** Nunca reenvia ou
  troca de adquirente sem antes consultar `GetTransactionStatusAsync` pela MESMA
  `IdempotencyKey` (contrato reforçado no XML doc de `TefTransactionRequest`: implementações reais
  DEVEM enviar essa chave ao adquirente — a fraqueza #2 do SMOS, onde o tipo carregava a chave mas
  o adapter nunca a enviava). Testado em runtime com `MockTefAdapter` simulando exatamente o
  cenário perigoso: timeout local (150ms) seguido de aprovação real no "adquirente" (400ms,
  processando em `CancellationToken.None` — independente do cliente ter desistido de esperar,
  como um adquirente real faria) → o coordinator consulta o status, encontra `Approved`, e
  **retorna esse resultado em vez de tentar cobrar de novo**. Se o status permanecer
  indeterminado após `TefStatusPollMaxAttempts` tentativas, retorna
  `RequiresManualConfirmation` — nunca decide sozinho.
- **`HardwareManager`** — wrappers `SafeXxx` (nunca lançam, mesmo que o adapter por trás tenha um
  bug e lance — defesa em profundidade), health-check periódico com backoff por dispositivo (5s,
  10s, 20s, 40s, 60s, configurável), reset do contador ao reconectar, troca de adapter em runtime
  (`SetPrinterAdapter`/`SetTefProviders`/...) sem reiniciar o terminal.
- **`PrintQueueProcessor`** — fila de impressão como outbox durável (mesmo princípio do dado de
  negócio, reaplicado a hardware). Dispara em 2 gatilhos: timer periódico E evento
  `HardwareManager.DeviceReconnected` (drena imediatamente quando a impressora volta, não espera
  o próximo tick).
- **`IBarcodeScannerAdapter`** — `NullBarcodeScannerAdapter` é o adapter CORRETO (não um stub) pro
  caso majoritário: scanner keyboard-wedge não precisa de driver, chega como teclado normal na UI.
  `SerialBarcodeScannerAdapter` existe pro caso minoritário de scanner serial/RS-232 cru.

### Stub / não construído

- **Adapters reais de adquirente** (`PayGoTefAdapter`, `SiTefAdapter`, `StoneTefAdapter`,
  `CapptaTefAdapter`, `ConnectTefAdapter`) — não implementados (exigem credenciais/SDK de cada
  adquirente). `TefProviderFactory` já tem os slots comentados prontos; `MockTefAdapter` é
  funcional para desenvolvimento/testes e já valida o contrato (`IdempotencyKey` viaja de fato).
- **Parsers de balança específicos por fabricante** (Toledo, Filizola, Urano, Balmak reais) — só
  o parser de referência genérico (`GenericAsciiBcc`) foi implementado; integrar um fabricante
  real exige o datasheet exato dele, seguindo o mesmo padrão (função pura + validação de checksum).
- **Captura de imagem para `PrintCommand.Imagem`** (rasterização de logo/QR visual em bitmap
  monocromático) — o comando ESC/POS (`GS v 0`) está implementado; gerar o bitmap a partir de um
  PNG/logo fica a cargo de quem monta o cupom (fora do escopo de hardware puro).

---

## 4. Fraquezas do Supermarket-OS — o que foi projetado pra fora (vs. só documentado)

| # | Fraqueza do SMOS | Como foi projetada pra fora aqui |
|---|---|---|
| 1 | Idempotency key com granularidade de 1 segundo (`strftime('%s','now')`), colidia em updates em lote e derrubava a transação inteira | `UlidGenerator` monotônico (48 bits ms + 80 bits aleatórios, incrementados sob lock no mesmo tick) — testado: 20.000 gerações em sequência apertada, todas únicas e em ordem crescente |
| 2 | Debounce de 2s no auto-save resetado a cada mudança — atividade contínua atrasa indefinidamente a durabilidade | Não aplicável a este escopo (auto-save de rascunho de UI fica no módulo de Vendas, não construído ainda) — mas o princípio "throttle, não debounce" está documentado no XML doc de `ICrashRecoveryHook` para quem for construir |
| 3 | Checagem de corrupção só no boot | `PeriodicIntegrityCheckService` roda `quick_check` em background durante o dia, configurável |
| 4 | Backup síncrono (`fs.copyFileSync`) podia travar a UI thread | `BackupManager` usa `FileStream` assíncrono ponta a ponta |
| 5 | Sem checagem de espaço em disco antes do backup — falha silenciosa | `BackupManager` verifica `DriveInfo.AvailableFreeSpace` e recusa + loga `Critical` explicitamente |
| 6 | Fila de sync sem alerta de tamanho | `SyncEngine` loga `Critical` acima de `PendingQueueAlertThreshold`, sem nunca bloquear a venda |
| 7 | Itens que excedem `maxRetries` ficam "mudos" (sem dead-letter visível) | `OutboxStatus.DeadLetter` explícito, consultável — a UI/admin (fora de escopo) pode construir a tela de retry manual em cima disso |
| 8 | Fallback de TEF entre provedores por timeout sem checar status original — risco de cobrança duplicada | `TefFallbackCoordinator` — a peça mais crítica desta entrega. Testado em runtime |
| 9 | `idempotencyKey` existia no tipo mas o adapter PayGo real nunca o enviava | Contrato reforçado no XML doc de `ITefAdapter.StartTransactionAsync`/`TefTransactionRequest`; `MockTefAdapter` demonstra o uso correto |
| 10 | Parser de balança sem validação de checksum apesar do comentário dizer que valida | `ScaleProtocolParsers.GenericAsciiBcc` valida BCC de verdade antes de aceitar a leitura |
| 11 | Gaveta sem sensor, mas o código finge saber o estado | `DrawerState` distingue explicitamente inferido vs. confirmado por sensor |
| 12 | Sync 2 camadas (terminal↔servidor único) — queda de internet mata tempo real entre PDVs se o servidor for na nuvem | Arquitetura de 3 camadas desde o desenho: `SyncEngine` reaproveitado nos 2 saltos, `ServidorDeLoja` como hub local que sobrevive à internet cair |

---

## 5. Verificação em runtime

Além de `dotnet build` (verde nos 3 projetos e na solução inteira), as costuras mais críticas
foram exercitadas em runtime com um console de verificação temporário (fora do repositório,
descartado após a checagem): ULID (unicidade/monotonicidade sob geração apertada),
`LocalUnitOfWork` (atomicidade commit/rollback do par negócio+outbox), `CorruptionRecoveryService`
(corrupção real do arquivo → preservação forense → fail-open funcional), `TefFallbackCoordinator`
(timeout local seguido de aprovação real no "adquirente" → resultado correto sem nova cobrança),
`EscPosBuilder` (gera bytes para um cupom completo sem lançar) e `ConflictMath.ReconcileByDelta`
(preserva mudança concorrente do servidor). Todas as 15 verificações passaram.
