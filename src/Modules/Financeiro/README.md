# Módulo Financeiro

> O coração do ERP. Implementação do modelo híbrido descrito em
> `docs/financeiro/financeiro-datamodel.md`: **single-entry na superfície** (o que a UI e os
> outros módulos veem) + **double-entry gerado automaticamente por baixo** (a checagem de
> integridade que garante que dinheiro não some nem aparece do nada).

## O que foi construído

### Domain (`SistemaX.Modules.Financeiro.Domain`)

**Motor invisível de partida dobrada** (`Contabil/`):
- `LancamentoContabil` — agregado com a invariante DURA `Σdébito == Σcrédito`, garantida no único
  portão de criação (`Criar`, factory estático — sem construtor público, sem setter de partida).
  Um lançamento desbalanceado simplesmente não existe: `Criar` retorna `Result.Falhar` com o
  código `financeiro.lancamento.desbalanceado`.
- `PartidaContabil` — linha débito/crédito imutável (`record`).
- `PlanoDeContasPadrao` — catálogo fixo de 6 contas de controle (Caixa/Bancos, Contas a Receber,
  Contas a Pagar, Impostos a Recolher, Receita, Custo/Despesa) — nunca exposto na UI operacional.
- `LancamentoContabilFactory` — o mapeamento determinístico **código, não input do usuário**:
  `ContaAReceber`/`ContaAPagar` → lançamento de competência; `MovimentoFinanceiro` → lançamento de
  caixa. É isso que torna a partida dobrada uma checagem automática e invisível para o dono leigo.
- **Estorno** (`LancamentoContabil.GerarEstorno`) — nunca edita/apaga; gera um novo lançamento com
  `ReversalOfId` apontando para o original e partidas espelhadas (débito↔crédito invertidos). Um
  conjunto balanceado espelhado é, por construção, outro conjunto balanceado.

**Competência** (`ContasAPagarReceber/`):
- `ContaFinanceiraBase` (abstract) + `ContaAReceber`/`ContaAPagar` — cada uma com N `Parcela`
  filhas (parcelamento nativo). Status (`StatusFinanceiro`: Aberto/Parcial/Pago/Atrasado/Cancelado)
  é sempre **derivado** do status agregado das parcelas, nunca setado diretamente.
- `Parcela` — entidade filha, mutação só via aggregate root (métodos `internal`).
- `SourceRef` (`Comum/`) — a chave de idempotência (`módulo:id`) carregada por toda conta.

**Caixa** (`Caixa/`):
- `MovimentoFinanceiro` — o fato de caixa, sempre com `ParcelaId` preenchido (nunca existe entrada
  de caixa sem rastro de competência, mesmo em venda à vista). Imutável; estorno via
  `GerarEstorno` (sinal invertido, `ReversalOfId`).
- `ContaBancariaCaixa` — saldo é **derivado** (soma de movimentos), nunca campo armazenado.
- `FormaDePagamento` — taxa e prazo de compensação (D+0 PIX, D+30 crédito).
- `Conciliacao` / `ExtratoBancarioItem` — vínculo movimento↔extrato importado.

**FSM** (`Fsm/StatusFinanceiroFsm`): `Pago` e `Cancelado` são terminais. Corrigir um fato pago
nunca é "voltar status" — é lançar um estorno.

**Categorização** (`Categorizacao/`): `Categoria` → `LinhaDre` (N:1), `CentroDeCusto` ortogonal.
Árvore de DRE fixa em `LinhasDrePadrao`.

**Recorrência** (`Recorrencia/Recorrencia.cs`): frequência + dia fixo + data fim. Multa/juros
pró-rata e ajuste de dia útil **ficam de fora deliberadamente** (TODO Fase 2 — a spec já indica
que a engine equivalente do ServicePro é madura o suficiente para não precisar redesenho agora).

### Application (`SistemaX.Modules.Financeiro.Application`)

- **`FinanceiroModule : IModule`** — registra os 6 handlers de evento de integração que o
  Financeiro assina, os casos de uso e os read-models.
- **Handlers** (`EventosDeIntegracao/Handlers/`) — um por evento do catálogo de
  `SistemaX.Modules.Abstractions.IntegrationEvents`: `VendaConcluidaHandler`,
  `VendaEstornadaHandler`, `CompraRecebidaHandler`, `OsFaturadaHandler`, `PedidoPagoHandler`,
  `FolhaLancadaHandler`. Todos idempotentes via `BuscarPorOrigemAsync(businessId, SourceRef.Chave)`
  antes de criar qualquer fato.
