# Mapa de cálculos — frontend SistemaX

> Doc vivo. Reflete o estado do frontend em `web/src/` no momento em que foi escrito (varredura manual
> de `lib/money.ts`, `lib/format.ts` e todos os `calc.ts`/`derive.ts`/`helpers.ts` de
> `web/src/components/**`). Se um `calc.ts` mudar de forma relevante e este índice não acompanhar,
> ele mente — trate divergência entre este doc e o código-fonte como bug de documentação, não como
> "o código está errado".
>
> Público-alvo: alguém com formação em matemática/estatística que precisa auditar uma fórmula
> específica sem vasculhar a árvore de componentes React inteira. Cada entrada dá arquivo:função e o
> teste que prova a fórmula — comece pelo teste, ele tem os casos de borda documentados em português.

## Onde mora a matemática

O SistemaX separa cálculo de apresentação em toda tela nova (frontend) por uma convenção de nome de
arquivo:

- **`calc.ts` / `derive.ts` / `helpers.ts`** (um por módulo/tela, ex.: `components/compras/calc.ts`) —
  **funções puras**: recebem dados já carregados (mock hoje, API depois) e devolvem número/string/
  geometria. Zero `useState`, zero JSX, zero `fetch`. Testáveis isoladas, sem montar componente nem
  DOM — é por isso que dá pra revisar cada uma lendo só a função + o `.test.ts` ao lado. Convenção:
  `derive.ts` é usado quando o módulo já tinha `types.ts` com um nome parecido e `calc.ts` colidiria
  (só `bancario/` usa esse nome); no resto é sempre `calc.ts`. `helpers.ts` aparece uma vez
  (`financial/relatorios/`) por herança do nome que a tela já tinha.
- **`lib/money.ts`** — fonte única de **dinheiro em centavos inteiros** (`Centavos = number`). Regra
  dura do sistema: nunca representar dinheiro como float de reais (erro de arredondamento é
  inaceitável no módulo cujo coração é o financeiro). Conversão pra reais só acontece na formatação
  de exibição (`formatCentavos*`), nunca em cálculo intermediário.
- **`lib/format.ts`** — formatação compartilhada de moeda/data/percentual que não é `Centavos`
  (`Intl.NumberFormat`/`Intl.DateTimeFormat` en­capsulados, sempre com fallback `'—'`/`'-'` pra
  entrada nula/inválida — nunca deixa um `RangeError` de `new Date(undefined)` vazar pra tela).
- **Wrappers `money.ts` por módulo do Financeiro** (`financial/{bancario,entradas-saidas,
  visao-geral}/money.ts`) — não são cálculo novo: são **reexport puro** de
  `formatCentavosWhole`/`formatSignedCentavosWhole` de `@/lib/money`, existem só porque os
  componentes de cada tela importam desse caminho relativo. `financial/recorrentes/calc.ts` faz o
  mesmo reexport dentro do próprio `calc.ts` (não tem `money.ts` separado). Se você está caçando a
  fórmula por trás de um desses reexports, o código de verdade está em `lib/money.ts` — a prova é
  `lib/money.test.ts`.

### TS (hoje) vs C# (o "lar" autoritativo que está chegando)

Todo o cálculo indexado aqui é **frontend TypeScript sobre dados mock** — a implementação de
referência da UI, não a fonte de verdade quantitativa do produto. Por `docs/financeiro/
inteligencia-arquitetura.md` (§3.4), o motor quant autoritativo do Financeiro está migrando para
**folds determinísticos em C#** (`Centavos[] → resultado`, mesmo padrão `calc.ts`+`calc.test.ts` do
front espelhado em services C# puros no back, ledger de eventos como fonte, fact tables como saída).
Ou seja:

- **Hoje**: toda fórmula abaixo é o único lugar onde aquele número é calculado (a tela usa mock,
  então o `calc.ts` É a implementação de referência).
- **Depois de F1/F3 do roadmap C#** (ver inteligencia-arquitetura.md §5): para as ~22 análises do
  catálogo quant, o C# vira o lar autoritativo (reproduzível, seed fixa, testado no back); o
  `calc.ts` correspondente do front deixa de calcular e passa a só *exibir* o que a API devolve —
  nesse ponto, ele deveria encolher pra formatação/geometria de UI (sparkline, aging bar, layout de
  SVG) e a matemática de negócio (variação %, EWMA, projeção, score) sai daqui.
- **Não há ambiguidade permitida**: quando um cálculo migrar, este documento deve ser atualizado no
  mesmo PR pra apontar "ver `X.cs`" em vez de descrever a fórmula em TS — nunca as duas versões como
  se fossem igualmente autoritativas.

### Convenção de teste

Cada `calc.ts`/`derive.ts`/`helpers.ts` tem um `.test.ts` irmão no mesmo diretório (`calc.test.ts`,
`derive.test.ts`, `helpers.test.ts`) com `describe` por função exportada. A coluna **Prova** abaixo
aponta esse arquivo; quando uma função não tem `describe` dedicado, está marcada **(sem teste)** —
isso é uma lacuna real de cobertura, não um erro de leitura deste doc.

---

## Dinheiro

### `web/src/lib/money.ts` — Prova: `web/src/lib/money.test.ts`

| Função | O que calcula | Input → Output |
|---|---|---|
| `reais(valorEmReais)` | Açúcar de autoria: reais → centavos inteiros (arredonda) | `number` → `Centavos` |
| `formatCentavos(centavos)` | Centavos → `"R$ 1.234,56"` (2 casas); `null`/`undefined`/`NaN` → `'—'` | `Centavos \| null \| undefined` → `string` |
| `formatSignedCentavos(centavos)` | Igual, com sinal explícito `+`/`−` (menos unicode) | `Centavos \| null \| undefined` → `string` |
| `formatCentavosWhole(centavos)` | Centavos → reais inteiros sem decimais, `"R$ 3.300"` | `Centavos \| null \| undefined` → `string` |
| `formatSignedCentavosWhole(centavos)` | Igual, com sinal explícito | `Centavos \| null \| undefined` → `string` |

