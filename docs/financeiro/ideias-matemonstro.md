# Ideias do matemonstro para o Financeiro — o que vale importar (filtrado por "sem complicar")

> Pesquisa READ-ONLY (2026-07-17). Fonte de conhecimento: `~/development/matemonstro`, trilhas de
> **Finanças Aplicadas** (`data/curriculum/tracks/fin-*.json`) e **Quant Finance** (`quant-*.json`).
> Alvo: o motor quant do Financeiro do SistemaX (`src/Modules/Financeiro/**/Application/Quant/`,
> `.../ReadModels/`). Critério de corte, imposto pelo dono: **robusto SEM complicar** — só entra o que
> agrega valor real ao negócio (assistência técnica, 3 correntes) sem inflar UX nem exigir dado que
> não temos. Nada de código foi alterado. Divergência entre este doc e o código depois = bug de doc.

---

## Resumo executivo

O matemonstro tem **7 trilhas de finanças aplicadas** (gestão, contabilidade, investimentos, crédito,
matemática financeira, estatística, data science) e **10 de quant** (mercados, Itô, derivativos, Monte
Carlo, risco, séries, otimização, ML…). A trilha decisiva é **`fin-gestao` (Gestão Financeira do
Negócio)** — é literalmente o manual de quem toca um comércio: precificação markup/margem, custeio,
ponto de equilíbrio, caixa/capital de giro, estoque, unit economics, Simples e DRE gerencial.

A boa notícia: **o motor do SistemaX já cobre ~90% do que essas trilhas ensinam** (bandas de caixa,
runway, breakeven, roll-rate, Radar do Simples, DRE por corrente, MRR/churn/LTV, VPL/TIR/payback/ROI,
receita diferida, MDR). Sobraram **3 lacunas de alto valor e baixa complexidade** que encaixam limpo
em read-models que já existem: **(1)** enriquecer o ponto de equilíbrio com margem de segurança + GAO
+ PE econômico; **(2)** um cálculo de **preço-piso / margem real por forma de pagamento** (o "preço por
divisor" — o erro de precificação que a trilha chama de "o que mais quebra comércio"); **(3)** o sinal
**accruals = lucro − caixa** ("lucro não é caixa"), que aproveita as duas lentes que o sistema já tem.
Todo o resto ou já existe, ou está no catálogo quant, ou foi descartado por complicar.

**Achado que importa para a estratégia multi-vertical** (MEI/micro): o matemonstro **não ensina técnica
com marca de nicho** — não há "engenharia de cardápio (stars/dogs)", "food cost %", "ocupação de
cadeira" nem "coorte" pelo nome (busca literal: zero). Ele ensina os **primitivos universais** (markup,
MC, quadrante volume×margem, CMV%, ticket, MRR) dos quais cada métrica de vertical é só um **rótulo +
limiar**. Isso **valida o desenho do dono**: núcleo universal (3 correntes) + **lente opt-in por
vertical** (padrão `ConfiguracaoFinanceiraTenant` de `design-analise-por-projeto.md`) que só liga pelo
tipo de negócio, sem poluir quem não usa. As 3 ideias ADOTAR-AGORA foram escolhidas também por
**generalizarem** — servem vários dos 6 segmentos-alvo de uma vez (ver seção "Por vertical").

---

## O que as trilhas ensinam (contexto honesto)

As trilhas de finanças **não são rasas** — cobrem quant sério. Mas quase tudo que é *aplicável a um ERP
de assistência técnica* o SistemaX já tem. Mapa rápido trilha → estado no SistemaX:

