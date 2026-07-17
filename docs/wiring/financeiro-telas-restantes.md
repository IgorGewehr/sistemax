# Financeiro — Wiring das 5 telas que ainda são MOCK

**Status:** especificação de wiring · **Data:** 2026-07-17 · **Escopo:** as **5 telas do Financeiro que
ainda rodam 100% sobre mock** — **Entradas & Saídas, Recorrentes, Bancário, Fluxo de Caixa, Relatórios**.
A **Visão Geral já é real** (ver `useVisaoGeral.ts`) e **não** faz parte deste documento.

> **Propósito.** Este doc é o mapa 1:1 mockup → tela → endpoint para que uma tarefa futura ligue cada
> tela ao backend **à risca do mockup, sem redescobrir nada**. Ele **complementa**
> `docs/wiring/financeiro-api-contract.md` (que já cobre o block→endpoint em profundidade): aqui a
> ênfase é a **extração 1:1 do mockup** — seções, colunas, KPIs, chips, filtros e os números de
> exemplo — que aquele doc não detalha. Onde há sobreposição, cito a seção correspondente do contrato.
>
> **Fonte da verdade** = os HTMLs em `docs/ui/mockups/` (Lei 1 de `docs/ui/financeiro-ui.md`). Os
> view-models tipados (`components/financial/<tela>/types.ts`) e os mocks (`mocks/financeiro/<tela>.ts`)
> já foram portados 1:1 desses mockups — as tabelas abaixo citam ambos.

---

## 0. O molde a seguir (padrão REAL da Visão Geral)

O projeto **não usa TanStack Query** — é `fetch` puro embrulhado em `useState`/`useEffect`
(`web/src/lib/api/client.ts`). O padrão consolidado na Visão Geral
(`components/financial/visao-geral/useVisaoGeral.ts`) é **um `Recurso<T>` por bloco**, não um
`carregando` único da tela inteira:

```ts
export interface Recurso<T> { dado: T | null; erro: string | null; carregando: boolean; }
```

- **1 chamada `financeiroApi.xxx()` por bloco** → **adapter puro** `de<Bloco>Dto(dto)` em
  `lib/api/adapters/financeiro/<tela>.ts` (DTO camelCase / `Money{centavos,moeda}` → view-model em
  `Centavos` puro) → `set<Bloco>({ dado, erro:null, carregando:false })`.
- **Um bloco quebrado não derruba os outros**: cada `Recurso<T>` tem seu **skeleton** (loading) e seu
  **`<EmptyState>`** (erro) próprios na página — exatamente como `VisaoGeral.tsx` faz card a card.
- **Blocos sem endpoint continuam vindo do mock**, marcados com `<MockBadge>`. A "flag" na prática é a
  presença do campo no retorno do hook (o arquivo `financeiro-flags.ts` proposto no contrato **não foi
  criado** — ver `financeiro-api-contract.md` §9.3). Seguir o padrão em uso, não o arquivo de flags.
- **Camadas:** `lib/api/financeiro.ts` (DTO + `financeiroApi.xxx()`) → `lib/api/adapters/financeiro/<tela>.ts`
  (adapter) → `components/financial/<tela>/use<Tela>.ts` (hook `Recurso<T>`) → página fina consome.

### Convenção de dinheiro (vale para TODAS as 5 telas)

- **No fio:** sempre `Money = { centavos: number, moeda: string }` (os read-models F1 devolvem `long`
  cru como `number` puro, sem `moeda` — ver comentário em `financeiro.ts`). Nunca float de reais.
- **No view-model:** `Centavos` (inteiro).
- **Na exibição:** **estas 5 telas mostram REAIS INTEIROS, sem centavos** (`formatCentavosWhole` →
  "R$ 4.230"). Cada tela tem seu `MoneyValue`/`MoneyWhole` local que chama `formatCentavosWhole`
  (ver `entradas-saidas/MoneyValue.tsx`, `bancario/BancarioMoneyValue.tsx`, `fluxo-caixa/MoneyWhole.tsx`).
  Centavos com 2 casas (`formatCentavos`) só onde o mockup mostra precisão — **não ocorre nas 5 telas**.

### Endpoints que o backend expõe hoje (pós-reconciliação read-models Entradas&Saídas/Recorrentes/Relatórios)

`SistemaX.Modules.Financeiro.Application/Endpoints/FinanceiroEndpointsModule.cs` — **23 rotas GET +
2 rotas POST** (as 6 rotas de `financeiro/caixa/*` do domínio `SessaoCaixa`, fora do escopo desta
rodada — ver §4 — não entram nesta contagem/tabela, documentadas à parte quando a tela Fluxo de
Caixa for a vez). Todas as GET com `businessId` só da sessão (`http.ObterBusinessId()`, R1) e
`.RequerPermissao(Financeiro, Ver)`; as 2 POST com o mesmo R1 reforçado no endpoint (busca o
`MovimentoFinanceiro` e confere `BusinessId == businessId` da sessão antes de mutar, 404 se não
bater) e `.RequerPermissao(Financeiro, Editar)`. **5 rotas novas nesta rodada** (linhas 19–23 abaixo):
`extrato`, `relatorios/dre`, `relatorios/contas-em-aberto`, `recorrentes/detalhe`, `recorrentes/fixas`
— todas read-only, todas `dotnet build`/`dotnet test` verdes (811 testes, 0 falhas). O front (`web/`)
**não foi tocado** — as 3 telas (Entradas & Saídas, Recorrentes, Relatórios) continuam servidas por
mock até a tarefa #33 (ver nota de escopo no fim desta seção).