`Centavos` (`type Centavos = number`) é o tipo-base reusado por praticamente todo arquivo deste mapa.

### `web/src/lib/format.ts` — Prova: `web/src/lib/format.test.ts`

| Função | O que calcula | Input → Output |
|---|---|---|
| `formatCurrency(value)` | Número (reais) → BRL via `Intl.NumberFormat` | `number \| null \| undefined` → `string` |
| `formatCurrencyCompact(value)` | Igual, notação compacta (eixo Y de gráfico) | `number \| null \| undefined` → `string` |
| `formatSignedCurrency(value)` | BRL com sinal `+` explícito quando positivo | `number \| null \| undefined` → `string` |
| `formatPercent(value, digits=0)` | `value.toFixed(digits) + '%'` | `number, number` → `string` |
| `formatDate(input)` | Data → `"DD/MM/AAAA"`, tolera `Date\|string\|number\|null\|undefined` | → `string` (`'-'` se inválida) |
| `formatDateShort(input)` | Data → `"DD/MM"` | → `string` |
| `formatWeekday(input)` | Data → dia da semana abreviado (`"seg"`) | → `string` |
| `formatDateTime(input)` | Data → `"DD/MM HH:MM"` | → `string` |
| `formatRelativeDays(input)` | Data → `"hoje"`/`"amanhã"`/`"ontem"`/`"em N dias"`/`"há N dias"` | → `string` |
| `daysBetween(a, b)` | Dias corridos entre duas datas (arredondado) | `Date\|string, Date\|string` → `number` |

`safeDate` (privada, não exportada) é o guard-rail por trás de todas as `format*` de data: força
`T00:00:00` local em strings `"yyyy-mm-dd"` (evita o `new Date('yyyy-mm-dd')` parsear como UTC e
desalinhar 1 dia em fusos negativos — Brasil incluso) e devolve `null` pra qualquer entrada inválida.

---

## Financeiro

### Entradas & Saídas — `web/src/components/financial/entradas-saidas/calc.ts`

Prova: `web/src/components/financial/entradas-saidas/calc.test.ts` (cobertura completa das funções
com matemática; `categoriaCorCss` é só lookup de mapa CSS, sem `describe` dedicado).

| Função | O que calcula | Input → Output |
|---|---|---|
| `mediaHistoricoAnterior(historicoCentavos)` | Média dos meses anteriores ao corrente (último item do array = mês corrente, excluído da média) | `Centavos[]` → `Centavos` |
| `variacaoPct(categoria)` | Variação % do mês corrente vs a média histórica anterior | `CategoriaDespesaResumo` → `number` |
| `isAnomalia(categoria)` | `true` se total ≥ R$1.000 **e** variação > 15% (ambas, não uma OU outra) | `CategoriaDespesaResumo` → `boolean` |
| `totalDespesasCentavos(categorias)` | Soma `totalCentavos` de todas as categorias | `CategoriaDespesaResumo[]` → `Centavos` |
| `buildBarras(categorias)` | Largura relativa à maior categoria + % do total, por categoria (painel "Para onde foi o dinheiro") | `CategoriaDespesaResumo[]` → `CategoriaBarra[]` |
| `fixoVariavelPct(categorias)` | % do total que é gasto fixo vs variável (somam 100 sempre) | `CategoriaDespesaResumo[]` → `{ fixoPct, varPct }` |
| `quemMaisSubiu(categorias)` | Categoria (≥ R$1.000/mês) com maior alta vs a própria média | `CategoriaDespesaResumo[]` → `LiderAlta \| null` |
| `categoriaDrillStats(categoria, totalDespesas)` | Média histórica, % do total, variação %, flag de anomalia — card do drill de categoria | `CategoriaDespesaResumo, Centavos` → `CategoriaDrillStats` |
| `atrasados30MaisDias(rows)` | Total e contagem de recebíveis com `status='atrasado'` e `diasAtraso > 30` | `LancamentoRow[]` → `Atrasados30DiasResumo` |
| `buildTimeline(rows, segFiltro, filtro)` | Monta a linha do tempo com divisor "Hoje" e resumo do PDV em posição fixa (âncora por id, não índice) | `LancamentoRow[], SegFiltro, FiltroAtivo\|null` → `TimelineEntry[]` |
| `insertLancamentoOrdenado(rows, novo)` | Insere um lançamento novo mantendo ordem decrescente por data | `LancamentoRow[], LancamentoRow` → `LancamentoRow[]` |
| `buildColunasGeometry(historicoCentavos, mediaCentavos)` | Geometria SVG das colunas do drill de categoria (altura escalada por `max*1.18`) | `Centavos[], Centavos` → `{ bars, avgY }` |
| `categoriaCorCss(cor)` | Token de categoria → variável CSS (`hsl(var(--...)`) | `CorCategoria` → `string` — **(sem teste)** |

### Fluxo de Caixa — `web/src/components/financial/fluxo-caixa/calc.ts`

Prova: `web/src/components/financial/fluxo-caixa/calc.test.ts` (cobertura completa, exceto `diaLabel`
e `horaAgora` — trivial/dependente do relógio real, sem `describe`).