| Trilha (matemonstro) | Conteúdo | Estado no SistemaX |
|---|---|---|
| `fin-gestao` | markup/margem, custeio, **ponto de equilíbrio**, caixa, estoque, unit economics, Simples, DRE | quase tudo feito; sobram MS/GAO/PE-econômico e preço-piso |
| `fin-contabilidade` | balanço, DRE, CCC/NCG, liquidez, endividamento, **DuPont**, **accruals** | DRE feito; CCC/NCG no catálogo (#5); accruals é lacuna barata |
| `fin-investimentos` | **VPL, TIR, payback, WACC, DCF, múltiplos** | VPL/TIR/payback/ROIC feitos (`design-imobilizado-roi.md`) |
| `fin-matematica` | juros compostos, PV/FV, SAC/Price, **taxa implícita de antecipação** | taxa de antecipação = catálogo #8 (Onda 2) |
| `fin-credito` | **EL = PD×LGD×EAD**, roll-rate, NPL, scoring, Basileia | roll-rate/PDD por aging feito (#3) |
| `fin-estatistica` | descritiva, Bayes, distribuições, **CV**, IC, testes A/B, regressão | CV → XYZ está no catálogo #12; binomial → #10 |
| `fin-dados` | SQL, pandas, **RFM**, média móvel/SES, MAPE, previsão | RFM = catálogo #17; forecast já é o bootstrap #1/#16 |
| `quant-risco` / `quant-montecarlo` / `quant-series` | **VaR/ES**, bootstrap, sazonalidade | bootstrap feito (#1); sazonalidade = #16; VaR = P5 já é CaR |

---

## Tabela priorizada de ideias

| Conceito (onde no matemonstro) | Onde encaixa no Financeiro | Valor | Complexidade | Veredito |
|---|---|---|---|---|
| **Margem de segurança + GAO + PE econômico** (`fin-gestao-03`: `MS=(V−V_pe)/V`, `GAO=MC/Lucro_op`, PE econômico soma lucro mínimo) | `PontoDeEquilibrioService.cs` + `BreakevenMensal.cs` (record `Resultado`) — endpoint `GET /financeiro/ponto-equilibrio` já existe | **Alto** | **Baixa** | **ADOTAR AGORA** |
| **Preço-piso / margem real por forma de pagamento** (`fin-gestao-01`: `PV=Custo/(1−%imp−%tax−%margem)`, preço-piso, canal) | novo `Application/Quant/PrecoPorDivisor.cs` (fn pura) + read-model observacional; insumos já existem (custo médio, `FormaDePagamento.TaxaPercentual`, alíquota efetiva do Radar, comissão de OS) | **Alto** | **Média** | **ADOTAR AGORA** |
| **Accruals = Lucro − FCO** ("lucro não é caixa", `fin-contabilidade` "Qualidade do Lucro" + `fin-gestao-04`) | fato/consultor cruzando DRE (competência) × `fato_caixa_diario` (caixa) — sinal do Consultor | **Alto** | **Baixa** | **ADOTAR AGORA** (casar com Fatia 6 / P1-3) |
| **PE financeiro** (breakeven de caixa, exclui depreciação — `fin-gestao-03`) | `PontoDeEquilibrioService` (subtrai depreciação dos custos fixos; depreciação já existe em `design-imobilizado-roi`) | Médio | Baixa | talvez depois |
| **Bridge de receita `N × Ticket × Frequência`** (`fin-gestao-06`) | narração do Consultor por corrente (depende de P0-1 corrente) | Médio | Baixa | talvez depois |
| **Índice de cobertura de juros / DSCR** (`fin-contabilidade` ICJ, `fin-credito` DSCR) | DRE (EBIT ÷ despesas financeiras) | Médio | Baixa | talvez depois (só se houver dívida estruturada) |
| **Liquidez corrente / endividamento geral** (`fin-contabilidade`) | balanço derivável da partida dobrada | Baixo p/ o dono | Média | talvez depois |
| **Identidade DuPont `ROE = margem × giro × alavancagem`** (`fin-gestao-08`, `fin-contabilidade`) | painel ROI (já tem ROIC) | Baixo | Média/Alta (exige balanço completo) | PULAR |
| **Fisher: crescimento real vs nominal** (`fin-matematica`, `fin-investimentos`) | séries de receita | Baixo | Baixa | PULAR (exige IPCA externo) |
| **Elasticidade / regressão para prever demanda** (`fin-estatistica`, `fin-dados`) | — | Baixo | Alta | PULAR (data-hungry, engana) |
| **VaR / ES / Sharpe** (`quant-risco`, `fin-investimentos`) | — | Baixo p/ 1 CNPJ | Alta | PULAR (a P5 já é caixa-em-risco) |
| **EL = PD×LGD×EAD, NPL, Basileia** (`fin-credito`) | inadimplência | — | — | PULAR (roll-rate #3 já resolve; resto é banco) |
| **EOQ / estoque de segurança / ponto de pedido** (`fin-gestao-05`) | Estoque | Médio | Média | PULAR aqui — **já é catálogo #13** |
| **Curva ABC, giro, cobertura, CCC/NCG, RFM, sazonalidade, antecipação, HHI** | vários | — | — | PULAR aqui — **já feito ou no catálogo** (#5,#8,#12,#16,#17,#18) |

---

## ADOTAR AGORA (detalhado)

### 1. Enriquecer o ponto de equilíbrio: margem de segurança + GAO + PE econômico

**De onde vem** — `fin-gestao-03` ("Ponto de Equilíbrio e Alavancagem Operacional"). A trilha ensina que
"zero a zero contábil não paga o dono nem gera caixa": além do PE contábil (que já temos), existem três
leituras clássicas que transformam o número num sinal de gestão.

**A matemática** (tudo derivável do que `PontoDeEquilibrioService` já calcula hoje):

- **Margem de segurança** — distância percentual entre a receita atual/projetada e o breakeven:
  `MS = (ReceitaProjetadaMês − ReceitaNecessária) / ReceitaProjetadaMês`. É a pergunta "quanto minha
  receita pode cair antes do prejuízo?". `ReceitaNecessária` já é a saída do `BreakevenMensal.Calcular`;
  a receita realizada do mês já é somada (`fato_receita_diaria` do 1º dia até hoje). **Zero dado novo.**
- **Grau de alavancagem operacional** — `GAO = MC / Lucro_op`, onde `MC = ReceitaProjetada × MC%` e
  `Lucro_op = MC − CustosFixos`. Mede quanto o lucro oscila por 1% de variação na receita — o "por que
  restaurante de aluguel caro sofre mais na crise". `MC%` e `CustosFixosMensais` já estão no `Resultado`.
- **PE econômico** — `ReceitaNecessária_econ = (CustosFixos + LucroMínimoDesejado) / MC%`. Soma o custo
  de oportunidade do dono ao custo fixo antes de dividir. O único insumo novo é o `LucroMínimoDesejado`
  (ou uma taxa de oportunidade × capital investido — que o painel ROI de `design-imobilizado-roi.md` já
  tem via aportes). É opcional e degrada para o PE contábil quando `LucroMínimo = 0`.

**Ponto de integração** — `src/Modules/Financeiro/.../ReadModels/PontoDeEquilibrioService.cs` e o record
`BreakevenMensal.Resultado`. São **três campos derivados a mais no mesmo record**, calculados no mesmo
`CalcularAsync` que já roda; o endpoint `GET /financeiro/ponto-equilibrio` só ganha 3 chaves. Uma função
pura em `BreakevenMensal` (`MargemDeSeguranca`, `Gao`, `BreakevenEconomico`) + casos em
`BreakevenMensalTests.cs`. **Nenhuma fact table, nenhum evento, nenhum fold novo.** É o encaixe mais
limpo do documento: enriquece um número que o dono já vê, sem tocar em infra.

> Invariante de teste: com `LucroMínimo = 0`, `BreakevenEconomico == BreakevenContábil`; com receita no
> breakeven exato, `MS == 0`; `GAO` indefinido (→ `null`) quando `Lucro_op ≤ 0`, nunca dividir por zero.

### 2. Preço-piso e margem real por forma de pagamento (o "preço por divisor")

**De onde vem** — `fin-gestao-01` ("Precificação: Markup e Margem"). A trilha é enfática: o erro que
"mais quebra comércio" é **aplicar markup sobre o custo quando imposto, taxa de cartão e comissão
incidem sobre o preço final**. O único cálculo correto é o **preço por divisor**:

```
PV = Custo / (1 − %impostos − %taxas − %comissão − %margem)
```

e o **preço-piso** é o mesmo com `%margem = 0`: o menor preço que ainda cobre custo e todos os custos
percentuais — o limite absoluto de qualquer desconto. A identidade markup⇄margem (`m = k/(1+k)`) fecha
a família.

**Por que é alto valor para ESTE CNPJ** — a auditoria (`revisao-domain-fit-cnpj.md` P1-6) já mostrou que
a corrente 3 (peças/varejo) é "cartão-pesado" e que o MDR (~3,5% no crédito) some do resultado. Um
produto com margem nominal de 8% vendido no crédito com 3,49% de MDR + alíquota efetiva do Simples +
comissão pode estar sendo vendido **abaixo do piso** — e ninguém vê. O mesmo vale para peça aplicada em
OS. Este é o lado *preventivo* (na hora de precificar/dar desconto) do que o motor hoje só mede
*depois* (na DRE/recebíveis).

**Ponto de integração** — os insumos **já existem todos no sistema**, o que torna isto barato apesar da
nota "média":

- custo → custo médio do Estoque (`saldo.CustoMedio`, já usado por `VendaItensMovimentadosHandler`);
- `%taxas` (MDR) → `FormaDePagamento.TaxaPercentual` (seed 1,39% débito / 3,49% crédito / 2% boleto);
- `%impostos` → alíquota efetiva de `RadarDoSimplesNacional.CalcularAliquotaEfetiva` (já implementada);
- `%comissão` → percentual do técnico de OS (config por tenant, citado em P1-7).

Novo `src/Modules/Financeiro/.../Application/Quant/PrecoPorDivisor.cs` — função pura
`Centavos × percentuais → { PrecoSugerido, PrecoPiso, MargemRealPorForma[] }`, com `PrecoPorDivisorTests.cs`.
Mantém a Lei 2 (observacional — informa, não age): é uma calculadora/sinal, não muda preço sozinho.
Superfície natural: um **fato do Consultor** "no crédito, este item rende só X% real — piso é R$ Y"
quando `MargemReal < limiar`, reusando o pipeline que já existe. **Não precisa de tela nova.**

> Cuidado de fronteira (a trilha alerta): somar os percentuais e multiplicar pelo custo (markup
> multiplicador) subestima o preço — o divisor é a única forma correta porque preço maior gera mais
> imposto e mais taxa. O teste deve travar `PV_divisor > PV_multiplicador` para os mesmos %.

### 3. Accruals: "lucro não é caixa" (Lucro − FCO)

**De onde vem** — `fin-contabilidade` ("Qualidade do Lucro e Red Flags": `Accruals = LL − FCO`) reforçado
por `fin-gestao-04` ("Empresa lucrativa quebra por falta de caixa"). O sinal: quando o lucro contábil
(competência) corre muito à frente do caixa operacional gerado, ou o negócio está inflando resultado
(estoque/recebível crescendo) ou vai ter aperto de caixa apesar do lucro no papel.

**Por que encaixa tão bem aqui** — o SistemaX se orgulha exatamente de ter **as duas lentes**
(`inteligencia-arquitetura.md`: "Caixa `MovimentoFinanceiro` vs. competência `ContaAReceber/Pagar` — as
duas lentes que todo modelo financeiro precisa"). Accruals é **uma subtração** de dois números que o
sistema já produz: `LucroLíquido` do `DreGerencialService` menos o fluxo de caixa operacional do
período. É o diagnóstico de maior sinal por menor custo do documento.

**A ressalva honesta (por isso "casar com Fatia 6")** — hoje `fato_caixa_diario` é **unilateral**
(`revisao-domain-fit-cnpj.md` P1-3: só entradas, sem baixas de conta), então o FCO ainda não é confiável.
A Fatia 6 já planejada (`ParcelaBaixada` → caixa bilateral) conserta isso. **Assim que P1-3 fechar**,
accruals vira um fato do Consultor de 3 linhas de código, sem nada novo além da subtração + um limiar.
Ponto de integração: `DreGerencialService` (já tem o lucro) + `FluxoDeCaixaService`/`fato_caixa_diario`
(o FCO), cruzados em `CrossModuleConsultorRules.cs`.

---

## Por vertical — a qual segmento cada ideia serve

Princípio de projeto (do dono): **o núcleo é universal, o vertical é uma lente opt-in**. A métrica que
cada nicho chama pelo "nome comercial" é, quase sempre, um primitivo que o SistemaX já calcula, só
**re-rotulado + um limiar** — ligado por `ConfiguracaoFinanceiraTenant.tipoNegocio`, invisível para
quem não marca. Por isso as 3 ideias ADOTAR-AGORA foram escolhidas pelo poder de generalização.

| Ideia ADOTAR-AGORA | 1. Vestuário / e-comm | 2. Restaurante / delivery | 3+6. Publicidade / BPO | 4. Beleza / estética | 5. Ensino / cursos |
|---|:--:|:--:|:--:|:--:|:--:|
| **#1 Margem de segurança + GAO + PE econômico** | ✅ | ✅✅ (GAO alto) | ✅ | ✅✅ (GAO alto) | ✅ |
| **#2 Preço-piso / margem real por forma** | ✅✅ (marketplace 12–20%) | ✅✅ (iFood 20–27%) | ➖ | ✅ (comissão) | ➖ |
| **#3 Accruals (lucro − caixa)** | ✅ | ✅ | ✅ | ✅✅ (pacotes pré-pagos) | ✅✅ (anuidade) |

✅✅ = fortalece de forma decisiva o vertical · ✅ = útil universal · ➖ = pouco relevante.

**Leitura por segmento (ligando ao que já existe + o que a lente opt-in relabela):**

1. **Comércio de vestuário/acessórios (loja + e-commerce).** É o vertical que o núcleo já serve melhor:
   curva ABC (#12), giro/cobertura (`estoque/calc.ts`), sazonalidade (#16) e margem por SKU/categoria
   (`fato_margem_produto`, #6) já existem. As 3 ideias somam: **#2** é o coração — markup×margem correto
   e **margem real por forma** quando marketplace/e-comm cobram 12–20% sobre o preço (a trilha
   `fin-gestao-01` cita exatamente esse caso); **#1** dá o "quanto posso cair" numa operação sazonal.
   Lente opt-in: "margem por SKU/categoria" é #6 re-rotulado, nada novo de motor.

2. **Restaurantes / alimentação / delivery.** Encaixe forte. **Food cost %** = CMV/venda — já é o
   `fato_custo_diario` da DRE (competência da venda), a lente só o exibe como % por categoria de
   cardápio. **Engenharia de cardápio (stars/dogs/puzzles/plowhorses)** = o **quadrante volume×MC do
   catálogo #6**, re-rotulado margem×popularidade — não precisa de matemática nova. **Ficha técnica** =
   o BOM que o Estoque já expande na baixa. **#2** é crítico aqui: o preço-piso com **comissão de
   delivery (iFood 20–27%)** no divisor evita vender no prejuízo — o caso-âncora literal da trilha.
   **#1 GAO** discrimina o restaurante de aluguel caro (a própria trilha usa esse exemplo).

3 e 6. **Publicidade/marketing e apoio administrativo/BPO.** Serviço recorrente por cliente/projeto: o
   MRR já existe e a **receita por cliente/projeto** já é o design opt-in `design-analise-por-projeto.md`
   (agregado `Projeto`, tagging `projetoId`). Aqui **#1** (margem de segurança do portfólio de contratos)
   e **#3** (accruals — contrato faturado por competência vs. caixa recebido) somam; **#2** é pouco
   relevante (não há custo de mercadoria/forma dominante). Nada novo de vertical — é o núcleo de serviço.

4. **Cabeleireiros / beleza / estética.** **Ocupação/produtividade (horas faturadas ÷ disponíveis)** =
   catálogo **#22** (ocupação/no-show da agenda) — a lente relabela para "produtividade da cadeira".
   **Receita por profissional** e **comissão** são a dimensão técnico/comissão que **#2** já consome no
   divisor (comissão como %-sobre-preço). **Pacotes/assinatura** = MRR + receita diferida (P1-5, já
   desenhado). **#1 GAO** de novo pega o negócio de alto custo fixo (aluguel + cadeiras); **#3** pega o
   pacote pré-pago (caixa entra antes da competência do serviço).

5. **Ensino / cursos / mentorias.** Mensalidade recorrente = MRR/churn/LTV (já temos, via hazard, bom
   para n pequeno). **Coorte de retenção** o matemonstro **não ensina pelo nome** (RFM em `fin-dados` é o
   mais próximo, = catálogo #17); é a única peça de nicho genuinamente ausente — e mesmo assim é
   extensão do que o hazard/#19 já faz, não motor novo. **#3 accruals** é forte para anuidade (recebe o
   ano inteiro, reconhece por mês — a receita diferida P1-5 é o mecanismo); **#1** dá a margem de
   segurança da base recorrente.

**Conclusão de generalização:** **#1** serve os 6 segmentos (todo negócio tem custo fixo e breakeven) e
**discrimina** os de alto custo fixo via GAO (restaurante, beleza). **#3** serve os 6 e brilha nos de
pré-pagamento (ensino, beleza, SaaS anual). **#2** cobre o cluster de %-sobre-preço (vestuário,
delivery, beleza) — onde o MDR/comissão/marketplace mais corrói margem. Nenhuma das 3 exige entidade
nova por vertical: são motor universal que a lente opt-in **re-rotula**, cumprindo o "sem complicar".

---

## Conscientemente PULADAS (registro)

- **Identidade DuPont (ROE = margem × giro do ativo × alavancagem)** — bonita, mas exige balanço
  patrimonial completo (ativo total, PL) e, para um CNPJ de dono único, decompor o ROE é análise de
  investidor externo, não decisão do dia. O painel ROI já entrega ROIC. Complica sem pagar.
- **Liquidez corrente / endividamento geral** — deriváveis da partida dobrada, mas para caixa o dono já
  tem ferramenta melhor (bandas P5/P50/P95 + runway). Índices estáticos de balanço servem a credor, não
  a operador. No máximo um KPI futuro, nunca prioridade.
- **Elasticidade-preço por regressão** — o simulador de desconto do catálogo (#9, `ΔQ = m/(m−d) − 1`) já
  responde "quanto preciso vender a mais para bancar o desconto" **sem estimar elasticidade**. Estimar a
  elasticidade real de vendas ruidosas é exatamente o tipo de coisa que engana e complica. Fica de fora.
- **VaR / Expected Shortfall / Sharpe** (`quant-risco`) — VaR de fluxo de caixa é conceitualmente a
  banda P5 que o motor já calcula (caixa-em-risco). Sharpe pede uma carteira. Formalizar VaR/ES aqui é
  vocabulário acadêmico sobre um número que já existe.
- **EL = PD×LGD×EAD, NPL, cobertura, Basileia** (`fin-credito`) — o roll-rate/PDD por aging (#3) já dá a
  provisão que o negócio precisa. LGD/EAD/Basileia são régua de banco, não de assistência técnica.
- **DCF / valuation por múltiplos / WACC** (`fin-investimentos`) — o painel de ROI/imobilizado já usa
  VPL/TIR/payback descontado com uma taxa de desconto. Valuation de empresa inteira é fora de escopo.
- **Fisher (real vs nominal), taxas equivalentes, SAC/Price** (`fin-matematica`) — corretos, mas ou
  exigem um índice de inflação externo (IPCA) que o sistema não guarda, ou são mecânica de financiamento
  que o ERP não origina.
- **EOQ, estoque de segurança, ponto de pedido, curva ABC×XYZ, RFM, sazonalidade, HHI, antecipação,
  CCC/NCG** — todos bons e todos **já no catálogo quant** (#5, #8, #12, #13, #16, #17, #18) ou já
  implementados (giro/cobertura/ABC). Não são ideias novas do matemonstro; são confirmação de que a
  priorização existente está alinhada com o que a trilha ensina.

---

*Gerado por pesquisa cruzada matemonstro × SistemaX em 2026-07-17. Read-only. As 3 ideias "ADOTAR
AGORA" foram escolhidas por serem, simultaneamente, alto valor + baixa complexidade + encaixe em
read-model existente + zero dado novo — os quatro filtros do "robusto sem complicar".*
