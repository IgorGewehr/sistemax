# Financeiro SistemaX — Plano de Arquitetura de Informação e Design de Tela

> **Escopo:** plano de design (não código) para o módulo Financeiro do SistemaX.
> **Missão:** dar ao dono leigo a clareza de um consultor financeiro sênior, sem parecer complexo.
> **Base:** `docs/financeiro/financeiro-features.md`, `financeiro-ux.md`, `financeiro-datamodel.md`,
> `docs/arquitetura/ARCHITECTURE.md` e o mockup aprovado `mockups/financeiro-assinaturas.html`.
> **Decisões do dono respeitadas integralmente:** 6 abas; E&S=fonte+futuro / Bancário=realizado-banco /
> Fluxo de Caixa=realizado-espécie; Visão Geral=santo graal sem cockpit; Relatórios=export;
> inteligência permeando as telas com IA só na linha de conselho.

---

## 0. A linguagem do mockup aprovado (o que TODA tela herda)

O mockup de Assinaturas define a gramática visual e de interação. Nenhuma tela nova inventa
gramática própria — todas compõem estes 7 blocos, nesta hierarquia:

| # | Bloco | Papel | Regra |
|---|---|---|---|
| 1 | **Header** (eyebrow · h1 · sub) | Diz onde estou e qual pergunta a tela responde | O `sub` é sempre a pergunta em português de rua ("Seus softwares, os clientes que pagam por eles, e o que isso vale de verdade.") |
| 2 | **Tabs** do Financeiro | Navegação de 1º nível | Ordem fixa: Visão geral · Entradas & saídas · Recorrentes · Bancário · Fluxo de caixa · Relatórios |
| 3 | **Subhead** (segmented + ctx-note) | Lentes dentro da aba | Segmented só quando há 2+ lentes reais (ex.: Contas fixas ⇄ Assinaturas). A `ctx-note` explica visibilidade condicional |
| 4 | **KPI row** (1 hero + 2-3 apoio) | O resumo antes do detalhe | Hero tem sparkline e gradiente radial; todo KPI tem delta com direção e cor semântica; nunca mais de 4 |
| 5 | **Linha do Super Consultor** | A ÚNICA linha de IA da tela | 1 card, borda gradiente, ≤2 frases com números interpolados + 1 prioridade. Motor de regras + template — nunca LLM por render |
| 6 | **Par interativo `grid2`** | O drill/dataviz — a análise no lugar de texto | Card esquerdo = índice clicável (barras). Clique → **os dois cards trocam juntos**: esq vira gráfico temporal do item, dir vira stat tiles do item. Botão ← volta. Animação `cardin` 220ms, sem mudar de rota |
| 7 | **Tabela(s)** | Os dados crus, onde se age item a item | Sempre o último bloco. Chips de status, hover, linha cancelada com strike |

Tokens: vermelho-600 (`--primary`) reservado a marca/hero/CTA primário; `--pos` verde para entrada/sobra,
`--crit` para saída/falta/risco, `--warn` para atenção; números em mono tabular (`.num`);
`rounded-xl/2xl`; dark/light via tokens HSL com toggle `data-theme`; sem blur em exit (regra GPU).

---

# A. Arquitetura de Informação final

## A.1 Uma aba, um trabalho

| Aba | O trabalho (job-to-be-done) | Regime | Janela de tempo | Escreve? |
|---|---|---|---|---|
| **Visão Geral** | "Estou bem? Vai faltar? O que faço agora?" — agrega e aponta, nunca gerencia | Ambos, traduzidos | Hoje + 30 dias | Não |
| **Entradas & Saídas** | A fonte + o futuro: tudo que entrou/saiu E o que ainda vem (a receber/a pagar). Onde se planeja, lança e dá baixa | Competência + previsto | Passado + futuro | Sim (lançar, dar baixa, editar) |
| **Recorrentes** | O que se repete: compromissos fixos (saída) e receita recorrente (lente Assinaturas, condicional) | Competência (templates) | Futuro previsível | Sim (recorrência, assinatura) |
| **Bancário** | O realizado que passou pelo BANCO (PIX, cartão, TED). Extrato + conciliação: "o mundo real bate com o sistema?" | Caixa (contas tipo banco) | Passado | Só conciliar/importar |
| **Fluxo de Caixa** | O realizado em ESPÉCIE: ritual diário da gaveta — abertura, fechamento, troco, sangria, diferença | Caixa (contas tipo caixa) | Passado (dia a dia) | Só abrir/fechar/sangria |
| **Relatórios** | Documentos formais para contador/sócios: DRE, extratos, fechamento mensal — PDF/Excel | Escolhível (toggle explícito) | Mês/período fechado | Não (gera arquivos) |

**Regra anti-repetição (a mais importante da IA):** todo número tem **uma aba dona**. Quando aparece
em outra aba, é read-only + link para a dona. Ex.: "a receber atrasado" é *dono* = E&S; na Visão Geral
ele só aparece se o Consultor o promover a prioridade, com CTA "→ Entradas & saídas". Saldo bancário é
*dono* = Bancário; na Visão Geral entra somado dentro do hero "Você pode tirar até". É isso que impede
as 3 telas parecidas de renascerem.

## A.2 A distinção E&S / Bancário / Fluxo de Caixa — confirmada, com 3 refinamentos

A distinção decidida é correta e mapeia 1:1 no domínio:

```
                       ┌─ previsto ──────────────► ContaAPagar/ContaAReceber + Parcela (aberto/atrasado)
  Entradas & Saídas ───┤
  (tudo + previsto)    └─ realizado (todas contas) ► MovimentoFinanceiro (qualquer ContaBancariaCaixa)

  Bancário ────────────── realizado, conta.tipo = banco ─► MovimentoFinanceiro + Conciliacao
  Fluxo de Caixa ──────── realizado, conta.tipo = caixa ─► MovimentoFinanceiro + SessaoCaixa
```

Bancário e Fluxo de Caixa são **a mesma família de read-model ("realizado por conta") com lentes e
rituais diferentes** — isso é uma economia de engenharia, não um argumento pra fundi-los (ver A.4).

**Refinamento 1 — vocabulário blindado contra a colisão de "fluxo de caixa".** No jargão, "fluxo de
caixa" = projeção; na aba decidida = dinheiro físico. Mantemos o nome da aba (decisão do dono), mas:
(a) o `sub` da aba diz sempre "Dinheiro em espécie — abertura, fechamento, troco e sangria";
(b) a projeção NUNCA é chamada de "fluxo de caixa" em nenhuma UI — chama-se **"Linha do tempo do
caixa"** (Visão Geral) e **"Como fecha o mês"** (E&S). Sem isso, o usuário clica na aba errada pra
sempre.