| Função | O que calcula | Input → Output |
|---|---|---|
| `totalSangriasCentavos(sessao)` | Soma o valor de todas as sangrias da sessão de caixa | `SessaoCaixa` → `Centavos` |
| `esperadoCentavos(sessao)` | `abertura + vendas em espécie − sangrias − troco` — sempre recalculado dos primitivos, nunca armazenado | `SessaoCaixa` → `Centavos` |
| `diferencaCentavos(sessao)` | `contado − esperado`; `null` enquanto a sessão está `aberto` | `SessaoCaixa` → `Centavos \| null` |
| `duracaoTurno(horaAbertura, horaFechamento)` | Duração do turno em `"Xh Ymin"`, vira o dia se fechamento < abertura | `string, string` → `string` |
| `sessoesFechadas(sessoes)` | Filtra só sessões com `status === 'fechado'` (type guard) | `SessaoCaixa[]` → `SessaoCaixaFechada[]` |
| `calcularEstatisticasMes(sessoes)` | Soma de diferenças + contagem de faltas/sobras do mês, sobre sessões fechadas | `SessaoCaixa[]` → `EstatisticasMes` |
| `calcularDiaCritico(sessoes)` | Dia da semana com a PIOR média de diferença (mais falta) | `SessaoCaixa[]` → `DiaCritico \| null` |
| `calcularMediaDiferencaDia(sessoes)` | Média da diferença diária, sobre sessões fechadas | `SessaoCaixa[]` → `Centavos` |
| `calcularSangriasMes(sessoes)` | Total + quantidade de sangrias do mês (todas as sessões, abertas ou não) | `SessaoCaixa[]` → `SangriasMes` |
| `descreverDiferenca(sessao)` | Texto do hint: `"em aberto"` / `"bateu certinho"` / `"sobra R$X"` / `"falta R$X"` | `SessaoCaixa` → `string` |
| `valorNaGaveta(sessao)` | Valor do KPI "Na gaveta agora": esperado se aberta, contado se fechada | `SessaoCaixa` → `{ centavos, sufixo }` |
| `descreverCaixaHojeFoot(sessao)` | Rodapé do KPI "Caixa de hoje" | `SessaoCaixa` → `string` |
| `diaLabel(dia)` | `16` → `"16/07"` (mês fixo do período corrente) | `number` → `string` — **(sem teste)** |
| `horaAgora()` | Relógio de parede `"HH:MM"` (usa `new Date()` real) | — → `string` — **(sem teste, depende do relógio)** |

### Recorrentes — `web/src/components/financial/recorrentes/calc.ts`

Prova: `web/src/components/financial/recorrentes/calc.test.ts` (cobertura completa de todas as
funções de cálculo).

| Função | O que calcula | Input → Output |
|---|---|---|
| `formatPctSigned(value, digits=1)` | `"+21,9%"`/`"−5,4%"` — sinal explícito, vírgula pt-BR | `number, number` → `string` |
| `formatPctPlain(value, digits=1)` | `"12,3%"` sem sinal | `number, number` → `string` |
| `derivarContaFixa(item)` | Deriva atual/mês-passado/média-6m/variação%/total-ano-corrente + flag de alerta (≥15%) do histórico bruto de 12 meses | `ContaFixa` → `ContaFixaDerivada` |
| `calcularTotaisFixas(itens, receitaMediaReferencia, diasUteisMes)` | Total atual/mês-passado, delta absoluto/%, custo por dia útil, peso sobre a receita | `ContaFixaDerivada[], Centavos, number` → `FixasTotais` |
| `calcularRetratoFixo(itens)` | Projeção anual (`atual × 12`) e variação vs 6 meses atrás | `ContaFixaDerivada[]` → `RetratoFixo` |
| `calcularSerieMensalFixas(itens)` | Soma mês a mês de todas as contas fixas (série p/ sparkline) | `ContaFixaDerivada[]` → `Centavos[]` |
| `calcularProximaGrande(itens, sufixoMesReferencia='07')` | Maior conta pendente no mês de referência | `ContaFixaDerivada[], string` → `ContaFixaDerivada \| null` |
| `anosDesde(ativaDesde)` | "X anos e Y meses de casa" a partir de `"mmm/aa"` até a referência fixa (jul/2026) | `string` → `string` |
| `calcularMrrTotal(servicos)` | Soma MRR de todos os serviços/assinaturas | `AssinaturaServico[]` → `Centavos` |
| `calcularPctMrr(servico, mrrTotal)` | % que um serviço representa do MRR total | `AssinaturaServico, Centavos` → `number` |
| `calcularChurnMesTotal(servicos)` | Soma o último mês da série `churn6m` de cada serviço | `AssinaturaServico[]` → `Centavos` |
| `calcularNovosMaisExpansaoMes(servicos)` | Soma o último mês da série `novos6m` de cada serviço | `AssinaturaServico[]` → `Centavos` |
| `calcularChurnClientesMesTotal(servicos)` | Soma `churnClientesMes` de todos os serviços | `AssinaturaServico[]` → `number` |
| `calcularNovosClientesMesTotal(servicos)` | Conta serviços com `novos6m` do último mês > 0 | `AssinaturaServico[]` → `number` |
| `calcularChurnPctBase(churnMes, mrrAtual)` | % do churn sobre a base de MRR ANTES do churn (`churn / (mrrAtual + churnMes)`) | `Centavos, Centavos` → `number` |
| `calcularArrEstimado(mrrAtual)` | `mrrAtual × 12` | `Centavos` → `Centavos` |
| `calcularTicketMedio(mrrAtual, assinaturasAtivasCount)` | MRR ÷ nº de assinaturas ativas (0 se nenhuma) | `Centavos, number` → `Centavos` |
| `calcularDeltaPct(atual, referencia)` | Variação % genérica `(atual − referencia) / referencia` | `Centavos, Centavos` → `number` |

### Bancário — `web/src/components/financial/bancario/derive.ts`

