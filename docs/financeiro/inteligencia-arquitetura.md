# Financeiro como Camada de Inteligência Quant — Arquitetura e Roadmap

> **Status:** Fase 0 e Fase 1 (motor + endpoints, CMV da DRE corrigido) FECHADAS (2026-07, `dotnet build`+`dotnet test` verdes — 709 testes) · **Decisão formal:** [ADR-0005](../arquitetura/adr/0005-financeiro-inteligencia.md)
> Consolida três estudos: (A) revisão crítica da fundação event-sourced do Financeiro, (B) catálogo de 22 análises quant determinísticas priorizadas, (C) arquitetura cost-efficient do Super Consultor.
> Progresso real vs. este documento: ver checklists da §6 (Fase 0 e Fase 1 marcadas item a item, inclusive o que ficou pendente de propósito, e a re-checagem de fechamento com os 3 gaps re-auditados antes de considerar a F1 fechada de verdade).

---

## 1. Tese

O Financeiro do SistemaX vira **a camada de inteligência quant do negócio**: todos os módulos (Vendas, Estoque, Compras, Fiscal, OS, Agenda) alimentam um **ledger de eventos persistido**; toda análise é um **fold determinístico** desse ledger para uma fact table; o **Super Consultor** é um redator LLM barato (~R$0,78/mês pessimista) que narra números já prontos — nunca calcula nada.

Três princípios não-negociáveis, herdados do que já está sólido no repo:

1. **Toda matemática é determinística em C#** (`Money` centavos-inteiros, seed fixa `tenantId+período` para qualquer bootstrap, 100% reprodutível). Custo de inferência analítica = zero.
2. **Insumo é evento, nunca leitura direta de tabela alheia** (R3 — idempotência via `ChaveIdempotencia`/ULID).
3. **LLM é narrador, não analista** (Lei 2 — IA observa/aconselha, nunca age; nunca vê linha crua do banco).

---

## 2. Diagnóstico honesto — de onde partimos

### O que já está sólido (não mexer)

| Peça | Por que importa para quant |
|---|---|
| `LancamentoContabil` — partida dobrada, Σdébito==Σcrédito, imutável, estorno via `GerarEstorno`/`ReversalOfId` | Checksum de integridade: dinheiro não some; auditável por construção |
| `Money` centavos-inteiros / `Quantidade` milésimos | Nunca float tocando saldo |
| Idempotência tripla: `SourceRef` + `ChaveIdempotencia` + `UNIQUE INDEX (business_id, origem_chave)` | Replay seguro |
| Caixa (`MovimentoFinanceiro`) vs. competência (`ContaAReceber/Pagar`) ligados por `ParcelaId` | As duas lentes (cash vs. accrual) que todo modelo financeiro precisa |
| ULID em tudo, `IRelogio`, FSM com terminais, ports/adapters, saldo sempre derivado | Ordenação temporal grátis; testável; determinístico |
| Catálogo de eventos aditivo-only (`IntegrationEvents.cs`) e plumbing de outbox transacional | O caminho para o ledger já está demonstrado no código |

### O gap central: o sistema hoje é state-sourced, não event-sourced

A promessa do ADR-0001 §3 ("read-model = fold determinístico do log") é **aspiracional hoje**:

- `InProcessIntegrationEventBus` entrega síncrono e o evento **evapora** — nenhuma persistência, nenhum replay.
- O outbox existente é **CDC de entidade** (log de *estado*), não log de *fatos de negócio*. "UPDATE em assinaturas" ≠ "AssinaturaCancelada".
- Os read-models (`FluxoDeCaixaService`, `DreGerencialService`, `ReceitaRecorrenteService`, `QuantoSobrouDeVerdadeService`) fazem fold sobre **tabelas de agregados**, não sobre um log.
- **Teste decisivo que falha:** "Qual era o MRR em 1º de março?" é irrespondível — mutações de `Assinatura` sobrescrevem estado. Séries históricas (MRR, aging ao longo do tempo, saldo as-of-date, concentração) são irreconstruíveis.
- Única exceção: `lancamentos_contabeis` é append-only de verdade — mas captura o fato *depois* do mapeamento para 6 contas fixas, com dimensões descartadas.

### Gaps secundários (em ordem de dor)