**Refinamento 2 — visibilidade condicional.** Negócio sem conta do tipo `caixa` (ex.: consultoria
100% PIX) não vê a aba Fluxo de Caixa — mesmo mecanismo da lente Assinaturas (`ctx-note`: "Visível
porque este negócio opera caixa em espécie"). A aba aparece no primeiro cadastro de conta-caixa ou
sessão de PDV. Isso mantém as 6 abas sem parecer cockpit pra quem só usa 4.

**Refinamento 3 — a ponte entre as três é a AÇÃO "dar baixa".** Dar baixa numa Parcela (em E&S)
obrigatoriamente pergunta "entrou/saiu por qual conta?" → cria o `MovimentoFinanceiro` que
imediatamente aparece em Bancário ou Fluxo de Caixa. O usuário aprende a distinção usando, não lendo:
E&S é onde a promessa vira fato; Bancário/Caixa é onde o fato é conferido.

## A.3 Aba vs sub-view — análise honesta

**Analisado: fundir Bancário + Fluxo de Caixa numa aba "Contas" com seletor.** Rejeitado por 3 razões:
1. **Personas e rituais diferentes.** Fluxo de Caixa é ritual diário do operador (abrir 8h, sangria,
   fechar e contar); Bancário é conferência semanal do dono (conciliar extrato). Fundir recria
   exatamente a sobreposição confusa que a decisão nº 2 acabou de desfazer.
2. **Estados diferentes.** Fluxo de Caixa tem máquina de estados própria (sessão aberta/fechada,
   diferença contado×esperado) que não existe no Bancário; Bancário tem conciliação (OFX/Open Finance)
   que não existe na gaveta.
3. **Condicionalidade resolve o custo.** O único argumento pró-fusão ("aba a mais") desaparece com o
   Refinamento 2 — quem não tem gaveta não vê a aba.

**Analisado: Assinaturas como aba própria.** Confirmado como **lente de Recorrentes** (decisão do
dono, já aprovada no mockup via segmented "Contas fixas ⇄ Assinaturas"): é o mesmo job ("o que se
repete") olhado do lado da receita, e a condicionalidade por vertical já está resolvida ali.

**Analisado: onde vive "Lançar".** Não é aba: é **FAB global ⊕** presente em todas as telas do módulo
(padrão do doc de UX, mantido). O lançamento grava na fonte (E&S); OCR/foto e "isso se repete todo
mês" (gancho pra Recorrentes) vivem no sheet do FAB.

## A.4 Mapa de navegação

```
                                 ┌───────────────────────────────────────────┐
                                 │              VISÃO GERAL                   │
                                 │  (agrega e aponta — nunca gerencia)        │
                                 └──┬─────────┬─────────┬─────────┬──────────┘
              hero "pode tirar"     │         │         │         │   linha do consultor
              clica na decomposição │         │         │         │   CTA → aba dona do problema
                                    ▼         ▼         ▼         ▼
                      ┌──────────────┐ ┌───────────┐ ┌──────────┐ ┌───────────────┐
   FAB ⊕ Lançar ────► │ ENTRADAS &   │ │RECORRENTES│ │ BANCÁRIO │ │ FLUXO DE CAIXA │
   (global, grava     │ SAÍDAS       │ │ fixas ⇄   │ │ extrato+ │ │ sessões da     │
    na fonte)         │ fonte+futuro │ │ assinatura│ │ concilia │ │ gaveta         │
                      └──────┬───────┘ └───────────┘ └────▲─────┘ └──────▲────────┘
                             │  "dar baixa" numa Parcela cria │              │
                             └── MovimentoFinanceiro ─────────┴──────────────┘
                                        (a ponte previsto → realizado)

   RELATÓRIOS ◄─── recebe "exportar" de qualquer tela (mesmo período/filtro pré-aplicado)
```

**Regras de navegação:**
- **Drill fica na tela.** Clicar em barra/dia/linha troca o par `grid2` in-place (padrão do mockup) —
  nunca muda de aba sem intenção explícita.
- **Cross-tab só por CTA nomeado** ("Ver no extrato →", "Cobrar →"), sempre levando filtro/período
  aplicado via query param (ex.: E&S aberto já filtrado em "atrasados").
- **Exportar de qualquer lugar** leva a Relatórios com o documento certo pré-selecionado — os insights
  não vivem lá, mas todo insight é exportável de onde nasce.

---

# B. Tela por tela

> A Visão Geral está especificada em profundidade na seção C. Aqui: as outras cinco.
> Formato por tela: trabalho único · dados que importam · wireframe · drills · insight vs export ·
> read-models que o backend fornece.

---

## B.1 Entradas & Saídas — "a fonte + o futuro"

**Trabalho único:** ver tudo que já entrou/saiu E tudo que ainda vai (a receber/a pagar em aberto),
num só lugar, em regime de competência com projeção. É onde se lança, se planeja e se dá baixa.
`sub`: *"Tudo que entrou, saiu — e o que ainda vem. É aqui que você planeja."*

**Os 4 dados que importam:**
1. **A receber em aberto** (com o atrasado destacado em `--crit`) — o dinheiro que é seu e não chegou.
2. **A pagar em aberto** — os compromissos assumidos.
3. **Resultado do mês (competência)** — vendeu − gastou, pago ou não. Rótulo: "Resultado do mês",
   com "(ⓘ competência: conta o que foi vendido/gasto, mesmo sem o dinheiro ter mudado de mão)".
4. **Como fecha o mês** (projeção): realizado até hoje + parcelas abertas do mês + recorrências → um
   número: "+R$ 2.100 se todo mundo pagar em dia".

**Dado game-changer da tela:** *onde o dinheiro está indo* — composição de despesas por categoria com
variação contra a própria média (o "gasto que subiu sem ninguém perceber") + aging de recebíveis.

**Wireframe:**

```
┌────────────────────────────────────────────────────────────────────────────────┐
│ FINANCEIRO · ENTRADAS & SAÍDAS                              Julho 2026 ▾  ⊕ Lançar
│ Tudo que entrou, saiu — e o que ainda vem. É aqui que você planeja.             │
│ [Visão geral][Entradas & saídas][Recorrentes][Bancário][Fluxo de caixa][Relatórios]
│ seg: [Tudo] [A receber] [A pagar]                                               │
├────────────────────────────────────────────────────────────────────────────────┤
│ ┌KPI hero────────────┐┌KPI───────────────┐┌KPI───────────────┐┌KPI────────────┐│
│ │ A RECEBER EM ABERTO ││ A PAGAR EM ABERTO ││ RESULTADO DO MÊS ││ COMO FECHA O  ││
│ │ R$ 9.430  ~sparkline││ R$ 7.210          ││ R$ 4.230 ▲12%    ││ MÊS (projeção)││
│ │ R$1.890 atrasado 🔴 ││ maior: Folha 30/07││ competência (ⓘ)  ││ +R$ 2.100     ││
│ └────────────────────┘└──────────────────┘└──────────────────┘└───────────────┘│
│ ┌💡 Consultor────────────────────────────────────────────────────────────────┐ │
│ │ Gasto com Fornecedores subiu 42% vs sua média — 3 lançamentos explicam      │ │
│ │ R$ 1.870 disso. [Ver os 3 →]                                                │ │
│ └────────────────────────────────────────────────────────────────────────────┘ │
│ ┌grid2 esq───────────────────────────┐ ┌grid2 dir──────────────────────────┐   │
│ │ PARA ONDE FOI O DINHEIRO (mês)     │ │ RAIO-X DO MÊS                     │   │
│ │ Folha        ████████████ R$ 4.900 │ │ ┌ Fixo vs variável: 61% / 39% ┐   │   │
│ │ Fornecedores ███████ R$ 3.100  ←clique │ ┌ Quem mais subiu: Fornec. +42%┐  │   │
│ │ Aluguel      █████ R$ 2.100       │ │ ┌ Atrasados >30d: R$ 1.240 (2) ┐  │   │
│ │ Impostos     ███ R$ 1.240         │ │   (tiles trocam no drill)        │   │
│ │  ⇄ drill: categoria · 6 meses      │ │  ⇄ drill: tiles da categoria     │   │
│ │  (colunas + top lançamentos)       │ │  (média 6m, maior lançamento,    │   │
│ └────────────────────────────────────┘ │   % do total)                    │   │
│                                        └──────────────────────────────────┘   │
│ ┌tabela: LINHA DO TEMPO───────────────────────────────────────────────────────┐│
│ │ 28/07  Folha julho            Folha        [previsto]      −R$ 4.900 [baixa]││
│ │ 22/07  Recebimento Maria      Serviços     [previsto]      +R$   890 [baixa]││
│ │ ────────────────────────────── HOJE · 16/07 ────────────────────────────────││
│ │ 15/07  Distribuidora Sul      Fornecedores  pago · Itaú    −R$   347        ││
│ │ 14/07  PIX Mercado São João   Serviços      pago · Itaú    +R$   349        ││
│ │ 12/07  Mensalidade João       Serviços      ATRASADO 12d 🔴 +R$   620 [cobrar]│
│ └─────────────────────────────────────────────────────────────────────────────┘│
└────────────────────────────────────────────────────────────────────────────────┘
```

**Drills:**
- Barra de categoria → par troca: esq = colunas 6 meses daquela categoria (com linha da média em
  tracejado); dir = tiles (média 6m · maior lançamento do mês · % do total). ← volta.
- Tile "Atrasados >30d" → tabela filtra em atrasados (sem trocar de aba) com ação [cobrar] por linha
  (dispara régua WhatsApp).
- Linha futura da tabela → [dar baixa] abre modal "entrou/saiu por qual conta?" (a ponte de A.2).
- Linha realizada → detalhe do lançamento (categoria, conta, conciliação, origem: venda/OS/pedido).

**Insight vs export:** insight = composição por categoria com variação + aging (na tela). Export =
"extrato do período" e "contas em aberto" → CTA "Exportar →" leva a Relatórios com filtro aplicado.

**Read-models (backend):**

| Read-model | Responde | Origem no domínio |
|---|---|---|
| `ExtratoUnificado(período, lente)` | linha do tempo passado+futuro | merge `MovimentoFinanceiro` (realizado) + `Parcela` status aberto/atrasado (previsto), ordenado por data, com `origem` (sourceRef) e conta |
| `ResumoPorCategoria(mês)` | pra onde foi + quem subiu | Σ por `Categoria` de Parcelas com competência no mês + variação vs média móvel 3-6m; flag fixo/variável vem do mapeamento Categoria→LinhaDRE |
| `AgingRecebiveis` | quem me deve há quanto tempo | `Parcela` (ContaAReceber) atrasadas em buckets 0-15/15-30/+30d, com clienteId |
| `ResultadoDoMes` | vendeu − gastou (competência) | Σ ContaAReceber − ContaAPagar por `dataCompetencia` no mês |
| `ProjecaoFimDeMes` | como fecha | realizado até hoje + Parcelas abertas com vencimento no mês + `Recorrencia` projetada |

---

## B.2 Recorrentes — "o que se repete" (fixas ⇄ Assinaturas)

**Trabalho único:** os compromissos e as receitas que se repetem. Duas lentes no segmented:
**Contas fixas** (todo negócio) e **Assinaturas** (condicional a quem vende serviço recorrente — já
aprovada no mockup, não redesenhar). `sub` (fixas): *"O que custa existir todo mês — antes de vender
qualquer coisa."*

**Os 4 dados que importam (lente Contas fixas):**
1. **Custo de existir**: Σ recorrências de saída normalizadas pro mês — "R$ 11.240/mês".
2. **A tradução acionável**: "= R$ 375 por dia útil só pra abrir as portas" (a semente do ponto de
   equilíbrio, sem o jargão).
3. **Variação**: quanto o fixo cresceu vs mês anterior/6m — o "compromisso que engordou em silêncio".
4. **Peso sobre a receita**: % da receita média comprometida com fixo (>60% = `--warn`).

**Dado game-changer:** o **histórico de valor de cada recorrência** — detecta degrau ("a luz veio 22%
acima da sua média de 6 meses") sem o dono comparar boleto a boleto. É o espelho exato do drill de
Assinaturas: lá a pergunta é "onde a receita recorrente vaza (churn)"; aqui é "qual compromisso fixo
está inchando".

**Wireframe (lente Contas fixas):**

```
│ seg: [Contas fixas] [Assinaturas]        ⓘ Assinaturas visível porque vende serviço recorrente
├────────────────────────────────────────────────────────────────────────────────┤
│ ┌KPI hero────────────────┐┌KPI──────────────┐┌KPI──────────────┐┌KPI──────────┐│
│ │ CUSTO DE EXISTIR        ││ VS MÊS PASSADO   ││ PESO NA RECEITA ││ PRÓXIMA GRANDE││
│ │ R$ 11.240/mês ~sparkline││ +R$ 340 (+3,1%) ││ 46% ⓘ           ││ Folha · 30/07 ││
│ │ = R$ 375/dia útil       ││ luz e internet   ││ da receita média││ R$ 4.900      ││
│ └────────────────────────┘└─────────────────┘└─────────────────┘└──────────────┘│
│ ┌💡 Consultor: A conta de luz veio 22% acima da sua média de 6 meses (R$ 618    │
│ │ vs R$ 507). Se virou o novo normal, seu custo fixo sobe R$ 1.330/ano. [Ver →] │
│ ┌grid2 esq────────────────────────────┐ ┌grid2 dir─────────────────────────┐    │
│ │ SEUS COMPROMISSOS FIXOS             │ │ RETRATO DO FIXO                  │    │
│ │ Folha     █████████████ R$ 4.900    │ │ ┌ Projeção anual: R$ 134.880 ┐   │    │
│ │ Aluguel   ██████ R$ 2.100    ←clique│ │ ┌ Cresceu 11% em 6 meses     ┐   │    │
│ │ Luz       ██ R$ 618 ⚠ +22%          │ │ ┌ 9 compromissos ativos      ┐   │    │
│ │ Internet  █ R$ 210                  │ │  ⇄ drill: tiles da recorrência   │    │
│ │  ⇄ drill: valor da recorrência,     │ │  (média 12m, desde quando,       │    │
│ │  12 meses (colunas + linha média)   │ │   Σ pago no ano)                 │    │
│ └─────────────────────────────────────┘ └──────────────────────────────────┘    │
│ ┌tabela: TODAS AS RECORRÊNCIAS────────────────────────────────────────────────┐ │
│ │ Nome · Categoria · Valor/mês · Dia · Próxima · Variação vs média · Status   │ │
│ └─────────────────────────────────────────────────────────────────────────────┘ │
```

**Drills:** barra da recorrência → esq = colunas 12m do valor + linha tracejada da média (degraus
saltam à vista); dir = tiles (média 12m · ativa desde · total pago no ano). Lente Assinaturas = mockup
aprovado (MRR por serviço → divergente Novos×Churn + tiles de retenção), intocada.

**Insight vs export:** insight = degrau de valor + peso sobre receita. Export = "compromissos fixos"
(anexo do fechamento mensal em Relatórios); Assinaturas exporta "relatório MRR" (condicional).

**Read-models:**

| Read-model | Responde | Origem |
|---|---|---|
| `CompromissosFixosMensal` | custo de existir | `Recorrencia` (saída) ativas, valor normalizado pro mês (semanal×4,33 etc.) |
| `HistoricoValorRecorrencia(id)` | degrau/inchaço | Parcelas geradas pela Recorrencia, últimos 12m, + média móvel |
| `PesoFixoSobreReceita` | % comprometido | Σ fixas ÷ receita média 3m (ContaAReceber por competência) |
| `AssinaturasOverview` | MRR, churn, novos, ARR, retenção | `Assinatura` (Recorrencia de entrada com clienteId+serviçoId) + Parcelas: série 6m novos×churn, tempo médio de casa, LTV, retenção 12m — exatamente os dados do mockup |

---

## B.3 Bancário — "o que passou pelo banco — e bate?"

**Trabalho único:** extrato consolidado do realizado em contas tipo banco (PIX, cartão, TED) +
conciliação. Hoje é o grosso do dinheiro. `sub`: *"O que de fato entrou e saiu das suas contas —
e se bate com o que você lançou."*

**Os 4 dados que importam:**
1. **Saldo em bancos** (por conta e total) — derivado de `MovimentoFinanceiro`, nunca armazenado.
2. **Entrou × saiu no mês** (realizado puro).
3. **Pendências de conciliação** — o nº de itens que não batem (a métrica de confiança de TODOS os
   outros números do módulo).
4. **Custo invisível dos meios de pagamento** — quanto as maquininhas/taxas levaram no mês.

**Dado game-changer:** a **conciliação em 3 baldes** (bateu / sobrou no banco / sobrou no sistema) —
onde aparecem venda não lançada, taxa errada e inadimplência real — com ação de 1 clique por item.

**Wireframe:**

```
│ chips de conta: [Todas] [Itaú PJ] [Nubank PJ] [Stone]        [Importar OFX] [Conectar banco]
├────────────────────────────────────────────────────────────────────────────────┤
│ ┌KPI hero────────────┐┌KPI──────────────┐┌KPI──────────────┐┌KPI───────────────┐│
│ │ SALDO EM BANCOS     ││ ENTROU NO MÊS    ││ SAIU NO MÊS     ││ A CONCILIAR      ││
│ │ R$ 12.740 ~sparkline││ R$ 38.240        ││ R$ 31.480       ││ 7 itens ⚠        ││
│ │ 3 contas            ││ 214 movimentos   ││ 89 movimentos   ││ R$ 2.310 em dúvida││
│ └────────────────────┘└─────────────────┘└─────────────────┘└──────────────────┘│
│ ┌💡 Consultor: As maquininhas levaram R$ 1.184 este mês (3,1% do que passou     │
│ │ por elas). No crédito parcelado a taxa efetiva foi 4,8%. [Ver por forma →]    │
│ ┌grid2 esq────────────────────────────┐ ┌grid2 dir─────────────────────────┐    │
│ │ ENTROU × SAIU POR SEMANA (realizado)│ │ CONCILIAÇÃO                      │    │
│ │      ▲verde: entradas               │ │ ┌ ✔ Bateu certinho: 132     ┐    │    │
│ │  ────┼──────────────── zero  ←clique│ │ ┌ ⚠ Sobrou no banco: 4      ┐←clique │
│ │      ▼vermelho: saídas   (divergente)│ │ ┌ ⚠ Sobrou no sistema: 3    ┐    │    │
│ │  ⇄ drill: semana → dias (colunas) + │ │  ⇄ drill: lista dos itens com    │    │
│ │  movimentos do dia                  │ │  ação 1-clique ("é a venda X?    │    │
│ └─────────────────────────────────────┘ │   Sim/Não")                      │    │
│                                         └──────────────────────────────────┘    │
│ ┌tabela: EXTRATO──────────────────────────────────────────────────────────────┐ │
│ │ Data · Descrição · Forma (PIX/cartão/TED) · Conta · Valor · Conciliação chip│ │
│ │ 15/07  PIX recebido Maria      PIX    Itaú   +R$ 890   ✔ conciliado auto    │ │
│ │ 15/07  TED Distribuidora Sul   TED    Itaú   −R$ 347   ⚠ sem lançamento     │ │
│ └─────────────────────────────────────────────────────────────────────────────┘ │
```

**Drills:** semana no divergente → dias da semana + movimentos; balde de conciliação → lista de
divergências com match sugerido (valor+data) e ação Sim/Não; linha do extrato → detalhe com origem
(`Conciliacao` ↔ `ExtratoBancarioItem` ↔ `MovimentoFinanceiro`).

**Insight vs export:** insight = 3 baldes + taxa efetiva por forma de pagamento. Export = "extrato por
conta e período" (→ Relatórios).

**Read-models:**

| Read-model | Responde | Origem |
|---|---|---|
| `SaldoPorConta` | quanto tem em cada banco | Σ `MovimentoFinanceiro` por `ContaBancariaCaixa` tipo banco (saldo derivado, nunca gravado) |
| `ExtratoBancario(conta, período)` | o extrato | MovimentoFinanceiro + `FormaDePagamento` + status de `Conciliacao` |
| `PendenciasConciliacao` | os 3 baldes | `Conciliacao` (nao_conciliado × direção: item de extrato órfão = "sobrou no banco"; movimento órfão = "sobrou no sistema") + match sugerido valor±data |
| `FluxoSemanalRealizado` | entrou×saiu | Σ MovimentoFinanceiro por semana e sinal |
| `TaxasPorFormaPagamento` | custo dos meios | metadados de taxa em FormaDePagamento × volume realizado |

---

## B.4 Fluxo de Caixa — "a gaveta bate?"

**Trabalho único:** o dinheiro físico. Sessão diária do caixa (abertura → sangrias/troco →
fechamento com contagem) e a diferença esperado×contado. `sub`: *"Dinheiro em espécie — abertura,
fechamento, troco e sangria."* (Aba condicional — ver A.2, Refinamento 2.)

**Os 4 dados que importam:**
1. **Na gaveta agora** (esperado da sessão aberta: abertura + entradas espécie − sangrias − trocos).
2. **Estado do caixa hoje** (aberto às 8h02 por Ana / fechado com sobra de R$ 3).
3. **Diferença acumulada do mês** (Σ contado − esperado — o número que denuncia furo/erro/fraude).
4. **Sangrias do mês** (quanto saiu da gaveta pro cofre/banco — a ponte física com o Bancário).

**Dado game-changer:** o **padrão das diferenças** — faltas que se concentram em dia/turno específico.
É análise de dado no lugar de desconfiança genérica: "as faltas somam R$ 84 e se concentram nas
quintas à tarde".

**Wireframe:**

```
├────────────────────────────────────────────────────────────────────────────────┤
│ ┌KPI hero────────────────┐┌KPI──────────────┐┌KPI──────────────┐┌KPI──────────┐│
│ │ NA GAVETA AGORA         ││ CAIXA DE HOJE    ││ DIFERENÇA DO MÊS││ SANGRIAS MÊS ││
│ │ R$ 412 (esperado)       ││ aberto 08:02 Ana ││ −R$ 84 🔴       ││ R$ 3.200 (8) ││
│ │ [Fechar caixa]          ││ abertura R$ 200  ││ 4 faltas 2 sobras││ p/ Itaú PJ  ││
│ └────────────────────────┘└─────────────────┘└─────────────────┘└─────────────┘│
│ ┌SESSÃO DE HOJE────────────────────────────────────────────────────────────────┐│
│ │ abertura R$200 + vendas espécie R$890 − sangria R$600 − troco R$78 = R$412   ││
│ └──────────────────────────────────────────────────────────────────────────────┘│
│ ┌grid2 esq────────────────────────────┐ ┌grid2 dir─────────────────────────┐    │
│ │ DIFERENÇAS POR DIA (mês)            │ │ PADRÃO DO CAIXA                  │    │
│ │  ▲verde: sobra                      │ │ ┌ Diferença média/dia: −R$ 3,80┐ │    │
│ │ ─┼───▂──▂─────▂── zero      ←clique │ │ ┌ Dia crítico: quinta (−R$46) ┐ │    │
│ │  ▼vermelho: falta  (divergente)     │ │ ┌ Vendas em espécie: 18%     ┐ │    │
│ │  ⇄ drill: sessão do dia (extrato    │ │  ⇄ drill: tiles da sessão        │    │
│ │  da sessão + quem abriu/fechou)     │ │  (operador, duração, sangrias)   │    │
│ └─────────────────────────────────────┘ └──────────────────────────────────┘    │
│ ┌tabela: SESSÕES──────────────────────────────────────────────────────────────┐ │
│ │ Dia · Operador · Abertura · Fechamento · Esperado · Contado · Diferença chip│ │
│ │ 15/07  Ana   08:02  18:34   R$ 435   R$ 431   −R$ 4  🔴falta               │ │
│ └─────────────────────────────────────────────────────────────────────────────┘ │
```

**Drills:** barra do dia → sessão completa (movimentos espécie, sangrias, quem abriu/fechou); linha da
tabela idem. Ação primária contextual: [Fechar caixa] quando aberto (modal de contagem cega —
digita o contado ANTES de ver o esperado, boa prática anti-viés).

**Insight vs export:** insight = padrão de diferenças. Export = "fechamentos de caixa do período"
(anexo do fechamento mensal).

**Read-models:**

| Read-model | Responde | Origem |
|---|---|---|
| `SessaoCaixaAtual` | gaveta agora | sessão aberta: abertura + Σ MovimentoFinanceiro espécie da sessão − sangrias/trocos |
| `HistoricoSessoes(mês)` | sessões e diferenças | sessões fechadas (abertura, esperado, contado, diferença, operador) — entidade de sessão vive no vertical PDV; o Financeiro consome via evento (`caixa.fechado` → MovimentoFinanceiro de ajuste p/ diferença, ContaBancariaCaixa tipo caixa) |
| `DiferencasPorDia(mês)` | padrão de furo | série diária de (contado − esperado), agregável por dia da semana/turno |
| `SangriasDoMes` | quanto foi pro banco | MovimentoFinanceiro de transferência caixa→banco (par de movimentos, nunca perde rastreio) |

---

## B.5 Relatórios — "o pacote pro contador"

**Trabalho único:** gerar documentos formais (PDF/Excel) pra contador e sócios. **Não é hub de
insight** — é a gráfica do módulo. `sub`: *"Documentos prontos pra mandar pro seu contador ou
sócios."*

**Os documentos que importam (cards, não KPIs):**
1. **DRE do mês** — a tabela contábil clássica ("ver como o contador vê"), competência, com
   comparativo mês anterior. Nasce do mapeamento Categoria→LinhaDRE, zero trabalho manual.
2. **Fechamento mensal (pacote)** — DRE + extratos por conta + contas em aberto + fechamentos de
   caixa + pendências de conciliação, num ZIP/PDF único. O "mandar tudo dia 1º".
3. **Extrato por conta** — período livre, por conta bancária/caixa.
4. **Contas em aberto** — a pagar e a receber com aging.
5. **Assinaturas/MRR** — condicional (mesma regra da lente).

**Wireframe:**

```
├────────────────────────────────────────────────────────────────────────────────┤
│ Período: [Julho 2026 ▾]      Regime: (● Competência ○ Caixa) ⓘ                 │
│ ⓘ "No regime de competência, conta quando foi vendido/gasto; no de caixa,      │
│    quando o dinheiro mudou de mão. Seu contador normalmente pede competência." │
│ ┌card──────────────────┐ ┌card──────────────────┐ ┌card──────────────────┐     │
│ │ 📄 DRE DO MÊS         │ │ 📦 FECHAMENTO MENSAL  │ │ 🏦 EXTRATO POR CONTA  │     │
│ │ Resultado: R$ 4.230   │ │ DRE + extratos +      │ │ Itaú · Nubank · Caixa │     │
│ │ (prévia em 5 linhas)  │ │ contas em aberto +    │ │ período livre         │     │
│ │ [PDF] [Excel] [Enviar]│ │ caixa + conciliação   │ │ [PDF] [Excel] [Enviar]│     │
│ └──────────────────────┘ │ [Gerar pacote]        │ └──────────────────────┘     │
│ ┌card──────────────────┐ └──────────────────────┘ ┌card· condicional─────┐     │
│ │ 📋 CONTAS EM ABERTO   │                          │ 📈 ASSINATURAS / MRR  │     │
│ └──────────────────────┘                          └──────────────────────┘     │
│ ┌HISTÓRICO DE EXPORTS (auditoria)────────────────────────────────────────────┐ │
│ │ 01/07  Fechamento junho · PDF · gerado por Igor · enviado p/ contador ✉    │ │
│ └────────────────────────────────────────────────────────────────────────────┘ │
```

**Interações:** cada card tem prévia (5 linhas), botões PDF/Excel e **[Enviar]** (e-mail/WhatsApp do
contador cadastrado — reusa o canal de notify). Toggle de regime só aqui é explícito e global da tela;
quando os dois regimes divergem muito na DRE, a frase educativa automática aparece na prévia
("O DRE mostra R$ 5.000 de resultado, mas o caixa recebeu R$ 1.200 — R$ 3.800 ainda estão pra
receber."). Sem grid2, sem consultor: **é a única tela sem linha de IA** — deliberado, reforça que
insight não mora aqui.

**Read-models:**

| Read-model | Responde | Origem |
|---|---|---|
| `DreCompleta(mês, regime)` | a DRE | árvore `LinhaDRE` ← `Categoria` ← Parcelas (competência) ou MovimentoFinanceiro (caixa); comparativo mês anterior |
| `FechamentoMensal(mês)` | o pacote | agregado dos read-models das outras telas, congelado no fechamento |
| `ExportJob` | geração/auditoria | job idempotente por `(businessId, documento, período, regime)` — regerar não duplica; histórico com actor |

---

# C. Visão Geral em detalhe — o santo graal

## C.1 O critério de corte

A Visão Geral responde, sem scroll e em 5 segundos, as únicas quatro perguntas que decidem
sobrevivência (a causa-raiz dos 48% do Sebrae):

1. **Tenho dinheiro de verdade?** (não é saldo — é saldo menos o que já tem dono)
2. **Vai faltar? Quando?** (antecedência é o diferencial do módulo)
3. **O negócio dá lucro?** (independente do caixa)
4. **O que eu faço agora?** (operacional: essa semana; estratégico: a prioridade nº 1)

**Tudo que não responde uma dessas quatro perguntas está proibido na Visão Geral.** É esse critério —
não uma preferência estética — que impede o cockpit.

## C.2 Os 5 elementos — exatamente estes, e por quê

### ① Hero: "Você pode tirar até R$ X" — o número que justifica o módulo
A resposta à pergunta 1 e a armadilha nº 1 do pequeno empresário (confundir saldo com dinheiro
disponível). Sempre com a decomposição em 3 linhas — o número sem a conta vira caixa-preta, e a conta
é a aula:

```
No banco e na gaveta hoje          R$ 8.400
Já tem dono (15 dias + imposto)   −R$ 5.100
──────────────────────────────────────────
Você pode tirar até                R$ 3.300 ✅
```

Fórmula: Σ saldos (`SaldoPorConta`, banco+caixa) − Parcelas a pagar ≤15/30d − imposto do mês reservado
− parcela de dívida do período − colchão mínimo (definido no onboarding). Cada linha da decomposição é
clicável: "no banco e na gaveta" → Bancário; "já tem dono" → E&S filtrado nos próximos 15 dias.
**Por que hero:** é o único número que nenhum banco, planilha ou concorrente dá pronto.

### ② Linha do tempo do caixa — 30 dias, realizado + previsto, ponto crítico
A resposta à pergunta 2. Linha contínua (realizado) + tracejada (previsto: Parcelas abertas +
Recorrencias + média móvel de vendas), zona abaixo de zero hachurada em `--crit/10`, e **um único
marcador automático** no primeiro dia em que o saldo projetado cruza zero: *"27/07 — aqui o caixa fica
negativo (−R$ 1.340)"*. Sem eixo Y poluído, sem legenda técnica — só o formato da curva e o marcador.
**Drill:** clicar num dia abre popover com o que entra/sai nele; clicar no marcador → E&S filtrado no
que vence até lá. **Por que aqui e não numa aba:** é o alerta de falência com duas semanas de
antecedência — se ele morar em aba secundária, ninguém vê a tempo.

### ③ Lucro do mês — "o negócio dá lucro?" (competência, traduzido)
A resposta à pergunta 3. Um número + delta + margem: **"R$ 4.230 ▲ 12% · de cada R$ 1 vendido sobram
R$ 0,18 (ⓘ)"**. Quando lucro e "pode tirar" divergem muito, a frase educativa automática aparece
embaixo em 12px: *"Lucro é maior que o disponível porque R$ 3.800 ainda estão pra receber."* — é o
antídoto da maior fonte de desconfiança de leigos (features §4.10). **Drill:** "ver de onde veio →"
abre E&S (dono do detalhe). Sem waterfall aqui — a DRE visual mora na prévia de Relatórios; aqui é só
o veredito.

### ④ Próximos 7 dias — a régua operacional
A resposta operacional à pergunta 4. Strip horizontal com os vencimentos da semana (a pagar `--crit`,
a receber `--pos`), cada ponto clicável → parcela em E&S. **Por que 7 dias e não 30:** semana é o
horizonte em que o dono age (features §4.1); 30 dias já está na curva do elemento ②. É o único
elemento "lista" da tela, e cabe numa linha.

### ⑤ Super Consultor — 1 frase, 1 prioridade, 1 ação
A resposta estratégica à pergunta 4 e a **única IA da tela**. O motor de regras (catálogo do doc de
UX §4: caixa cruza zero, categoria subiu, concentração de receita, recebível parado, fixo inchando…)
rankeia por severidade × valor e emite **um** card no padrão do mockup: *"Prioridade da semana: o
aluguel (R$ 2.100) vence antes de você receber R$ 3.200 atrasados. Cobre os 3 clientes hoje.
[Cobrar →]"*. O CTA leva pra aba dona do problema com filtro aplicado. "Ver como calculamos ⌄"
expande a conta (progressive disclosure, audita o conselho).

## C.3 O que ficou de fora — e onde foi parar

| Candidato rejeitado | Por que não entra | Onde vive |
|---|---|---|
| Termômetro/score 0–100 (doc UX) | Score sintético vira decoração: o dono não sabe o que fazer com "62". As mesmas variáveis já aparecem como ①–③, acionáveis | A severidade do score sobrevive como **cor/tom do elemento ⑤** |
| A receber atrasado (widget fixo) | É problema intermitente — widget permanente vira ruído nos meses bons | Promovido pelo ⑤ quando é a prioridade; dono = E&S |
| MRR/churn | Só existe pra quem tem a lente | Recorrentes › Assinaturas |
| Pendências de conciliação | Métrica de higiene, não de decisão | KPI do Bancário |
| Margem por produto/canal, ponto de equilíbrio | Fase 2 do roadmap; profundidade, não veredito | E&S/Recorrentes (evolução futura) |
| Gráficos comparativos, período configurável | Cockpit | Abas |

## C.4 Regras anti-cockpit (invariantes da tela)

1. **Máximo 5 blocos, 1 pergunta por bloco, 1 número dominante por bloco.**
2. **Zero controles** além do seletor de mês global: sem filtros, sem segmented, sem toggles.
3. **Um gráfico só** (elemento ②). Sparklines dentro de KPI não contam como gráfico.
4. **Nada se gerencia aqui**: nenhuma tabela, nenhum CRUD, nenhum "dar baixa". Toda ação é um link
   pra aba dona com contexto aplicado ("agregar e apontar").
5. **A régua de crescimento é substituição, não adição:** se um dado novo provar que muda o jogo,
   ele entra no lugar de um dos 5 — a tela nunca passa de 5 blocos.

## C.5 Layout

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ FINANCEIRO · VISÃO GERAL                                       Julho 2026 ▾   │
│ Como está o dinheiro do seu negócio — em 5 segundos.                          │
│ [Visão geral][Entradas & saídas][Recorrentes][Bancário][Fluxo de caixa][Relatórios]
├──────────────────────────────────────────────────────────────────────────────┤
│ ┌① HERO (2/3 da largura)──────────────────────────┐ ┌③ LUCRO DO MÊS (1/3)──┐ │
│ │ VOCÊ PODE TIRAR ATÉ                              │ │ R$ 4.230  ▲ 12%      │ │
│ │ R$ 3.300                                         │ │ de cada R$1 vendido  │ │
│ │ ────────────────────────────────                 │ │ sobram R$ 0,18 (ⓘ)  │ │
│ │ no banco e na gaveta hoje    R$ 8.400  →Bancário │ │ "maior que o dispo-  │ │
│ │ já tem dono (15d + imposto) −R$ 5.100  →E&S      │ │  nível porque R$3,8k │ │
│ │ livre de verdade             R$ 3.300 ✅         │ │  estão pra receber"  │ │
│ └──────────────────────────────────────────────────┘ │ [ver de onde veio →] │ │
│                                                      └──────────────────────┘ │
│ ┌② O CAIXA NOS PRÓXIMOS 30 DIAS────────────────────────────────────────────┐  │
│ │ ── realizado  ╌╌ previsto                                                │  │
│ │  ▄▅▆▅▆▇▆▅╌╌╌╌╌╲╌╌╌╌╌╌╱╌╌╌╌       ▼ 27/07 — aqui fica negativo (−R$1.340)│  │
│ │ ░░░░░░░░░░░░░░░░▓▓▓▓░░░░░░ zona de risco    (clique no dia → o que vence)│  │
│ └──────────────────────────────────────────────────────────────────────────┘  │
│ ④ PRÓXIMOS 7 DIAS ──●───────────●──────────────●───────────●──                │
│    qua +R$890 Maria    sex −R$2.100 aluguel      seg −R$430 luz                │
│ ┌⑤ 💡 SUPER CONSULTOR──────────────────────────────────────────────────────┐  │
│ │ Prioridade da semana: o aluguel (R$ 2.100) vence antes de você receber   │  │
│ │ R$ 3.200 atrasados. Cobre os 3 clientes hoje. [Cobrar →]  Ver como calculamos ⌄│
│ └──────────────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────────┘
```

**Read-models da Visão Geral:**

| Read-model | Alimenta | Origem |
|---|---|---|
| `DisponivelParaRetirada` | ① | Σ `SaldoPorConta` (banco+caixa) − Parcelas a pagar ≤15/30d − imposto reservado do mês − parcela de dívida − colchão mínimo (config) |
| `ProjecaoCaixaDiaria(30d)` | ② | saldo realizado (MovimentoFinanceiro) até hoje + Parcelas abertas + Recorrencias projetadas + média móvel de vendas; marca o 1º dia < 0 |
| `ResultadoDoMes` | ③ | reuso do read-model de E&S (mesma fonte, nunca recalculado à parte) |
| `VencimentosProximos(7d)` | ④ | Parcelas abertas com vencimento ≤7d, sinal por tipo |
| `InsightPrioritario` | ⑤ | motor de regras sobre os read-models acima + catálogo (UX §4); persistido com idempotência `(businessId, ruleId, período)` |

---

# D. Padrões transversais

## D.1 O padrão drill de Assinaturas, generalizado

A mecânica aprovada — **"índice clicável à esquerda ⇄ contexto à direita; clique troca os dois cards
juntos; ← volta; a tabela embaixo nunca muda"** — se aplica igual em toda aba:

| Tela | Índice (esq, barras clicáveis) | Drill esq (gráfico do item) | Drill dir (tiles do item) |
|---|---|---|---|
| Assinaturas (aprovado) | MRR por serviço | Divergente Novos×Churn 6m | Tempo de casa · LTV · retenção |
| E&S | Despesas por categoria | Colunas 6m da categoria + média tracejada | Média 6m · maior lançamento · % do total |
| Recorrentes (fixas) | Compromissos por recorrência | Colunas 12m do valor (degraus visíveis) | Média 12m · desde quando · Σ ano |
| Bancário | Entrou×saiu por semana (divergente) | Dias da semana + movimentos | — (dir é a conciliação em 3 baldes) |
| Fluxo de Caixa | Diferenças por dia (divergente) | Extrato da sessão do dia | Operador · duração · sangrias |
| Visão Geral | — (sem grid2: drill é popover no gráfico ② e links nos demais) | | |

Invariantes da mecânica: o drill **nunca muda de rota** (estado local, animação `cardin` 220ms,
`prefers-reduced-motion` respeitado); todo item clicável tem `:focus-visible` e hover; a hint no
título do card ensina a interação ("clique num serviço p/ churn e retenção →").

## D.2 Quando usar cada forma (árvore de decisão)

```
A informação responde…
├─ "quanto? / estou bem?" (1 valor agregado)
│    ├─ é A pergunta existencial da tela ────────► KPI HERO (com sparkline e delta)
│    └─ é propriedade do contexto selecionado ───► STAT TILE (grupos de 3, trocam no drill)
├─ "onde está concentrado? / quem é o maior?" ───► BARRAS HORIZONTAIS clicáveis (índice do drill)
├─ "como evoluiu? / quando acontece?" ───────────► LINHA/COLUNAS no tempo
│    └─ futuro incluído? ────────────────────────► linha contínua (realizado) + tracejada (previsto)
├─ "dois fluxos opostos" (entra×sai, novo×churn,
│   sobra×falta) ────────────────────────────────► DIVERGENTE ± em torno do zero (pos ▲ / crit ▼)
├─ "qual item? preciso agir/auditar um a um" ────► TABELA (sempre o último bloco, com chips e ação por linha)
└─ "o que isso significa e o que eu faço?" ──────► 1 LINHA DE CONSULTOR (máx. 1 por tela)
```

Regras duras: nunca dois gráficos respondendo a mesma pergunta na mesma tela; pizza/donut proibidos
(composição = barras); todo gráfico tem no máximo 2 cores semânticas + neutros; eixo/legenda só quando
a curva sozinha não basta.

## D.3 A regra da linha de IA (decisão 5 operacionalizada)

- **Exatamente 1 por tela** (0 em Relatórios). Nunca feed, nunca parágrafos.
- **Motor de regras + templates com números interpolados** dos read-models — determinístico, barato,
  idempotente por `(businessId, ruleId, período)`. LLM não roda por render/usuário; no máximo
  refinamento offline dos templates.
- **Anatomia fixa:** o que notei (número) + causa + ação no imperativo + 1 CTA que é ação real do
  produto. Checklist de tom do doc de UX §12 vale como gate de revisão de toda copy nova.
- **Prioridade única:** quando várias regras disparam, mostra-se a de maior severidade×valor; as
  demais ficam atrás do sino/central de alertas — nunca empilhadas na tela.

## D.4 Tradução caixa × competência (o antídoto padronizado)

Sempre que dois números de regimes diferentes coexistirem visíveis (lucro ③ vs disponível ① na Visão
Geral; DRE vs extrato em Relatórios), a frase-ponte automática é obrigatória:
*"[maior] é maior que [menor] porque R$ X ainda estão pra receber / já estão reservados pra imposto."*
Nunca dois números divergentes sem a ponte — é a diferença entre educar e parecer bug.

## D.5 Semântica de cor e número (herdada do mockup, congelada)

- `--pos` verde = entrada/sobra/saudável · `--crit` = saída/falta/risco · `--warn` = atenção ·
  `--primary` (vermelho-600) = marca, hero, CTA primário e tab ativa — **nunca** pra "negativo"
  (o negativo é `--crit`, um vermelho distinto, como no mockup).
- Todo valor monetário em `.num` (mono tabular). Deltas sempre com seta + percentual + referência
  ("vs mês passado"). Chips de status com dot (`ativa/risco/atraso/cancelada` → generalizam pra
  `pago/previsto/atrasado/conciliado/falta/sobra`).
- Empty states seguem a tabela do doc de UX §8 (cada tela vazia ensina + 1 CTA).

## D.6 Consolidado de read-models (contrato Financeiro.Application → UI)

| Read-model | Telas | Entidades de origem |
|---|---|---|
| `DisponivelParaRetirada` | Visão Geral ① | ContaBancariaCaixa (saldos derivados), Parcela, imposto reservado, config colchão |
| `ProjecaoCaixaDiaria` | Visão Geral ②, E&S (fim de mês) | MovimentoFinanceiro, Parcela, Recorrencia |
| `ResultadoDoMes` | Visão Geral ③, E&S, Relatórios (prévia DRE) | ContaAReceber/ContaAPagar por dataCompetencia |
| `VencimentosProximos` | Visão Geral ④ | Parcela |
| `InsightPrioritario` | todas menos Relatórios | motor de regras sobre os demais |
| `ExtratoUnificado` | E&S | MovimentoFinanceiro + Parcela |
| `ResumoPorCategoria` | E&S | Parcela × Categoria × LinhaDRE (fixo/variável) |
| `AgingRecebiveis` | E&S | Parcela (ContaAReceber) × cliente |
| `CompromissosFixosMensal` / `HistoricoValorRecorrencia` / `PesoFixoSobreReceita` | Recorrentes | Recorrencia + Parcelas geradas |
| `AssinaturasOverview` | Recorrentes › Assinaturas | Assinatura (Recorrencia de entrada + clienteId/serviçoId) + Parcelas |
| `SaldoPorConta` / `ExtratoBancario` / `PendenciasConciliacao` / `FluxoSemanalRealizado` / `TaxasPorFormaPagamento` | Bancário | MovimentoFinanceiro, ContaBancariaCaixa, Conciliacao, FormaDePagamento |
| `SessaoCaixaAtual` / `HistoricoSessoes` / `DiferencasPorDia` / `SangriasDoMes` | Fluxo de Caixa | sessões (evento `caixa.fechado`), MovimentoFinanceiro tipo caixa |
| `DreCompleta` / `FechamentoMensal` / `ExportJob` | Relatórios | LinhaDRE ← Categoria ← Parcela/MovimentoFinanceiro |

Notas de arquitetura (amarradas ao ARCHITECTURE.md): todos são **views derivadas, nunca coleções
gravadas** (exceto `InsightPrioritario` e `ExportJob`, persistidos por idempotência); saldos sempre
derivados de MovimentoFinanceiro; o Financeiro só é alimentado por eventos de integração
(venda.concluida, pedido.pago, os.faturada, caixa.fechado…) — nenhuma tela escreve em entidade de
outro módulo; camada de execução: read-models computam no Store.Server (LAN, offline-first) e a
nuvem consolida multi-loja.

---

## Sequência de construção sugerida (amarra ao roadmap de features)

1. **E&S + FAB de lançamento** (esqueleto: ExtratoUnificado, ResumoPorCategoria) — é a fonte.
2. **Bancário** (extrato + conciliação 3 baldes) — é a confiança nos números.
3. **Visão Geral** (①②③④ com dados já confiáveis; ⑤ com 3 regras: caixa cruza zero, conta vencendo,
   categoria subiu).
4. **Recorrentes** (lente fixas; Assinaturas já aprovada).
5. **Fluxo de Caixa** (condicional, junto do vertical PDV).
6. **Relatórios** (DRE + fechamento mensal) — fecha o ciclo com o contador.
