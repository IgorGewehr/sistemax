# Financeiro ServicePro — UX do "Super Consultor de Bolso"

> Objetivo de produto: **"mesmo que eu seja leigo, eu consigo ter a visão de um super
> consultor financeiro e me organizar a ponto de não falir."**
> Este documento projeta a experiência tela a tela para atingir isso — sem
> perder profundidade para quem já entende de finanças.

Ambiente técnico considerado (ver `CLAUDE.md` e `docs/architecture-map.md`):
React + Tailwind + MUI v6 + Framer Motion, acento `red-600/500`, `Inter` (corpo) +
`Plus Jakarta Sans` (`.font-display`), Lucide icons, dark mode via `.dark`,
dados em `transactions` / `bankAccounts` / `financialCategories` (Firestore,
sempre filtrado por `businessId`). O módulo já existe em
`app/components/features/financial/FinancialModule.tsx` (6.6k linhas, 8 abas:
Visão Geral, Transações, Recorrentes, Contas, Projetos, Comissões,
Conciliação, Auditoria) com FSM de status já modelada em
`lib/contracts/fsm/transaction.ts` (`pendente → pago/atrasado/cancelado`).
Este redesenho **evolui** o que existe — não descarta.

---

## 0. Pesquisa — o que o mercado já resolve bem