Prova: `web/src/components/financial/bancario/derive.test.ts` (cobertura completa exceto
`ordenarMovimentosDesc`, sem `describe` dedicado).

| Função | O que calcula | Input → Output |
|---|---|---|
| `somaCentavos(valores)` | Soma genérica de um array de centavos | `Centavos[]` → `Centavos` |
| `saldoTotalContas(contas)` | Soma o saldo de todas as contas bancárias conectadas | `ContaBancaria[]` → `Centavos` |
| `semanaEntrouTotal(semana)` / `semanaSaiuTotal(semana)` | Soma entrou/saiu por dia dentro de uma semana | `SemanaMovimento` → `Centavos` |
| `mesEntrouTotal(semanas)` / `mesSaiuTotal(semanas)` | Agrega o total do mês somando os totais semanais | `SemanaMovimento[]` → `Centavos` |
| `formatSaldoFoot(contas, formatCentavos)` | Rodapé "Itaú R$X · Nubank R$Y · Stone R$Z" | `ContaBancaria[], (Centavos)=>string` → `string` |
| `ordenarMovimentosDesc(movimentos)` | Ordena o extrato do mais recente pro mais antigo (chave `semana×100 + dia`) | `MovimentoExtrato[]` → `MovimentoExtrato[]` — **(sem teste)** |
| `pendingCount(banco, sistema)` | Soma o tamanho dos dois baldes de pendência de conciliação | `unknown[], unknown[]` → `number` |
| `pendingTotalCentavos(banco, sistema)` | Soma `\|valorCentavos\|` dos dois baldes de pendência | `{valorCentavos}[], {valorCentavos}[]` → `Centavos` |
| `computeDivergentLayout(items, clickable)` | Geometria SVG do gráfico de barras divergentes "entrou × saiu" (escala por maior valor, uma barra acima/abaixo do zero) | `DivergentChartItem[], boolean` → `DivergentChartLayout` |

### Relatórios — `web/src/components/financial/relatorios/helpers.ts`

Prova: `web/src/components/financial/relatorios/helpers.test.ts` (cobertura completa).

| Função | O que calcula | Input → Output |
|---|---|---|
| `docGenKey(cardId, format)` | Chave composta do mapa de estado de geração de documento | `string, DocFormat` → `string` |
| `getDocGenState(map, cardId, format)` | Lê o estado (`'idle'` por default) de um card/formato no mapa | `Record<string,DocGenState>, string, DocFormat` → `DocGenState` |
| `agingWidths(buckets)` | Larguras % da barra de aging, derivadas do valor de cada faixa (nunca hardcoded) | `AgingBucket[]` → `number[]` |
| `toggleAccountSelection(selected, clickedId, allId='todas')` | Toggle de seleção de conta com "Todas" exclusivo (some se ficar vazio) | `string[], string, string` → `string[]` |
| `extratoSummaryLabel(selected, accounts, allId='todas')` | Texto "Selecionado: ..." a partir da seleção de contas | `string[], AccountOption[], string` → `string` |

### Visão Geral — `web/src/components/financial/visao-geral/timelineGeometry.ts`

Prova: **nenhuma** — não existe `timelineGeometry.test.ts` neste diretório. É a única peça de
geometria/matemática não-trivial do módulo Financeiro sem prova automatizada; candidata natural a
`__tests__` novo (ex.: validar `crossIndex`/`crossX` contra uma série sintética com cruzamento
conhecido).

| Função | O que calcula | Input → Output |
|---|---|---|
| `computeTimelineGeometry(valoresDiarios, hojeIndex)` | Geometria SVG do gráfico "caixa nos próximos 30 dias": path sólido (passado) + tracejado (projeção), acha o único cruzamento de zero a partir de hoje (interpolação linear entre os dois pontos que trocam de sinal) pra desenhar a área negativa | `Centavos[], number` → `TimelineGeometry` |

`financial/{bancario,entradas-saidas,visao-geral}/money.ts` (reexports puros) e
`financial/fluxo-caixa/MoneyWhole.tsx` (componente que também só reexporta o formatador) não entram
na tabela — não têm lógica própria, ver seção "Dinheiro" acima.

---

## Estoque — `web/src/components/estoque/calc.ts`

Prova: `web/src/components/estoque/calc.test.ts` — cobre só `kpisDe`, `categoriasDe`,
`semCustoMedioDe` diretamente. `estadoDe`, `joinProdutosComSaldo`, `categoriaNomeDe`,
`categoriasNomesDe`, `filtrarProdutos`, `fmtQty`, `consultorDe` **não têm teste dedicado** (embora
`estadoDe`/`categoriaNomeDe` sejam exercitadas indiretamente como dependência interna de `kpisDe`/
`categoriasDe` nos testes existentes).

