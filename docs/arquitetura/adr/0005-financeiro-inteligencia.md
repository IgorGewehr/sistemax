# ADR-0005 — Financeiro como camada de inteligência quant: ledger de eventos consultável + folds determinísticos + LLM só como narrador

**Status:** Aceito · **Data:** 2026-07-17 · **Contexto do produto:** o Financeiro deve virar a camada de inteligência do negócio, alimentada por **todos** os módulos (Vendas, Estoque, Compras, Fiscal, OS, Agenda), com o Super Consultor gerando conselhos claros para leigos a custo marginal ~zero. Detalhamento completo em `docs/financeiro/inteligencia-arquitetura.md`.

## Pergunta que este ADR responde
> "Como o Financeiro vira inteligente — que dado guardamos, quem calcula o quê, e onde entra a IA?"

**Resposta curta:** **persistimos o log de eventos de integração (hoje ele evapora); toda análise é um fold determinístico C# desse log para fact tables; o LLM nunca calcula — só reescreve fatos numéricos prontos em frases, com teto de custo provado de < R$1/CNPJ/mês.** O ADR-0001 prometeu "read-model = fold do log"; este ADR paga essa promessa — hoje o sistema é state-sourced com eventos em trânsito, e séries históricas (ex.: "MRR em 1º de março") são irreconstruíveis.

## Decisão

1. **Ledger de eventos como cidadão de 1ª classe (a peça nº 1, antes de qualquer análise).** Tabela `integration_events` append-only (ULID pk, `tipo`, `tenant_id`, `payload_json`, `ocorrido_em`, `chave_idempotencia UNIQUE`), gravada **na mesma transação SQLite** do commit de origem (plumbing já demonstrado no outbox). O bus vira **persist-then-dispatch**. O outbox CDC de entidade **não** substitui isso: "UPDATE em assinaturas" ≠ "AssinaturaCancelada" — análise quant precisa de fatos de negócio, não de log de replicação de estado. Cada dia sem o ledger é história perdida para sempre.

2. **Projeções com cursor + fact tables reprocessáveis.** `projection_state (nome, ultimo_ulid_processado)`; folds constroem tabelas achatadas (`fato_caixa_diario`, `fato_receita_diaria`, `fato_margem_produto`, `fato_recebiveis`, ...). **Nova análise = novo fold + replay** — o passado nunca é migrado, só re-derivado; fact table com bug se corrige com `DROP` + replay. Read-models atuais migram quando tocados, sem big-bang.

3. **Eventos ganham dimensão por companions aditivos** (padrão aditivo-only já vigente): `ClienteId`/canal/desconto em `VendaConcluida`; Vendas passa a publicar `VendaItensMovimentados`; novo `CustoBaixadoPorVenda` (Estoque→Financeiro) corrige o CMV — hoje `CompraRecebida` é classificada como custo, o que distorce margem; handlers passam a usar `FormaDePagamento.PrazoCompensacao/Taxa` (MDR e lag D+30 no caixa projetado); bucketing por **timezone do tenant**, não UTC.

4. **Toda matemática é determinística em C#** (`Money` centavos-inteiros; estimadores fechados — EWMA, bootstrap com seed fixa `tenantId+período`, roll-rate/Markov, Kaplan-Meier, índices sazonais, Little, binomial exata; zero ML treinado). Funções puras `Centavos[] → resultado` com teste por tela (padrão `calc.ts`). Priorização do catálogo (22 análises, valor-ao-leigo × viabilidade): **motor base** = sazonalidade (#16) + margem de contribuição por item (#6); **quick-wins fase 1** = previsão de caixa com bandas P5/P50/P95 (#1), runway (#2), score de inadimplência com roll-rate (#3), Radar do Simples Nacional (#4), breakeven day (#7). Ondas seguintes: lucro/estoque, clientes/HHI, verticais OS/agenda.