| Referência | O que copiar | Fonte |
|---|---|---|
| **Nubank PJ / Inter Empresas** | Número gigante do saldo como hero; tom de conversa em 1ª pessoa; extrato com ícones por categoria | conhecimento de produto |
| **Copilot Money** | Aba "Cash Flow" separa **Realizado vs Projetado** com trend visual limpo; web app "rápido e nativo"; recomendações de categorização por padrão de gasto | [Cash Flow Tab Overview](https://help.copilot.money/en/articles/9682232-cash-flow-tab-overview), [Copilot Money for Web](https://help.copilot.money/en/articles/11780342-copilot-money-for-web) |
| **Conta Azul Mais (Dashboard de Fluxo de Caixa, 2025)** | Linha **contínua = Realizado**, linha **tracejada = Previsto**; drill-down mês → trimestre → ano; resumo "recebimentos vs pagamentos" lado a lado | [Dashboard de Fluxo de Caixa](https://ajuda.contaazul.com/hc/pt-br/articles/44911365743501) |
| **QuickBooks Cash Flow Planner** | Forecast de 90 dias baseado em histórico + lançamentos futuros; "e se" simulável; central que junta todos os saldos de conta num só lugar | [QuickBooks Cash Flow Planner](https://quickbooks.intuit.com/learn-support/en-us/help-article/budget-forecast-reports/use-cash-flow-planner-quickbooks-online/L2l59mIqe_US_en_US) |
| **Ramp** | Não mostra só gráfico — mostra **achado + ação recomendada** ("gasto duplicado", "pico de fornecedor X"); anomalias sinalizadas em tempo real, não em relatório mensal | [Ramp Intelligence](https://ramp.com/intelligence) |

**Síntese que orienta este design:** número grande + "por quê" em 1 frase +
ação recomendada (Ramp) · realizado vs. previsto sempre visualmente distintos
(Conta Azul/Copilot) · forecast olhando pra frente, não só espelho do passado
(QuickBooks) · tom de conversa em 1ª pessoa, não "extrato de banco" (Nubank).

---

## 1. Princípios de design (norte para toda decisão)

1. **Regra dos 5 segundos.** A Home responde nesta ordem, sem scroll:
   *estou bem ou mal → por quê → o que fazer agora*. Se uma tela exige que o
   usuário some números na cabeça pra saber se está bem, a tela falhou.
2. **Todo número tem um "porquê" a um toque de distância.** Nunca mostrar um
   KPI puro sem explicação acessível (tooltip, expansão, ou frase ao lado).
3. **Toda frase do sistema é dita por um consultor, não por um banco.**
   1ª pessoa do consultor → 2ª pessoa do usuário. "Notei que..." em vez de
   "Alerta: variação detectada." Sempre com número + prazo + causa + ação.
4. **Lançar dado tem que ser mais rápido que anotar no caderno.** Se o app
   perder pro Bloco de Notas do celular, o dono some. Meta: **< 10 segundos**
   por lançamento manual, **< 5 segundos** por lançamento com foto/OCR.
5. **Progressive disclosure, não dumbing down.** Simples por padrão ≠ menos
   dado guardado. Guardamos tudo (centro de custo, projeto, DRE); só não
   *mostramos* tudo de cara.
6. **Vocabulário técnico é traduzido, nunca escondido.** "DRE", "capital de
   giro", "margem de contribuição" aparecem — sempre com `(ⓘ)` que explica em
   1 frase simples. Isso ensina o leigo a virar profissional aos poucos.
7. **Toda tela vazia ensina.** Nenhum empty state genérico ("Nenhum dado").
   Empty state = a melhor aula de educação financeira que o produto pode dar.

---

## 2. Arquitetura de informação (5 áreas)

```
Financeiro
├── Início            (era "Visão Geral") — HOME, tela padrão ao abrir
├── Consultor          NOVO — feed de insights + termômetro + simulador
├── Fluxo de Caixa     NOVO destaque — hoje enterrado dentro de Visão Geral
├── Lançamentos        (existente) — entrada rápida vive aqui + FAB global
└── Mais ▾             Recorrentes · Contas · Projetos · Comissões ·
                        Conciliação · Auditoria · Categorias
```

`Mais ▾` é o "modo avançado" — ver §8 (Progressive Disclosure). No dia a dia,
80% do uso deve viver em **Início + Consultor + Lançamentos**.

---

## 3. TELA 1 — Início (Home financeiro)

### Objetivo
Responder em 5 segundos: **estou bem ou mal? por quê? o que faço agora?**

### Anatomia (topo → base)

1. **Termômetro do Negócio** (hero, ocupa a dobra inteira em mobile/balcão)
   — não é "saldo em conta", é um **score de saúde financeira 0–100**
   calculado a partir de: dias de fôlego de caixa, % de receita atrasada,
   margem real do mês, concentração de receita, peso dos compromissos fixos
   sobre a receita.
   - 5 faixas com cor e rótulo em linguagem humana (nunca "score: 62"):
     `0–20 Crítico` (vermelho) · `21–40 Atenção` (laranja) · `41–60 Estável`
     (âmbar) · `61–80 Saudável` (verde) · `81–100 Ótimo` (verde-esmeralda).
   - Abaixo do termômetro, **uma frase-resumo** gerada dinamicamente —
     é o "por quê" em 1 linha.
   - Abaixo da frase, **um botão de ação primária** — o "o que fazer agora"
     (link direto pro insight #1 do Consultor).

2. **3 números que importam** (cards, não 8 KPIs genéricos):
   `Caixa hoje` · `Vai sobrar/faltar em 30 dias` · `Lucro do mês` — cada um
   com seta de tendência vs. mês anterior e cor semântica.

3. **Faixa "Próximos 7 dias"** — timeline horizontal compacta com os
   próximos vencimentos (a pagar em vermelho, a receber em verde) — resposta
   rápida pra "o que vence essa semana".

4. **Atalho de lançamento** — banner fino "Ainda não lançou nada hoje?
   Leva 10 segundos" com botão, se o dia estiver sem lançamentos (nudge de
   hábito, não intrusivo).

5. **Prévia do Consultor** (3 cards de insight, "Ver todos" leva à Tela 2).

### Wireframe ASCII

```
┌──────────────────────────────────────────────────────────────────────┐
│  Financeiro           [Início] Consultor  Fluxo de Caixa  Lançamentos │
│                                                          Mais ▾  🔔 3 │
├──────────────────────────────────────────────────────────────────────┤
│                                                                        │
│   ┌────────────────────────────────────────────────────────────┐     │
│   │            SEU NEGÓCIO ESTÁ:   ●●●●○  SAUDÁVEL              │     │
│   │                                                              │     │
│   │     "Você fecha o mês com lucro de R$ 4.230, mas 32% da      │     │
│   │      sua receita ainda está atrasada há mais de 15 dias."    │     │
│   │                                                              │     │
│   │              [ Cobrar quem está atrasado → ]                 │     │
│   └────────────────────────────────────────────────────────────┘     │
│                                                                        │
│   ┌───────────────────┐ ┌───────────────────┐ ┌───────────────────┐  │
│   │ CAIXA HOJE         │ │ EM 30 DIAS         │ │ LUCRO DO MÊS      │  │
│   │ R$ 18.420  ▲ 6%    │ │ + R$ 6.100  ▲      │ │ R$ 4.230   ▲ 12%  │  │
│   │ vs. mês passado    │ │ (projeção)          │ │ margem: 18%  (ⓘ) │  │
│   └───────────────────┘ └───────────────────┘ └───────────────────┘  │
│                                                                        │
│   Próximos 7 dias ───────────────────────────────────────────────    │
│   │ hoje    ter      qua      qui  ●R$2.100    sex     sáb   dom │   │
│   │                                 Aluguel                       │   │
│   │  ●R$890 a receber (Maria)                        ●R$430 a pagar│  │
│                                                                        │
│   ┌────────────────────────────────────────────────────────────┐     │
│   │  💡 SEU CONSULTOR DIZ                          Ver tudo →    │     │
│   │  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐          │     │
│   │  │ ⚠ Caixa fica  │ │ 📈 Gasto com  │ │ 💰 R$ 3.200   │          │     │
│   │  │ negativo em   │ │ Fornecedores  │ │ parado há 45  │          │     │
│   │  │ 12 dias        │ │ subiu 42%     │ │ dias           │          │     │
│   │  └──────────────┘ └──────────────┘ └──────────────┘          │     │
│   └────────────────────────────────────────────────────────────┘     │
│                                                                        │
│                                                    ⊕  Lançar (FAB)    │
└──────────────────────────────────────────────────────────────────────┘
```

### Componentes-chave
- `HealthGauge` — arco semicircular animado (Framer Motion `spring`,
  duração ~800ms no mount, sem `blur` no exit por causa da regra de GPU do
  projeto). Cor interpolada via `red-600 → amber-500 → emerald-500`.
- `KpiCard` — já existe (linha 2929 do `FinancialModule.tsx`); adicionar
  micro-tooltip `(ⓘ)` explicando o cálculo em 1 frase ("Margem = lucro ÷
  receita. Você fica com 18 centavos de cada R$1 que entra.").
- `Next7DaysStrip` — nova; reusa dados de `pending` (aging já calculado nas
  linhas 2985-2989) e `upcomingRecurrences`.
- `InsightPreviewRow` — 3 `InsightCard` truncados, ver Tela 2.

### Microcopy — frase-resumo do Termômetro (exemplos por faixa)
- **Crítico:** "Seu caixa fica negativo em **8 dias** se nada mudar. A causa
  principal é o aluguel de amanhã (R$ 2.100) somado a R$ 1.900 que você
  ainda não recebeu do André."
- **Atenção:** "Você está no zero a zero este mês. Se cortar R$ 400 em
  Fornecedores ou receber 1 fatura atrasada, vira lucro."
- **Estável:** "Fechando no azul, mas apertado: sobra só R$ 380 depois de
  tudo pago. Um imprevisto de R$ 500 já te tira do positivo."
- **Saudável:** "Você fecha o mês com lucro de R$ 4.230, mas 32% da sua
  receita ainda está atrasada há mais de 15 dias."
- **Ótimo:** "Melhor mês dos últimos 6: lucro de R$ 9.840, sem nenhuma conta
  atrasada. Bom momento pra guardar uma reserva ou investir em estoque."

---

## 4. TELA 2 — O Super Consultor (números → conselho)

Este é o coração do produto: motor de regras (evoluível pra ML depois) que lê
`transactions` + `recurrence` (já existe detecção de padrão em
`FIN-R23`, linha 5527) + `bankAccounts` + `CashFlowSummary` e produz
**cartões de insight acionáveis** — nunca só um gráfico pra interpretar.

### Estrutura de um `InsightCard`
Todo insight segue o mesmo esqueleto de frase (é a "voz do consultor"):

> **[O que eu notei]** + **[por quê / causa]** + **[o que fazer]** (1–3 CTAs)

```
┌────────────────────────────────────────────────────────────┐
│ ⚠ URGENTE                                     agora · 2min  │
│                                                              │
│ Seu caixa vai ficar negativo em 12 dias                     │
│                                                              │
│ No dia 27/07, sua conta cai pra −R$ 1.340. A causa: o        │
│ aluguel (R$ 2.100) vence antes de você receber R$ 3.200     │
│ que 3 clientes ainda devem.                                 │
│                                                              │
│ [ Cobrar os 3 clientes agora ]  [ Adiar o aluguel 5 dias ]  │
│                                                              │
│                                       Ver como calculamos ⌄  │
└────────────────────────────────────────────────────────────┘
```

- **Ver como calculamos** (progressive disclosure) expande e mostra a conta
  real: saldo atual, + recebimentos previstos, − pagamentos previstos, dia a
  dia até o ponto crítico — pro usuário avançado auditar o conselho.
- Cada CTA é **uma ação real do produto**, não um "saiba mais": abre o
  WhatsApp/cobrança com o cliente pré-selecionado, ou abre o modal de editar
  a recorrência do aluguel já no campo de data.

### Catálogo de insights (motor de regras v1 — mapeado a dados que já existem)

| # | Gatilho (dado já existente) | Título do insight | Ação sugerida |
|---|---|---|---|
| 1 | Projeção de saldo diário (saldo atual + recorrências + pendentes) cruza 0 | "Seu caixa fica negativo em N dias" | Cobrar / adiar pagamento / usar linha de crédito |
| 2 | `Transaction.category` agregado por mês, variação > 30% vs. média 3 meses | "Gasto com [categoria] subiu X%" | Ver lançamentos da categoria / criar alerta de teto |
| 3 | `status='pendente'`, `dueDate` > 30 dias atrás | "Você tem R$X a receber há mais de 30 dias" | Cobrar em 1 toque (WhatsApp) |
| 4 | Saldo em `bankAccounts` tipo `corrente`/`caixa` parado (sem sair/entrar) > 30 dias acima de X | "R$X parado sem render" | Sugerir CDB/poupança/linha (parceria) |
| 5 | `clientId` concentra > 50% da receita do trimestre | "Um cliente é X% da sua receita" | Diversificar — sugestão educativa |
| 6 | `lucro / receita` do mês < benchmark do segmento (`Business.segment`, ver `project_turmas_e_vertical_agente`) | "Sua margem está abaixo do normal pro seu tipo de negócio" | Ver DRE simplificado |
| 7 | Soma de `recurrence` ativas cresceu > 20% em 3 meses | "Seus compromissos fixos cresceram — hoje R$X/mês" | Revisar assinaturas/recorrências |
| 8 | Padrão sazonal (mesmo mês, ano anterior, receita < média) | "Nos últimos anos, [mês] costuma ser mais fraco pra você" | Preparar colchão / antecipar vendas |
| 9 | `installmentGroupId` com parcelas futuras somando > receita projetada do período | "Você já comprometeu R$X em parcelas dos próximos meses" | Ver cronograma de parcelas |
| 10 | Nenhum lançamento de despesa/receita nos últimos 3 dias úteis | "Faz 3 dias que não é lançado nada — seus números podem estar desatualizados" | Lançar agora (abre FAB) |

### Simulador "E se…" (profundidade sob demanda)
Dentro do insight #1, botão secundário **"E se eu…"** abre um mini-simulador
com sliders/inputs simples:
- "E se eu atrasar esse pagamento **[N] dias**?"
- "E se eu receber **[X]%** do que está atrasado esta semana?"
- "E se minhas vendas caírem **[X]%** no próximo mês?"

O gráfico de Fluxo de Caixa (Tela 4) recalcula ao vivo — mesma lógica do
"Cash Flow Planner" da QuickBooks, mas em linguagem de pergunta, não de
parâmetro técnico.

### Microcopy — glossário traduzido (ⓘ sob demanda)
| Termo técnico (mostrado) | Explicação simples (tooltip) |
|---|---|
| Margem líquida | "De cada R$1 que entra, quanto sobra depois de pagar tudo." |
| Capital de giro | "O dinheiro que você precisa ter guardado pra cobrir o dia a dia até as vendas virarem caixa." |
| Ponto de equilíbrio | "Quanto você precisa vender por mês só pra não ter prejuízo — abaixo disso, você paga pra trabalhar." |
| DRE | "Um resumo de tudo que entrou e saiu, mês a mês, pra você ver se o negócio dá lucro de verdade." |
| Fôlego de caixa (*runway*) | "Quantos dias seu negócio sobrevive se nada mais entrar, só o que já está programado sair." |
| Inadimplência | "Quanto os seus clientes te devem e não pagaram no prazo combinado." |

---

## 5. TELA 3 — Entrada de dados (o teste real do produto)

**Premissa:** se lançar for chato, o dono volta pro caderno/planilha em 1
semana. Meta de fricção: **1 toque para o caso comum, nunca mais que 3 campos
obrigatórios.**

### 3 caminhos de entrada, sempre acessíveis por um FAB global (⊕ no canto,
presente em toda tela do módulo, igual ao padrão do PDV/Kanban do produto):

```
┌───────────────────────────────┐
│         Lançar algo            │
│                                 │
│  📸  Tirar foto da nota/boleto  │  ← OCR, o caminho "sem pensar"
│  💸  Recebi um pagamento        │  ← receita rápida
│  🧾  Paguei uma conta           │  ← despesa rápida
│  🔁  Criar lançamento recorrente│  ← aluguel, assinaturas, folha
│                                 │
└───────────────────────────────┘
```

### 3a. Fluxo com foto (OCR) — o caminho campeão
Hoje existe um tooltip "Em breve: extrair dados do comprovante via OCR
(Google Cloud Vision)" desabilitado (linha 2141 do `FinancialModule.tsx`).
Este design assume a ativação dessa capability + um segundo canal:

1. Usuário tira foto (câmera do dispositivo **ou** manda a foto pelo
   WhatsApp do negócio — o produto já tem infraestrutura omnichannel via
   Meta Cloud/Baileys; um número dedicado "Financeiro" recebendo mídia e
   criando o rascunho de lançamento é reuso direto da arquitetura existente
   em `app/api/webhooks/meta`).
2. Google Cloud Vision extrai: valor, data, nome do fornecedor/pagador,
   possível categoria (via mapa histórico `description → category` do
   próprio tenant).
3. Tela de confirmação — **nunca** salva direto sem revisão, mas o padrão é
   "tudo certo, só confirme":

```
┌──────────────────────────────────────────┐
│  ← Confirmar lançamento         [🖼 nota]  │
│                                            │
│   Valor           R$ 347,90               │
│   Data            15/07/2026              │
│   Pago para       Distribuidora Sul Ltda  │
│   Categoria       🏷 Fornecedores  ▾      │
│               (sugerido — você já         │
│                categorizou assim 8x)      │
│   Conta            Conta Corrente PJ  ▾   │
│                                            │
│         [ Tá certo, lançar ✓ ]            │
│              Editar algo                  │
└──────────────────────────────────────────┘
```

4. Se a extração falhar/confiança baixa: não trava o fluxo — cai pro
   formulário rápido já com a foto anexada, mensagem: "Não consegui ler tudo
   da nota, mas já anexei — só completa os campos que faltam."

### 3b. Lançamento rápido (sem foto) — 3 campos, 1 toque de categoria
```
┌──────────────────────────────────────────┐
│  ← Paguei uma conta                       │
│                                            │
│   R$  [        ]                          │
│                                            │
│   Foi pra quem/o quê?  [________________] │
│                                            │
│   Categoria (sugestões automáticas):      │
│   ( Fornecedores ) ( Aluguel ) ( Energia ) │
│   ( + outra categoria )                    │
│                                            │
│   Já foi pago    ⚪ hoje  ⚪ ainda vou pagar│
│                                            │
│         [ Lançar ]                        │
│                                            │
│   ☐ Isso se repete todo mês               │
└──────────────────────────────────────────┘
```
- As "sugestões automáticas" de categoria são as 3 categorias mais usadas
  pra descrições parecidas (fuzzy match simples sobre `description` do
  tenant) — reduz o dropdown a chips de 1 toque na maioria dos casos.
- O toggle **"Isso se repete todo mês"** já é o gancho de entrada pro
  `TransactionRecurrence` existente — não precisa abrir outra tela.

### 3c. Detecção automática de recorrência (evolução do que já existe)
O código já tem detecção de padrão recorrente (`FIN-R23`, linha 5527). Hoje
provavelmente é passivo dentro da aba Recorrentes — a proposta é torná-lo
**proativo**, como um nudge de 1 toque na Home/Lançamentos:

> 💡 "Notei que você lança 'Aluguel — R$ 2.100' todo dia 5 há 3 meses. Quer
> que eu deixe isso automático, sem você precisar lançar de novo?"
> **[ Sim, automatizar ]** &nbsp; **[ Não, prefiro lançar na mão ]**

### 3d. Conciliação de 1 toque
Na aba Conciliação (já existe, `ConciliacaoTab.tsx`), sempre que um item do
extrato bancário casar com um lançamento pendente por valor+data aproximada,
oferecer confirmação de 1 toque:

> "Esse PIX de R$ 890 recebido dia 14 bate com a cobrança da Maria. Confirma
> que é o mesmo?" **[ Sim, é esse ]** / **[ Não é ]**

### Microcopy de confirmação (reforço positivo, sem infantilizar)
- "Lançado. Seu caixa de hoje já está atualizado." (toast, 2s, sem exigir
  fechar)
- "Boa — mais um lançamento em dia. Faltam 2 pra fechar a semana." (nudge de
  hábito, opcional, some após dispensar 2x)
- Erro de OCR: "Não consegui ler essa foto direito. Tenta tirar de novo com
  mais luz, ou preenche na mão — já deixei o valor que consegui ver."

---

## 6. TELA 4 — Fluxo de Caixa

### Objetivo
Responder "o dinheiro vai faltar em algum momento nos próximos meses, e
quando exatamente?" — com **realizado vs. projetado sempre visualmente
distintos** (padrão Conta Azul/Copilot) e uma **linha de risco** clara.

```
┌────────────────────────────────────────────────────────────────────┐
│  Fluxo de Caixa                     [30 dias][60 dias][90 dias]     │
│                                       Cenário: ●Realista ○Otimista   │
│                                                        ○Pessimista   │
├────────────────────────────────────────────────────────────────────┤
│  R$                                                                  │
│  20k ┤        ╭──╮                                                   │
│      │      ╭─╯  ╰╮        ╭╌╌╌╌╌╌╮                                  │
│  15k ┤   ╭──╯     ╰─╮    ╭╌╯      ╰╌╌╮                                │
│      │ ╭─╯          ╰──╮╌╯            ╰╌╌╮                           │
│  10k ┤╌╯                ╲                 ╰╌╌╌╮      ╭╌╌╌╌╌          │
│      │                   ╲                     ╰╌╌╌╌╌╯               │
│   5k ┤                    ╲___                                       │
│      │                        ╲___         ← ponto crítico:          │
│   0k ┼────────────────────────────╲───────── 27/07 (R$ −1.340)──────│
│      │▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓ zona de risco ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓ │
│      └────────────────────────────────────────────────────────────  │
│        hoje        1 sem        2 sem        3 sem       30 dias    │
│        ── linha cheia = realizado     ╌╌╌ tracejada = previsto      │
│                                                                       │
│   ● 20/07 Aluguel −R$2.100    ● 22/07 Recebimento Maria +R$890       │
│   ● 27/07 Fornecedor −R$1.800  ● 30/07 Folha −R$3.200                │
│                                                                       │
│                          [ Simular "E se…" ]                          │
└────────────────────────────────────────────────────────────────────┘
```

### Componentes-chave
- `CashFlowChart` — área/linha combinada (Recharts, já usado no projeto
  provavelmente via MUI charts ou biblioteca própria — verificar
  dependência atual antes de introduzir nova lib). Realizado = linha sólida
  `slate-700`/`gray-200` (dark); Previsto = linha tracejada mesma cor com
  opacidade 60%. Faixa abaixo de zero pintada `red-500/10` com hachurado
  sutil (`zona de risco`).
- Marcadores (`●`) nos pontos de vencimento relevantes — clicáveis, abrem
  popover com o lançamento.
- Seletor de cenário **Otimista/Realista/Pessimista**: ajusta a taxa de
  conversão dos recebíveis pendentes (ex.: Realista assume que atrasados há
  >30 dias só entram com 50% de chance; Pessimista assume 20%; Otimista
  assume 100% no prazo). É a mesma ideia do "what-if" do QuickBooks Cash
  Flow Planner, mas com rótulo humano em vez de parâmetro técnico.
- Drill-down: clicar em um dia do gráfico abre a lista de lançamentos
  daquele dia (reaproveita `TransactionList` existente).

### Microcopy
- Chip do ponto crítico: **"27/07 — aqui o caixa fica negativo (−R$ 1.340)"**
- Rodapé educativo (1ª vez que o usuário abre a tela): "A linha cheia é o
  que já aconteceu. A tracejada é o que a gente prevê baseado nas suas
  contas programadas e recorrências. Quanto mais você lança, mais essa
  previsão fica precisa."

---

## 7. Alertas e nudges

### Camadas de alerta (para não virar spam)

| Camada | Onde aparece | Quando disparar | Exemplo |
|---|---|---|---|
| **Crítico (vermelho)** | Badge no sino + banner persistente na Home até resolver ou dispensar | Risco real de caixa negativo em ≤ 14 dias; pagamento vencido hoje | "Seu caixa fica negativo em 8 dias" |
| **Atenção (âmbar)** | Feed do Consultor + badge no sino | Variação anômala, atraso de recebível, recorrência subindo | "Gasto com Fornecedores subiu 42%" |
| **Informativo (azul)** | Só no feed do Consultor, sem badge urgente | Oportunidades, curiosidades, sazonalidade, dinheiro parado | "R$3.200 parados há 45 dias" |
| **Nudge de hábito (neutro)** | Banner fino dispensável, cooldown de 1x/dia | Sem lançamento hoje; recorrência detectada sem estar cadastrada | "Ainda não lançou nada hoje" |

### Central de alertas (sino no topo, já referenciado no código via
`reminderDismissedFor`)
```
🔔 3
┌──────────────────────────────────┐
│  ⚠ Aluguel vence amanhã — R$2.100 │
│  📈 Fornecedores subiu 42%        │
│  💰 R$3.200 parados há 45 dias    │
│                                    │
│  [ Ver tudo no Consultor → ]      │
└──────────────────────────────────┘
```
- Cada item: 1 toque = vai direto pro insight completo (Tela 2).
- Swipe/botão de "Lembrar depois" (usa `reminderDismissedFor`, já existe no
  schema de `TransactionRecurrence`) — nunca "excluir alerta" sem
  reaparecer, senão o usuário perde visibilidade real do risco.

### Canais proativos (fora do app)
Como o produto já tem infraestrutura omnichannel (`conversations`, Meta
Cloud/Baileys), alertas **críticos** devem poder sair também por WhatsApp —
é o canal onde o dono de pequeno negócio realmente olha:
> "🔴 [Nome do negócio] — seu caixa fica negativo em 8 dias (27/07). Abra o
> Financeiro pra ver o que fazer: [link]"

Resumo semanal (domingo à noite ou segunda de manhã, configurável): "Essa
semana você lucrou R$X, tem R$Y a receber e R$Z a pagar nos próximos 7
dias." — não é insight novo, é o hábito de revisão que consultor de verdade
cobra do cliente.

### Regra de "não cansar o usuário"
- Máximo de 1 nudge de hábito visível por sessão.
- Insight dispensado 2x sem ação = rebaixa de "Atenção" pra "Informativo" por
  30 dias (a menos que o risco tenha se agravado).
- Nunca empilhar mais de 3 críticos ao mesmo tempo — se houver mais,
  agrupar: "Você tem 5 situações que merecem atenção agora →".

---

## 8. Onboarding e empty states

### Onboarding (3 passos, pulável, ≤ 90 segundos)

1. **"Como você recebe e paga hoje?"** — conecta conta bancária (Open
   Finance/OFX) *ou* "vou lançar na mão por enquanto" (nunca bloqueia o
   avanço por falta de integração bancária).
2. **"O que é normal gastar no seu tipo de negócio?"** — template de
   categorias por segmento (o produto já modela `segment`/vertical do
   negócio, ver `project_turmas_e_vertical_agente`): restaurante, salão,
   academia, loja, serviços — pré-popula `financialCategories` mais usadas
   pra não começar do zero.
3. **"Qual é o seu colchão mínimo de caixa?"** — valor abaixo do qual o
   usuário quer ser avisado com urgência máxima ("Se meu saldo chegar a
   R$_____, me avisa igual emergência"). Isso vira o parâmetro do
   Termômetro/insight #1 — dá controle e ensina o conceito de reserva.

Ao final: primeira leitura do Termômetro já aparece, mesmo com poucos
dados — nunca tela em branco: "Ainda estamos aprendendo seu negócio. Lance
seus primeiros lançamentos e o Consultor começa a te avisar de riscos
automaticamente."

### Empty states (por área — cada um ensina algo)

| Tela vazia | Copy | CTA |
|---|---|---|
| Lançamentos (zero registros) | "Nenhum lançamento ainda. É aqui que sua visão financeira nasce — sem lançamento, nem o melhor consultor do mundo consegue te ajudar." | `[ Lançar o primeiro agora ]` |
| Consultor (dados insuficientes) | "Preciso de pelo menos 2 semanas de lançamentos pra te dar conselhos confiáveis. Continue lançando — a barra abaixo mostra o quanto falta." + barra de progresso | `[ Lançar mais ]` |
| Fluxo de Caixa (sem recorrências cadastradas) | "Sem suas contas fixas cadastradas, essa previsão é só o passado. Cadastre o aluguel, folha e assinaturas pra prever o futuro de verdade." | `[ Cadastrar recorrência ]` |
| Conciliação (sem conta bancária conectada) | "Conecte sua conta pra eu cruzar automaticamente o extrato com o que você já lançou — economiza a conferência manual no fim do mês." | `[ Conectar conta ]` |
| Contas a receber vencidas (zero atrasos — estado positivo!) | "Nenhum cliente te deve nada em atraso. Isso é raro — parabéns, poucos negócios chegam aqui." | — (celebração, sem CTA) |

---

## 9. Progressive disclosure — simples por padrão, fundo pra quem quer

### Toggle global "Modo Simples / Modo Avançado"
Persistido por usuário (não por negócio — o dono e o contador podem ter
preferências diferentes no mesmo tenant).

| | Modo Simples (padrão) | Modo Avançado |
|---|---|---|
| KPIs da Home | 3 números (Caixa hoje / 30 dias / Lucro) | + margem, ticket médio, DSO (prazo médio de recebimento), DPO |
| Categorização | Chips sugeridos, categoria única | + centro de custo, projeto, unidade de negócio |
| Fluxo de Caixa | Gráfico + cenário Realista/Otimista/Pessimista | + exportar CSV, editar taxa de conversão manualmente, comparar períodos |
| Relatórios | "Como estou indo" (linguagem natural) | DRE completa, balanço simplificado, exportação contábil |
| Consultor | Cartão com 1 causa + 1-3 ações | + "Ver como calculamos" sempre expandido por padrão |

Nunca esconder **dado** — só **densidade de UI**. Todo campo avançado
continua gravado no Firestore mesmo em Modo Simples (ex.: usuário pode não
ver "centro de custo" na tela, mas se o lançamento já tinha um, ele
persiste).

### Padrão de interação para revelar profundidade
- `(ⓘ)` ao lado de qualquer termo/número → tooltip com explicação simples.
- "Ver como calculamos ⌄" → accordion inline (Framer Motion `height: auto`,
  sem blur no exit).
- Long-press / clique direito em KPI (desktop) → menu "Ver detalhado",
  "Exportar", "Comparar período".
- Botão flutuante discreto "Modo especialista" no rodapé de qualquer
  gráfico — não é um menu de configurações escondido, é uma affordance
  visível mas não competindo com a ação primária.

---

## 10. Sistema de componentes (nomeação sugerida, consistente com o
codebase atual)

```
components/features/financial/
├── home/
│   ├── HealthGauge.tsx            (termômetro 0–100, arco animado)
│   ├── HeroSummaryCard.tsx        (frase-resumo + CTA primário)
│   ├── ThreeNumbersRow.tsx        (Caixa hoje / 30 dias / Lucro)
│   ├── Next7DaysStrip.tsx
│   └── InsightPreviewRow.tsx
├── consultor/
│   ├── InsightFeed.tsx
│   ├── InsightCard.tsx            (variantes: critico/atencao/info)
│   ├── InsightCalculationDrawer.tsx  ("ver como calculamos")
│   └── WhatIfSimulator.tsx
├── cashflow/
│   ├── CashFlowChart.tsx          (realizado sólido / previsto tracejado)
│   ├── ScenarioToggle.tsx
│   └── RiskZoneOverlay.tsx
├── quick-entry/
│   ├── QuickEntryFab.tsx          (⊕ global)
│   ├── QuickEntrySheet.tsx        (bottom sheet 4 opções)
│   ├── ReceiptOcrCapture.tsx
│   ├── ReceiptConfirmForm.tsx
│   ├── QuickAmountForm.tsx        (receita/despesa rápida)
│   └── RecurrenceDetectedNudge.tsx (evolução de FIN-R23)
├── alerts/
│   ├── AlertBell.tsx
│   └── AlertDropdown.tsx
└── onboarding/
    ├── OnboardingWizard.tsx
    └── EmptyState.tsx             (variant por tela, tabela §8)
```

Tokens visuais reaproveitados do design system existente:
- `rounded-xl` cards padrão, `rounded-2xl` no `HeroSummaryCard`/gauge.
- Cor semântica: `emerald` (positivo), `amber`→`orange` (atenção), `red-600`
  (crítico/urgente — reserva a cor de marca só pra isso, sem competir com
  CTAs neutros).
- Animação de entrada de `InsightCard`: stagger 60ms, `opacity + y:8→0`,
  nunca `filter: blur` no exit (regra do projeto).

---

## 11. Notas de implementação e gaps (para o time de eng)

- **Contrato Zod novo necessário (R2 do CLAUDE.md):** `lib/contracts/domain/financialInsight.ts`
  — schema do `InsightCard` (`type: 'critico'|'atencao'|'info'`, `trigger`,
  `title`, `body`, `ctas[]`, `calculationTrace[]`, `dismissedAt?`,
  `snoozedUntil?`). Persistir em `financialInsights/{businessId}_{ruleId}_{period}`
  pra idempotência (R3) — não recalcular/reenviar o mesmo insight 2x no
  mesmo período.
- **FSM:** nenhuma mudança necessária em `TransactionStatus` — o desenho
  usa o enum existente (`pendente/pago/atrasado/cancelado`,
  `lib/contracts/fsm/transaction.ts`).
- **OCR:** ativar a integração Google Cloud Vision já referenciada (stub em
  `FinancialModule.tsx:2141`); novo endpoint
  `app/api/financial/ocr/route.ts` (Request: imagem/base64; Response:
  `{ amount, date, vendor, suggestedCategory, confidence }`) — declarar
  contrato em `lib/contracts/api/financial/ocr.ts` antes de implementar (R2).
  Canal WhatsApp reaproveita `app/api/webhooks/meta` — anexar novo caso de
  mídia recebida em número dedicado ao financeiro.
- **Motor de insights:** side-effect cross-módulo nenhum na v1 (é
  read-only sobre `transactions`/`bankAccounts`), mas se um insight
  disparar ação automática (ex.: auto-cobrança via WhatsApp) isso vira
  evento em `lib/contracts/events/` por tocar o módulo de conversas (R5).
- **Cenários do Fluxo de Caixa:** cálculo pode rodar client-side (dado já
  vem via query com `businessId`) — não precisa de nova API se o volume de
  `transactions` por tenant for razoável; monitorar performance se
  `recurrence` + parcelamento crescer muito (paginação/agregação
  server-side como fallback).
- **Segmento do negócio** (usado nos templates de onboarding e benchmark de
  margem) depende do trabalho de vertical/segmento já em andamento — ver
  `project_turmas_e_vertical_agente` na memória do projeto.

---

## 12. Checklist de "tom de consultor" (para revisão de qualquer microcopy nova)

Antes de publicar qualquer texto novo no módulo, verificar:

- [ ] Tem um **número concreto** (R$, %, dias)? Frase vaga tipo "atenção
      com seus gastos" é proibida.
- [ ] Tem uma **causa** nomeada ("porque o aluguel vence antes de você
      receber...")? Nunca só o sintoma.
- [ ] Tem uma **ação com verbo no imperativo** ("Cobre agora", "Adie",
      "Revise")? Nunca terminar em "fique atento".
- [ ] Está em **1ª pessoa do consultor, 2ª do usuário** ("Notei que
      você...")? Nunca 3ª pessoa impessoal ("O sistema detectou...").
- [ ] Jargão tem **tradução acessível** a 1 toque? Se não tiver `(ⓘ)`,
      trocar o termo por linguagem simples direto.
- [ ] Cabe em **2 linhas no card colapsado**? Se não cabe, o "porquê" vai
      pro "Ver como calculamos".

---

## Referências consultadas

- [Cash Flow Tab Overview — Copilot Help Center](https://help.copilot.money/en/articles/9682232-cash-flow-tab-overview)
- [Copilot Money for Web — Copilot Help Center](https://help.copilot.money/en/articles/11780342-copilot-money-for-web)
- [Dashboard de Fluxo de Caixa — Conta Azul Mais](https://ajuda.contaazul.com/hc/pt-br/articles/44911365743501-Dashboard-da-Conta-Azul-Mais-Dashboard-de-Fluxo-de-caixa)
- [Use the cash flow planner in QuickBooks Online](https://quickbooks.intuit.com/learn-support/en-us/help-article/budget-forecast-reports/use-cash-flow-planner-quickbooks-online/L2l59mIqe_US_en_US)
- [Ramp Intelligence — AI for finance](https://ramp.com/intelligence)