| Função | O que calcula | Input → Output |
|---|---|---|
| `estadoDe(produto, saldo)` | Estado do item (`servico`/`zerado`/`baixo`/`ok`) — confia nos flags já computados pelo backend, não reimplementa o corte de mínimo | `ProdutoDto, PosicaoDeItemDto\|null` → `EstadoItem` |
| `joinProdutosComSaldo(produtos, saldos)` | Join produto × saldo (por `produtoId`) + estado calculado | `ProdutoDto[], PosicaoDeItemDto[]` → `ProdutoComSaldo[]` |
| `categoriaNomeDe(item)` | Nome da categoria com fallback `"Sem categoria"` | `ProdutoComSaldo` → `string` |
| `kpisDe(itens)` | Valor total em estoque, itens com saldo, abaixo do mínimo, zerados, cadastrados — só itens que controlam estoque (exceto o total cadastrado) | `ProdutoComSaldo[]` → `EstoqueKpis` |
| `categoriasDe(itens)` | Agrupa itens controlados por categoria, soma valor, ordena desc | `ProdutoComSaldo[]` → `CategoriaResumo[]` |
| `categoriasNomesDe(itens)` | Nomes de categoria únicos (catálogo inteiro, inclusive serviços), ordenados pt-BR | `ProdutoComSaldo[]` → `string[]` |
| `semCustoMedioDe(itens)` | Conta itens controlados com saldo mas custo médio zerado (sinal de qualidade de dado) | `ProdutoComSaldo[]` → `number` |
| `filtrarProdutos(itens, filtro)` | Filtro combinado: busca por nome/SKU, categoria, estado, "só problema" | `ProdutoComSaldo[], ProdutosFiltro` → `ProdutoComSaldo[]` |
| `fmtQty(milesimos, unidade)` | Milésimos-inteiros → texto legível (`"UN"` sempre inteiro, demais até 3 casas) | `number\|null\|undefined, string` → `string` |
| `consultorDe(itens)` | Zerados/baixos + o zerado mais valioso (foco do Super Consultor) — só afirma o que dá pra provar com o saldo atual | `ProdutoComSaldo[]` → `ConsultorResumo` |

---

## Vendas — `web/src/components/vendas/calc.ts`

Prova: `web/src/components/vendas/calc.test.ts` — cobre `formatPct1`, `deltaTone`,
`formatFormasPagamento`, `filtrarVendasTabela`, `buildSparkline`. `isPagamentoDividido` e
`normalizarBusca` **não têm `describe` próprio** (`normalizarBusca` é exercitada indiretamente pelos
casos de busca de `filtrarVendasTabela`).

| Função | O que calcula | Input → Output |
|---|---|---|
| `formatPct1(value)` | 1 casa decimal, vírgula pt-BR | `number` → `string` |
| `deltaTone(pct)` | Tom de cor do delta: `pct >= 0` é `'pos'`, senão `'crit'` (aqui, mais venda é sempre bom) | `number` → `Tone` |
| `formatFormasPagamento(formas)` | Junta formas de pagamento com `" + "` | `MetodoPagamento[]` → `string` |
| `isPagamentoDividido(formas)` | `formas.length > 1` | `MetodoPagamento[]` → `boolean` — **(sem teste dedicado)** |
| `normalizarBusca(texto)` | Remove acento + normaliza caixa (NFD) pra busca | `string` → `string` — **(sem teste dedicado, coberto indiretamente)** |
| `filtrarVendasTabela(vendas, filtros)` | Filtro combinável client-side: estornadas, canal, operador, forma de pagamento, busca normalizada | `VendaRow[], FiltrosVendas` → `VendaRow[]` |
| `buildSparkline(valores)` | Geometria do sparkline (path + area SVG), mesma matemática em todos os módulos que têm KPI hero | `Centavos[]` → `SparklineGeometria` |

---

## Compras — `web/src/components/compras/calc.ts`

Prova: `web/src/components/compras/calc.test.ts` — cobre 18 das 23 funções de cálculo.
`categoriaCorCss`, `buildGroupedBars`, `buildLineChart`, `filtrarNotasTabela`, `filtrarPedidosTabela`,
`filtrarFornecedoresTabela` **não têm teste dedicado** — `buildGroupedBars`/`buildLineChart` são
geometria SVG com matemática real (escala por máximo, auto-scale ±12%/10%) e ficaram sem prova.

| Função | O que calcula | Input → Output |
|---|---|---|
| `fornById(fornecedores, id)` / `notaById(notas, id)` | Lookup por id | `[]T, string` → `T \| undefined` |
| `formatPct1(value)` | 1 casa decimal, vírgula pt-BR | `number` → `string` |
| `deltaBadge(pct)` | Classifica variação de custo vs compra anterior: `novo`/`flat`/`up-bad`/`up-crit` (≥12%)/`down-good` | `number\|null\|undefined` → `DeltaBadge` |
| `isItemPedido(item)` | Type guard: item é de three-way match (Pedido×Nota×Físico)? | `ItemNota` → `item is ItemNotaPedido` |
| `custoUnitCentavosOf(item)` | Custo unitário de exibição (0 se for item de pedido, sem custo calculado nesta tela) | `ItemNota` → `Centavos` |
| `matchCounts(itens)` | Conta itens por tipo de match (auto/sugerido/sem match) | `ItemNotaPadrao[]` → `{auto,sugerido,semmatch}` |
| `pendentesPadrao(itens)` / `resolvidosOuIgnorados(itens)` | Separa itens pendentes de match dos já resolvidos/ignorados | `ItemNotaPadrao[]` → `ItemNotaPadrao[]` |
| `itensComVariacao(notas, fornecedores)` | Todos os itens com Δ de custo ≠ 0, ordenado por \|Δ\| desc | `NotaEntrada[], Fornecedor[]` → `ItemComVariacao[]` |
| `buildHomeKpis(notas, pedidos, fornecedores)` | 4 KPIs do topo: comprado no mês, pedidos abertos + total, notas a conferir, subiram/cairam | `NotaEntrada[], Pedido[], Fornecedor[]` → `HomeKpis` |
| `buildFornecedorRanking(fornecedores)` | Top 3 fornecedores por comprado (90d) + "resto" agregado, com % de participação | `Fornecedor[]` → `FornecedorRanking` |
| `atrasoDias(fornecedor)` | Lead time real − prometido (positivo = atrasado) | `Fornecedor` → `number` |
| `parcelasResumoTxt(parcelas, formatCentavos)` | `"à vista"` ou `"3× R$X"` | `{valorCentavos}[], fn` → `string` |
| `itensQueEntram(itens)` | Quantos itens entram no estoque ao confirmar (exclui `match==='ignorado'`) | `ItemNota[]` → `number` |
| `categoriaCorCss(cor)` | Token de categoria → variável CSS | `CategoriaCusto['cor']` → `string` — **(sem teste)** |
| `buildGroupedBars(meses, categorias, formatCentavos)` | Geometria SVG de barras agrupadas por mês×categoria (largura/altura escaladas pelo maior valor) | `string[], CategoriaCusto[], fn` → `GroupedBarsGeometry` — **(sem teste)** |
| `buildLineChart(series)` | Geometria SVG de linha multi-série, eixo Y auto-escalado (`max×1.12` / `min×0.9`) | `HistoricoCustoSerie[]` → `LineChartGeometry` — **(sem teste)** |
| `filtrarNotasTabela(notas, fornecedores, busca, filtro)` | Filtro de texto + status da tabela de notas | `NotaEntrada[], Fornecedor[], string, FiltroStatusNota` → `NotaEntrada[]` — **(sem teste)** |
| `filtrarPedidosTabela(pedidos, fornecedores, busca)` | Filtro de texto da tabela de pedidos | `Pedido[], Fornecedor[], string` → `Pedido[]` — **(sem teste)** |
| `filtrarFornecedoresTabela(fornecedores, busca)` | Filtro de texto da tabela de fornecedores | `Fornecedor[], string` → `Fornecedor[]` — **(sem teste)** |
| `fatorSugeridoNumero(fatorSugerido)` | Extrai o número depois do `"="` em `"1 CX = 10,000 kg"` | `string\|undefined` → `string` |
| `buildSparkline(valores)` | Geometria do sparkline (mesma matemática do módulo Vendas/Clientes) | `Centavos[]` → `SparklineGeometria` |

