# Módulo Estoque

O **segundo assinante-mestre** do sistema (espelho do Financeiro — `docs/arquitetura/ARCHITECTURE.md`
§2.3/§5): assim como "tudo alimenta o financeiro" (dinheiro), **tudo que move mercadoria alimenta o
Estoque** (quantidade + custo). Nunca é chamado por ninguém — só assina eventos de integração e
mantém saldo como consequência. Plano completo em `scratchpad/design/estoque-plano.md`.

## O que existe

```
SistemaX.Modules.Estoque.Domain/
  Comum/Quantidade.cs          VO milésimos-inteiros — o "Money das quantidades"
  Comum/SourceRef.cs, IdGenerator.cs   cópias de propósito (mesmo padrão do Financeiro)
  Catalogo/Produto.cs           agregado — SKU, EANs[], unidade, fiscal, mínimo/reposição, BOM
  Catalogo/ComponenteDeFicha.cs VO da ficha técnica (BOM)
  Catalogo/CodigoDeBarras.cs, UnidadeDeMedida.cs, PoliticaDeValorizacao.cs
  Razao/MovimentoDeEstoque.cs   agregado — o RAZÃO append-only (a entidade central do módulo)
  Razao/TipoMovimento.cs        Entrada · Saida · Perda · Ajuste · Reserva · LiberacaoReserva
  Saldos/SaldoDeItem.cs         read-model persistido (físico/reservado/disponível/custo médio)
  Saldos/CalculadoraDeCustoMedio.cs   custo médio móvel (fórmula do plano §3.5)

SistemaX.Modules.Estoque.Application/
  Ports/                        IProdutoRepository, IMovimentoRepository, ISaldoRepository
  EventosDeIntegracao/Handlers/ Venda(Itens/Estornada) · Compra(Itens) · os 4 promovidos da OS
  CasosDeUso/                   Entrada manual · Perda · Recalcular saldo (replay de manutenção)
  ReadModels/                   SaldoAtual · CurvaAbc · GiroDeEstoque · Ruptura
  Comum/                        ExpansorDeFichaTecnica (BOM), AlertaDeEstoqueMinimo, constantes
  EstoqueModule.cs               IModule (Codigo="estoque")

SistemaX.Modules.Estoque.Infrastructure/
  InMemory/                     3 adapters in-memory
  EstoqueInfrastructureModule.cs IModule (Codigo="estoque.infra", DependeDe=["estoque"])
```

## Regra central: o razão nunca guarda `previousStock`/`newStock`

`MovimentoDeEstoque` é append-only — sem update, sem delete. `SaldoDeItem.Fisico` é sempre
**derivado** (`Σ EfeitoFisico` de todo o razão), nunca um campo editável. É a mesma política de
"contador de estoque = soma de delta" já canonizada para sync multi-terminal: dois PDVs vendendo a
última unidade offline geram dois movimentos de saída, o saldo fica negativo, e isso é o
comportamento CERTO (sinal de inventariar, nunca exceção que bloqueia a venda) — nunca perda
silenciosa de um dos dois movimentos por causa de um `previousStock` que só um dos dois viu.

Só `Ajuste` carrega delta assinado; todos os outros tipos guardam `Quantidade` sempre positiva — o
sentido físico/reservado é **derivado do `Tipo`** (`MovimentoDeEstoque.EfeitoFisico`/
`EfeitoReservado`), nunca do sinal armazenado.

## Custo médio: `Saida` nunca recalcula, só congela

A cada `Entrada`, `SaldoDeItem.AplicarMovimento` chama `CalculadoraDeCustoMedio.Recalcular` usando
o `Fisico` **anterior** à aplicação. `Saida`/`Perda` não tocam `CustoMedio` — o handler que monta o
movimento já preenche `CustoUnitario` com o custo médio vigente no instante da baixa (congela o
CMV da operação). `Fisico ≤ 0` na entrada zera a história e adota o custo da entrada — não existe
"custo médio negativo" com significado contábil. FIFO por camadas (`PoliticaDeValorizacao.Fifo`) é
o gancho de domínio para V5; **nesta entrega toda a valorização é custo médio**, independente do
valor configurado no produto (documentado em `PoliticaDeValorizacao.cs`).

## Evolução de eventos — o que foi ADICIONADO, o que NÃO foi TOCADO

