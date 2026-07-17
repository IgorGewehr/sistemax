# Financeiro — Contrato de API (wiring UI ↔ .NET)

**Status:** Em andamento (2 de 6 telas parcialmente ligadas) · **Data:** 2026-07-17 · **Contexto:**
as 6 telas de `web/src/pages/financeiro/` estão construídas à risca dos mockups
(`docs/ui/financeiro-ui.md`). Duas delas — **Visão Geral** e **Recorrentes** — já consomem dado
real do .NET nos blocos onde o read-model existe (o resto de cada uma continua em
`mocks/financeiro/*.ts`, marcado com `MockBadge`); as outras 4 (Entradas & Saídas, Bancário,
Fluxo de Caixa, Relatórios) ainda rodam 100% sobre mock. O motor .NET (`src/Modules/Financeiro/`)
já tem Domain + Application (casos de uso, read-models) com persistência SQLite para a maioria dos
ports. Este documento é esse mapa — não um plano de sprint, um **contrato**: toda vez que alguém
(humano ou IA) for expor um endpoint novo do Financeiro ou trocar um bloco de mock por API, a
resposta "qual é o shape?" mora aqui.

## Pergunta que este documento responde

> "Para a tela X do Financeiro parar de usar mock e passar a usar dado real do .NET, o que
> precisa existir — de um lado e do outro — e o que já existe hoje?"

**Resposta curta:** dos ~35 blocos de dado que as 6 telas consomem, **10 endpoints de leitura
existem hoje** (3 do wiring original + 4 do motor quant "Sobrevivência" F1 + 3 fact tables F0),
e **6 blocos já estão ligados de verdade** — os 6 de Visão Geral (`disponivel`, `timeline`,
`runway`, `breakeven`, `inadimplencia`, `radarSimples`) e o resumo agregado de Recorrentes
(`assinaturasAtivas`/`mrr`/`arr`/`ticketMedio`/churn via `useReceitaRecorrente`). O resto — a
maioria — não tem endpoint, e uma fração relevante (Bancário inteiro, Fluxo de Caixa inteiro,
Pacote/Relatórios) não tem sequer o read-model ou a entidade de domínio por trás. Abaixo, tela por
tela, o que existe, o que falta, e um padrão mecânico de troca mock→API para não reinventar a
integração 6 vezes.

---

## 1. Estado atual (medido no código, não no plano)

**Backend — 10 endpoints HTTP no ar**, todos `GET`, todos em
`SistemaX.Modules.Financeiro.Application.Endpoints.FinanceiroEndpointsModule`:

| Rota | Read-model por trás | Registrado em DI? | Consumido por alguma tela? |
|---|---|---|---|
| `GET /api/financeiro/receita-recorrente` | `ReceitaRecorrenteService` | sim (`FinanceiroModule.cs`) | sim — Recorrentes (`useReceitaRecorrente`) |
| `GET /api/financeiro/disponivel-retirada` | `QuantoSobrouDeVerdadeService` | sim | sim — Visão Geral (`useVisaoGeral`) |
| `GET /api/financeiro/fluxo?diasHistorico&diasProjecao` | `FluxoDeCaixaService` | sim | sim — Visão Geral (`useVisaoGeral`) |
| `GET /api/financeiro/previsao-caixa?dias` | `PrevisaoDeCaixaService` (motor quant F1) | sim | sim — Visão Geral, bloco "Sobrevivência" |
| `GET /api/financeiro/ponto-equilibrio` | `PontoDeEquilibrioService` (F1) | sim | sim — Visão Geral, bloco "Sobrevivência" |
| `GET /api/financeiro/inadimplencia` | `InadimplenciaService` (F1) | sim | sim — Visão Geral, bloco "Sobrevivência" |
| `GET /api/financeiro/radar-simples?anexo` | `RadarDoSimplesService` (F1) | sim | sim — Visão Geral, bloco "Sobrevivência" |
| `GET /api/financeiro/fato-receita-diaria?de&ate` | `IFatoReceitaDiariaRepository` (F0) | sim | não — série bruta, sem tela ainda |
| `GET /api/financeiro/fato-caixa-diario?de&ate` | `IFatoCaixaDiarioRepository` (F0) | sim | não |
| `GET /api/financeiro/fato-custo-diario?de&ate` | `IFatoCustoDiarioRepository` (F0) | sim | não |
| `GET /api/financeiro/fato-margem-produto?produtoId&de&ate` | `IFatoMargemProdutoRepository` (F0) | sim | não |

Os 4 read-models do motor quant ("Sobrevivência" — `previsao-caixa`/`ponto-equilibrio`/
`inadimplencia`/`radar-simples`) alimentam um bloco **novo, sem mockup próprio**
(`SobrevivenciaSection`, ver §3) — não um dos 5 blocos originais do mockup de Visão Geral.

Além desses, **dois read-models já existem e já estão em DI mas não têm endpoint**:
`DreGerencialService` (DRE por competência) e `AlertaFinanceiroService` (alertas de vencimento +
caixa negativo). Expor os dois é o menor esforço possível — mesmo padrão de 10 linhas dos que
já existem, sem escrever lógica nova.

**Frontend — 2 de 6 telas com dado real, por bloco.** `web/src/lib/api/financeiro.ts` declara
`financeiroApi.disponivelParaRetirada()`, `.fluxo()`, `.receitaRecorrente()`,
`.previsaoCaixa()`, `.pontoEquilibrio()`, `.inadimplencia()`, `.radarSimples()` + as 4 `.fato*()`,
com DTOs tipados batendo com os 10 read-models acima.
`pages/financeiro/VisaoGeral.tsx` chama `useVisaoGeral()` (não mais o mock direto): 6 blocos
(`disponivel`, `timeline`, `runway`, `breakeven`, `inadimplencia`, `radarSimples`) vêm do
backend via um `Recurso<T>` por bloco (`{ dado, erro, carregando }` — ver
`components/financial/visao-geral/useVisaoGeral.ts`), cada um com skeleton de loading e
`EmptyState` de erro próprios, para um card quebrado não derrubar os outros; `lucroDoMes`,
`proximosVencimentos` e `consultor` continuam vindo de `visaoGeralMock`, marcados com
`MockBadge`. `pages/financeiro/Recorrentes.tsx` chama `useReceitaRecorrente()` para o resumo
agregado (`ResumoRealAssinaturas`, exibido em `AssinResumoReal` com `MockBadge` explicando o que
ainda é mock) e mantém `servicos[]`/`carteira`/lente "Contas fixas" em `RECORRENTES_MOCK`. As
outras 4 páginas (`EntradasSaidas.tsx`, `Bancario.tsx`, `FluxoCaixa.tsx`, `Relatorios.tsx`) ainda
importam o mock direto — zero bloco ligado. Compare com `Estoque`, onde `useEstoque.ts` já chama
`estoqueApi.listarProdutos()`/`listarSaldos()` de verdade — é o padrão-alvo (§9), embora a
implementação real de Visão Geral/Recorrentes tenha usado `Recurso<T>` por bloco em vez do
`financeiro-flags.ts` proposto ali (nota em §9.3).