---

## OS (Ordem de Serviço) — `web/src/components/os/calc.ts`

Prova: `web/src/components/os/calc.test.ts` — cobre 21 das 25 funções. `hoje`, `diaSemana`, `uid`
(triviais/dependentes de `HOJE`/`Math.random`) e `acaoPrimaria` **não têm teste dedicado**.

| Função | O que calcula | Input → Output |
|---|---|---|
| `hoje(offsetDias)` | `HOJE` (âncora fixa `new Date(2026,6,18,10,0)`) + N dias | `number` → `Date` — **(sem teste)** |
| `addDias(data, dias)` / `diasEntre(de, ate)` / `diasDesde(d)` | Aritmética de datas; `diasDesde` faz clamp em 0 pro futuro | `Date[, Date]` → `Date\|number` |
| `diaSemana(d)` | Nome do dia da semana capitalizado | `Date` → `string` — **(sem teste)** |
| `ehTerminal(status)` | `true` se `Entregue`/`Cancelada`/`DevolvidaSemReparo` | `OsStatus` → `boolean` |
| `totalOrcamento(orc)` | `maoDeObra + Σ(preço×qtd das peças)` | `OrdemServico['orcamento']` → `Centavos` |
| `totalExecucaoAtual(os)` | Igual, usando valores finais de execução (com fallback pro orçamento) | `OrdemServico` → `Centavos` |
| `valorAtual(os)` | Valor "em jogo" da OS — fórmula muda por status (Entregue soma serviço+peças; execução usa `totalExecucaoAtual`; orçamento usa `totalOrcamento`; Cancelada é sempre 0) | `OrdemServico` → `Centavos` |
| `entrouEm(os, status)` | Data em que a OS entrou num status, via histórico | `OrdemServico, OsStatus` → `Date \| null` |
| `atrasada(os)` | `true` se não-terminal e hoje > prazo (comparação por dia, sem hora) | `OrdemServico` → `boolean` |
| `orcamentoVencido(os)` | `true` se `AguardandoAprovacao` e hoje passou de `enviadoEm + validadeDias` | `OrdemServico` → `boolean` |
| `buildKpis(lista)` | KPIs da fila: na bancada, esperando (+ dias médio de espera), prontas (+ mais antiga), faturado no mês (+ delta vs mês anterior, split serviço/peças) | `OrdemServico[]` → `KpisLista` |
| `buildConsultorInsight(lista)` | Insight do Super Consultor: orçamentos parados 5+ dias (o maior por valor) ou, se nenhum, a prateleira de prontas | `OrdemServico[]` → `ConsultorInsightData` |
| `bucketDados(lista)` / `bucketPorChave(lista, key)` | Agrupa OS em 5 baldes do funil (aguardando/execução/prontas/diagnóstico/abertas), com contagem e valor | `OrdemServico[][, BucketKey]` → `Bucket[]\|Bucket` |
| `bucketItensOrdenados(bucket)` | OS de um balde ordenadas por mais antiga na etapa | `Bucket` → `OrdemServico[]` |
| `bucketDrillStats(bucket)` | Tempo médio na etapa, mais antiga (dias), valor total do balde | `Bucket` → `BucketDrillStats` |
| `operacaoStats(lista)` | Porta-a-porta médio (dias), taxa de aprovação %, ticket médio | `OrdemServico[]` → `OperacaoStats` |
| `uid()` | Id curto aleatório (`'l' + base36`) | — → `string` — **(sem teste)** |
| `centavosOuTraco(centavos)` | `null` se ≤ 0 (pro `<MoneyValue>` renderizar `'—'`) | `Centavos` → `Centavos \| null` |
| `acaoPrimaria(os, handlers)` | Rótulo + handler da ação primária da fila, por status (inclui contagem "N/M peças aplicadas") | `OrdemServico, handlers` → `AcaoPrimaria \| null` — **(sem teste)** |
| `filtrarFila(lista, filtro, busca)` | Filtra (ativas/todas/encerradas) + busca, ordena atrasadas primeiro e depois por antiguidade na etapa | `OrdemServico[], filtro, string` → `OrdemServico[]` |
| `passosDaLinhaDoTempo(os)` / `indiceAtualDaLinha(os, passos)` | Passos da linha do tempo (ramo principal ou reprovado) e índice atual nela | `OrdemServico[, passos]` → `OsStatus[]\|number` |