| # | Rota | Serve alguma das telas mock? |
|---|---|---|
| 1 | `GET /financeiro/receita-recorrente` | **sim** — Recorrentes (resumo, já ligado) e Relatórios (card MRR, ainda não ligado) |
| 2 | `GET /financeiro/disponivel-retirada` | não (é da Visão Geral) |
| 3 | `GET /financeiro/fluxo?diasHistorico&diasProjecao` | **possivelmente** — o KPI "Como fecha o mês" de Entradas & Saídas (ver §1, achado) |
| 4 | `GET /financeiro/fato-receita-diaria?de&ate` | não diretamente (série bruta) |
| 5 | `GET /financeiro/fato-caixa-diario?de&ate` | não diretamente |
| 6 | `GET /financeiro/fato-custo-diario?de&ate` | não diretamente |
| 7 | `GET /financeiro/fato-margem-produto?produtoId&de&ate` | não diretamente |
| 8 | `GET /financeiro/recebiveis?de&ate` | não diretamente |
| 9 | `GET /financeiro/previsao-caixa?dias` | não (Visão Geral / Sobrevivência) |
| 10 | `GET /financeiro/ponto-equilibrio` | não |
| 11 | `GET /financeiro/inadimplencia` | não |
| 12 | `GET /financeiro/radar-simples?anexo` | não |
| 13 | `GET /financeiro/contas-bancarias` | **sim** — Bancário (✅ ligado) |
| 14 | `GET /financeiro/formas-pagamento` | **sim** — Bancário (✅ ligado) |
| 15 | `GET /financeiro/movimentos?de&ate&contaId` | **sim** — Bancário (✅ ligado, novo nesta reconciliação) |
| 16 | `GET /financeiro/movimentos-semana?de&ate` | **sim** — Bancário (✅ ligado, novo) |
| 17 | `GET /financeiro/conciliacao?de&ate` | **sim** — Bancário (✅ ligado, novo) |
| 18 | `GET /financeiro/taxas-por-forma?de&ate` | **sim** — Bancário (✅ ligado, novo) |
| — | `POST /financeiro/conciliacao` | **sim** — Bancário, ação "Confirmar" (✅ ligado, novo) |
| — | `POST /financeiro/conciliacao/ignorar` | **sim** — Bancário, ação "Ignorar" (✅ ligado, novo) |
| 19 | `GET /financeiro/extrato?de&ate&tipo&categoria` | **sim** — Entradas & Saídas, `rows: LancamentoRow[]` da "Linha do tempo" (✅ read-model pronto, **novo**; front continua mock — task #33) |
| 20 | `GET /financeiro/relatorios/dre?de&ate` | **sim** — Relatórios (`dre.byRegime.competencia`) **e** Entradas & Saídas (`kpis.resultadoMesCentavos`) — mesmo `DreGerencialService`, dois consumidores (✅ read-model pronto, **novo**; só regime competência — regime caixa/bridge continuam ❌; front continua mock) |
| 21 | `GET /financeiro/relatorios/contas-em-aberto` | **sim** — Relatórios, card "Contas em aberto" (`aberto`, com os 3 baldes de aging) (✅ read-model pronto, **novo**; front continua mock) |
| 22 | `GET /financeiro/recorrentes/detalhe` | **sim** — Recorrentes lente Assinaturas, tabela "Todas as assinaturas" (id/cliente/serviço/valor-ciclo/próxima cobrança/status) (✅ read-model pronto, **novo**; front continua mock) |
| 23 | `GET /financeiro/recorrentes/fixas` | **sim** — Recorrentes lente Contas fixas, tabela "Todas as recorrências" — **parcial**: devolve só o TEMPLATE (`ContaFixaResumo`: valor previsto, dia fixo, próxima ocorrência), não o histórico de 12 meses/variação/`emAlerta` (cruzamento com `ContaAPagar`/`ContaAReceber` por `SourceRef` documentado como fora de escopo no XML doc de `ContasFixasService` — ver §2) |
| — | `GET /financeiro/consultor?topN` | não (Super Consultor da Visão Geral) |

Um read-model **existe e está em DI mas NÃO tem endpoint** (expor = ~10 linhas, sem lógica nova):
`AlertaFinanceiroService` (alertas). `DreGerencialService` **ganhou endpoint nesta rodada**
(`GET /financeiro/relatorios/dre`, ver linha 20 acima) — só regime competência, regime caixa
continua sem serviço (ver §5). Vários casos de uso de **escrita** já estão prontos sem casca HTTP
(`LancarConta*`, `BaixarParcela`, `*Assinatura`) — ver `financeiro-api-contract.md` §10
(`ConciliarMovimentoUseCase` já ganhou casca numa reconciliação anterior, ver linhas 17-18 acima).

**Convenções para todo endpoint novo abaixo** (idênticas às já expostas): rota `/financeiro/{recurso}`;
`businessId` só da sessão; dinheiro `Money{centavos,moeda}`; erro de negócio `{codigo,mensagem}` 422;
GET sem idempotência; DTO de fio é record próprio, nunca o agregado serializado.
**Nota de divergência (Bancário):** o texto original desta convenção previa `X-Idempotency-Key`
obrigatório em toda escrita que cria recurso. Os dois `POST /financeiro/conciliacao*` **não**
adotaram esse header — nenhum outro `*EndpointsModule` do código (Vendas, Estoque) o usa hoje, e
`ConciliarMovimentoUseCase` já é naturalmente idempotente pela chave composta (par
movimento+extrato — reconciliar/ignorar o mesmo par duas vezes é no-op). Seguimos o padrão REAL do
código, não a aspiração do documento; se `X-Idempotency-Key` virar convenção de fato num PR futuro,
revisitar os dois endpoints aqui.

---

## 1. Entradas & Saídas

- **Mockup:** `docs/ui/mockups/entradas-saidas.html`
- **Renderiza hoje:** `web/src/pages/financeiro/EntradasSaidas.tsx` → hook `useEntradasSaidas`
  (`components/financial/entradas-saidas/useEntradasSaidas.ts`, que hoje lê o mock direto).
- **Mock/badge a remover:** `mocks/financeiro/entradas-saidas.ts` (`ENTRADAS_SAIDAS_MOCK`);
  `<MockBadge>` no `PageHeader` ("Extrato unificado, KPIs e categorias ainda não têm endpoint").
- **View-model:** `components/financial/entradas-saidas/types.ts` (`EntradasSaidasMock`).

### Seções, na ordem do mockup (1:1)

1. **PageHeader** — eyebrow "Financeiro · Entradas & Saídas"; título "Entradas & Saídas"; subtítulo
   "Tudo que entrou, saiu — e o que ainda vem. É aqui que você planeja."; pill de período "Julho 2026 ▾".
2. **SegmentedFiltro** — 3 segmentos: **Tudo · A receber · A pagar** (`SegFiltro`).
3. **KpiRow** — **4 KPIs** (`grid md:grid-cols-4`):
   - **"A receber em aberto"** (hero) = **R$ 7.479**; sublinha crit "R$ 1.890 atrasado"; sparkline;
     rodapé "12 parcelas em aberto".
   - **"A pagar em aberto"** = **R$ 7.210**; "maior: **Folha** · 30/07"; rodapé "3 lançamentos abertos".
   - **"Resultado do mês"** (ícone info: competência) = **R$ 4.230**; "▲ 9% vs junho"; rodapé
     "regime de competência".
   - **"Como fecha o mês"** = **+R$ 3.580** (com sinal); "projeção do caixa"; rodapé "se tudo que
     vence em julho for pago".
   - **Bridge note** logo abaixo: resultado R$ 4.230 × caixa R$ 3.580 × diferimento R$ 650.
4. **ConsultorFornecedores** (Super Consultor, read-only) — "+42% vs média", média histórica R$ 2.183,
   total mês R$ 3.100, 3 pagamentos → link "Ver detalhe" (drill de navegação, não ação).
5. **Análise** (`grid lg:grid-cols-[1.15fr_1fr]`):
   - **GastosPorCategoria** — "Para onde foi o dinheiro" — barras de 6 categorias de despesa (Folha
     4.900 · Fornecedores 3.100 · Aluguel 2.100 · Impostos 1.240 · Software 540 · Marketing 310), cada
     uma com histórico de 6 meses (Fev→Jul) e drill de colunas.
   - **RaioXDoMes** — "Raio-X do mês" — % fixo vs variável, líder de alta, tile "atrasados +30d"
     (valor + qtd clientes) clicável.
6. **LinhaDoTempo** — "Linha do tempo" — **DataTable** colunas: **Data | Descrição | Categoria | Status
   | Valor | Ação**. Chips de status: **Previsto** (neutro) · **Pago** (mostra conta + origem, "✓
   Conciliado") · **Atrasado Nd** (crit). Divisor "Hoje". Linha de resumo do PDV (12 vendas · R$ 9.782).
   Ações por linha: **Dar baixa** (ModalDarBaixa), **Cobrar**, **Detalhe** (ModalDetalhe);
   "Lançamento rápido" (ModalLancamento); "Ver extrato completo".

**Unidades:** tudo em **reais inteiros** (`MoneyValue` local → `formatCentavosWhole`). "Como fecha o mês"
usa sinal (`signed`).

### Dados que a tela precisa × endpoint

| Bloco (view-model) | Endpoint | Situação |
|---|---|---|
| `rows: LancamentoRow[]` (Data ISO, desc, sub, categoria, tipo, status, valorCentavos, conta?, origem?, diasAtraso?) | `GET /financeiro/extrato?de&ate&tipo&categoria` (**ENTREGUE nesta rodada**, ver §0 linha 19) | ✅ read-model pronto — `ExtratoUnificadoService` junta pago (`MovimentoFinanceiro`) + previsto/atrasado (parcelas abertas de `ContaAPagar`/`ContaAReceber`); `conta`/`origem` só preenchidos quando `status=pago`. Front continua mock (task #33) |
| `kpis.aReceber*` / `aPagar*` (aberto, atrasado, parcelas/lançamentos, maior label+data) | **NOVO** `GET /api/financeiro/kpis-entradas-saidas?inicio&fim` | ❌ dado-base nos ports, soma/agrupamento não — `ExtratoKpis` (do `extrato`) só dá total entradas/saídas/saldo do período, não o detalhamento aberto×atrasado×"maior" que este KPI pede |
| `kpis.resultadoMesCentavos` + `resultadoDeltaPct` + `resultadoComparadoMes` | `GET /financeiro/relatorios/dre?de&ate` (**ENTREGUE nesta rodada**, ver §0 linha 20) | ⚠️ `resultadoMesCentavos` (= `DreResultado.ResultadoOperacional`) já servível; `resultadoDeltaPct`/`resultadoComparadoMes` (comparar com o mês anterior) NÃO estão no serviço — precisam 2ª chamada com período anterior + cálculo no consumidor |
| `kpis.fechamentoCaixaCentavos` ("projeção do caixa se tudo que vence em julho for pago") | **provavelmente `GET /financeiro/fluxo` (JÁ EXISTE)** | 🔎 **achado** — ver nota abaixo |
| `kpis.sparklineReceber` (paths SVG) | **NOVO** série diária de "a receber em aberto" (front desenha o path) | ❌ falta read-model de série temporal |
| `bridge: BridgeNoteData` (resultado × caixa × diferimento) | `GET /financeiro/dre?regime=caixa` (mesmo gap de Relatórios §5) | ❌ regime de caixa é serviço novo |
| `consultorFornecedores` | **NOVO** `GET /api/financeiro/consultor-fornecedores` → `{ deltaPct, mediaHistoricaCentavos, totalMesCentavos, qtdPagamentos }` | ❌ |
| `categorias: CategoriaDespesaResumo[]` (6m por categoria) | **NOVO** `GET /api/financeiro/categorias-resumo?meses=6` | ❌ **+ decisão de catálogo** — `CategoriaId` da UI (`folha\|fornecedores\|aluguel\|impostos\|software\|marketing\|servicos`) ≠ `CategoriaFinanceiraPadrao` do .NET (ver contrato §4/§11) |
| `contasDisponiveis` (select do modal) / `categoriasLancamentoRapido` | `GET /financeiro/contas-bancarias` (já existe — ver §3) | ⚠️ repo/endpoint já resolvidos pela reconciliação do Bancário; falta só o front consumir aqui |
| Ação "Lançamento rápido" | **NOVO** `POST /api/financeiro/contas-a-pagar` **e** `.../contas-a-receber` + `X-Idempotency-Key` | ⚠️ casos de uso `LancarConta*UseCase` prontos, sem endpoint |
| Ação "Dar baixa" | **NOVO** `POST /api/financeiro/contas/{contaId}/parcelas/{parcelaId}/baixar` | ⚠️ `BaixarParcelaUseCase` pronto; pede `contaBancariaCaixaId`/`formaPagamentoId` (mesmo gap de conta) |

**🔎 Achado (divergência com o contrato-irmão §4):** o KPI `fechamentoCaixaCentavos` está copiado no
mockup como **"Como fecha o mês / projeção do caixa / se tudo que vence em julho for pago"** — isso é o
**saldo projetado de fim de mês**, que é exatamente o que `FluxoDeCaixaService`/`GET /financeiro/fluxo`
já produz (`pontos[].saldoAcumulado` do último ponto de julho). O contrato §4 marcou esse KPI como
"bloqueado por sessão de caixa física (§4.5)" — **provavelmente incorreto**: não é o ritual de gaveta
(tela Fluxo de Caixa), é projeção de caixa. **Recomendo confirmar e, se procede, ligar esse KPI ao
endpoint EXISTENTE `/financeiro/fluxo`** — seria o único bloco desta tela ligável hoje.

**Contrato do endpoint novo principal (`lancamentos`), resposta camelCase:**
```
GET /api/financeiro/lancamentos?inicio=YYYY-MM-DD&fim=YYYY-MM-DD&tipo=entrada|saida&status=previsto|pago|atrasado
→ 200 [{ id, data (ISO), descricao, sub|null, categoriaId, tipo, status,
         valor: {centavos,moeda}, conta?, origem?, diasAtraso? }]
```

### Estados (padrão Visão Geral)
- **loading:** skeleton por bloco (KpiRow, categorias, LinhaDoTempo cada um o seu).
- **erro:** `<EmptyState>` por bloco (`Recurso<T>.erro`), sem derrubar os outros.
- **vazio:** LinhaDoTempo sem linhas → estado vazio "nenhum lançamento no período"; KPIs em zero são
  válidos (mostrar "R$ 0"), não vazio.

### Lei 2
**ConsultorFornecedores** é read-only — observa ("gastou +42% vs a média") e aponta ("Ver detalhe" =
navegação). Nenhuma copy de IA-que-age. Ao ligar, manter: nada de "aplicar", "automatizar", "quer que
eu…".

---

## 2. Recorrentes

- **Mockups:** `docs/ui/mockups/recorrentes.html` (lente Contas fixas) **+**
  `docs/ui/mockups/financeiro-assinaturas.html` (números da lente Assinaturas).
- **Renderiza hoje:** `web/src/pages/financeiro/Recorrentes.tsx` (LensSwitch `fixas`/`assinaturas`).
  Lente `assinaturas` já usa **`useReceitaRecorrente` (REAL)** no bloco `AssinResumoReal`.
- **Mock/badge a remover:** `mocks/financeiro/recorrentes.ts` (`RECORRENTES_MOCK`) — **parcial**:
  o `<MockBadge>` do header explica que **o resumo agregado já é real** e o resto continua mock.
  Remover o mock só do que for sendo ligado; o resumo agregado **já não usa mock**.
- **View-model:** `components/financial/recorrentes/types.ts` (`RecorrentesViewModel`).

### Lente "Contas fixas" (mockup `recorrentes.html`) — 0% backend

Seções: PageHeader (título "Contas fixas", botão "Nova recorrência") → LensSwitch → "Retrato do fixo"
(`RetratoFixo`: projeção anual, variação 6m %, total há 6 meses, total atual, compromissos ativos) →
FixasConsultor (Super Consultor, nota "daqui a 2 dias") → "Seus compromissos fixos" (análise) →
**"Todas as recorrências"** — tabela colunas: **Nome | Categoria | Valor/mês | Dia venc. | Próxima |
Variação vs média | Status**. 11 contas fixas com **histórico de 12 meses** (Ago→Jul) cada; chip de
status = `emAlerta` quando variação ≥ 15%.

| Bloco | Endpoint | Situação |
|---|---|---|
| `fixas.itens` — só o TEMPLATE (nome, categoria, valor previsto, dia venc., próxima, frequência) | `GET /financeiro/recorrentes/fixas` (**ENTREGUE nesta rodada**, ver §0 linha 23) | ⚠️ **parcial** — `ContasFixasService` expõe `IRecorrenciaRepository.ListarAtivasAsync` (template + próxima ocorrência projetada); front continua mock (task #33) |
| `ContaFixaDerivada` (atual, mesPassado, media6m, variacaoPct, emAlerta, totalAnoCorrente — o histórico de 12m da coluna "Variação vs média") | **NOVO** — read-model maior, não coberto por `recorrentes/fixas` | ❌ **fora de escopo desta rodada** (documentado no XML doc de `ContasFixasService`) — precisa cruzar template `Recorrencia` + realizado (`ContaAPagar`/`ContaAReceber` por `SourceRef` `recorrencia:{id}:`); sem esse cruzamento não há "variação vs média de 6 meses" nem flag `emAlerta` |
| `RetratoFixo` (KPIs do topo — projeção anual, variação 6m, compromissos ativos) | depende do read-model acima (soma sobre histórico) | ❌ mesmo gap — bloqueado pelo item anterior |
| Ação "Nova recorrência" | **NOVO** `POST /api/financeiro/recorrencias` | ❌ **nem o caso de uso existe** — só há `GerarContasRecorrentesUseCase` (materializa), falta `CriarRecorrenciaUseCase` |

### Lente "Assinaturas" (mockup `financeiro-assinaturas.html`)

Seções: **AssinResumoReal** (REAL) → **PainelAssinaturas** (MOCK): "MRR por serviço" (4 serviços:
TensorRoot Gov 2.500 · Gestão Raiz Fiscal 2.090 · ServicePro 1.047 · Brain 440), "Retenção da carteira"
(tempo médio 14m · LTV R$ 10.640 · retenção 82%), **"Todas as assinaturas"** — tabela colunas:
**Serviço | Cliente | Valor/mês | Ciclo | Próx. cobrança | Status**; sparklines novos6m/churn6m por serviço.

| Bloco | Endpoint | Situação |
|---|---|---|
| Resumo agregado: assinaturas ativas (8), MRR, ARR, ticket médio, churn | `GET /financeiro/receita-recorrente` | ✅ **JÁ LIGADO** — `useReceitaRecorrente` → `deReceitaRecorrenteDto` → `AssinResumoReal` (sem MockBadge) |
| `mrrMesAnterior` | mesmo endpoint com **`?referencia=`** | ⚠️ o serviço `CalcularAsync` já aceita `DateTimeOffset referencia`; falta o `MapGet` ler da query (1 linha) |
| **"Todas as assinaturas"** (tabela nominal: id, cliente, serviço, valor/ciclo, ciclo, próxima cobrança, status) | `GET /financeiro/recorrentes/detalhe` (**ENTREGUE nesta rodada**, ver §0 linha 22) | ✅ read-model pronto — `AssinaturaDetalheService` lista assinaturas ativas com `ProximaCobranca` DERIVADA (nunca persistida, projetada a partir do ciclo/dia de cobrança). Front continua mock (task #33) |
| `servicos[]` completo (clientes, churnClientesMes, tempoMedioMeses, ltv, retencaoPct, novos6m[], churn6m[]) — "MRR por serviço"/"Retenção da carteira" | **NOVO** — `receita-recorrente` só devolve `porServico{servicoId,servicoNome,mrr,percentual}` (raso demais); `recorrentes/detalhe` não agrega por serviço nem calcula LTV/retenção | ❌ read-model mais rico ainda por fazer; LTV/retenção não existem em serviço nenhum |
| `carteira` / `sparklineMrr6m` | **NOVO** `GET /api/financeiro/receita-recorrente/serie?meses=6` (ou método `CalcularSerieAsync`) | ❌ evita 6 round-trips |
| `concentracaoServicoId` / nomes de churn/novo | mesmo endpoint (`maiorConcentracao`) | ⚠️ **não ligado de propósito** — ids do seed ≠ ids do mock (ver contrato §5) |
| Ações Pausar/Reativar/Cancelar/Nova assinatura | **NOVO** `POST /api/financeiro/assinaturas[/{id}/pausar\|reativar\|cancelar]` | ⚠️ casos de uso prontos, zero endpoint |

**Unidades:** reais inteiros.

### Estados
- **loading/erro:** `AssinResumoReal` já implementa `Recurso<T>` (loading + erro). Replicar para os
  novos blocos (`servicos[]`, `contas-fixas`).
- **vazio:** sem assinaturas ativas → resumo em zero; "Todas as recorrências"/"Todas as assinaturas"
  sem linhas → estado vazio.

### Lei 2
FixasConsultor e AssinConsultor são read-only (explicam variação, apontam concentração). Manter assim.

---

## 3. Bancário — ✅ LIGADO (dado real, sem mock)

> **Status pós-reconciliação (ver `CLAUDE.md`/roadmap #32-#33):** as 5 telas mock começaram este
> capítulo como "ZERO ligável sem endpoint novo" — **Bancário já não é mais uma delas**. As duas
> seções abaixo (o que existia antes vs. o que fecha o domínio agora) ficam registradas porque o
> restante do documento (Entradas & Saídas, Fluxo de Caixa) ainda cita este parágrafo como
> referência do padrão a seguir.

- **Mockup:** `docs/ui/mockups/bancario.html`
- **Renderiza hoje:** `web/src/pages/financeiro/Bancario.tsx` → `useBancario` (Recurso<T> por
  bloco) → `BancarioBoard` — **100% dado real**, `mocks/financeiro/bancario.ts` foi removido.
- **View-model:** `components/financial/bancario/types.ts` (`BancarioViewModel`); adapter puro em
  `lib/api/adapters/financeiro/bancario.ts`.

### Seções, na ordem do mockup (1:1)

1. **PageHeader** — título "Bancário"; subtítulo "O que de fato entrou e saiu das suas contas — e se
   bate com o que você lançou."; período "Julho 2026 ▾". FAB "Lançar" (+).
2. **AccountFilterBar** — chips por conta: **Itaú PJ · Nubank PJ · Stone** (+ "Todas").
3. **BancarioKpiRow** — **3 KPIs**:
   - **"Saldo em bancos"** = **R$ 12.740**; delta "+R$ 860 (+7,2%) no mês"; rodapé "Itaú R$ 8.120 ·
     Nubank R$ 3.410 · Stone R$ 1.210".
   - **"Entrou no mês"** = **R$ 38.240**; "+9,3% vs junho"; rodapé "214 movimentos".
   - **"Saiu no mês"** = **R$ 31.480**; "+14,1% vs junho"; rodapé "89 movimentos".
4. **WeeksAnalysisCard** — "Entrou × saiu por semana" — gráfico divergente, 3 semanas (01–05 jul ·
   06–12 jul · 13–19 jul\* parcial), drill para os dias da semana.
5. **ExtratoTable** — "Extrato" — colunas: **Data | Descrição | Forma | Conta | Valor | Conciliação**.
   Chip conciliação: **✓ Conciliado** (pos) / **pendente**. Hint "amostra do período · 214 entradas +
   89 saídas no total". 15 movimentos (valores com sinal +/−).
6. **ConciliacaoCard** — "Conciliação" — 3 baldes: **"Bateu certinho"** (132 total + amostra de 3),
   **"Sobrou no banco"** (4 itens com `sugestao` heurística + 2 botões: "É essa! confirmar"/"Não é
   essa", "Lançar como despesa"/"Ignorar", "Buscar cliente"/"Ignorar"), **"Sobrou no sistema"** (3 itens).
7. **SuperConsultorBancario** — taxa total **R$ 1.184** (3,1% do volume), crédito parcelado 4,8%;
   painel "Ver por forma": PIX 0% · Cartão débito 1,8% · Cartão crédito à vista 2,9% · Cartão crédito
   parcelado 4,8% (destaque vermelho) · TED/Boleto R$ 12 fixo.

**Unidades:** reais inteiros, com sinal no extrato (`BancarioMoneyValue signed`).

### Estado ANTES desta reconciliação (histórico — não reflete mais o código)

Este texto descrevia o bloqueio original e fica só como referência do que foi resolvido:
"`ContaBancariaCaixa` e `FormaDePagamento` não têm repositório no Application (só
`Conciliacao`/`ExtratoBancarioItem` têm port); há uma única conta-caixa hardcoded
(`ClassificadorFormaPagamento.ContaCaixaPadraoId`) — antes de qualquer endpoint, criar
`IContaBancariaCaixaRepository` + `IFormaDePagamentoRepository`". **Isso já foi feito** — os dois
repositórios (`IContaBancariaCaixaRepository`/`IFormaDePagamentoRepository`, com `Sqlite*`/`InMemory*`
+ `FinanceiroBootstrapSeeder` idempotente) existem desde a reconciliação anterior a esta, e os
endpoints `GET /financeiro/contas-bancarias`/`GET /financeiro/formas-pagamento` já estavam expostos.
O que faltava — e fecha agora — eram os OUTROS endpoints que o mockup Bancário exige:

| Bloco | Endpoint | Situação |
|---|---|---|
| `contas: ContaBancaria[]` (label + saldo) | `GET /financeiro/contas-bancarias` | ✅ já existia — `ContasBancariasService` |
| `movimentos: MovimentoExtrato[]` | `GET /financeiro/movimentos?de&ate&contaId` → `[{ id, data(ISO), descricao, forma, contaBancariaCaixaId, valor:{centavos,moeda}(signed), conciliado }]` | ✅ `MovimentosBancariosService` — junta `MovimentoFinanceiro` + nome da forma (`IFormaDePagamentoRepository`) + descrição de competência (`ResolvedorDeDescricaoDeMovimento`, via `ContaAReceber`/`ContaAPagar` por `ContaOrigemId`) + status via `IConciliacaoRepository.ListarPorBusinessIdAsync` (método novo no port) |
| `semanas: SemanaMovimento[]` | `GET /financeiro/movimentos-semana?de&ate` → `[{ numero, inicio, fim, parcial, dias:[{dia,entradas,saidas}] }]` | ✅ `MovimentosSemanaisService` — baldes de 7 dias corridos a partir de `de`; `parcial` quando `ate` corta a última semana antes de completar |
| `conciliacaoInicial` (3 baldes) | `GET /financeiro/conciliacao?de&ate` → `{ bateuCertinhoTotal, bateuCertinhoAmostra[], sobrouNoBanco[], sobrouNoSistema[] }` | ✅ `ConciliacaoBancariaService` — cruza `ExtratoBancarioItem`/`MovimentoFinanceiro` via `Conciliacao`; cada item de sobra carrega `sugestao` (texto) + `idSugerido` (heurística: mesmo valor absoluto, data mais próxima do lado oposto) |
| Ação confirmar/ignorar conciliação | `POST /financeiro/conciliacao` / `POST /financeiro/conciliacao/ignorar` | ✅ casca HTTP do `ConciliarMovimentoUseCase` já pronto — R1 reforçado no endpoint (confere `MovimentoFinanceiro.BusinessId == businessId` da sessão antes de mutar, 404 se não bater, mesmo padrão de `VendasEndpointsModule`) |
| `consultor` (taxa por forma) | `GET /financeiro/taxas-por-forma?de&ate` → `{ taxaTotal, volumeTotal, percentualVolume, porForma:[{formaPagamentoId,forma,volume,taxaPercentual,taxa}] }` | ✅ `TaxasPorFormaService` — só sobre `TipoMovimentoFinanceiro.Entrada`, `FormaDePagamento.TaxaPercentual`/`CalcularTaxa` é o LAR único do MDR (nenhum número hardcoded) |
| `kpiSaldoDelta`/`kpiEntrouDelta`/`kpiSaiuDelta` (vs período anterior) | sem endpoint novo — calculado no front (`useBancario`) chamando `movimentos-semana` duas vezes (período atual + período anterior de mesma duração) | ✅ front-only; sem endpoint de "comparar 2 períodos" reusável no backend ainda (oportunidade futura se mais telas precisarem do mesmo padrão) |

**Divergência honesta do mockup:** `TipoFormaPagamento` (.NET) não distingue "crédito à vista" de
"crédito parcelado" — são a mesma forma `Credito` com uma única `TaxaPercentual`. O painel "Ver por
forma" real mostra as formas cadastradas de verdade (dinheiro/pix/débito/crédito/boleto, seed de
`FinanceiroBootstrapSeeder`), não a lista fantasiosa do mockup — mesma diretriz de não inventar dado
que `deTimelineDto`/`useVisaoGeral` já seguem (ver §0).

### Estados
- **loading:** skeleton por bloco (KPIs, extrato, conciliação, semanas) — `BancarioBoard` decide por
  `Recurso<T>.carregando`, cada bloco com seu próprio `<Skeleton>`.
- **erro:** `<EmptyState>` por bloco (`Recurso<T>.erro`), sem derrubar os outros.
- **vazio:** extrato sem movimentos → tabela vazia; conciliação sem pendências → `ConciliacaoCard`
  já mostra "Tudo resolvido por aqui" (comportamento pré-existente do componente); item de sobra sem
  `idSugerido` (heurística não achou par) → sem botões de ação, só a explicação (não força um par
  inválido no backend).

### Lei 2
**SuperConsultorBancario** é read-only (mostra taxa por forma, aponta a forma mais cara). A
`sugestao`/`idSugerido` da conciliação são **heurística de match determinística** (mesmo valor
absoluto + data mais próxima — `ConciliacaoBancariaService.MelhorCandidato`), não o Super Consultor —
e confirmar/ignorar são sempre **ações do usuário** (clique explícito), nunca automáticas. Nenhuma
copy de IA-que-age.

---

## 4. Fluxo de Caixa — ✅ LIGADO (dado real, sem mock)

> **Status pós-reconciliação:** as seções abaixo descreviam esta tela como "0 de 4 blocos ligáveis,
> precisa de domínio novo" — **isso já não procede**. O agregado `SessaoCaixa` (Domain + Application
> + Infrastructure, `SistemaX.Modules.Financeiro.*`) e as 6 rotas `financeiro/caixa/*` citadas em §0
> existem e estão em uso pelo front. O texto "Dados × endpoint" original fica preservado logo abaixo
> só como referência histórica do que foi resolvido — mesmo padrão de §3 (Bancário).

- **Mockup:** `docs/ui/mockups/fluxo-de-caixa.html`
- **Renderiza hoje:** `web/src/pages/financeiro/FluxoCaixa.tsx` → `useFluxoCaixa`
  (`components/financial/fluxo-caixa/useFluxoCaixa.ts`, `Recurso<T>` por bloco, mesmo padrão de
  `useVisaoGeral`) → `FluxoCaixaBoard` — **100% dado real**, `mocks/financeiro/fluxo-caixa.ts`
  (`FLUXO_CAIXA_MOCK`) não é mais consumido pela página.
- **View-model:** `components/financial/fluxo-caixa/types.ts` (`SessaoCaixa`/`SessaoCaixaFechada`);
  adapter puro em `lib/api/adapters/financeiro/fluxoCaixa.ts` (`deSessaoCaixaDto`).

> **⚠️ Colisão de nome, não de conceito.** `GET /financeiro/fluxo` (existente) é **projeção de saldo
> diário** e alimenta a *timeline da Visão Geral* — **NÃO** esta tela. A tela "Fluxo de Caixa" é o
> **ritual do caixa físico em espécie** (sessão de caixa / till session): abertura com fundo de troco,
> vendas em espécie, sangrias, fechamento cego, quebra. `useFluxoCaixa` não reutiliza `/financeiro/fluxo`.

### Seções, na ordem do mockup (1:1)

1. **KpisSection** — **4 KPIs**:
   - **"Na gaveta agora"** (hero) = valor na gaveta + botão **"Fechar caixa"** (se aberto).
   - **"Caixa de hoje"** = badge **Aberto/Fechado** + rodapé descritivo.
   - **"Diferença do mês"** (tone auto: verde sobra / vermelho falta) + rodapé "N faltas · N sobras".
   - **"Sangrias do mês"** = total (qtd) + rodapé "maior parte → Itaú PJ".
2. **ConsultorSection** (Super Consultor) — "Prioridade da semana: as faltas do mês somam R$ 100
   (parcialmente compensadas por R$ 16 de sobra) e se concentram nas **quintas à tarde** — média
   −R$ 46, sempre no fechamento sozinho da Ana. Ponha um segundo conferente nesse horário…" +
   link "**Ver as quintas →**".
3. **SessoesTable** — "Sessões" — colunas: **Dia | Operador | Abertura | Fechamento | Esperado |
   Contado | Diferença**. Sessões fechadas do mês + a de hoje (aberta, quando houver). Drill de sessão
   (SessaoDrillStats/Timeline/SessaoHojeFormula "Quanto tem na gaveta?").
4. **AnaliseInterativa** — "Diferenças por dia (mês)" (OverviewChart) + "Padrão do caixa"
   (PadraoCaixaStats).
5. **Modais** — **ModalAbrirCaixa** ("Abrir caixa", fundo de troco), **ModalFecharCaixa** (contagem
   cega), **ModalNovaSangria** ("Registrar sangria", select "Foi para onde" alimentado por
   `GET /financeiro/contas-bancarias`), **ModalNovoSuprimento** ("Registrar suprimento").

**Unidades:** reais inteiros (`MoneyWhole`), diferença com sinal/tone.

### Dados × endpoint — as 4 ações do operador fecham o domínio

| Bloco | Endpoint | Situação |
|---|---|---|
| `sessaoHoje` (a sessão aberta agora, se houver) | `GET /financeiro/caixa/atual?contaCaixaId` | ✅ `SessaoCaixaUseCases`/`ISessaoCaixaRepository` — `sessaoHoje === null` (nenhuma sessão aberta ainda hoje) é estado de 1ª classe: `FluxoCaixaBoard` mostra o convite "Abrir caixa" no lugar do formulário/KPIs de hoje |
| `sessoesFechadas[]` (mês corrente) | `GET /financeiro/caixa/historico?contaCaixaId&de&ate` | ✅ combinado com `caixaAtual` num único `Recurso<BoardReal>` (`useFluxoCaixa.carregar`) — as duas leituras compõem o mesmo conceito de tela, carregadas juntas |
| Ação "Abrir caixa" (fundo de troco) | `POST /financeiro/caixa/abrir` `{ saldoAberturaCentavos, operadorId, operadorNome }` | ✅ `AbrirSessaoCaixaUseCase` — `operadorId` hoje é slug derivado do nome (`operadorIdDeNome`), não UUID real; trocar quando existir endpoint "quem sou eu" (ver comentário em `useFluxoCaixa.ts`) |
| Ação "Registrar suprimento" | `POST /financeiro/caixa/suprimento` `{ sessaoId, valorCentavos, motivo, operadorId, operadorNome }` | ✅ |
| Ação "Registrar sangria" | `POST /financeiro/caixa/sangria` `{ sessaoId, valorCentavos, motivo, operadorId, operadorNome }` | ✅ |
| Ação "Fechar caixa" (contagem cega) | `POST /financeiro/caixa/fechar` `{ sessaoId, contadoCentavos }` | ✅ `FecharSessaoCaixaUseCase` |
| `consultorInsight` ("quintas à tarde", operador crítico) | sem endpoint novo — calculado no front (`calc.ts`: `calcularDiaCritico`/`operadorMaisFrequenteNoDia`) sobre `todasAsSessoes` já carregado | ✅ front-only, mesmo padrão de `kpiSaldoDelta` do Bancário (§3) |
| `vendasEspeciePercentual` | sem endpoint (Vendas/PDV não expõe "venda em espécie" ainda) | ⚠️ fixo em `0` de propósito — é o valor REAL enquanto nenhuma venda em espécie for registrada na sessão, não um placeholder inventado (comentário em `useFluxoCaixa.ts`) |
| `destinosSangria[]` (select) | `GET /financeiro/contas-bancarias` (já real, ver §3) | ✅ reaproveitado + `'Cofre da loja'` fixo como fallback |

**Decisão que ficou registrada:** `SessaoCaixa` é dono do **Financeiro** (não Vendas/PDV) — Domain +
Application + Infrastructure vivem em `SistemaX.Modules.Financeiro.*` (`Domain/Caixa/`,
`SessaoCaixaUseCases`, `SqliteSessaoCaixaRepository`/`InMemorySessaoCaixaRepository`).

### Estados
- **loading:** skeleton por bloco — `FluxoCaixaBoard` decide por `Recurso<T>.carregando` (mesmo
  padrão de `BancarioBoard`).
- **erro:** `<EmptyState>` no board quando a leitura combinada hoje+histórico falha — um bloco
  quebrado não impede o resto da página.
- **vazio:** sem sessões no mês → `SessoesTable` mostra "Nenhuma sessão de caixa neste mês."; sessão
  de hoje ausente (`sessaoHoje === null`) → convite "Abrir caixa" no lugar do formulário/KPIs de hoje.

### Lei 2
**ConsultorSection** é read-only: explica o padrão ("faltas nas quintas à tarde, fechamento sozinho da
Ana") e **aconselha o usuário** ("ponha um segundo conferente") — o **usuário** age, a IA não. "Ver as
quintas →" é drill de navegação. Nenhuma copy de IA-que-age. Mantido assim.

---

## 5. Relatórios

- **Mockup:** `docs/ui/mockups/relatorios.html`
- **Renderiza hoje:** `web/src/pages/financeiro/Relatorios.tsx` → `useRelatoriosController(RELATORIOS_MOCK)`.
- **Mock/badge a remover:** `mocks/financeiro/relatorios.ts` (`RELATORIOS_MOCK`); `<MockBadge>` no
  header ("DRE, extrato, aberto e pacote ainda não têm endpoint").
- **View-model:** `components/financial/relatorios/types.ts` (`ReportsViewModel`).

### Seções, na ordem do mockup (1:1)

1. **PageHeader** — título "Relatórios"; subtítulo "Documentos prontos pra mandar pro seu contador ou
   sócios."
2. **SubheadControls** — select de período (Julho 2026 · Junho 2026 · Maio 2026 · 2º trimestre 2026)
   + **toggle de regime: competência | caixa** (único toggle global de regime do Financeiro).
3. **InfoNote**.
4. **DocGrid** — **5 cards de documento**:
   - **DreCard** "DRE do mês" — por regime. **Competência:** Receita bruta **R$ 42.380** · (–) Impostos
     **R$ 3.680** · (–) Despesas e custos **R$ 34.470** · **Resultado do mês R$ 4.230** · "▲ 12% vs
     Junho (R$ 3.780)". **Caixa:** Recebido no caixa **R$ 39.380** · (–) Pago no caixa 34.470 · (–)
     Impostos pagos 3.680 · **Resultado no caixa R$ 1.230** · "▼ R$ 3.000 abaixo da competência". Bridge
     note. Ações: **PDF / Excel / Enviar**.
   - **PacoteCard** "Fechamento mensal" — checklist: DRE do mês · Extratos por conta (3 contas) · Contas
     em aberto (a pagar e a receber) · Fechamentos de caixa (23 sessões) · Pendências de conciliação
     (7 itens). Zip `fechamento-julho-2026.zip`. "Gerar pacote" / "Baixar zip".
   - **ExtratoCard** "Extrato por conta" — multiselect (Todas · Itaú PJ · Nubank PJ · Caixa loja),
     período default 01/07→16/07, PDF/Excel.
   - **AbertoCard** "Contas em aberto" — a receber em aberto **R$ 9.430** · atrasado **R$ 1.890** ·
     a pagar em aberto **R$ 7.210**; aging: **0–15d R$ 900 · 15–30d R$ 600 · +30d R$ 390**.
   - **MrrCard** "Assinaturas / MRR" — "Visível porque vende serviço recorrente"; MRR **R$ 6.077** ·
     churn mês **R$ 1.239** · ARR estimado **R$ 72.924**.
5. **HistoryTable** — "Histórico de exports" — colunas: **Data | Documento | Formato | Gerado por |
   Envio**. Chips de envio: **✓ E-mail / ✓ WhatsApp / — Não enviado**. 4 linhas iniciais.

**Unidades:** reais inteiros.

### Dados × endpoint

| Bloco | Endpoint | Situação |
|---|---|---|
| `mrr` (MrrCard: mrr, churnMes, arrEstimado) | `GET /financeiro/receita-recorrente` | ✅ **existe, ainda não ligado** — `mrr=Mrr`, `churnMes=MrrChurnNoMes`, `arrEstimado=Arr`; `condicaoLabel` é copy estático. **Ganho barato** |
| `aberto` (receber/pagar em aberto + aging buckets) | `GET /financeiro/relatorios/contas-em-aberto` (**ENTREGUE nesta rodada**, ver §0 linha 21) | ✅ read-model pronto — `ContasEmAbertoService` soma `ListarAbertasAteAsync` de `ContaAReceber`/`ContaAPagar` e balda o atrasado em 0–15/15–30/+30d. Front continua mock (task #33) |
| `dre.byRegime.competencia` | `GET /financeiro/relatorios/dre?de&ate` (**ENTREGUE nesta rodada**, ver §0 linha 20) | ⚠️ `DreGerencialService` exposto e servindo `ReceitaBruta`/`CustoDireto`/`DespesaOperacional`/`ResultadoOperacional`; **atenção à taxonomia** — o agrupamento do mockup ("Impostos"/"Despesas e custos") ≠ serviço (`CustoDireto` inclui CMV real via `fato_custo_diario` + comissões, `DespesaOperacional` é o resto) — front precisa mapear, não é 1:1 direto. Front continua mock |
| `dre.byRegime.caixa` + `bridgeNote` | `GET /financeiro/relatorios/dre?...&regime=caixa` | ❌ regime de caixa **não implementado** nesta rodada — `DreGerencialService.CalcularAsync` só tem o caminho de competência; é serviço novo (compartilhado com o `bridge` de Entradas & Saídas §1 — resolver 1×, 2 consumidores) |
| `pacote` (ZIP) | **NOVO** `POST /api/financeiro/relatorios/pacote` | ❌ nenhuma geração de PDF/Excel/ZIP no Application — Fase 2/3 |
| `extrato` (PDF/Excel por conta) | **NOVO** `GET /api/financeiro/relatorios/extrato?contaId&inicio&fim&formato` | ⚠️ dado-base existe; geração de arquivo não; depende de `contas-bancarias` (§3) |
| `initialHistory[]` + envio | **NOVO** `GET /api/financeiro/relatorios/historico` + `POST .../{id}/enviar` | ❌ falta entidade + persistência + integração de envio |
| `contact` (e-mail/WhatsApp do contador) | **fora do Financeiro** — módulo Settings/Business | ❌ não é responsabilidade deste módulo |

### Estados
- **loading/erro:** `Recurso<T>` por card (MrrCard e AbertoCard primeiro, que são baratos).
- **vazio:** sem histórico → tabela vazia "nenhum documento gerado ainda"; DRE sem movimento → zeros.

### Lei 2
**Não há Super Consultor nesta tela** — só geração/envio de documentos (ações explícitas do usuário).
**N/A.** (Cuidado apenas para não introduzir copy de IA-que-age ao ligar o envio.)

---

## Resumo executivo

| Tela | Endpoint já existe? | Blocos ligáveis HOJE | Esforço |
|---|---|---|---|
| **Entradas & Saídas** | **Parcial (novo)** — `extrato` (linha do tempo) + `relatorios/dre` (resultado do mês, competência) já servem 2 blocos; `fechamentoCaixa` via `/fluxo` existente ainda é achado a confirmar | 2–3 de ~9 (era 0–1) | **Médio** (era Alto) |
| **Recorrentes** | **Parcial (ampliado)** — `receita-recorrente` (resumo, já ligado) + `recorrentes/detalhe` (tabela nominal de assinaturas, novo) + `recorrentes/fixas` (template de contas fixas, novo, sem histórico) | resumo agregado (feito) + tabela de assinaturas (feito) + `?referencia` (1 linha) + fixas-template (feito, falta histórico) | **Médio** |
| **Bancário** | **✅ Sim — 100% ligado** (`contas-bancarias`, `formas-pagamento`, `movimentos`, `movimentos-semana`, `conciliacao` GET+POST, `taxas-por-forma`) | 7 de 7 | Concluído |
| **Fluxo de Caixa** | **✅ Sim — 100% ligado** (`caixa/atual`, `caixa/historico`, `caixa/abrir`, `caixa/suprimento`, `caixa/sangria`, `caixa/fechar` — ver §4) | 4 de 4 (as 4 ações do operador: abrir, suprimento, sangria, fechar) | Concluído |
| **Relatórios** | **Parcial (ampliado)** — `receita-recorrente` serve o card MRR (falta ligar front); `relatorios/contas-em-aberto` (novo) e `relatorios/dre` competência (novo) já são read-model pronto | MRR (barato, falta front) + aberto (feito) + DRE competência (feito, falta regime caixa) | **Médio→Baixo** |

**Conclusão:** das 5 telas originais, **Bancário e Fluxo de Caixa fecharam o domínio** — o segundo via
o agregado `SessaoCaixa` (Domain + Application + Infrastructure em
`SistemaX.Modules.Financeiro.*`) e as 6 rotas `financeiro/caixa/*` (`useFluxoCaixa.ts`, ver §4). Numa
rodada anterior (read-models de Entradas & Saídas/Recorrentes/Relatórios), **5 endpoints novos**
fecharam a maior parte do backend que faltava para essas 3 telas: `extrato` (E&S), `relatorios/dre`
(E&S + Relatórios, regime competência), `relatorios/contas-em-aberto` (Relatórios),
`recorrentes/detalhe` (Recorrentes/Assinaturas) e `recorrentes/fixas` (Recorrentes/Contas fixas — só
template). `dotnet build`/`dotnet test` verdes (811 testes). **O que ainda falta nessas 3 telas** não
é mais "endpoint não existe" na maioria dos blocos, e sim: (a) agregações finas ainda sem read-model
(`kpis.aReceber*`/`aPagar*` de E&S, `servicos[]` rico de Assinaturas, histórico/`emAlerta` de Contas
fixas — este último documentado como fora de escopo no próprio `ContasFixasService`), (b) regime de
caixa do DRE (bridge note) e (c) **o front (`web/`) não foi tocado nessas 3 telas** — Entradas &
Saídas, Recorrentes e Relatórios continuam servidas por mock nos blocos sem read-model.

**Ordem sugerida de menor→maior esforço** (alinhada ao contrato §10, atualizada pós-read-models
E&S/Recorrentes/Relatórios e pós-wiring de Bancário/Fluxo de Caixa):
(1) regime `caixa` do DRE (destrava `bridge` de E&S + `dre.byRegime.caixa` de Relatórios);
(2) parametrizar `?referencia` em `receita-recorrente` (Recorrentes `mrrMesAnterior`); (3) histórico
de 12m/`emAlerta` de Contas fixas (cruzamento `Recorrencia`×`ContaAPagar`/`ContaAReceber` por
`SourceRef` — read-model novo, fora do escopo desta rodada); (4) `servicos[]` rico de Assinaturas
(LTV/retenção/sparklines); (5) casos de uso de escrita já prontos (contas a pagar/receber, baixa);
(6) pacote/extrato/histórico de Relatórios.

**Ver também:** `docs/wiring/financeiro-api-contract.md` (block→endpoint em profundidade, §3–§8) e
`docs/ui/financeiro-ui.md` (contrato de UI, Lei 1/Lei 2).