Regra de execução desta tarefa: nenhuma assinatura de evento já existente em
`Modules.Abstractions/IntegrationEvents.cs` mudou, e nenhum outro módulo (Vendas, Financeiro,
Assistência) ou o Host foi tocado. Tudo abaixo é **aditivo**:

- **`ItemMovimentado`** — record de linha compartilhado (produto, quantidade em milésimos, preço/
  custo unitário, item/lote/validade opcionais).
- **`VendaItensMovimentados`** / **`CompraItensRecebidos`** — companions NOVOS de `VendaConcluida`/
  `CompraRecebida` (que continuam intactos — o Financeiro não muda nada). **GAP DOCUMENTADO**: o
  módulo Vendas (e um futuro módulo Compras, que ainda não existe no repo) não publicam esses
  eventos hoje — fora do escopo desta tarefa tocar neles. O Estoque já tem os handlers prontos e
  testados; falta a torneira do lado de quem emite. O gesto de wiring é o mesmo já demonstrado por
  `VendaConcluidaDomainEvent.ParaEventoDeIntegracao()`: publicar os dois eventos lado a lado,
  pós-commit, a partir do mesmo domain event (que já tem os itens).
- **`PecaReservada` / `PecaConsumida` / `ReservaLiberada` / `ConsumoEstornado`** — **PROMOVIDOS** de
  evento de domínio (presos em `Verticals/Assistencia/.../OrdemDeServicoDomainEvents.cs`, que já
  documentava esse gap) para evento de integração, com o payload/chave já fixados pelo plano da OS.
  **GAP DOCUMENTADO**: `ParaEventoDeIntegracao()` nos 4 `DomainEvent`s da Assistência (o mesmo
  gesto de 5 linhas que `OsFaturadaDomainEvent` já faz) e a publicação pós-commit via outbox ficam
  como follow-up de quem mantiver a Assistência — regra de execução proíbe tocar em outro módulo.
- **`EstoqueAbaixoDoMinimo` / `ReservaDescoberta` / `PerdaRegistrada`** — NOVOS, publicados pelo
  próprio Estoque. `InventarioAjustado` (plano §4.4) **não foi implementado** — depende do agregado
  `InventarioFisico`, fora do escopo desta entrega (ver seção abaixo).

## O que é stub / não está aqui

- **Persistência real**: 3 adapters in-memory — trocar por SQLite (mesma transação/outbox do
  `UnitOfWork` local, `ARCHITECTURE.md` §2.4) é um novo adapter atrás de cada port, zero mudança em
  Domain/Application.
- **`InventarioFisico`** (contagem com FSM, `InventarioAjustado`), **multi-depósito real**,
  **`LoteDeEstoque`/FEFO** e os **7 relatórios PDF (QuestPDF)** do plano §7 — roadmap V3–V5, fora do
  pedido explícito desta entrega (Domain: Produto/saldo/movimento/reserva/custo médio; Application:
  handlers + read-models ABC/giro/ruptura; Infrastructure: InMemory). O campo `LoteId` já existe no
  razão desde já (custa nada agora; migração depois custaria muito).
- **Curva ABC por RECEITA real**: o razão só retém CUSTO (CMV congelado na baixa) — `CurvaAbcService`
  classifica por valor de custo baixado, documentado como simplificação de escopo em
  `CurvaAbcService.cs` (uma curva por receita pertence a um read-model que cruze com Vendas).
  `GiroDeEstoqueService` usa o valor imobilizado ATUAL como aproximação de "estoque médio do
  período" pela mesma razão (sem snapshot histórico de saldo nesta entrega).
- Registro em `SistemaXHost.Bootstrap` (`.Adicionar(new EstoqueModule()).Adicionar(new
  EstoqueInfrastructureModule())`) fica para quem for religar o composition root do Host.

## Testes

`tests/SistemaX.Modules.Estoque.Tests` (66 testes) — VO `Quantidade` (aritmética/arredondamento
bancário), invariantes de `Produto`/`MovimentoDeEstoque`, custo médio, expansão de ficha técnica
(incl. detecção de ciclo), idempotência de todos os handlers (replay não duplica), reserva nunca
bloqueia (reserva descoberta), alerta de mínimo só na transição, e os 3 read-models de análise
(ABC 80/15/5, giro/cobertura, ruptura/venda perdida) com cenários construídos via handler real
(não seed direto), para o razão ficar coerente ponta a ponta.