---

## Recorrentes, Bancário

Ver seção **Financeiro** acima — ambos vivem em `components/financial/{recorrentes,bancario}/` e
foram descritos lá para manter o Financeiro inteiro num só lugar.

---

## Agenda — `web/src/components/agenda/calc.ts`

Prova: `web/src/components/agenda/calc.test.ts` — cobertura muito completa (todas as funções de
data/horário/conflito/FSM/recorrência/insight têm `describe`). `formatPeriodo` e `uid` **não têm
teste dedicado** (segunda é geração de id aleatório, baixa prioridade).

| Função | O que calcula | Input → Output |
|---|---|---|
| `toISODate(d)` / `parseISODate(iso)` | Date ↔ `"yyyy-mm-dd"`, sempre meia-noite LOCAL (nunca UTC) | `Date`↔`string` |
| `addDias(d,n)` / `addSemanas(d,n)` / `addMeses(d,n)` | Aritmética de calendário | `Date, number` → `Date` |
| `startOfWeek(d)` / `endOfWeek(d)` | Início (domingo) / fim (sábado) da semana | `Date` → `Date` |
| `startOfMonth(d)` / `endOfMonth(d)` | Primeiro/último dia do mês | `Date` → `Date` |
| `isSameDiaISO(a, isoB)` | Compara `Date` com data ISO | `Date, string` → `boolean` |
| `isMesmoMes(a, b)` | Mesmo ano e mês | `Date, Date` → `boolean` |
| `isHojeReal(d)` | Compara com o relógio real do dispositivo (não com a data-âncora do mock) | `Date` → `boolean` |
| `buildWeekDays(currentDate)` | 7 datas da semana (dom→sáb) | `Date` → `Date[7]` |
| `buildMonthGrid(currentDate)` | 42 células (6 semanas) da grade mensal | `Date` → `Date[42]` |
| `formatPeriodo(viewMode, currentDate)` | Texto do período: `"16 de julho"` / `"13 – 19 de julho 2026"` / `"julho de 2026"` | `ViewMode, Date` → `string` — **(sem teste)** |
| `timeToMinutes(hhmm)` / `minutesToTime(min)` | `"HH:MM"` ↔ minutos desde meia-noite | `string`↔`number` |
| `addDuracao(horaInicio, duracaoMin)` | Soma duração a um horário | `string, number` → `string` |
| `getBlockTop(horaInicio, startHour, hourHeight)` | Posição vertical (px) do bloco na grade | `string, number, number` → `number` |
| `getBlockHeight(duracaoMin, hourHeight)` | Altura (px) do bloco, piso de 24px | `number, number` → `number` |
| `checkConflito(agendamentos, profissionalId, data, horaInicio, horaFim, excludeId?)` | Overlap de horário do mesmo profissional no mesmo dia (ignora cancelados e o próprio em edição) | `Agendamento[], ...` → `ConflitoResultado` |
| `groupByData(agendamentos)` | Agrupa por data ISO | `Agendamento[]` → `Map<string, Agendamento[]>` |
| `filterByProfissional(agendamentos, profissionalId)` | Filtra por profissional (ou `'todos'`) | `Agendamento[], string` → `Agendamento[]` |
| `computeStatusSummary(agendamentos)` | Conta agendamentos por status (inclusive os com 0) | `Agendamento[]` → `Record<Status,number>` |
| `podeTransitar(de, para)` | FSM: transição de status permitida? | `AgendamentoStatus, AgendamentoStatus` → `boolean` |
| `generateRecorrenciaDatas(dataInicioISO, freq, ocorrencias)` | Gera as N datas de uma série recorrente (diária/semanal/quinzenal/mensal) | `string, RecorrenciaFrequencia, number` → `string[]` |
| `buildConsultorInsight(agendamentos, hojeISO)` | Insight do dia: contagem, buracos livres de 45min+ (janela 06h–22h), não confirmados | `Agendamento[], string` → `ConsultorInsightData` |
| `parseCentavosDigitados(raw)` | Digitação livre → centavos (sem lib de máscara) | `string` → `number` |
| `uid()` | Id curto aleatório (`'a' + base36`) | — → `string` — **(sem teste)** |

---

## Clientes — `web/src/components/clientes/calc.ts`

Prova: `web/src/components/clientes/calc.test.ts` — cobertura completa (todas as 14 funções têm
`describe`).