5. **LLM é redator, não analista (Lei 2).** Contrato `IConsultorFactProvider`/`ConsultorFato` em `Modules.Abstractions/Consultor/` — cada módulo registra o seu via DI (R5, zero `if(vertical)`); regras compostas cross-módulo são código (`CrossModuleConsultorRules.cs`), não LLM. Pipeline diário idempotente no Host.Desktop: coleta → rank por score (impacto em centavos × severidade × novidade) → top-8 → `Cloud.Api /api/consultor/narrate` (a key OpenAI vive só na nuvem; **dado cru jamais sai da máquina** — só facts pré-formatados tipo `"R$ 4.200,00"`). Cache por `sha256(facts)`, budget mensal por tenant, validação anti-alucinação (frase deve conter cada número dos facts; reprova → template). **Falha nunca vira erro na UI**: pior caminho é sempre `source: 'template'`. Painel "Ver como calculamos" vem dos facts, nunca do LLM.

6. **Teto de custo provado:** gpt-4o-mini, 1 chamada batch/dia (~1.900 tok in / ~650 out). Nominal **R$0,11/mês**; pessimista com re-runs e relatório semanal em gpt-4.1 **≈ R$0,78/mês**. Budget duro de ~300 narrates/mês (≈R$1,10) é circuit-breaker de anomalia, não restrição de produto — sobra ~R$14–19/CNPJ para chat futuro.

7. **Extensibilidade em três portas** (para novas análises quant, sem as sete camadas atuais):
   - **Notebook**: SQLite local aberto read-only (WAL) para DuckDB/pandas + export parquet — hipóteses nascem aqui, zero C#;
   - **Séries**: endpoint genérico `/financeiro/series?metrica=&bucket=&de=&ate=` sobre as fact tables — mesma superfície serve humano e, futuramente, o Consultor como tool;
   - **Produto**: fold novo (cursor + replay) + função pura + testes; opcionalmente uma rule no `IConsultorFactProvider` — pipeline, ranking, narração, cache e UI pegam de carona, zero mudança em infra.

## Por que NÃO "LLM que analisa os dados"
Inferência analítica por LLM é cara, não-determinística, não-auditável e alucina número — inaceitável para conselhos financeiros a leigos. Invertendo (C# calcula, LLM narra, validação rejeita frase sem os números exatos), o custo cai ~3 ordens de grandeza, todo resultado é reprodutível (seed fixa) e o painel "Ver como calculamos" é sempre verdadeiro. Pelo mesmo motivo, **não** adotamos leitura direta de tabelas alheias como insumo de análise: insumo é evento idempotente (R3), senão o histórico continua irreconstruível e cada análise re-acopla módulos.

## Consequências
- **(+)** O ADR-0001 §3 deixa de ser aspiracional: histórico completo do negócio; qualquer análise futura é um fold; perguntas as-of-date passam a ser respondíveis (do deploy em diante).
- **(+)** CMV/margem economicamente corretos; caixa projetado com MDR e lag real; séries diárias sem ruído UTC.
- **(+)** Consultor inteligente com custo desprezível, privado (facts, nunca dado cru) e à prova de falha (template como piso).
- **(+)** Igor pluga análises novas de quant finance por notebook → séries → produto, cada etapa opcional.
- **(−)** Mais engenharia inicial: tabela de eventos + cursores + companions dimensionais antes das análises "brilhantes" (Fase 0 do roadmap). É o preço de não perder história.
- **(−)** História anterior ao ledger não existe — séries longas só amadurecem com o tempo; read-models atuais seguem como ponte.

## Estado atual no repo
`InProcessIntegrationEventBus` entrega e descarta (nenhum replay possível); read-models fazem fold sobre agregados; `VendaConcluida` carrega só total+forma; DRE trata compra como custo; consultor da UI é mock (`FLUXO_CAIXA_MOCK`, 7 cards template). Sólido e intocável: `LancamentoContabil` (partida dobrada imutável), `Money`, idempotência tripla, caixa×competência ligados por `ParcelaId`, ULID em tudo. Roadmap em 5 fases (0: ledger; 1: motor base + quick-wins; 2: Consultor real; 3: ondas 2–3; 4: verticais + chat) em `docs/financeiro/inteligencia-arquitetura.md`. Referência de implementação do narrador: `saas-erp/app/api/financial/consultor/route.ts`.