- **Casos de uso** (`CasosDeUso/`): `LancarContaAPagarUseCase`/`LancarContaAReceberUseCase`
  (lançamento nativo, ex. aluguel), `BaixarParcelaUseCase` (pagar conta/baixar parcela — orquestra
  competência + caixa + lançamento contábil numa única unidade lógica), `ConciliarMovimentoUseCase`,
  `EstornarMovimentoUseCase` (a peça reusável de estorno, usada por `VendaEstornadaHandler` e
  disponível para qualquer outro handler futuro), `AvaliarParcelasVencidasUseCase` (o "cron
  financeiro" — ver decisão sobre `ParcelaVencida` abaixo).
- **Ports** (`Ports/`): `IContaAReceberRepository`, `IContaAPagarRepository`,
  `IMovimentoFinanceiroRepository`, `ILancamentoContabilRepository`, `IConciliacaoRepository`,
  `IExtratoBancarioItemRepository`, `IRelogio` (abstração de tempo — testes nunca dependem de
  `DateTimeOffset.UtcNow` real).
- **Read-models** (`ReadModels/`) — as views matadoras da spec, recomputadas a partir dos fatos
  (nunca persistidas):
  - `FluxoDeCaixaService` — realizado (soma diária de `MovimentoFinanceiro`) + projetado (parcelas
    em aberto por vencimento), com detecção do primeiro dia de saldo negativo.
  - `DreGerencialService` — DRE gerencial simplificado por competência.
  - `QuantoSobrouDeVerdadeService` — "quanto dá pra tirar sem sufocar o caixa amanhã".
  - `AlertaFinanceiroService` — os 2 alertas do escopo MVP (conta vencendo/vencida + caixa
    projetado negativo).

### Infrastructure (`SistemaX.Modules.Financeiro.Infrastructure`)

- **`FinanceiroInfrastructureModule : IModule`** (`Codigo: "financeiro.infra"`,
  `DependeDe: ["financeiro"]`) — registra os adapters concretos dos ports acima.
- **Adapters in-memory** (`InMemory/`) — `ConcurrentDictionary`-based, um por port. Suficientes
  para rodar o módulo e os testes sem infraestrutura externa. **Extensível para SQLite sem tocar
  Domain/Application**: este projeto já referencia `SistemaX.Infrastructure.Local`, que carrega
  `Microsoft.Data.Sqlite` — trocar `InMemoryContaAReceberRepository` por
  `SqliteContaAReceberRepository` (implementando o mesmo `IContaAReceberRepository`) é a única
  mudança necessária.
- `RelogioSistema : IRelogio` — `DateTimeOffset.UtcNow` real.

## Decisões de design (e por quê)

1. **Dois `IModule`, não um.** O grafo de referência de projeto da solução é
   `Infrastructure → Application → Domain` (nunca o inverso). Um único `IModule` que registrasse
   handlers/casos de uso **e** adapters concretos exigiria Application referenciar Infrastructure
   — quebraria a regra de "NÃO modifique referências entre projetos". Solução:
   `FinanceiroModule` (Application, `Codigo: "financeiro"`) registra o que só depende dos ports;
   `FinanceiroInfrastructureModule` (Infrastructure, `Codigo: "financeiro.infra"`,
   `DependeDe: ["financeiro"]`) registra os adapters. O Host descobre e registra os dois.

2. **`ParcelaVencida` não tem handler no Financeiro.** Pelo próprio catálogo de
   `docs/financeiro/financeiro-datamodel.md` §4.2, a origem desse evento é o "Cron financeiro" —
   ou seja, o PRÓPRIO Financeiro é quem **publica** esse evento (`AvaliarParcelasVencidasUseCase`),
   não quem o consome. Registrar um handler para consumir um evento que o módulo mesmo produz
   seria um ciclo sem propósito. Isto é uma leitura deliberada da spec fornecida, não um
   esquecimento — documentado também no XML-doc de `FinanceiroModule`.

3. **Estorno de comissão em `os.faturada` não implementado.** A spec (§4.2) descreve que uma OS
   faturada com comissão configurada deveria também gerar uma `ContaAPagar` de comissão, mas
   `OsFaturada` (o evento real em `SistemaX.Modules.Abstractions.IntegrationEvents`) não carrega
   `profissionalId` nem percentual de comissão — não há dado no tipo para calcular isso (regra
   "não inventar dado que o tipo não expressa"). Gap documentado no XML-doc de `OsFaturadaHandler`.

4. **Classificação à-vista-vs-a-prazo por string, não por `FormaDePagamento` cadastrada.** Os
   eventos do catálogo (`VendaConcluida.FormaPagamento`, `PedidoPago.FormaPagamento`) carregam só
   um rótulo de texto. `ClassificadorFormaPagamento` trata "dinheiro"/"pix" como à vista e
   qualquer outra forma como a prazo (vencimento padrão de 30 dias). Documentado como
   simplificação de MVP — ver XML-doc da classe.

5. **Categoria por slug, não por entidade `Categoria` resolvida.** `CategoriaFinanceiraPadrao`
   expõe constantes de string (`servicos`, `comissoes`, `cmv-fornecedor`, ...) usadas diretamente
   como `CategoriaId` pelos handlers, em vez de resolver uma `Categoria` cadastrada por tenant via
   repositório. Suficiente para o motor financeiro (DRE, categorização) funcionar e ser testado
   sem exigir seed de dados por tenant nesta fase.

6. **`QuantoSobrouDeVerdadeService` usa fórmula reduzida.** A spec completa subtrai também
   imposto reservado (DAS/MEI) e parcela de dívida/empréstimo — dados que vivem em entidades
   (`DasRecord`, `Employee`) de outros módulos (Fiscal, RH) que não existem nesta partição ainda.
   A fórmula implementada é `saldo em caixa − contas a pagar nos próximos 30 dias`.

7. **IDs são ULID via pacote `Ulid` (Cysharp), não GUID.** Ordenável por tempo de criação,
   gerável no terminal sem coordenação com servidor — mesma lição de
   `docs/robustez/robustez-hardware-licoes.md` §3 sobre numeração não depender do servidor estar
   de pé.

## O que é stub / gap conhecido (Fase 2+)

- **Persistência real.** Todos os adapters são in-memory. Ver nota de extensão para SQLite acima.
- **`IIntegrationEventBus` não tem implementação nesta partição.** É um contrato cross-módulo
  (usado por Vendas, Compras, etc. além do Financeiro) — implementá-lo pertence a uma camada
  compartilhada/Host, não ao módulo Financeiro isoladamente. Os testes usam um
  `FakeIntegrationEventBus` (em `tests/.../Fakes/`) para verificar o que
  `AvaliarParcelasVencidasUseCase` publica.
- **3 dos 5 alertas inteligentes não implementados**: queda de margem (precisa CMV/estoque de
  outro módulo), cliente inadimplente recorrente (precisa histórico de cliente/CRM), imposto a
  recolher (precisa `DasRecord`/Fiscal). Só os 2 do escopo MVP da priorização
  (`docs/financeiro-features.md` §5) estão implementados.
- **DRE gerencial é simplificado**: agrupa só pelas 3 categorias nativas conhecidas pelos
  handlers (CMV, Comissões, resto = despesa operacional), não resolve `Categoria.LinhaDreId` de
  verdade nem separa Deduções/Resultado Financeiro.
- **Ponto de equilíbrio, margem de contribuição, custo por canal, análise de rentabilidade,
  cenários "e se"** (Fase 2/Futuro na priorização da spec) — não implementados; dependem de dados
  de CMV/estoque/BOM que vivem em outros módulos ainda não construídos.
- **Recorrência sem multa/juros pró-rata nem ajuste de dia útil** — ver decisão 7 acima (Domain).
- **Comissão de OS** — ver decisão 3 acima.

## Testes (`tests/SistemaX.Modules.Financeiro.Tests`)

32 testes xUnit, todos verdes (`dotnet test`). Cobrem as 4 invariantes pedidas:

1. **Partida dobrada sempre balanceia** — `LancamentoContabilTests` (criação com partidas
   desbalanceadas falha; só débito falha; valor zero falha; estorno produz partidas espelhadas
   ainda balanceadas).
2. **Estorno imutável** — `EstornoImutavelTests` (o original nunca muda; o estorno é sempre um
   novo registro com `ReversalOfId`; estornar um estorno é rejeitado; `EstornarMovimentoUseCase`
   chamado 2x para o mesmo original não duplica).
3. **Handler idempotente (mesmo evento 2x = 1 lançamento)** — `IdempotenciaHandlerTests` (todos os
   6 handlers + `AvaliarParcelasVencidasUseCase` chamados 2x com o mesmo evento/estado produzem
   exatamente 1 fato).
4. **Caixa vs competência geram as duas visões corretas** — `CaixaVsCompetenciaTests` (cenário
   canônico da spec: venda a prazo de 30 dias) e `FluxoDeCaixaServiceTests` (o read-model separa
   corretamente pontos realizados de projetados e detecta o primeiro dia de saldo negativo).

Mais `ContaFinanceiraFsmTests` (estados terminais nunca saem; liquidação parcial→total transita
corretamente; cancelar conta com pagamento é rejeitado).