| Função | O que calcula | Input → Output |
|---|---|---|
| `diasEntre(dataDDMMAAAA, hojeDDMMAAAA)` | Dias corridos entre duas datas `"DD/MM/AAAA"` via `Date.UTC` | `string, string` → `number` |
| `ehAniversarianteNoMes(aniversario, hojeDDMMAAAA)` | Compara só o mês (`"MM"`) do aniversário `"DD/MM"` | `string\|null, string` → `boolean` |
| `ehAniversarianteNaSemana(aniversario, hojeDDMMAAAA)` | Aniversário nos próximos 7 dias corridos (testa ano corrente e seguinte, cobre virada dez→jan) | `string\|null, string` → `boolean` |
| `ehNovoNoMes(criadoEm, hojeDDMMAAAA)` | Cliente cadastrado no mês/ano corrente | `string, string` → `boolean` |
| `estaSemComprar90d(cliente, hojeDDMMAAAA)` | `true` só se `status==='ativo'` **e** `diasEntre(ultimaVisita,hoje) >= 90` | `Cliente, string` → `boolean` |
| `buildKpis(clientes, hojeDDMMAAAA)` | Agrega: ativos, novos no mês/semana, aniversariantes, sem comprar 90d — tudo sobre o subconjunto ativo | `Cliente[], string` → `ClientesKpis` |
| `somaGastoVidaCentavos(clientes)` | Soma `totalGastoVidaCentavos` de um segmento — nunca hardcoda a cifra ao lado da copy do Consultor | `Cliente[]` → `Centavos` |
| `filtrarClientes(clientes, filtro, buscaNormalizada, hojeDDMMAAAA)` | Filtro exclusivo da tabela (segmento + busca por nome/telefone/email) | `Cliente[], FiltroClientes, string, string` → `Cliente[]` |
| `clienteById(clientes, id)` | Lookup por id | `Cliente[], string` → `Cliente \| undefined` |
| `statusHistoricoTone(statusLabel)` | Tom de cor a partir de um label de status de OS (string livre, vem de outro módulo) | `string \| undefined` → `Tone` |
| `parseAniversario(valor)` | Valida/normaliza máscara livre `"DD/MM"`; `''`→`null`, formato/dia inválido→`undefined` | `string` → `string \| null \| undefined` |
| `buildSparkline(valores)` | Geometria do sparkline (mesma matemática de Vendas/Compras) | `number[]` → `SparklineGeometria` |
| `ticketMedioExibicaoCentavos(cliente)` | Ticket médio de exibição, 0 se `comprasCount === 0` (evita ticket "fantasma") | `Cliente` → `Centavos` |
| `frequenciaMediaDias(cliente, hojeDDMMAAAA)` | Dias desde o cadastro ÷ nº de compras (piso de 1 dia); `null` se nunca comprou | `Cliente, string` → `number \| null` |

---

## Configurações — `web/src/components/configuracoes/calc.ts`

Prova: **nenhuma** — não existe `calc.test.ts` neste diretório. É o único módulo com `calc.ts`
totalmente sem cobertura de teste. `alternarCelula`/`papelResolvidoParaSalvar` têm lógica de
negócio não-trivial (diff contra o padrão do papel; trava de "founder intocável") e seriam os
melhores candidatos a teste novo se este módulo virar prioridade de revisão.

| Função | O que calcula | Input → Output |
|---|---|---|
| `usuarioById(usuarios, id)` | Lookup por id | `Usuario[], string` → `Usuario \| undefined` |
| `celulaLigada(papel, overrides, modulo, acao)` | Uma célula módulo×ação do grid de permissões está ligada (papel + overrides)? | `Papel, PermissaoOverride[], Modulo, Acao` → `boolean` |
| `alternarCelula(papel, overrides, modulo, acao)` | Alterna 1 célula do grid — grava sempre o DIFF contra o padrão do papel (remove o override se o novo estado já é o de fábrica) | `Papel, PermissaoOverride[], Modulo, Acao` → `PermissaoOverride[]` |
| `papelResolvidoParaSalvar(papelDesejado, papelOriginal, atorPapel)` | Trava de "founder intocável": só outro founder pode conceder/remover o papel founder | `Papel, Papel\|undefined, Papel` → `Papel` |
| `iniciais(nome)` | Iniciais (1-2 letras) pra avatar/logo | `string` → `string` |
| `formatCnpj(valor)` | Máscara `"12.345.678/0001-90"` a partir de dígitos crus (idempotente) | `string` → `string` |
| `emailValido(email)` | Regex simples de e-mail | `string` → `boolean` |
| `pinValido(pin)` | PIN = 4 a 6 dígitos numéricos | `string` → `boolean` |

---

## Resumo de cobertura (gaps a considerar antes de confiar cegamente num número)

| Arquivo | Situação |
|---|---|
| `configuracoes/calc.ts` | **Sem teste algum** (nenhum `.test.ts` no diretório) |
| `financial/visao-geral/timelineGeometry.ts` | **Sem teste algum** — geometria com interpolação (`crossX`) não verificada |
| `compras/calc.ts` | `categoriaCorCss`, `buildGroupedBars`, `buildLineChart`, `filtrarNotasTabela`, `filtrarPedidosTabela`, `filtrarFornecedoresTabela` sem `describe` — os dois primeiros (`buildGroupedBars`/`buildLineChart`) são geometria com escala real, não só filtro |
| `estoque/calc.ts` | `estadoDe`, `joinProdutosComSaldo`, `categoriaNomeDe`, `categoriasNomesDe`, `filtrarProdutos`, `fmtQty`, `consultorDe` sem `describe` direto |
| `os/calc.ts` | `hoje`, `diaSemana`, `uid`, `acaoPrimaria` sem `describe` (as 3 primeiras são baixa prioridade) |
| `agenda/calc.ts` | `formatPeriodo`, `uid` sem `describe` (baixa prioridade) |
| `vendas/calc.ts` | `isPagamentoDividido`, `normalizarBusca` sem `describe` direto (a segunda é exercitada indiretamente) |
| `financial/bancario/derive.ts` | `ordenarMovimentosDesc` sem `describe` |
| `financial/fluxo-caixa/calc.ts` | `diaLabel`, `horaAgora` sem `describe` (baixa prioridade) |
| `financial/entradas-saidas/calc.ts` | `categoriaCorCss` sem `describe` (baixa prioridade, é lookup de mapa) |

Todo o resto (`lib/money.ts`, `lib/format.ts`, `clientes/calc.ts`,
`financial/{recorrentes,relatorios}/*`, `financial/entradas-saidas/calc.ts` exceto o item acima,
`financial/fluxo-caixa/calc.ts` exceto os 2 itens acima, `financial/bancario/derive.ts` exceto o item
acima) tem cobertura completa: toda função com matemática real tem `describe` dedicado no `.test.ts`
irmão.