**Cobertura por tela (grosseira, por nº de blocos de dado servidos vs necessários — não conta o
bloco novo "Sobrevivência", que não faz parte dos 5 blocos originais do mockup):**

| Tela | Blocos servidos hoje | Maior gap |
|---|---|---|
| Visão Geral | 2 de 5 do mockup original (`disponivel`, `timeline`) **+ 4 novos fora do mockup** (bloco "Sobrevivência": `runway`, `breakeven`, `inadimplencia`, `radarSimples`, todos reais e com loading/erro tratados) | `consultor` (insight composto) e `lucroDoMes` (precisa `GET /financeiro/dre`) ainda não têm read-model |
| Entradas & Saídas | 0 de 8 | nenhum endpoint de leitura nem escrita exposto |
| Recorrentes | 1 de 2 lentes, parcial — resumo agregado (`assinaturasAtivas`/`mrr`/`arr`/`ticketMedio`) já chamado por `useReceitaRecorrente` e exibido em `AssinResumoReal` (ver §5) | `servicos[]`/`carteira` continuam mock (raso demais); lente "Contas fixas" = 0% backend |
| Bancário | 0 de 7 | `ContaBancariaCaixa`/`FormaDePagamento` não têm repositório algum |
| Fluxo de Caixa | 0 de 4 | **não existe entidade de domínio** para sessão de caixa físico (ver §4.5) |
| Relatórios | 0 de 6, mas 2 são baratos (`mrr` reaproveita `receita-recorrente`; `aberto` é soma direta) | `pacote` (export ZIP/PDF) não tem infraestrutura nenhuma |

---

## 2. Convenções de contrato (valem para todo endpoint abaixo)

Estas regras já estão em vigor nos 3 endpoints existentes e nos módulos-irmãos (Vendas,
Estoque) — todo endpoint novo do Financeiro as segue por padrão, sem repetir a decisão:

1. **Rota:** `/api/financeiro/{recurso}` — mapeado em `FinanceiroEndpointsModule.MapearEndpoints`
   (um único `IModuleEndpoints` por módulo; zero `if` no Host sobre qual módulo é qual).
2. **`businessId` só da sessão (R1):** `http.ObterBusinessId()` — nunca query string, nunca body.
   Nenhum DTO de request carrega `businessId`.
3. **Dinheiro no wire é sempre `Money` = `{ centavos: number, moeda: string }`** — nunca float de
   reais. O front converte com `moneyToReais`/`reaisToCentavos` de `lib/api/client.ts`, os
   view-models internos das telas continuam em `Centavos` puro (`number`), como já é hoje.
4. **Erro é `{ codigo, mensagem }`, status 422** (`ErrorHttpExtensions.ParaRespostaHttp`) — regra
   de negócio violada, não payload malformado (isso é 400 do model binding, antes de chegar ao
   handler).