1. **Eventos dimensionalmente pobres** — `VendaConcluida` = só `TotalCentavos + FormaPagamento` string. Sem `ClienteId`, canal, itens, desconto → sem coorte, sem LTV, sem inadimplência por cliente. `VendaItensMovimentados` existe no catálogo mas Vendas não publica.
2. ~~**CMV economicamente errado**~~ — CORRIGIDO (2026-07, fechamento da F1): `DreGerencialService` classificava `CompraRecebida` como "CustoDireto": encher estoque aparecia como custo do mês; margem distorcida. O evento simétrico `CustoBaixadoPorVenda` (Estoque→Financeiro) já existia desde a F0/F1 (padrão que `PecaConsumida` estabeleceu) e alimenta `fato_custo_diario` — só faltava o DRE somar dali em vez do `ContaAPagar` por categoria (ver §6 F1).
3. **Zero infra de série temporal** — full-scan + LINQ em memória (O(dias × movimentos) no `FluxoDeCaixaService`; `CalcularSaldoAsync` soma desde o big bang). Sem rollups, sem cursor de projeção, sem query as-of.
4. **Bucketing em dia UTC** (`DateOnly.FromDateTime(m.DataMovimento.UtcDateTime)`) — venda das 22h cai no dia seguinte; ~8–12% de ruído sistemático em série diária.
5. **`FormaDePagamento` modela taxa e prazo de compensação (D+30) e nenhum handler usa** — handler grava a string crua e o classificador chuta 30 dias fixos. Caixa projetado ignora MDR e lag de liquidação.
6. Menores: `CentroDeCusto` nunca preenchido; DRE de 3 baldes fixos; `CompraEstornada` sem handler; cron de `AvaliarParcelasVencidasUseCase` sem dono; partidas contábeis sem dimensões.
7. **Super Consultor atual é mock** — 7 cards alimentados por `FLUXO_CAIXA_MOCK` e templates hardcoded; não há superfície analítica para uma IA consumir.

---

## 3. Arquitetura-alvo

```
 Vendas   Estoque   Compras   Fiscal   OS/Agenda   Financeiro
    │        │         │        │          │           │
    └────────┴────┬────┴────────┴──────────┴───────────┘
                  ▼  (persist-then-dispatch, mesma transação SQLite)
   ┌──────────────────────────────────────────────────┐
   │  integration_events  (append-only, ULID pk)      │  ← A VERDADE HISTÓRICA
   └──────────────┬───────────────────────────────────┘
                  ▼  folds com cursor (projection_state)
   ┌──────────────────────────────────────────────────┐
   │  FACT TABLES: fato_caixa_diario, fato_receita_   │  ← reprocessáveis do zero
   │  diaria, fato_margem_produto, fato_recebiveis,   │
   │  fato_estoque_diario, ...                        │
   └───────┬───────────────┬──────────────┬───────────┘
           ▼               ▼              ▼
   Read-models UI    Motor Quant     Escape hatch Igor
   (telas atuais)    (calc.ts / C#   (DuckDB/pandas read-only
                     funções puras)   no SQLite; export parquet;
           │               │          /financeiro/series)
           │               ▼
           │      ConsultorFato (facts formatados, score)
           │               ▼
           │      ConsultorPipeline (cron 06:00, idempotente)
           │               ▼  só facts, nunca dado cru
           │      Cloud.Api /consultor/narrate
           │      (cache por hash + budget + gpt-4o-mini
           │       + validação anti-alucinação + fallback template)
           ▼               ▼
        ┌────────────────────────┐
        │  UI: cards Consultor + │
        │  "Ver como calculamos" │
        └────────────────────────┘
```

### 3.1 Ledger de eventos (a peça nº 1)

Tabela `integration_events`, append-only:

```
id (ULID pk) · tipo · tenant_id · payload_json · ocorrido_em · chave_idempotencia (UNIQUE)
```

Gravada **na mesma transação** do commit de origem (o plumbing do outbox já demonstra como). O bus vira **persist-then-dispatch**. É uma tabela + uma mudança no bus — uma decisão, não uma reengenharia. **Cada dia sem o ledger é história que se perde para sempre**, por isso ele é a Fase 0 e nada mais entra na frente.

### 3.2 Projeções com cursor + fact tables

- `projection_state (nome, ultimo_ulid_processado)` — cada fold retoma de onde parou; reprocessar do zero é sempre seguro (idempotência por ULID).
- Fact tables achatadas e baratas de consultar: `fato_caixa_diario`, `fato_receita_diaria`, `fato_margem_produto`, `fato_recebiveis` (parcelas emitido→pago com dias de atraso), `fato_estoque_diario`.
- **Nova análise = novo fold + replay.** O passado nunca é migrado, só re-derivado — aí o ADR-0001 §Consequências vira verdade.
- Read-models existentes migram para as fact tables *quando tocados* (sem big-bang): os atuais continuam corretos sobre agregados enquanto o histórico acumula no ledger.

### 3.3 Enriquecimento dimensional dos eventos (companions aditivos)

No padrão aditivo-only já estabelecido no catálogo:

| Mudança | Desbloqueia |
|---|---|
| `VendaConcluida` + `ClienteId`, canal, desconto | Coorte, LTV, RFM, inadimplência por cliente (#3, #17) |
| Vendas passa a publicar `VendaItensMovimentados` (gap já documentado no próprio `IntegrationEvents.cs`) | Margem por item, ABC por MC, ruptura (#6, #12, #14) |
| Novo `CustoBaixadoPorVenda(vendaId, custoTotalCentavos)` Estoque→Financeiro | **CMV real** → DRE correta, margem por produto (#6, #7) |
| Handlers usam `FormaDePagamento.PrazoCompensacao/Taxa` (em vez da string crua) | Caixa projetado com lag de cartão e MDR (#1, #8) |
| Política de tempo: bucketing por timezone do tenant (America/Sao_Paulo), fixada num só lugar | Séries diárias sem ruído UTC |
| Dimensões nas partidas contábeis (`centro_custo`, `categoria`, `source_ref` por partida) | Ledger contábil vira tabela-mestre de análise, já balanceada por construção |

### 3.4 Motor quant — convenções

- Funções puras `Centavos[] → resultado`, no padrão `calc.ts` + `calc.test.ts` por tela (front) e services C# puros (back).
- Estimadores incrementais (EWMA, roll-rate, Kaplan-Meier, índices sazonais) alimentados por eventos, com reprocessamento idempotente.
- Aleatoriedade (bootstrap do fluxo de caixa) sempre com **seed fixa derivada de `tenantId+período`** → mesmo input, mesmo output, sempre.
- Zero ML treinado: tudo é estatística fechada (EWMA, bootstrap em blocos, cadeia de Markov de aging, decomposição sazonal multiplicativa, Little's Law, KM). Auditável no painel "Ver como calculamos".

### 3.5 Super Consultor — pipeline e teto de custo

**Split físico** (SistemaX é local-first; key OpenAI não pode embarcar no desktop):

```
Host.Desktop (local)                          Cloud.Api (tem OPENAI_API_KEY)
coleta facts (SQLite)     ──HTTP: só facts──▶ cache global por (ruleId, factsHash)
ranking determinístico                        budget mensal por tenant
cache local (SQLite)      ◀──frases JSON────  1 chamada gpt-4o-mini batch
fallback template                             validação anti-alucinação
UI lê só do cache local
```

Dado cru **jamais** sai da máquina — só dezenas de strings pré-formatadas tipo `"R$ 4.200,00"`. Privacidade e custo resolvidos no mesmo movimento.

**Contrato central** (`Modules.Abstractions/Consultor/`):

```csharp
public interface IConsultorFactProvider   // cada módulo registra o seu via IModule/DI (R5)
{
    Task<IReadOnlyList<ConsultorFato>> ColetarAsync(PeriodoRef periodo, CancellationToken ct);
}

public sealed record ConsultorFato(
    string Modulo,            // "financeiro", "estoque", "vendas", "compras", "fiscal"
    string RuleId,            // "fin.aluguel-antes-do-atrasado"
    string Tela,              // slot de UI: "visao-geral" | "fluxo-caixa" | ...
    int Score,                // impacto em centavos × severidade × novidade
    IReadOnlyDictionary<string, string> Facts,  // valores JÁ formatados
    string TemplateFallback,  // frase pronta interpolada — sempre renderizável
    DrillTarget? Drill);      // navegação read-only (Lei 2)
```

**Pipeline diário** (hosted service, 06:00 local ou boot com catch-up):

1. Idempotência: kv `consultor_run:{yyyy-MM-dd}` já existe? → skip.
2. Coleta: enumera `IConsultorFactProvider` via ModuleRegistry → ~15–30 fatos.
3. Rank por Score; garante ≥1 fato por tela com card; corta top-N (N=8). Se `sha256(Facts)` == hash do último narrado da mesma rule → reusa frase antiga (custo 0).
4. Narra: POST `Cloud.Api /api/consultor/narrate` — cache hit devolve sem tocar OpenAI; budget estourado devolve `source: "template"`; miss → 1 chamada `gpt-4o-mini` (JSON mode, temperature 0,35, max_tokens 900), valida cada frase (números dos facts presentes, ≤220 chars, 1 frase — mesmo `isValidConsultorPhrase` do saas-erp), grava cache, incrementa budget.
5. Persiste em `consultor_insights` local; UI lê `GET /api/consultor/insights?tela=X` — substitui os mocks. "Ver como calculamos" vem de `facts_json`, nunca do LLM.

**Regras compostas cross-módulo são código, não LLM:** `CrossModuleConsultorRules.cs` consome facts de 2+ providers e emite fato novo (ex.: `cross.capital-parado-vs-caixa` — "R$ 18.400 parados em estoque sem giro enquanto o caixa projeta negativo dia 22"). O LLM só narra.

**Falha nunca vira erro na UI:** o pior caminho em qualquer ponto (sem rede, OpenAI 500, teto, validação reprovou) é `source: 'template'` — frase correta, só menos gostosa. Circuit breaker no Cloud.Api. Re-narração intra-dia opcional: 1 re-run a cada 4h se factsHash mudou (máx. ~4 chamadas/dia).

**Prova de custo** (gpt-4o-mini US$0,15/1M in, US$0,60/1M out; ~1.900 tok in + ~650 tok out por run de 8 insights; câmbio R$5,40):

| Cenário | Runs/mês | BRL/mês |
|---|---|---|
| Nominal (1 run/dia, cache ~40%) | 30 | **R$ 0,11** |
| Pessimista (4 runs/dia, cache frio) | 120 | **R$ 0,44** |
| + relatório semanal longo (gpt-4.1, 4×/mês) | +4 | +R$ 0,34 |
| **Total pessimista** | | **≈ R$ 0,78** |

Cabe no teto com folga de ~20–25×. O budget guard (teto duro ~300 chamadas narrate/mês ≈ R$1,10) é **circuit-breaker de anomalia, não restrição de produto** — sobra ~R$14–19/CNPJ de headroom para um futuro chat "pergunte ao consultor" (~150 perguntas/mês no mesmo teto).

**Referências a portar:** `saas-erp/app/api/financial/consultor/route.ts` (cache `sha256(facts)[:8]`, validação, fallback nunca-500) e `saas-erp/lib/channels/media-enrichment.ts` (fetch OpenAI). UI de destino: 7 cards em `web/src/components/financial/*/`, `ComprasConsultor.tsx`, primitivo `shared/ConsultorInsight.tsx`.

---

## 4. Catálogo quant priorizado (o que calcular)

Resumo do catálogo completo — 22 análises, todas determinísticas, ranking = valor-ao-leigo × viabilidade. Detalhe de método por análise no estudo B (fórmulas fechadas: bootstrap com seed, EWMA, roll-rate/Markov, Laspeyres, Kaplan-Meier, Little, binomial exata).

### Fase 1 — Quick-wins ("sobrevivência", o que mata PME)

| # | Análise | Pergunta do leigo | Insumo crítico |
|---|---|---|---|
| 16 | **Sazonalidade** (motor compartilhado, índices dia-semana/mês) | "Fevereiro cai ou é impressão?" | fato_receita_diaria |
| 6 | **Margem de contribuição por item** + quadrante volume×MC | "O que dá lucro DE VERDADE?" | `VendaItensMovimentados` + `CustoBaixadoPorVenda` + imposto real + taxa rateada |
| 1 | **Previsão de caixa com bandas P5/P50/P95** + P(saldo<0 em ≤30d) + first hitting time | "Quando fico sem caixa? Com que certeza?" | fato_caixa_diario, AR com atraso esperado (#3), sazonalidade (#16) |
| 2 | **Runway** (bruto: saldo/burn EWMA; realista: P50 do #1) | "Quantos dias aguento se parar de vender?" | subproduto do #1 |
| 3 | **Score de inadimplência** (EWMA de atraso por cliente + matriz roll-rate da carteira → provisão) | "Esse 'a receber' vale quanto de verdade?" | fato_recebiveis |
| 4 | **Radar do Simples Nacional** (RBT12 rolling, alíquota efetiva/marginal, distância ao degrau, projeção do cruzamento) | "Vou pular de faixa? Vale vender mais?" | receita fiscal mensal + anexo do tenant |
| 7 | **Ponto de equilíbrio vivo + breakeven day** | "Quanto preciso vender por dia?" | fixas (já marcadas) + índice MC do #6 |

#16 e #6 são o **motor base** — quase tudo depende deles; implementar primeiro.

### Fases seguintes (ordem de dependência)

- **Onda 2 (lucro escondido):** #8 custo real do parcelado/antecipação (decisão binária calculada), #9 simulador de desconto (fórmula fechada `ΔQ = m/(m−d) − 1`), #11 estoque morto em R$ + curva de liquidação, #14 custo da ruptura, #5 capital de giro/CCC/NCG.
- **Onda 3:** #12 ABC×XYZ com política por célula, #13 ponto de pedido com lead time medido, #15 índice Laspeyres de insumos + repasse, #17 clientes sumidos (RFM + gap individual), #18 concentração HHI (clientes E fornecedores), #10 vazamentos operacionais (z-score + binomial exata, p<0,01 antes de acusar).
- **Onda 4 (verticais):** #19 sobrevivência de assinaturas KM + NRR + LTV, #20 lucro por OS/hora técnica + retrabalho, #21 prazo prometível (Little + p90 das transições FSM), #22 ocupação/no-show da agenda.

**Não duplicar o que já existe** — formalizar/upgrade: "Livre de verdade", anomalia de categoria (`entradas-saidas/calc.ts`), fixo×variável, DRE competência×caixa, aging, MRR/churn, taxa efetiva por forma, diferença de caixa por operador, ABC/giro/cobertura, FSM de OS.

---

## 5. Extensibilidade — como o Igor pluga uma análise nova

Hoje, plugar "sazonalidade de entradas por forma de pagamento" custa **sete camadas** (port → 2 impls → service → module → endpoint+permissão → client TS → componente). Isso mata experimentação. A arquitetura abre **três portas, da mais barata à mais integrada**:

### Porta 1 — Notebook direto no SQLite (custo: zero código C#)

O SQLite local é um *feature*. Schema das fact tables documentado + leitura read-only (WAL permite leitor concorrente):

```python
import duckdb
con = duckdb.connect()
df = con.execute("""
    SELECT * FROM sqlite_scan('~/sistemax/data/tenant.db', 'fato_caixa_diario')
""").df()
# pandas/scipy/statsmodels à vontade — sem tocar C#, endpoint ou UI
```

Alternativa: comando de export parquet (`sistemax export --parquet`). É aqui que hipóteses nascem — Igor estuda quant finance no notebook, valida a análise com dados reais, e **só promove para o produto o que provou valor**.

### Porta 2 — Endpoint genérico de séries (custo: query string)

`GET /financeiro/series?metrica=caixa_diario&bucket=dia&de=&ate=` servindo as fact tables. Um endpoint só, N métricas registradas. **É também o contrato de tool que o Super Consultor/LLM consumirá** quando ganhar chat — a mesma superfície serve humano e IA.

### Porta 3 — Promover a análise para o produto (custo: 2 classes + testes)

Receita fixa para uma análise validada no notebook virar feature:

1. **Fold novo** (se precisa de fact table nova): classe que implementa o replay do ledger com cursor — registra em `projection_state`, reprocessa do zero na primeira execução. Passado inteiro disponível de graça.
2. **Função pura de cálculo** `Centavos[] → resultado` + teste com fixture (padrão `calc.ts`/`calc.test.ts` já estabelecido).
3. **(Opcional) Card no Consultor:** uma entrada no `IConsultorFactProvider` do módulo — `RuleId` novo, facts formatados, template fallback. O pipeline, o ranking, a narração, o cache e a UI **já existem**; a regra nova pega tudo de carona. Zero mudança em infra.

O ciclo completo: **notebook (hipótese) → série/fact table (validação) → provider (produto)**. Cada etapa é opcional e independente; nada exige as sete camadas de antes.

---

## 6. Roadmap faseado

### Fase 0 — Fundação: o ledger (ENTREGUE — 2026-07)

> Cada dia sem o ledger é história que se perde para sempre. É a diferença entre "ERP bem escrito com views derivadas" e "histórico completo do negócio sobre o qual qualquer análise futura é um fold".

- [x] Tabela `integration_events` append-only + bus persist-then-dispatch (mesma transação; plumbing do outbox como modelo) — `IIntegrationEventLedgerStore`/`SqliteIntegrationEventLedgerStore`, `InProcessIntegrationEventBus` grava no ledger antes de despachar.
- [x] `projection_state` (cursores) + esqueleto de fold reprocessável — `IProjectionStateStore`, `ProjectionRunner` (`ExecutarUmaAsync`/`ReconstruirAsync`), contrato `IProjection` em `Modules.Abstractions.Runtime`.
- [x] Política de tempo: bucketing por timezone do tenant, num só lugar — `BucketingTemporalDoTenant.DiaLocal` (America/Sao_Paulo fixo, único ponto que muda quando ficar configurável por tenant).
- [x] Companions dimensionais (PARCIAL — os 3 que desbloqueiam a F1 estão prontos, os 3 que ainda não têm consumidor seguem em aberto):
  - [x] `ClienteId`/canal/desconto em `VendaConcluida`.
  - [x] Vendas publica `VendaItensMovimentados` (`VendaUseCases.ConcluirVendaAsync` → `ParaVendaItensMovimentados()`).
  - [x] Novo `CustoBaixadoPorVenda` (Estoque→Financeiro), publicado por `VendaItensMovimentadosHandler` — é o insumo que `fato_custo_diario` (F0) e `fato_margem_produto` (F1, novo) foldam.
  - [ ] Handlers usam `FormaDePagamento.PrazoCompensacao/Taxa` — o tipo já existe no domínio (`FormaDePagamento.cs`), mas nenhum handler o lê ainda; caixa projetado continua sem MDR/lag de cartão (fica para quando #1/#8 precisarem de verdade).
  - [ ] Handler para `CompraEstornada` — Compras já publica o evento; nem Financeiro nem Estoque assinam ainda (gap documentado no próprio `IntegrationEvents.cs`).
  - [ ] Dono para o cron de `AvaliarParcelasVencidasUseCase` — o caso de uso existe, mas nenhum `BackgroundService`/host o chama periodicamente ainda (ao contrário do catch-up de projeções, que ganhou o seu na F1 — ver abaixo).

**Critério de pronto:** ✅ replay do ledger reconstrói `fato_caixa_diario`/`fato_receita_diaria`/`fato_custo_diario`/`fato_margem_produto` idênticos ao fold incremental (`ProjectionRunnerReprocessabilityTests`, `FatoMargemProdutoProjectionTests`). "MRR as-of-date" segue não implementado (não fazia parte do escopo mínimo de prova da F0/F1 — seria um novo fold sobre os eventos de `Assinatura`, ainda sem companion no ledger).

### Fase 1 — Motor base + quick-wins de sobrevivência (ENTREGUE — 2026-07, motor + endpoints; UI fica para quem tocar o frontend)

- [x] Fact tables: `fato_caixa_diario` (F0), `fato_receita_diaria` (F0), `fato_margem_produto` (NOVO — `FatoMargemProdutoProjection`, rateio de `CustoBaixadoPorVenda` proporcional à receita de cada item via `RateioProporcional`, contract test em InMemory+Sqlite). `fato_recebiveis` **não foi construída** — o score de inadimplência (#3) consultou `ContaAReceber`/`Parcela` ao vivo em vez de uma fact table dedicada (decisão desta fase: sem histórico de snapshots mensais de carteira ainda, uma fact table de recebíveis não teria o que uma tabela ao vivo já não desse; fica para quando o roll-rate empírico da Fase 3 precisar de snapshots).
- [x] Motor #6 (MC por item, com CMV real da F0/F1 via `fato_margem_produto`). Motor **#16 (sazonalidade) não entrou nesta rodada** — segue como próximo item do "motor base" antes das ondas 2/3.
- [x] Análises de sobrevivência — motor quant determinístico (`Application/Quant/*`, funções puras com XML doc da fórmula + `*Tests.cs`) e endpoints `GET /financeiro/*` (permissão `Financeiro.Ver`):
  - [x] #1 bandas P5/P50/P95 (`BandasDeFluxoDeCaixa` — bootstrap em blocos com seed determinística via `SeedDeterministico`) + probabilidade de saldo negativo em 30 dias — `GET /financeiro/previsao-caixa`.
  - [x] #2 runway bruto (saldo/burn EWMA) e realista (primeiro dia P50 negativo) — mesmo endpoint acima (`RunwayCalculator`).
  - [x] #3 inadimplência por faixa de atraso + provisão (`InadimplenciaRollRate` — curva padrão de perda por aging; `EstimarMatrizRollRate` já pronto, não usado em produção ainda por falta de snapshots históricos) — `GET /financeiro/inadimplencia`.
  - [x] #4 Radar do Simples Nacional (`RadarDoSimplesNacional` — só Anexo I populado nesta fase, mesmo padrão "preparado, não operante" de `RegimeTributario.LucroReal`) — `GET /financeiro/radar-simples`.
  - [x] #7 ponto de equilíbrio vivo + dia do breakeven (`BreakevenMensal`, custos fixos = `Recorrencia` ativas do tipo `APagar` normalizadas pra mensal) — `GET /financeiro/ponto-equilibrio`.
  - [ ] UI ("dashboard sobrevivência" na tela) — esta rodada foi só motor + API; os cards na "Visão Geral"/timeline do Financeiro (`web/src/components/financial/**`) ainda consomem os read-models antigos, não estes endpoints novos.
- [x] CMV corrigido na DRE (`CustoBaixadoPorVenda` em vez de `CompraRecebida` como custo) — **FEITO no fechamento da F1 (2026-07)**: `DreGerencialService` somava direto o `ContaAPagar` categorizado `CustoMercadoriaVendida` (a COMPRA, balanço) como custo do período; agora soma `fato_custo_diario` (o CMV RECONHECIDO por venda, via `CustoBaixadoPorVenda`) no período do DRE — `ContaAPagar` de compra sai do cálculo inteiramente (nem custo direto, nem despesa operacional; é troca de ativo, não resultado). Decisão de implementação: usou-se `fato_custo_diario` (agregado diário, F0) em vez de `fato_margem_produto` (granular por produto, F1) — mesmo total exato (rateio proporcional soma 100% do custo da venda), sem a complexidade extra da alocação por produto que o DRE agregado não precisa. `DreGerencialServiceTests.cs` (4 casos, `tests/.../DreGerencialServiceTests.cs`) trava: compra do mês sem venda correspondente não vira CMV; venda com CMV real via fato + compra grande no mesmo mês não contamina o resultado; comissões continuam via `ContaAPagar`; despesa operacional ignora as duas categorias de custo direto.
- [ ] Porta 1 da extensibilidade (schema documentado + acesso read-only + export parquet) — não feito nesta rodada.
- [x] Catch-up de projeções agora é PERIÓDICO, não só no boot — `ProjectionCatchUpHostedService` virou `BackgroundService` com `LocalDatabaseOptions.ProjectionCatchUpInterval` (default 30s), fail-open por ciclo (nunca derruba o host). Antes só rodava uma vez no `StartAsync`.

**Re-checagem de fechamento (2026-07) — os 3 gaps que o próprio documento já assumia como pendentes:**

1. **CMV na DRE** — corrigido acima (deixou de ser gap).
2. **UI não consome os 5 endpoints novos** — RE-CONFIRMADO: `grep` por `previsao-caixa`, `ponto-equilibrio`, `inadimplencia`, `radar-simples`, `fato-margem-produto` em `web/src/**` não encontra nenhuma ocorrência; os 6 componentes `*Consultor*`/`ConsultorSection` existentes em `web/src/components/financial/**` seguem lendo os read-models antigos (mocks/F0). Decisão: fica para a Fase 2 (que já é o momento formal de "substituir os mocks" — ver roadmap abaixo), não bloqueia o fechamento da F1 — a própria entrada da F1 já dizia "UI fica para quem tocar o frontend"; a F1 entrega motor+API testados, o consumo visual é responsabilidade de uma rodada de frontend dedicada.
3. **`EstimarMatrizRollRate` não usado em produção** — RE-CONFIRMADO e a razão documentada se sustenta na leitura do código: o estimador exige uma lista de transições OBSERVADAS (`(FaixaDeAtraso De, FaixaDeAtraso Para)` da MESMA parcela entre dois snapshots consecutivos da carteira — `InadimplenciaRollRate.cs` linha 102-121). Não existe hoje nenhuma fonte desse par De→Para: `fato_recebiveis` (a fact table que teria os snapshots mensais) foi deliberadamente NÃO construída nesta F1 (ver item logo acima na lista de fact tables) por decisão explícita ("sem histórico de snapshots mensais de carteira ainda, uma fact table de recebíveis não teria o que uma tabela ao vivo já não desse"). Forçar o uso agora significaria alimentar o estimador com zero transições reais — a suavização de Laplace devolveria a matriz uniforme (todo destino com peso `1/6`), estatisticamente pior que a curva padrão de aging (`TaxaDePerdaPadrao`) hoje em uso, que pelo menos reflete a convenção real de PDD por atraso. Manter `InadimplenciaRollRate.CalcularProvisao` com a curva padrão até a Fase 3 construir `fato_recebiveis` com snapshots é a decisão correta, não uma pendência esquecida.

### Fase 2 — Super Consultor de verdade (substituir os mocks)

- [ ] `IConsultorFactProvider` + `ConsultorFato` em `Modules.Abstractions/Consultor/`.
- [ ] `FinanceiroConsultorFactProvider` sobre os read-models/fact tables (menor esforço — mapa do wiring doc).
- [ ] `ConsultorPipeline` (hosted service, cron+catch-up, idempotência kv) + migração `consultor_insights`.
- [ ] `Cloud.Api /api/consultor/narrate`: cache por hash, budget/tenant, gpt-4o-mini JSON mode, `isValidConsultorPhrase`, circuit breaker — porta do `route.ts` do saas-erp.
- [ ] `web`: `useConsultorInsights(tela)` troca mock→API nos 7 cards; painel "Ver como calculamos" de `facts_json`.
- [ ] Providers de Estoque/Vendas/Compras/Fiscal + `CrossModuleConsultorRules.cs`.

**Critério de pronto:** zero mocks; teto de custo medido em produção ≤ R$1,10/CNPJ/mês; fallback template verificado em cada modo de falha.

### Fase 3 — Lucro escondido + estoque (ondas 2–3 do catálogo)

- [ ] #8, #9, #11, #14, #5; depois #12, #13, #15, #17, #18, #10.
- [ ] `fato_estoque_diario` + lead time medido por fornecedor (mediana pedido→recebimento NF-e).
- [ ] Porta 2: endpoint genérico `/financeiro/series`.
- [ ] Dimensões nas partidas contábeis (centro_custo, categoria, source_ref por partida).
- [ ] Migração incremental dos read-models legados para fold-sobre-ledger (quando tocados; sem big-bang).

### Fase 4 — Verticais + interatividade

- [ ] #19 (KM/NRR/LTV de assinaturas), #20–#22 (OS/agenda).
- [ ] Relatório semanal longo (gpt-4.1, 4×/mês, já dentro do teto provado).
- [ ] Chat "pergunte ao consultor" sobre a superfície de séries (Porta 2 como tool) — cabe no headroom de R$14–19/CNPJ.

---

## 7. Riscos e salvaguardas

| Risco | Salvaguarda |
|---|---|
| Ledger cresce sem limite no SQLite | Append-only compacta bem; rollups diários nas fact tables absorvem as queries; arquivamento por período se necessário (decisão futura, não bloqueia) |
| Fold com bug corrompe fact table | Fact tables são descartáveis por construção — `DROP` + replay do zero é a correção canônica |
| LLM alucina número | Validação pós-LLM exige cada valor dos facts presente na frase; reprova → template |
| Custo LLM foge do controle | Budget duro por tenant no Cloud.Api (~300 narrates/mês); pior caso do produto é template, nunca erro |
| Acusação injusta em vazamento operacional (#10) | Binomial exata, só narra com p<0,01, e o texto manda "conferir o processo antes de desconfiar de pessoa" |
| Análise nova quebra o existente | Eventos aditivos-only; nova análise = novo fold; o passado nunca é migrado, só re-derivado |

---

## 8. Arquivos-chave

**Existentes (base):** `src/Modules/Abstractions/.../IntegrationEvents.cs`, `.../Runtime/InProcessIntegrationEventBus.cs`, `src/Infrastructure/SistemaX.Infrastructure.Local/Outbox/`, `src/Modules/Financeiro/.../Contabil/LancamentoContabil.cs`, `.../Caixa/MovimentoFinanceiro.cs`, `.../Application/ReadModels/*.cs`, `.../Handlers/VendaConcluidaHandler.cs`, `web/src/components/financial/**`, `docs/wiring/financeiro-api-contract.md`.

**Novos (por fase):** `integration_events` + `projection_state` (F0); `src/Modules/Abstractions/Consultor/{IConsultorFactProvider,ConsultorFato}.cs`, `src/Modules/{X}/Application/Consultor/{X}ConsultorFactProvider.cs`, `src/Infrastructure.Local/Consultor/{ConsultorPipeline,INarradorConsultor}.cs`, `src/Hosts/SistemaX.Cloud.Api/.../ConsultorNarrateEndpoint.cs`, `web/src/lib/api/consultor.ts` (F2); folds/fact tables por análise (F1/F3).

**F1 (entregues nesta rodada):** `Financeiro.Application/Analitico/{FatoMargemProduto,FatoMargemProdutoProjection}.cs`, `Financeiro.Application/Ports/IFatoMargemProdutoRepository.cs`, `Financeiro.Infrastructure/{InMemory,Sqlite}/*FatoMargemProdutoRepository.cs`, `Financeiro.Infrastructure/Sqlite/FinanceiroSchemaMigrationV10.cs` — a fact table nova. `Financeiro.Application/Quant/{SeedDeterministico,RateioProporcional,BandasDeFluxoDeCaixa,RunwayCalculator,BreakevenMensal,InadimplenciaRollRate,RadarDoSimplesNacional}.cs` — o motor quant puro (cada um com `*Tests.cs` irmão em `tests/.../Quant/`). `Financeiro.Application/ReadModels/{PrevisaoDeCaixaService,PontoDeEquilibrioService,InadimplenciaService,RadarDoSimplesService}.cs` — a orquestração dos ports reais sobre o motor. Endpoints em `Financeiro.Application/Endpoints/FinanceiroEndpointsModule.cs` (`/financeiro/{fato-margem-produto,previsao-caixa,ponto-equilibrio,inadimplencia,radar-simples}`). `Infrastructure.Local/Projections/ProjectionCatchUpHostedService.cs` virou `BackgroundService` periódico (`LocalDatabaseOptions.ProjectionCatchUpInterval`).

**F1 — fechamento (CMV da DRE, 2026-07):** `Financeiro.Application/ReadModels/DreGerencialService.cs` (agora injeta `IFatoCustoDiarioRepository` e soma o CMV realizado dali, em vez de `ContaAPagar` categorizado `CustoMercadoriaVendida`); `tests/SistemaX.Modules.Financeiro.Tests/DreGerencialServiceTests.cs` (novo — 4 casos, primeiro teste de `DreGerencialService` no repo).

**Referências externas a portar:** `saas-erp/app/api/financial/consultor/route.ts`, `saas-erp/lib/channels/media-enrichment.ts`, padrão `lib/agent/circuit-breaker`.