5. **Idempotência em toda escrita que cria recurso.** Os comandos já pedem
   `IdempotencyKey: string` (`LancarContaComando`, `BaixarParcelaComando`) mas **nenhum endpoint
   ainda lê isso de request HTTP** — é um gap real, não só documentação. Convenção proposta (seguindo o próprio comentário de `LancarContaComando`, que já antecipa "equivalente ao header
   X-Idempotency-Key do resto do sistema"): o endpoint lê `X-Idempotency-Key` do header; se
   ausente, 400 antes de chegar no caso de uso — nunca gera uma chave no servidor (isso quebraria
   a garantia de idempotência do lado do cliente que reenvia).
6. **GET não precisa idempotência** — são leituras puras, sem efeito colateral.
7. **DTOs de fio são records próprios, nunca o agregado de domínio serializado direto** — mesmo
   critério de `VendaDto`/`ProdutoDto`: a UI recebe só o que o view-model pede, o agregado não
   vaza invariantes internas (ex.: `Parcela` interna do agregado `ContaAPagar`).

---

## 3. Visão Geral

**View-model:** `components/financial/visao-geral/types.ts` (`VisaoGeralViewModel`) · **Mock:**
`mocks/financeiro/visao-geral.ts` · **Página:** `pages/financeiro/VisaoGeral.tsx` — **2 dos 5 blocos
originais + o bloco "Sobrevivência" (4 blocos novos, fora do mockup) já são reais**, os outros 3
continuam mock com `MockBadge` (ver `useVisaoGeral.ts`).

| Bloco do VM | Método/Rota | Request | Response | Status |
|---|---|---|---|---|
| `disponivel.livreDeVerdadeCentavos`, `.noBancoEGaveta.valorCentavos`, `.jaTemDono.valorCentavos` | `GET /financeiro/disponivel-retirada` | — | `{ saldoEmCaixa, jaTemDono, podeTirar }: Money` cada | ✅ **ligado** (`QuantoSobrouDeVerdadeService`, via `useVisaoGeral` → `HeroDisponivelCard`, com skeleton/erro próprios) |
| `timeline.valoresDiarios`, `.hojeIndex` (derivado) | `GET /financeiro/fluxo?diasHistorico=30&diasProjecao=30` | query opcional | `{ pontos: { data, entradas, saidas, saldoAcumulado, projetado }[], primeiroDiaNegativo }` | ✅ **ligado** (`FluxoDeCaixaService`, via `useVisaoGeral` → `CashTimelineSection`) — mapeia `pontos[].saldoAcumulado` → `valoresDiarios`; `hojeIndex` = índice do último ponto com `projetado:false` |
| `timeline.eventosPorDia` (tooltip "Aluguel", "Folha de pagamento…") | *nenhum hoje* | — | precisa descrição por dia, não só soma | ❌ **falta** — `PontoFluxoCaixa` não carrega descrição de origem; precisa join com `ContaAPagar.Descricao`/`ContaAReceber.Descricao` por vencimento no dia |
| `lucroDoMes.lucroCentavos` | `GET /financeiro/dre?inicio&fim` (proposto) | `inicio`, `fim` ISO | `DreResultado{ ReceitaBruta, CustoDireto, DespesaOperacional, ResultadoOperacional }` | ⚠️ **serviço existe (`DreGerencialService`), endpoint não** — expor é cópia do padrão dos outros 3 |
| `lucroDoMes.deltaPercentual/.deltaDirecao` | mesmo endpoint, 2 chamadas (mês atual + anterior) | — | — | ❌ **falta cálculo** — serviço não compara meses, o client (ou um novo método do serviço) precisa chamar 2x e diferenciar |
| `lucroDoMes.margemPorRealCentavos` | derivado client-side de `ResultadoOperacional / ReceitaBruta` | — | — | ✅ derivável do dado acima, sem endpoint novo |
| `lucroDoMes.aReceberCentavos` | *nenhum hoje* | — | soma de parcelas em aberto de `ContaAReceber` | ❌ **falta** — dado existe via `IContaAReceberRepository.ListarAbertasAteAsync`, mas sem read-model/endpoint que some |
| `proximosVencimentos[]` | *nenhum hoje* | — | lista unificada `ContaAPagar` + `ContaAReceber` em aberto, ordenada por vencimento | ❌ **falta read-model novo** — precisa juntar os dois ports (`ListarAbertasAteAsync`) por `Parcela.Vencimento`, incluir `tone` (pagar=crit/receber=pos) e `descricao` |
| `consultor.*` (aluguel + atrasado + narrativa "caixa cruza zero em N dias") | parcialmente coberto por `AlertaFinanceiroService.AvaliarAsync` (`CaixaProjetadoNegativo`) | — | `AlertaFinanceiro[]` | ⚠️ **parcial** — o alerta de caixa negativo dá a narrativa de dias-até-zero, mas **não** cobre "atrasado, N clientes" (só avalia `ContaAPagar`, nunca `ContaAReceber`) nem monta o card composto (aluguel + atrasado + cálculo passo-a-passo). Requer um read-model novo, não só expor o serviço existente |

**Nota:** `disponivel.jaTemDono.sublabel` no mock diz `"(15 dias + imposto)"`, mas
`QuantoSobrouDeVerdadeService.HorizonteDiasContasAPagar` é **30 dias fixo** e não subtrai imposto
(o próprio XML doc do serviço admite isso: "fórmula do MVP, REDUZIDA"). Quando este endpoint virar
a fonte real, o sublabel do mockup precisa ser revisto para não prometer algo que o dado não
confere (ou o horizonte vira parâmetro de query como em `/fluxo`).

### Bloco "Sobrevivência" — novo, sem mockup próprio, 100% real

Os 4 read-models do motor quant F1 não têm um bloco correspondente no mockup original de Visão
Geral — foi adicionado como uma seção nova (`SobrevivenciaSection`) porque as 4 perguntas
("quando fico sem caixa?", "quanto preciso vender pra empatar?", "quanto do meu a receber é
lixo?", "estou perto de mudar de faixa do Simples?") não tinham lugar nenhum na UI antes. Todos
os 4 usam o mesmo padrão `Recurso<T>` (loading/erro por card) do resto de `useVisaoGeral`:

| Bloco (`sobrevivencia.*`) | Rota | Response | Status |
|---|---|---|---|
| `runway` (`RunwayCardData`) | `GET /financeiro/previsao-caixa?dias=30` | `{ bandas: {data,p5,p50,p95}[], probabilidadeSaldoNegativoEm30Dias, primeiroDiaP50Negativo, diasRunwayBruto, diasRunwayRealista }` | ✅ **ligado** (`PrevisaoDeCaixaService`) |
| `breakeven` (`BreakevenCardData`) | `GET /financeiro/ponto-equilibrio` | `{ custosFixosMensaisCentavos, margemContribuicaoPercentual, receitaNecessariaMensalCentavos, receitaNecessariaDiariaCentavos, receitaAcumuladaNoMesCentavos, diaDoEquilibrio, jaAtingiuNoMes }` | ✅ **ligado** (`PontoDeEquilibrioService`) |
| `inadimplencia` (`InadimplenciaCardData`) | `GET /financeiro/inadimplencia` | `{ valorTotalEmAbertoCentavos, provisaoEsperadaCentavos, valorLiquidoEsperadoCentavos, porFaixa[] }` | ✅ **ligado** (`InadimplenciaService`) |
| `radarSimples` (`RadarSimplesCardData`) | `GET /financeiro/radar-simples?anexo=I` | `{ rbt12Centavos, faixaAtual, aliquotaEfetiva, distanciaAoProximoDegrauCentavos, mesesProjetadosAteOProximoDegrau }` | ✅ **ligado** (`RadarDoSimplesService`) — só Anexo I suportado hoje, outro valor de `anexo` devolve 422 |

Adapters: `lib/api/adapters/financeiro/sobrevivencia.ts` (`deRunwayDto`/`deBreakevenDto`/
`deInadimplenciaDto`/`deRadarSimplesDto`), com testes em `sobrevivencia.test.ts`.

---

## 4. Entradas & Saídas

**View-model:** `components/financial/entradas-saidas/types.ts` (`EntradasSaidasMock`) · **Mock:**
`mocks/financeiro/entradas-saidas.ts` · **Página:** `pages/financeiro/EntradasSaidas.tsx`.

| Bloco do VM | Método/Rota proposta | Request | Response | Status |
|---|---|---|---|---|
| `rows: LancamentoRow[]` | `GET /financeiro/lancamentos?inicio&fim&tipo&status` | filtros de período/tipo/status | lista unificada de `Parcela` (de `ContaAPagar`+`ContaAReceber`) com `descricao`, `categoria`, `tipo`, `status`, `valorCentavos`, `conta`/`origem` (se paga), `diasAtraso` (se atrasada) | ❌ **falta** — é o "ExtratoUnificado" que o próprio comentário do type já antecipa ("`MovimentoFinanceiro` + `Parcela` do domínio"); precisa read-model novo cruzando os 2 ports de conta + `IMovimentoFinanceiroRepository` |
| `kpis.aReceberAbertoCentavos/.aReceberAtrasadoCentavos/.aReceberParcelasAbertas` | `GET /financeiro/kpis-entradas-saidas` (proposto) | período | somas/contagens sobre `ContaAReceber.ListarAbertasAteAsync` | ❌ **falta** — dado-base existe no port, soma/agrupamento não |
| `kpis.aPagarAbertoCentavos/.aPagarMaiorLabel/.aPagarLancamentosAbertos` | mesmo endpoint | — | idem sobre `ContaAPagar` | ❌ **falta** |
| `kpis.resultadoMesCentavos/.resultadoDeltaPct` | reaproveita `GET /financeiro/dre` (§3) | — | `ResultadoOperacional` + delta vs mês anterior | ⚠️ depende do mesmo gap de "expor DRE + comparar meses" do §3 |
| `kpis.fechamentoCaixaCentavos` | *nenhum hoje* | — | — | ❌ **bloqueado por §4.5** — depende do conceito de sessão de caixa física, que não existe |
| `kpis.sparklineReceber` (paths SVG) | derivado client-side de uma série diária de "a receber em aberto" | — | precisa série histórica, não só o total atual | ❌ **falta read-model de série temporal** — nenhum serviço soma "em aberto" dia a dia no passado |
| `bridge: BridgeNoteData` (resultado × caixa × diferimento) | `GET /financeiro/dre?regime=caixa` (proposto, ver §6 Relatórios) | — | mesmo shape do bridge de Relatórios — **deveria ser o mesmo endpoint reusado**, não duplicado | ❌ **falta** (e é o mesmo gap do DRE por regime de caixa, §6) |
| `consultorFornecedores` (delta vs média 6m, total mês, qtd pagamentos) | `GET /financeiro/consultor-fornecedores` (proposto) | — | agregação mensal de `ContaAPagar` da categoria `fornecedores`/CMV, 6 meses | ❌ **falta read-model** |
| `categorias: CategoriaDespesaResumo[]` (6 meses de histórico por categoria) | `GET /financeiro/categorias-resumo?meses=6` (proposto) | — | totais mensais agrupados por `CategoriaId` | ❌ **falta read-model** — **e há um mismatch de vocabulário a resolver antes**: o enum `CategoriaId` da UI (`folha\|fornecedores\|aluguel\|impostos\|software\|marketing\|servicos`) não bate com `CategoriaFinanceiraPadrao` do .NET (`servicos\|comissoes\|cmv-fornecedor\|delivery\|despesa-com-pessoal\|estorno-venda`) — são dois catálogos diferentes hoje. `Categoria`/`ICategoriaRepository` (a entidade "de verdade", por tenant) existe no Domain mas **não tem port nem repositório** — é decisão em aberto, não só endpoint faltando (ver §7) |
| `contasDisponiveis: ContaDisponivel[]` (select do modal de lançamento) | `GET /financeiro/contas-caixa` (proposto) | — | lista de `ContaBancariaCaixa` | ❌ **falta port inteiro** — não existe `IContaBancariaCaixaRepository` no Application (mesmo gap do Bancário, §5) |
| `categoriasLancamentoRapido` | reaproveita catálogo de categorias acima | — | — | ❌ mesmo gap de categoria |
| `proximoIdSequencial` | — | — | — | **N/A na API real** — artefato só do mock (sequência local `r22, r23...`); some quando a tela chama `POST` e recebe o `id` real gerado pelo servidor (`IdGenerator.NovoId()`) |
| Ação "Lançamento rápido" (`ModalLancamento`) | `POST /financeiro/contas-a-pagar` **e** `POST /financeiro/contas-a-receber` (proposto, uma rota por tipo) | `{ descricao, categoriaId, dataCompetencia, valorTotalCentavos, parcelas[], centroDeCustoId?, contraparteId? }` + header `X-Idempotency-Key` | `ContaDto` (a criada, ou a existente se a chave já foi usada) | ⚠️ **caso de uso pronto (`LancarContaAPagarUseCase`/`LancarContaAReceberUseCase`), endpoint não existe** |
| Ação "Dar baixa" (`ModalDarBaixa`) | `POST /financeiro/contas/{contaId}/parcelas/{parcelaId}/baixar` (proposto) | `{ valorPagoCentavos, dataPagamento, contaBancariaCaixaId, formaPagamentoId }` + header idempotência | `MovimentoFinanceiroDto` | ⚠️ **caso de uso pronto (`BaixarParcelaUseCase`), endpoint não existe**; e o request pede `contaBancariaCaixaId`/`formaPagamentoId` que a UI hoje não tem de onde escolher (mesmo gap de `ContaBancariaCaixa`/`FormaDePagamento` sem repositório) |
| `resumoPdvMes` | evento de integração já cobre a criação da `ContaAReceber` (`VendaConcluidaHandler`) — leitura seria uma contagem/soma por `SourceRef` prefixo `sale:` | — | — | ❌ **falta read-model** (dado nasce certo, falta a agregação de leitura) |

---

## 5. Recorrentes

**View-model:** `components/financial/recorrentes/types.ts` (`RecorrentesViewModel`, duas lentes:
`fixas` e `assinaturas`) · **Mock:** `mocks/financeiro/recorrentes.ts` · **Página:**
`pages/financeiro/Recorrentes.tsx`.

### Lente "Assinaturas" — a única com algum backend hoje

| Bloco do VM | Rota | Request | Response | Status |
|---|---|---|---|---|
| `assinaturasAtivasCount`/`mrr`/`arr`/`ticketMedio`/churn do mês (agregados, não por-serviço) | `GET /financeiro/receita-recorrente` | — | `assinaturasAtivas`/`mrr`/`arr`/`ticketMedio`/`mrrChurnNoMes`/`clientesChurnNoMes`/`churnPercent` | ✅ **ligado** — `useReceitaRecorrente` (`components/financial/recorrentes/useReceitaRecorrente.ts`) chama de verdade via `deReceitaRecorrenteDto` (`lib/api/adapters/financeiro/recorrentes.ts`); exibido em `AssinResumoReal` (bloco próprio, sem `MockBadge`) — **não** escrito de volta em `assinaturas.assinaturasAtivasCount`/`data.servicos` porque `PainelAssinaturas` deriva `mrrTotal`/`ticketMedio` a partir do array mock (misturar quebraria a conta) |
| `assinaturas.concentracaoServicoId` | mesmo endpoint (`maiorConcentracao.servicoId`) | — | — | ⚠️ **não ligado de propósito** — os ids do `DemoSeeder` (`servicepro`/`gestao-raiz`/`brain`) não batem com os ids do mock (`RECORRENTES_MOCK.assinaturas.servicos`, ex. `tensorroot`); usar o id real quebraria o `servicos.find(s => s.id === ...)` do drill "Ver concentração". `AssinResumoReal` não expõe esse campo até o mock ser realinhado ou o breakdown por serviço virar real (linha abaixo) |
| `assinaturas.mrrMesAnterior` | mesmo endpoint, chamado com `referencia` do mês anterior (o serviço já aceita `DateTimeOffset referencia`) | `referencia` como query | — | ⚠️ **endpoint não expõe `referencia` como parâmetro** — `ReceitaRecorrenteService.CalcularAsync` já aceita, só falta o `MapGet` ler da query em vez de usar sempre `DateTimeOffset.UtcNow` |
| `assinaturas.sparklineMrr6m` | *nenhum hoje* | — | série de 6 chamadas ao serviço acima (uma por mês) ou um novo método que devolve a série direto | ❌ **falta** — funciona por composição do endpoint acima, mas ninguém fez o loop ainda; melhor um novo método `CalcularSerieAsync(businessId, meses)` no serviço para não fazer 6 round-trips |
| `assinaturas.servicos[]` (`AssinaturaServico` completo: clientes, churn, tempoMedioMeses, ltv, retencaoPct, novos6m, churn6m) | mesmo endpoint devolve só `PorServico: { servicoId, servicoNome, mrr, percentual }` | — | **muito mais raso que o VM pede** | ❌ **falta read-model mais rico** — `clientes`/`churnClientesMes`/`tempoMedioMeses`/`ltv`/`retencaoPct`/séries de 6 meses não são calculados em lugar nenhum hoje |
| `assinaturas.carteira` (tempoMedioMeses/ltv/retencaoPct agregados) | *nenhum hoje* | — | — | ❌ **falta** — LTV e retenção não existem em nenhum serviço |
| `assinaturas.churnClienteNomes/.novoClienteNomes/.concentracaoClienteNome` | *nenhum hoje* | — | precisa nome do cliente por trás do churn/novo/concentração do período | ❌ **falta join** — `Assinatura.ClienteNome` existe no domínio, mas o read-model atual não devolve qual assinatura específica gerou o churn/entrada do mês |
| Ações Pausar/Reativar/Cancelar assinatura | `POST /financeiro/assinaturas/{id}/pausar`, `/reativar`, `/cancelar` (propostos) | `{ motivo }` no cancelar | `AssinaturaDto` | ⚠️ **casos de uso prontos (`PausarReativarAssinaturaUseCase`, `CancelarAssinaturaUseCase`), zero endpoint** |
| Ação "Nova assinatura" | `POST /financeiro/assinaturas` (proposto) | `{ clienteId, clienteNome, servicoId, servicoNome, valorPorCicloCentavos, ciclo, diaCobranca, dataInicio }` | `AssinaturaDto` | ⚠️ **caso de uso pronto (`CriarAssinaturaUseCase`), zero endpoint** |

### Lente "Contas fixas" — 0% backend

| Bloco do VM | Status |
|---|---|
| `fixas.itens: ContaFixa[]` (12 meses de histórico por conta) | ❌ **falta endpoint de leitura** — `IRecorrenciaRepository.ListarAtivasAsync` existe mas não é exposto, e mesmo expondo só devolve o **template** (`Recorrencia`: descrição, valor previsto, dia fixo, frequência), não o histórico mensal realizado |
| `ContaFixaDerivada` (`atual`, `mesPassado`, `media6m`, `variacaoPct`, `emAlerta`, `totalAnoCorrente`) | ❌ **falta read-model que cruza 2 fontes**: o template (`IRecorrenciaRepository`) + as contas geradas por ele (`IContaAPagarRepository`/`IContaAReceberRepository`, filtrando por `SourceRef` com prefixo `recorrencia:{id}:`), agrupadas por mês. Nenhum dos dois ports tem um método "buscar por prefixo de origem" hoje — precisa ou um novo método de porta ou filtrar client-side depois de listar tudo do período |
| Ação "criar recorrência" (nova conta fixa) | ❌ **nem o caso de uso existe** — `GerarContasRecorrentesUseCase` só *materializa* ocorrências de templates já cadastrados; não há `CriarRecorrenciaUseCase` para cadastrar um novo template a partir da UI. `Recorrencia.Criar(...)` existe no Domain, só falta a casca de Application (comando + caso de uso) igual ao padrão de `CriarAssinaturaUseCase` |

---

## 6. Bancário

**View-model:** `components/financial/bancario/types.ts` (`BancarioViewModel`) · **Mock:**
`mocks/financeiro/bancario.ts` · **Página:** `pages/financeiro/Bancario.tsx`.

**Este é o maior gap de infraestrutura das 6 telas** — não é "falta expor", é "falta o
repositório". As entidades de domínio existem (`ContaBancariaCaixa`, `FormaDePagamento`,
`ExtratoBancarioItem`, `Conciliacao`, todas em `Domain/Caixa/`), mas **só `Conciliacao` e
`ExtratoBancarioItem` têm port + adapter registrados** (`IConciliacaoRepository`,
`IExtratoBancarioItemRepository`, ambos in-memory hoje). `ContaBancariaCaixa` e
`FormaDePagamento` não têm `IContaBancariaCaixaRepository`/`IFormaDePagamentoRepository`
nenhum — hoje o sistema todo funciona com uma única conta hardcoded
(`ClassificadorFormaPagamento.ContaCaixaPadraoId = "conta-caixa-padrao"`), então **múltiplas
contas bancárias (Itaú/Nubank/Stone do mock) não existem no backend, nem como conceito
persistido.**

| Bloco do VM | Status |
|---|---|
| `contas: ContaBancaria[]` (lista + saldo por conta) | ❌ **falta port `IContaBancariaCaixaRepository` (list) + endpoint** — saldo por conta é derivável de `IMovimentoFinanceiroRepository.CalcularSaldoAsync(businessId, contaId, ...)`, que já aceita `contaBancariaCaixaId` como filtro, então a metade "saldo" está pronta assim que a lista de contas existir |
| `semanas: SemanaMovimento[]` (entrou/saiu por dia, agrupado em semanas) | ❌ **falta read-model** — dado-base (`IMovimentoFinanceiroRepository.ListarPorPeriodoAsync`) existe, agrupamento semanal não |
| `movimentos: MovimentoExtrato[]` (extrato com forma+conta+status conciliação) | ❌ **falta endpoint + join** — `ListarPorPeriodoAsync` existe como port, mas não exposto via HTTP; precisa juntar com nome de `FormaDePagamento` (sem repositório) e `ContaBancariaCaixa` (sem repositório) |
| `conciliacaoInicial.sobrouNoBanco[]` (itens do extrato bancário sem par no sistema) | ⚠️ **ports existem (`IExtratoBancarioItemRepository.ListarNaoConciliadosAsync`), endpoint não** — mas falta a "sugestão" heurística de match, que não existe em lugar nenhum (é IA/heurística, não implementada) |
| `conciliacaoInicial.sobrouNoSistema[]` (movimentos internos sem par no extrato) | ❌ **falta método de port** — não existe um "listar `MovimentoFinanceiro` sem conciliação" simétrico ao de `ExtratoBancarioItem` |
| `conciliacaoInicial.bateuCertinhoTotal/.bateuCertinhoAmostra` | ❌ **falta read-model** — contaria `Conciliacao` com `Status` confirmado, mas não há endpoint de listagem |
| Ação confirmar/ignorar item de conciliação | `POST /financeiro/conciliacao` / `POST /financeiro/conciliacao/ignorar` (propostos) | ⚠️ **caso de uso pronto (`ConciliarMovimentoUseCase`), zero endpoint** |
| `consultor: ConsultorBancarioInsight` (taxa por forma de pagamento) | ❌ **falta tudo** — `FormaDePagamento.TaxaPercentual`/`CalcularTaxa` existem no Domain, mas sem repositório para listar formas cadastradas nem read-model que cruza volume×taxa por forma |
| `kpiSaldoDelta`/`kpiEntrouDelta`/`kpiSaiuDelta` (comparação vs mês anterior) | ❌ **falta** — mesmo padrão de "comparar 2 períodos" que aparece em Visão Geral/Recorrentes, nenhum lugar faz isso ainda de forma reutilizável |

**Decisão bloqueante antes de wirar esta tela:** criar `IContaBancariaCaixaRepository` e
`IFormaDePagamentoRepository` (list + create, seguindo o padrão dos outros ports), os adapters
in-memory/SQLite correspondentes, e populá-los (mesmo que via seed) — sem isso, nenhum dos 7
blocos acima tem de onde vir. Isso também destrava o `ClassificadorFormaPagamento` sair do
hardcode de conta única (Fase 2 documentada no próprio código).

---

## 7. Fluxo de Caixa

**View-model:** `components/financial/fluxo-caixa/types.ts` (`FluxoCaixaData`) · **Mock:**
`mocks/financeiro/fluxo-caixa.ts` · **Página:** `pages/financeiro/FluxoCaixa.tsx`.

**Atenção — colisão de nome, não de conceito.** O `FluxoDeCaixaService`/`GET /financeiro/fluxo`
do .NET é **projeção de saldo diário** (regime caixa realizado + competência projetada) — é o
dado que alimenta a *timeline* da Visão Geral. A tela **"Fluxo de Caixa" da UI é outra coisa**:
o ritual físico do caixa da loja — abrir com um fundo de troco, vender em espécie ao longo do
dia, registrar sangrias, fechar com contagem cega, apurar diferença. É conceitualmente uma
**sessão de caixa (till session)** de PDV, não uma projeção financeira. Isso não é um erro de
nomenclatura para corrigir — são **dois domínios diferentes que por acaso teriam nomes
parecidos em português**; o documento evita a confusão daqui pra frente chamando o de .NET de
"fluxo de caixa (projeção)" e o da UI de "sessão de caixa" nas seções seguintes.

**Não existe nenhuma entidade de domínio para sessão de caixa** — nem em
`Domain/Caixa/` (que hoje só tem `MovimentoFinanceiro`, `ContaBancariaCaixa`, `FormaDePagamento`,
`Conciliacao`, `ExtratoBancarioItem`) nem em Vendas/PDV. Todos os blocos do VM dependem dela:

| Bloco do VM | O que precisaria existir |
|---|---|
| `sessoesFechadas[]` / `sessaoHojeInicial` (abertura, vendas em espécie, sangrias[], troco, fechamento, contagem, status aberto/fechado) | Um agregado novo — `SessaoCaixa` (ou nome equivalente) — com estados `Aberta`/`Fechada`, eventos (`SessaoCaixaAberta`, `SangriaRegistrada`, `SessaoCaixaFechada`) e port `ISessaoCaixaRepository` |
| `consultorInsight` (padrão "quintas à tarde", operador crítico) | Read-model novo sobre o histórico de sessões — nem esse cálculo existe hoje |
| `vendasEspeciePercentual` | Derivável depois que sessões existirem (espécie / total vendido no período, cruzando com Vendas) |
| `destinosSangria[]` (opções do select) | Provavelmente reaproveita `ContaBancariaCaixa` (mesmo gap do Bancário, §6) — sangria "vai para" uma conta |

**Decisão em aberto antes de qualquer endpoint aqui:** que módulo é dono de `SessaoCaixa` —
Financeiro (por ser fato de caixa) ou Vendas/PDV (por ser operação de loja, ligada a
`operador`/turno)? A resposta afeta onde o agregado e os casos de uso (`AbrirSessaoUseCase`,
`RegistrarSangriaUseCase`, `FecharSessaoUseCase` com contagem cega) são declarados. Recomenda-se
tratar como uma mini-spec própria (Domain + Application + 3 endpoints) antes de tocar nesta tela
— não é wiring mecânico, é desenho de domínio novo.

---

## 8. Relatórios

**View-model:** `components/financial/relatorios/types.ts` (`ReportsViewModel`) · **Mock:**
`mocks/financeiro/relatorios.ts` · **Página:** `pages/financeiro/Relatorios.tsx`.

| Bloco do VM | Rota proposta | Status |
|---|---|---|
| `dre.byRegime.competencia` | `GET /financeiro/dre?inicio&fim` (mesmo endpoint de §3/§4) | ⚠️ **serviço pronto (`DreGerencialService`), endpoint não exposto** — `topLine`=`ReceitaBruta`; mas o agrupamento do mockup ("(–) Impostos" / "(–) Despesas e custos") **não bate** com o agrupamento do serviço (`CustoDireto` = CMV+Comissões / `DespesaOperacional` = resto) — decisão de taxonomia em aberto, ver §9 |
| `dre.byRegime.caixa` | `GET /financeiro/dre?inicio&fim&regime=caixa` (proposto) | ❌ **falta serviço inteiro** — `DreGerencialService` é só por competência (o próprio XML doc do arquivo confirma); regime de caixa recalcularia as mesmas linhas a partir de `MovimentoFinanceiro` realizado, não `ContaAPagar`/`ContaAReceber` |
| `dre.byRegime.*.bridgeNote` | mesmo endpoint devolveria os 3 números (`resultadoCentavos`, `caixaCentavos`, `diferimentoCentavos`) | ❌ **compartilhado com o gap do `bridge` de Entradas & Saídas (§4)** — resolver uma vez, reusar nas duas telas |
| `pacote` (ZIP com DRE + extratos + aberto + fechamentos + conciliação) | `POST /financeiro/relatorios/pacote` (proposto) | ❌ **falta infraestrutura inteira** — nenhuma geração de PDF/Excel/ZIP existe no Application hoje; é composição de todos os outros endpoints + um gerador de arquivo, candidato a Fase 2/3 |
| `extrato` (extrato por conta, PDF/Excel) | `GET /financeiro/relatorios/extrato?contaId&inicio&fim&formato` (proposto) | ⚠️ **dado-base existe (`IMovimentoFinanceiroRepository.ListarPorPeriodoAsync`), geração de arquivo não** — e depende do mesmo gap de `ContaBancariaCaixa` (§6) pro seletor de conta |
| `aberto` (contas em aberto + aging buckets 0-15/15-30/+30d) | `GET /financeiro/contas-em-aberto` (proposto) | ✅ **candidato mais barato de todos** — todo o dado-base já existe via `IContaAReceberRepository`/`IContaAPagarRepository.ListarAbertasAteAsync`; só falta somar e colocar em baldes por dias de atraso, sem entidade nova nem serviço novo além de um read-model simples |
| `mrr` | `GET /financeiro/receita-recorrente` (mesmo de §5) | ✅ **quase de graça** — `mrr`=`Mrr`, `churnMes`=`MrrChurnNoMes`, `arrEstimado`=`Arr`; `condicaoLabel` é copy estático (não é dado de backend) |
| `contact` (e-mail/whatsapp do contador) | *fora do Financeiro* | ❌ **não é responsabilidade deste módulo** — é dado de perfil da empresa/configurações; precisa endpoint do módulo de Settings/Business, não do Financeiro |
| `initialHistory[]` (log de documentos gerados) + `SendMenu` (enviar por e-mail/whatsapp) | `GET /financeiro/relatorios/historico` + `POST /financeiro/relatorios/historico/{id}/enviar` (propostos) | ❌ **falta entidade + persistência + integração de envio** — nada disso existe hoje; depende de `pacote`/geração de arquivo primeiro |

---

## 9. Padrão mecânico de troca mock → API (adapter + flag)

O objetivo aqui não é "trocar tudo de uma vez quando o backend estiver 100%" — dado o tamanho dos
gaps acima (§4–§8), isso levaria meses sem nenhuma tela mostrando dado real no meio do caminho. O
padrão abaixo permite **fechar um bloco de dado por vez**, na ordem que for ficando pronta no
.NET, sem quebrar o resto da tela nem o `typecheck`.

### 9.1 As 4 camadas (a 1ª e a 3ª já existem no repo; a 2ª e a 4ª são a proposta nova)

```
lib/api/financeiro.ts                    (1) DTOs + financeiroApi.xxx() — já existe, estender
lib/api/adapters/financeiro/<tela>.ts    (2) NOVO — dto → fatia do view-model, função pura
lib/api/financeiro-flags.ts              (3) NOVO — o que já é "ao vivo" por bloco, não por tela
components/financial/<tela>/use<Tela>.ts (4) hook — mescla mock + live conforme a flag
```

**(1) `lib/api/financeiro.ts`** — mesmo padrão já usado em `estoque.ts`/`vendas.ts`: um DTO por
endpoint, `financeiroApi.nomeDoEndpoint()`. Já existem 3; adicionar um por endpoint novo conforme
as tabelas acima forem sendo implementadas no .NET — sem mudar o padrão.

**(2) `lib/api/adapters/financeiro/<tela>.ts`** — função pura `de<Bloco>Dto(dto) => FatiaDoViewModel`,
zero React, testável isolado. Isola o shape do DTO (`camelCase`, `Money` como
`{centavos,moeda}`) do shape do view-model (`Centavos` puro, campos derivados como `tone`,
labels compostos). Quando o .NET mudar um DTO, só o adapter muda — o view-model e os componentes
da tela, não.

**(3) `lib/api/financeiro-flags.ts`** — granularidade **por bloco do view-model**, não por tela
inteira (uma tela como Visão Geral tem blocos prontos e blocos que ainda dependem de read-model
novo — ver §3):

```ts
// lib/api/financeiro-flags.ts
// Liga um bloco assim que o endpoint .NET correspondente existir E o adapter estiver escrito.
// Cada chave aqui tem uma linha correspondente numa tabela de docs/wiring/financeiro-api-contract.md.
export const FINANCEIRO_LIVE = {
  visaoGeral: {
    disponivel: true,        // GET /financeiro/disponivel-retirada — existe
    timeline: true,          // GET /financeiro/fluxo — existe (eventosPorDia ainda mock, ver adapter)
    lucroDoMes: false,       // depende de GET /financeiro/dre (não exposto ainda)
    proximosVencimentos: false,
    consultor: false,
  },
  entradasSaidas: { kpis: false, rows: false, bridge: false, categorias: false, contasDisponiveis: false },
  recorrentes: {
    assinaturasResumo: true, // GET /financeiro/receita-recorrente — existe, mas raso (ver §5)
    fixas: false,
  },
  bancario: { contas: false, semanas: false, movimentos: false, conciliacao: false, consultor: false },
  fluxoCaixa: { sessoes: false }, // bloqueado por design de domínio, ver §7
  relatorios: { dre: false, aberto: false, mrr: true, pacote: false, extrato: false, historico: false },
} as const;
```

**(4) `use<Tela>.ts`** — mesmo esqueleto de `useEstoque.ts` (fetch em `useEffect`, `carregando`/
`erroCarregamento`, sem TanStack Query — `web/` não tem essa dependência, ver `lib/api/client.ts`),
mas parte do **mock como base** e sobrescreve só os blocos com flag ligada:

```ts
// components/financial/visao-geral/useVisaoGeral.ts (novo — hoje a página usa o mock direto)
import { useEffect, useState } from 'react';
import { financeiroApi } from '@/lib/api/financeiro';
import { ApiError } from '@/lib/api/client';
import { FINANCEIRO_LIVE } from '@/lib/api/financeiro-flags';
import { visaoGeralMock } from '@/mocks/financeiro/visao-geral';
import { deDisponivelDto, deTimelineDto } from '@/lib/api/adapters/financeiro/visao-geral';
import type { VisaoGeralViewModel } from './types';

export function useVisaoGeral() {
  const [vm, setVm] = useState<VisaoGeralViewModel>(visaoGeralMock);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    async function carregarBlocosAoVivo() {
      try {
        if (FINANCEIRO_LIVE.visaoGeral.disponivel) {
          const dto = await financeiroApi.disponivelParaRetirada();
          setVm((atual) => ({ ...atual, disponivel: deDisponivelDto(dto, atual.disponivel) }));
        }
        if (FINANCEIRO_LIVE.visaoGeral.timeline) {
          const dto = await financeiroApi.fluxo(30, 30);
          setVm((atual) => ({ ...atual, timeline: deTimelineDto(dto, atual.timeline) }));
        }
        // lucroDoMes/proximosVencimentos/consultor ficam no mock até a flag correspondente ligar —
        // nenhum código aqui precisa saber disso, é a MESMA função independente da flag.
      } catch (e) {
        setErro(e instanceof ApiError ? e.message : 'Não foi possível carregar a Visão Geral.');
      }
    }
    void carregarBlocosAoVivo();
  }, []);

  return { vm, erro };
}
```

Note que o adapter recebe `(dto, valorMockAtual)` — para blocos parcialmente cobertos (ex.:
`timeline.eventosPorDia`, que o endpoint não devolve, ver §3) o adapter preenche o que o DTO tem
e **mantém o resto do mock como placeholder**, em vez de a tela quebrar ou mostrar `undefined`.
Isso é temporário por natureza: quando o read-model do bloco faltante existir, o adapter para de
precisar do segundo argumento.

`pages/financeiro/VisaoGeral.tsx` troca `const vm = visaoGeralMock` por
`const { vm, erro } = useVisaoGeral()` — uma linha, e é a única mudança na página (mesmo espírito
de "página fina" do contrato de UI, §4 de `docs/ui/financeiro-ui.md`).

### 9.2 Por que flag por bloco, não por tela ou por variável de ambiente global

Os módulos já migrados (Estoque) trocaram mock→API **de uma vez, quando o backend inteiro daquele
recurso ficou pronto** — não precisaram de flag porque não havia meio-termo. Financeiro é
diferente: o backend está sendo construído endpoint a endpoint, em paralelo a este documento, e
as 6 telas já existem inteiras. Uma flag por tela obrigaria esperar os ~6 blocos mais lentos de
cada tela (ex.: Visão Geral esperaria o `consultor` composto, o gap mais difícil) antes de mostrar
qualquer dado real. Por bloco, cada linha das tabelas de §3–§8 vira uma flag independente que liga
no dia em que o endpoint correspondente for exposto — sem esperar as outras, sem cross-talk entre
telas.

Uma variável de ambiente global (`VITE_FINANCEIRO_MOCK=1`) pode **coexistir** por cima disso, só
para forçar 100% mock em demo/offline independente do estado real das flags — mas não substitui
o controle por bloco.

### 9.3 O que foi implementado de fato em Visão Geral/Recorrentes (diverge do §9.1 proposto)

As duas telas já ligadas **não** usaram `lib/api/financeiro-flags.ts` — esse arquivo não existe
no repo. Em vez de uma flag booleana por bloco lida por um hook que faz merge com o mock, o hook
de cada tela (`useVisaoGeral.ts`, `useReceitaRecorrente.ts`) declara um `Recurso<T>` (`{ dado,
erro, carregando }`) só para os blocos que **já têm read-model** e devolve o mock (com
`MockBadge` na página) para os que não têm — a "flag" vira, na prática, a própria presença do
campo no retorno do hook, não uma tabela de booleans separada. Efeito prático igual ao proposto
(liga bloco a bloco, sem esperar a tela inteira, um card quebrado não derruba os outros), mas sem
o arquivo `financeiro-flags.ts` central — cada hook é a fonte da verdade do seu próprio conjunto
de blocos. As camadas (1) `lib/api/financeiro.ts` e (2)
`lib/api/adapters/financeiro/<tela>.ts` foram seguidas à risca. Ao wirar as próximas 4 telas,
seguir o padrão realmente em uso (`Recurso<T>` por bloco no hook) em vez de criar o arquivo de
flags — a menos que uma tela específica precise mesclar campo a campo dentro do mesmo objeto (aí
o argumento `(dto, valorMockAtual)` do adapter, como no exemplo de código acima, continua válido).

---

## 10. Ordem de rollout sugerida (do mais barato ao mais caro)

1. **Expor os 2 read-models que já existem e estão em DI, sem lógica nova:** `DreGerencialService`
   → `GET /financeiro/dre`; `AlertaFinanceiroService` → `GET /financeiro/alertas`. Destrava
   `lucroDoMes` (parcial) na Visão Geral e `dre.byRegime.competencia` em Relatórios.
2. **`GET /financeiro/contas-em-aberto`** (Relatórios `aberto`) — todo o dado-base já existe nos
   ports, é só somar e colocar em baldes por atraso.
3. **Parametrizar `referencia` em `/financeiro/receita-recorrente`** — o serviço já aceita,
   destrava `mrrMesAnterior` (Recorrentes) com uma linha de código.
4. **Expor os casos de uso de escrita já prontos** (`LancarContaAPagar/AReceberUseCase`,
   `BaixarParcelaUseCase`, `ConciliarMovimentoUseCase`, `CriarAssinaturaUseCase` +
   pausar/reativar/cancelar) — zero lógica nova, só a casca HTTP + leitura de
   `X-Idempotency-Key`.
5. **Criar `IContaBancariaCaixaRepository`/`IFormaDePagamentoRepository`** — destrava a metade do
   Bancário e o seletor de conta do modal de lançamento de Entradas & Saídas.
6. **Read-models de agregação temporal** (kpis de Entradas & Saídas, séries de 6 meses de
   Recorrentes, semanas do Bancário) — trabalho novo, mas sobre dado que já existe nos ports.
7. **Decisão de domínio: `SessaoCaixa`** (Fluxo de Caixa) — não é wiring, é modelagem nova; tratar
   como spec própria antes de estimar.
8. **Export/pacote de Relatórios e catálogo real de `Categoria`** — maior esforço, menor
   urgência (nenhuma outra tela depende deles para funcionar).

---

## 11. Decisões em aberto que bloqueiam wiring 100% (não são só "falta código")

- **Catálogo de categorias:** `CategoriaId` da UI vs `CategoriaFinanceiraPadrao` do .NET não
  batem; `Categoria`/`LinhaDre` existem no Domain mas sem port. Precisa decisão de produto (quais
  categorias o MVP realmente cadastra) antes de qualquer read-model de "categorias" ser
  desenhado — ver task pendente #6 (persistência) e o próprio comentário de
  `CategoriaFinanceiraPadrao` ("Fase 2: resolver `Categoria.LinhaDreId` de verdade").
- **Dono de `SessaoCaixa`:** Financeiro ou Vendas/PDV? Ver §7.
- **Multi-conta bancária:** hoje há uma única conta-caixa hardcoded
  (`ClassificadorFormaPagamento.ContaCaixaPadraoId`). Expor `contas[]` no Bancário sem resolver
  isso mostraria uma lista vazia ou fake — a UI precisa de pelo menos uma tela de cadastro de
  conta (ou seed) para o dado fazer sentido em produção, não só em dev.
- **Regime de caixa no DRE:** `DreGerencialService` é só competência; regime caixa é serviço
  novo. Cross-referenciado em Entradas & Saídas (`bridge`) e Relatórios (`dre.byRegime.caixa`) —
  resolver uma vez, dois consumidores.
- **Conciliação bancária completa** e **persistência SQLite de todos os repos** já são tasks
  pendentes conhecidas (#4 e #6 do roadmap) — este documento não as duplica, só aponta onde elas
  batem em cada tela.

---

## Apêndice — mapa rota → arquivo .NET (existentes hoje)

| Rota | Arquivo |
|---|---|
| `GET /api/financeiro/receita-recorrente` | `Application/Endpoints/FinanceiroEndpointsModule.cs` → `Application/ReadModels/ReceitaRecorrenteService.cs` |
| `GET /api/financeiro/disponivel-retirada` | idem → `Application/ReadModels/QuantoSobrouDeVerdadeService.cs` |
| `GET /api/financeiro/fluxo` | idem → `Application/ReadModels/FluxoDeCaixaService.cs` |
| `GET /api/financeiro/previsao-caixa` | idem → `Application/Quant/PrevisaoDeCaixaService.cs` |
| `GET /api/financeiro/ponto-equilibrio` | idem → `Application/Quant/PontoDeEquilibrioService.cs` |
| `GET /api/financeiro/inadimplencia` | idem → `Application/Quant/InadimplenciaService.cs` |
| `GET /api/financeiro/radar-simples` | idem → `Application/Quant/RadarDoSimplesService.cs` |
| `GET /api/financeiro/fato-receita-diaria` | idem → `Application/Ports/IFatoReceitaDiariaRepository.cs` |
| `GET /api/financeiro/fato-caixa-diario` | idem → `Application/Ports/IFatoCaixaDiarioRepository.cs` |
| `GET /api/financeiro/fato-custo-diario` | idem → `Application/Ports/IFatoCustoDiarioRepository.cs` |
| `GET /api/financeiro/fato-margem-produto` | idem → `Application/Ports/IFatoMargemProdutoRepository.cs` |
| *(não exposto)* `DreGerencialService` | `Application/ReadModels/DreGerencialService.cs` |
| *(não exposto)* `AlertaFinanceiroService` | `Application/ReadModels/AlertaFinanceiroService.cs` |
| Casos de uso de escrita prontos, sem endpoint | `Application/CasosDeUso/{LancarContaUseCase,BaixarParcelaUseCase,ConciliarMovimentoUseCase,EstornarMovimentoUseCase,AssinaturaUseCases,GeracaoRecorrenteUseCases}.cs` |
| Registro de DI de tudo acima | `Application/FinanceiroModule.cs` |
| Adapters concretos dos ports (in-memory/SQLite) | `Infrastructure/FinanceiroInfrastructureModule.cs` |
| Cliente HTTP tipado do front | `web/src/lib/api/financeiro.ts` (+ `client.ts` para `Money`/sessão/erros) |
| Adapters DTO → view-model (blocos já ligados) | `web/src/lib/api/adapters/financeiro/{visaoGeral,sobrevivencia,recorrentes}.ts` (+ `*.test.ts`) |
| Hooks de dado real por tela (blocos já ligados) | `web/src/components/financial/visao-geral/useVisaoGeral.ts`, `web/src/components/financial/recorrentes/useReceitaRecorrente.ts` |
