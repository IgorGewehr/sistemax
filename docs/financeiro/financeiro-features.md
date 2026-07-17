# Módulo Financeiro — Design Brief

> **Missão do módulo:** dar a um dono de restaurante, mercado, posto, assistência técnica,
> padaria ou lojinha — que nunca leu um livro de contabilidade — a mesma clareza que um
> consultor financeiro sênior teria sobre o próprio negócio. Não é "um sistema de
> lançamentos". É o sistema imunológico financeiro do pequeno negócio brasileiro.
>
> Segundo o Sebrae, **48% das micro e pequenas empresas fecham por falta de planejamento
> financeiro e descontrole de caixa** — não por falta de vendas. O financeiro é o módulo
> que ataca essa causa-raiz diretamente. Todo outro módulo (PDV, Pedidos, Agenda, Compras,
> Estoque, CRM) existe, em parte, para alimentar este com dados verdadeiros.

---

## 0. Três princípios de design que governam tudo abaixo

1. **Tradução, não jargão.** Nunca mostrar "DRE", "margem de contribuição" ou "regime de
   competência" como primeira camada. Mostrar a pergunta que o dono realmente tem
   ("quanto sobrou de verdade?", "posso pagar o fornecedor sexta?", "esse produto dá
   lucro ou só dá trabalho?") e revelar o termo técnico como *rótulo secundário*, não
   como *rótulo primário*. Todo card tem um "i" com explicação em 1 frase, sem link pra
   glossário externo.
2. **Read-only por padrão, escrita por exceção.** 90% dos dados financeiros já nascem em
   outro módulo (venda no PDV, pedido no Pedidos, comissão na Agenda, compra em Compras).
   O Financeiro deve ser onde o dono **entende**, não onde ele **digita de novo**. Toda
   feature nova primeiro pergunta "esse dado já existe em algum módulo?" antes de propor
   um formulário.
3. **Alertar antes do fato consumado, não relatar depois.** Um relatório de margem caindo
   em março sobre dados de fevereiro é história. Um alerta de "caixa fica negativo dia 18"
   emitido dia 3 é gestão. O diferencial competitivo deste módulo é a **antecedência**, e
   o ServicePro tem algo que Conta Azul/Omie/Granatum não têm: um agente de IA e canal de
   WhatsApp já plugados (`lib/agent`, `/api/financial/notify`) — os alertas podem chegar
   como mensagem conversacional, não como badge que ninguém abre.

---

## 1. O que já existe no ServicePro (ponto de partida real, não greenfield)

Antes de desenhar features novas, mapeei o que já está construído em
`app/components/features/financial/FinancialModule.tsx` (6.611 linhas) e
`lib/types/index.ts`, pra não duplicar contrato nem reinventar dado que já existe.

| Já existe | Onde | Cobre parcialmente qual feature abaixo |
|---|---|---|
| `Transaction` completo: `category`, `costCenter`, `businessUnitId`, `sectorId`, `channelType`, `projectId`, `clientId`, `installmentGroupId` | `lib/types/index.ts:1222` | Categorias, centro de custo, análise por canal/setor/projeto |
| `TransactionRecurrence` com frequência, multa/juros pró-rata, ajuste de dia útil, histórico de pagamentos | `lib/types/index.ts:1191` | Recorrências (já é o mais maduro do módulo — não mexer na engine, só na apresentação) |
| Tabs hoje: `visao-geral, lancamentos, recorrentes, contas, projetos, comissoes, conciliacao, auditoria` | `FinancialModule.tsx:162` | Estrutura de navegação — DRE/Ponto de Equilíbrio/CLT precisam de tabs novas |
| Gráfico "Fluxo de Caixa" | `FinancialModule.tsx:3160` | Fluxo de caixa **realizado** já existe; **projetado/cenários não** |
| `BankAccount`, `ReconciliationItem`, `BankStatementImport` (OFX/CSV) | `lib/types/index.ts:1452-1512` | Conciliação bancária — falta PIX/cartão automático (Open Finance) |
| `Budget` (meta orçada por categoria/mês) | `lib/types/index.ts:1360` | Base pronta pra "orçado vs realizado", hoje sem UI dedicada visível |
| `DasRecord` (Simples Nacional: RBT12, anexo, alíquota efetiva, vencimento) | `lib/types/index.ts:1376` | Carga tributária — o cálculo já existe, falta o **simulador de Fator R** e a UI "vou pagar isso" |
| `Employee` (nome, cargo, salário, benefícios) | `lib/types/index.ts:1515` | Custo CLT — falta o **motor de cálculo de encargos** (hoje é só cadastro cru) |
| `Partner` (sócio, % de participação, valor investido) | `lib/types/index.ts:1530` | Base pra distribuição de lucro entre sócios |
| `BusinessUnit` | `lib/types/index.ts:1437` | Rentabilidade por unidade de negócio (múltiplos SaaS/lojas) |
| `Product.costPrice` / `Product.salePrice` | `lib/types/index.ts:1553-1554` | Base pronta pra margem por produto — falta ligar ao CMV real (com componentes/BOM) |
| `/api/financial/notify` — WhatsApp + e-mail de vencimento e cobrança, idempotente (`dueSoonNotifiedAt`/`overdueNotifiedAt`) | `app/api/financial/notify/service.ts` | Já resolve "conta vencendo" pro **cliente**; falta o mesmo canal apontado pro **dono** (caixa negativo, margem caindo, DAS a vencer) |
| `FinancialAuditLog` | `lib/types/index.ts:1399` | Trilha de auditoria já cobre create/update/delete/pay/cancel/restore |

**Conclusão prática:** a fundação de dados é sólida (rara pra um MVP). O gap real está em
**camadas de leitura/insight** (DRE, ponto de equilíbrio, margem de contribuição, CLT,
projeção, alertas proativos ao dono) — não em modelagem de dado bruto. Isso muda a
priorização: menos "criar coleção nova", mais "criar view derivada + motor de cálculo".

---

## 2. Referências de mercado — o que roubar, o que evitar

| Produto | O que roubar | O que evitar |
|---|---|---|
| **Conta Azul** | Separação clara Dashboard de Fluxo de Caixa (regime de caixa, "quando o dinheiro muda de mão") vs Dashboard de DRE (regime de competência, "quando a obrigação nasceu") como **duas telas distintas com nomes explícitos da diferença**. Centro de custo como rateio simples de lançamento. | Vocabulário contábil como primeira camada (feito pra contador acompanhar cliente, não pro dono leigo decidir sozinho). Preço/complexidade que assusta quem tem 1 funcionário. |
| **Granatum** | DRE que nasce **automaticamente da categorização do lançamento** (a categoria já diz se é custo direto, variável ou despesa fixa) — elimina o trabalho manual de montar DRE. Conteúdo educativo formatado dentro do próprio produto. | É só financeiro — não recebe feed automático de PDV/estoque/agenda, então o dono ainda digita tudo à mão. O ServicePro já tem o feed; a DRE deve nascer **de graça** disso. |
| **QuickBooks (Cash Flow Planner/Projector)** | Horizonte de projeção editável (6 semanas / 90 dias / 1 ano), itens recorrentes que entram automático na projeção, cenários "e se" aplicados sobre a mesma base. Granularidade semanal (mais acionável que mensal). | Modelo de competência estilo GAAP americano — não серve pro regime brasileiro (Simples Nacional, PIX, boleto, DAS). Não copiar a lógica fiscal, só a UX de projeção. |
| **Nubank PJ / Stone** | Extrato como fonte de verdade sem fricção — o dono não deveria precisar "importar CSV" quando o dado já está disponível via Open Finance. Simplicidade radical de linguagem em app. | São contas bancárias, não cérebro financeiro — não fazem DRE, ponto de equilíbrio ou análise de rentabilidade. Não competir em ser banco; competir em ser o que o banco não é. |
| **Omie** | Financeiro "de fábrica", não add-on: contas a pagar/receber + fluxo de caixa + DRE + regime de competência funcionando desde o dia 1, com API que expõe DRE estruturada (permite embutir em dashboard). | Complexidade cresce rápido com volume (pensado pra quem emite 800+ NF-e/mês) — para o público-alvo do ServicePro (padaria, assistência técnica) isso é peso morto na UI. |
| **Bling** | Nada específico de financeiro a copiar (força é fiscal/venda). | DRE fraca, terceirizada para Power BI externo. Nunca terceirizar analytics financeiro — o ServicePro já tem a base transacional pra fazer nativo. |
| **Asaas / réguas de cobrança automatizadas** | Régua de cobrança multicanal (WhatsApp + e-mail) sem custo extra, com escalonamento por dias de atraso. Dado de mercado: em PME B2B com inadimplência 2-4%, régua automática resolve 70-80% do problema sozinha. | Tom robótico/agressivo genérico — o ServicePro tem agente de IA que pode personalizar o tom por cliente/histórico, não só disparar template. |
| **SisFood / ERPs de restaurante** | Margem por produto **e** por canal **e** por horário automática; alerta de "item com margem apertada antes que vire prejuízo". Dado de mercado: delivery via app de terceiro custa 35-40% do faturamento do canal (comissão ~23% + taxas + ads) — a "conta completa" costuma revelar margem bem menor que salão. | Ferramentas 100% verticais em food não generalizam pra posto/assistência técnica. O conceito certo a generalizar é **"canal de venda"** (mesa/balcão/entrega própria/app terceiro/PDV/OS) aplicável a qualquer vertical do público-alvo. |
| **Open Finance (Pluggy, Nibo Conciliador, Brickup)** | Conciliação automática puxando extrato de qualquer banco via API, não só upload manual de OFX. Dado de mercado: menos de 10% dos consentimentos ativos no Open Finance são de empresas PJ — quem se mover primeiro nesse nicho ainda tem vantagem competitiva real em 2026. | Ficar refém de 1 único provedor Open Finance sem plano B (upload manual precisa continuar existindo como fallback). |

---

## 3. Origem de dados por módulo (mapa de dependência)

```
PDV/Vendas ────────► sales, transactions, stockMovements ──► receita realizada, CMV por venda
Pedidos (delivery) ─► deliveryOrders, transactions ─────────► receita por canal (app terceiro vs próprio)
Agenda ─────────────► appointments, transactions (comissão) ─► receita de serviço, custo de comissão
Compras ────────────► purchaseNotes, transactions ──────────► despesa de insumo/fornecedor, custo variável
Estoque ────────────► products (costPrice, components[]) ───► CMV real por produto/BOM, giro de estoque
CRM/Clientes ───────► clients, contactId em Transaction ─────► inadimplência por cliente, LTV, cobrança
Settings ───────────► Business.fiscal, Employee, Partner ────► regime tributário, custo CLT, distribuição de lucro
Financeiro (nativo) ─► Transaction, BankAccount, Budget ─────► tudo que não vem de outro módulo (aluguel, água, luz, pró-labore)
```

Regra de design: **toda vez que um dado já existe em outro módulo, o Financeiro
consome via leitura (query filtrada por `businessId`), nunca duplica em formulário
manual.** Onde isso já não acontece hoje (ex: custo de funcionário, carga tributária)
é exatamente onde o gap de UX está.

---

## 4. Features — catálogo completo

Cada feature segue o formato: **O que é** · **Por que importa pra sobrevivência** ·
**Dados e origem** · **Como aparece pro leigo**.

### 4.1 Fluxo de caixa — realizado

- **O que é:** saldo de caixa dia a dia, mês a mês, considerando **regime de caixa**
  (data em que o dinheiro efetivamente entrou/saiu, não a data da venda).
- **Por que importa:** é a pergunta mais visceral de sobrevivência: "tenho dinheiro no
  banco pra passar a semana?". Lucro no papel não paga boleto — só caixa paga.
- **Dados/origem:** `Transaction.status === 'pago'` + `paymentDate`, agrupado por
  `bankAccountId`; saldo inicial de `BankAccount.balance`. Já existe (gráfico
  `FinancialModule.tsx:3160`) — manter, mas adicionar granularidade **semanal**, não só
  mensal (semana é o horizonte em que o dono realmente age).
- **Como aparece pro leigo:** um número gigante no topo — "Você tem R$ 4.230 hoje" —
  com uma linha do tempo abaixo (últimos 30 dias) sem eixo/legenda técnica, só
  "subiu"/"desceu" com cor verde/vermelha nos dias de queda forte.

### 4.2 Fluxo de caixa — projetado/previsto

- **O que é:** projeção do saldo de caixa pros próximos 7/15/30/90 dias, combinando
  contas a pagar e a receber já lançadas (`dueDate` futuro, `status: 'pendente'`) +
  recorrências programadas (`TransactionRecurrence.nextDueDate`) + média histórica de
  vendas não lançadas ainda (heurística: média móvel dos últimos 4 períodos iguais).
- **Por que importa:** é a feature #1 de prevenção de falência. Segundo pesquisa de
  mercado, sinalizar que o caixa fica negativo dia 18 do mês que vem dá ao dono **duas
  semanas de antecedência** pra agir (cobrar, antecipar recebível, renegociar prazo,
  segurar uma compra) — a diferença entre gestão e apagar incêndio.
- **Dados/origem:** `transactions` pendentes (ambos os módulos que geram Transaction:
  PDV, Pedidos, Agenda, Compras) + `TransactionRecurrence` + histórico de `sales`/
  `deliveryOrders` pra estimar receita não lançada.
- **Como aparece pro leigo:** mesma linha do tempo do 4.1, mas a parte futura em
  tracejado, com um marcador vermelho automático no primeiro dia em que o saldo
  projetado fica negativo — texto plain: **"Se nada mudar, seu caixa fica negativo em
  18 de agosto (R$ -830)."** Botão de ação direto: "ver o que vence essa semana".

### 4.3 Contas a pagar e a receber

- **O que é:** lista operacional de tudo que vai sair/entrar, com status
  (pendente/pago/atrasado/cancelado), vencimento, e ação em 1 clique pra dar baixa.
- **Por que importa:** é o "boleto que não pode esquecer" — atraso gera multa/juros e,
  pior, corte de crédito com fornecedor/energia/água, que pode parar a operação.
- **Dados/origem:** já existe integralmente (`Transaction.type`, `.dueDate`,
  `.status`, `.paymentMethod`). Ligação automática: venda a prazo no PDV → conta a
  receber; compra a prazo → conta a pagar; comissão de agenda → conta a pagar.
- **Como aparece pro leigo:** duas listas simples — "Vai sair" (vermelho) e "Vai
  entrar" (verde) — cada linha com quem, quanto, quando, e badge "atrasado" em
  destaque. Nunca a palavra "contas a pagar/receber" sozinha sem o ícone de seta.

### 4.4 DRE gerencial simplificado

- **O que é:** Demonstração do Resultado — mas **por competência**, não por caixa —
  respondendo "esse mês, considerando tudo que foi vendido/gasto (pago ou não), sobrou
  quanto de verdade?". Estrutura mínima: Receita bruta → (-) Deduções/impostos → Receita
  líquida → (-) Custos diretos/variáveis (CMV, comissão, taxa de cartão/app) → Margem
  bruta → (-) Despesas fixas (aluguel, folha, contas) → Resultado operacional →
  (-) Pró-labore/distribuição → Resultado do dono.
- **Por que importa:** é o retrato mais fiel de "o negócio, no fundo, dá lucro?" —
  diferente do caixa, que pode estar positivo só porque um empréstimo entrou. É a
  pergunta que decide se vale a pena continuar, expandir ou fechar uma unidade/produto.
- **Dados/origem:** `Transaction.category` + novo campo de classificação por linha de
  DRE (custo direto / despesa fixa / dedução), derivável automaticamente de
  `FinancialCategory.type` já existente — só precisa de um subtipo a mais (ver §7,
  contrato novo). CMV vem de `stockMovements` (saída por venda) × `Product.costPrice`
  (já com suporte a BOM via `components[]`).
- **Como aparece pro leigo:** nunca a tabela contábil crua. Um "termômetro" visual:
  Receita → menos isso → menos aquilo → **sobrou X** — com cada bloco do tamanho
  proporcional (waterfall chart), e comparação automática "mês passado sobrou Y, esse
  mês sobrou X" (alta/queda em %). Toggle "modo detalhado" pra quem quer ver a tabela
  clássica (contador da empresa vai querer exportar isso em PDF/Excel).

### 4.5 Conciliação (bancária, cartão, PIX)

- **O que é:** cruzar automaticamente o que o sistema registrou como recebido/pago com
  o que efetivamente entrou/saiu na conta bancária/maquininha/PIX, sinalizando só as
  divergências.
- **Por que importa:** é onde fraude, taxa cobrada errado, venda não lançada e
  duplicidade aparecem. Sem isso, o dono só descobre que "faltou dinheiro" quando já é
  tarde. Também é pré-requisito de confiança pra qualquer relatório financeiro fazer
  sentido (garbage in, garbage out).
- **Dados/origem:** já existe base sólida (`ReconciliationItem`, `BankStatementImport`,
  `ReconciliationRule` para categorização automática por padrão de texto). Falta:
  entrada automática via Open Finance (puxar extrato via API, não só upload de
  OFX/CSV) e reconciliação de maquininha/gateway (Stone/Cielo/Pagar.me/Mercado Pago) —
  hoje cobre banco, falta cartão/PIX de adquirente.
- **Como aparece pro leigo:** três colunas simples — "bateu certinho" (verde, maioria),
  "sobrou no banco" (algo entrou que o sistema não sabe — normalmente venda não
  lançada), "sobrou no sistema" (algo que o sistema acha que devia ter entrado e não
  entrou — normalmente inadimplência ou erro de lançamento). Ação de 1 clique por item
  divergente ("isso é a venda X? Sim/Não").

### 4.6 Ponto de equilíbrio

- **O que é:** quantidade de vendas (em R$ ou em unidades) necessária pra cobrir todos
  os custos fixos e variáveis — abaixo disso, o mês dá prejuízo.
  Fórmula: `Ponto de Equilíbrio (R$) = Custos Fixos / Margem de Contribuição %`.
- **Por que importa:** responde à pergunta mais concreta de sobrevivência mensal:
  "quanto eu preciso vender esse mês só pra não perder dinheiro?" — vira a meta diária/
  semanal mais acionável que existe (mais útil que "meta de faturamento" arbitrária).
- **Dados/origem:** custos fixos = soma de `Transaction` recorrentes categorizadas como
  despesa fixa (aluguel, folha, contas); margem de contribuição % vem de 4.7.
- **Como aparece pro leigo:** **"Você precisa vender R$ 187/dia (ou 12 pratos) só pra
  cobrir as contas do mês. Hoje você já vendeu R$ 210 — parabéns, esse dia já 'pagou a
  casa'."** Barra de progresso diária/mensal com o ponto de equilíbrio marcado, e
  tudo que passa disso pintado como "lucro puro do dia".

### 4.7 Margem de contribuição

- **O que é:** quanto sobra de cada R$1 vendido depois de descontar **só os custos
  variáveis** (o que muda direto com a venda: insumo, embalagem, taxa de cartão,
  comissão de app) — antes de entrar custo fixo. `MC% = (Receita - Custo Variável) /
  Receita`.
- **Por que importa:** é o número que decide se vender mais resolve o problema ou só
  aumenta o prejuízo. Um produto/canal com margem de contribuição negativa **piora**
  quanto mais vende — clássica armadilha do "vendi muito e não sobrou nada".
- **Dados/origem:** por venda/pedido: `Sale.items`/`DeliveryOrderItem` (preço) menos
  `Product.costPrice` (+ componentes BOM) menos taxa de canal (cartão/app, se
  configurada por `PaymentMethod`/canal).
- **Como aparece pro leigo:** ao lado de cada produto/canal no catálogo, um selo
  simples: 🟢 "sobra bem" / 🟡 "sobra pouco" / 🔴 "não sobra nada (ou dá prejuízo)",
  com o número exato ao passar o mouse/tocar. Nunca a sigla "MC" como rótulo principal.

### 4.8 Custo por produto/serviço/canal

- **O que é:** custo real (CMV/CPV) de cada item vendido e de cada forma de venda
  (salão, balcão, entrega própria, app terceiro, PDV presencial, ordem de serviço),
  incluindo taxas específicas do canal.
- **Por que importa:** delivery por app de terceiro parece ótimo em volume, mas o custo
  real (comissão ~23% + taxas + anúncio patrocinado) pode chegar a **35-40% do
  faturamento daquele canal** — a "conta completa" às vezes revela prejuízo líquido
  onde parecia sucesso. Sem isso, o dono decide baseado só em "quanto vendeu", nunca em
  "quanto sobrou".
- **Dados/origem:** `Product.costPrice`/`components[]` (BOM), `stockMovements` por
  origem (`saleId`/`orderId`), `channelType` em `Transaction`, taxas de canal
  configuráveis por método de pagamento/plataforma de delivery.
- **Como aparece pro leigo:** tabela ranqueada "seus 5 produtos mais lucrativos" e "seus
  5 produtos que mais dão prejuízo/trabalho", e comparação lado a lado por canal:
  "Vender pelo salão sobra R$ 18. Vender pelo app X sobra R$ 4." — decisão acionável
  na hora (reprecificar no app, ou tirar do cardápio de delivery).

### 4.9 Análise de rentabilidade (cliente, produto, canal, setor/unidade)

- **O que é:** cruzamento de receita − custo − tempo/esforço por diferentes cortes:
  cliente (LTV, ticket médio, frequência), produto/serviço, canal de venda, setor
  (`sectorId`), projeto/unidade de negócio (`projectId`/`businessUnitId`).
- **Por que importa:** 20% dos clientes/produtos normalmente geram 80% do lucro (regra
  de Pareto) — sem essa lente, o dono trata todo cliente/produto como igual e investe
  atenção/desconto onde não deveria.
- **Dados/origem:** já há campos prontos em `Transaction` (`clientId`, `sectorId`,
  `projectId`, `businessUnitId`, `channelType`) — feature é majoritariamente de
  **agregação e apresentação**, não de captura de dado novo.
- **Como aparece pro leigo:** rankings simples com linguagem de negócio: "Seus clientes
  fiéis" (top LTV), "Clientes que só compram no desconto" (baixa margem, alta
  frequência), "Seu produto-âncora" (maior contribuição de lucro total, não maior
  volume).

### 4.10 Regime de caixa vs competência

- **O que é:** um toggle explícito em qualquer relatório monetário — "ver como
  dinheiro que mudou de mão" (caixa) vs "ver como resultado do que foi vendido/gasto,
  pago ou não" (competência) — nunca dois números diferentes sem explicação do porquê.
- **Por que importa:** é a maior fonte de confusão de PME leiga: "o sistema disse que
  lucrei R$5 mil mas não tem R$5 mil no banco" — sem explicar a diferença, o dono perde
  confiança no sistema inteiro (acha que tem bug).
- **Dados/origem:** mesma base de `Transaction`, só muda o filtro (`paymentDate` vs
  data de competência/criação do fato gerador).
- **Como aparece pro leigo:** toda vez que os dois números aparecem juntos e diferem
  significativamente, um texto automático explica: **"O DRE mostra que sobrou R$5.000
  (o que você vendeu), mas seu caixa só tem R$1.200 porque R$3.800 ainda estão pra
  receber de clientes."** Isso educa em vez de confundir.

### 4.11 Categorias e centros de custo

- **O que é:** classificação de toda receita/despesa em categorias (aluguel, insumo,
  marketing, folha...) e, opcionalmente, rateio por centro de custo (loja A, cozinha,
  entrega, setor X) quando o negócio tem mais de uma unidade/área.
- **Por que importa:** é o alicerce de todo o resto (DRE, margem, rentabilidade não
  existem sem isso). Também é onde o dono, ao simplesmente lançar/revisar, já aprende
  onde o dinheiro está indo — o ato de categorizar já é diagnóstico.
- **Dados/origem:** `FinancialCategory`, `Transaction.category`, `Transaction.costCenter`
  — já existe. Melhoria: **categorização automática** (por regra hoje via
  `ReconciliationRule`; sugerir extensão pra sugestão por IA quando não há regra,
  aproveitando o agente já existente).
- **Como aparece pro leigo:** categorias pré-cadastradas por vertical (padaria já vem
  com "farinha/insumo", "gás", "entrega"; posto já vem com "combustível", "ISS
  bandeira"...) em vez de plano de contas contábil genérico em branco — reduz a fricção
  de configuração inicial a quase zero.

### 4.12 Recorrências

- **O que é:** lançamentos que se repetem (aluguel, salário, assinatura, financiamento)
  com geração automática da próxima parcela, multa/juros pró-rata em atraso e ajuste
  pra dia útil.
- **Por que importa:** é o que garante que a projeção de caixa (4.2) e o ponto de
  equilíbrio (4.6) não fiquem cegos pro que é previsível — a maior parte do custo fixo
  de uma PME é recorrente.
- **Dados/origem:** **já existe e é a parte mais madura do módulo**
  (`TransactionRecurrence` com `frequency`, `holidayAdjust`, `lateFeePct`,
  `interestPctMonth`, `history`). Não propor redesenho — só garantir que 4.2/4.4/4.6
  consomem essa engine em vez de recalcular na mão.
- **Como aparece pro leigo:** lista "seus compromissos fixos" com ícone de recorrência,
  próxima data e valor, e alerta automático se um valor recorrente subiu (ex: conta de
  luz 20% mais cara que o mês passado) — sinal de atenção sem o dono precisar comparar
  manualmente.

### 4.13 Projeção/cenários "e se..."

- **O que é:** simulador onde o dono altera uma variável (preço, custo de insumo,
  quantidade de funcionários, aluguel novo, queda de X% nas vendas) e vê o impacto
  projetado em caixa, DRE e ponto de equilíbrio, sem alterar nenhum dado real.
- **Por que importa:** é a ferramenta que transforma decisão de "achismo" em decisão
  simulada — "e se eu aumentar o preço 10%?", "e se eu contratar mais um funcionário?",
  "e se o aluguel subir R$ 500?" — perguntas que hoje só um consultor responderia com
  planilha.
- **Dados/origem:** parte da mesma base de custos fixos/variáveis já mapeada (4.6/4.7),
  aplicada sobre uma cópia em memória (não grava nada em `transactions` — é
  hipotético). Requer um contrato novo (`Scenario`, ver §7).
- **Como aparece pro leigo:** três a cinco "cenários prontos" com um clique (Cenário
  "Vendas caem 20%", "Aumento de aluguel", "Contratar mais 1 pessoa", "Aumentar preço
  em X%") + um modo livre com sliders. Resultado sempre em frase, não em tabela: **"Se
  as vendas caírem 20% no próximo mês, seu caixa fica negativo em 12 dias."**

### 4.14 Alertas inteligentes

Cinco alertas mínimos, cada um com gatilho, dado de origem e canal de entrega (bell
in-app já existe via `TransactionRecurrence.reminderDismissedFor`; WhatsApp/e-mail já
existe via `/api/financial/notify` — hoje falando só com o **cliente**, precisa
ganhar uma via falando com o **dono**).

| Alerta | Gatilho | Origem do dado | Mensagem pro leigo |
|---|---|---|---|
| Conta vencendo/vencida | `dueDate` ≤ N dias ou já passou, `status: 'pendente'` | `Transaction` (já parcialmente coberto — falta apontar pro dono, não só pro cliente) | "Sua conta de energia (R$ 340) vence amanhã." |
| Caixa projetado negativo | Projeção de 4.2 cruza zero num horizonte de 30/90 dias | `transactions` pendentes + recorrências | "Se nada mudar, seu caixa fica negativo em 18/08 (R$ -830). Toque aqui pra ver o que fazer." |
| Queda de margem | Margem de contribuição de um produto/canal cai > X p.p. vs média móvel | 4.7 comparado período a período | "A margem da pizza calabresa caiu de 42% pra 28% este mês — o queijo subiu de preço?" |
| Cliente inadimplente (risco de virar prejuízo) | `Transaction` receita `overdue` > N dias, ou cliente com 2+ atrasos em 90 dias | `transactions` + `clients` | "João está com R$ 620 em atraso há 12 dias — esse é o 3º atraso dele este ano." |
| Imposto a recolher | `DasRecord.vencimento` próximo, ou RBT12 perto de mudar de faixa/anexo | `DasRecord` (já existe cálculo, falta o alerta proativo) | "Seu DAS de R$ 612 vence dia 20. Guarde esse valor à parte." |

- **Por que importa (conjunto):** cada um desses cinco é, isoladamente, um motivo comum
  de PME quebrar (inadimplência não vista, imposto atrasado com multa, margem que
  erodiu sem ninguém perceber, conta vencida gerando corte de serviço, caixa que vira
  negativo sem aviso). Juntos, é a rede de segurança do módulo.
- **Como aparece pro leigo:** sino no topo (já existe o padrão de badge do Financeiro)
  **+ mensagem de WhatsApp do próprio número comercial pro dono** (reaproveitando o
  canal `/api/financial/notify` e a infra do agente) — o alerta que mais importa não
  deve depender do dono abrir o sistema.

### 4.15 Custo real de funcionário CLT

- **O que é:** ao cadastrar um `Employee` com salário bruto, calcular automaticamente o
  custo mensal total pra empresa — não só o salário.
- **Por que importa:** é uma das contas mais erradas por leigos. O custo real de um CLT
  fica **1,6x a 1,8x o salário bruto** somando férias (+1/3), 13º, FGTS (8%), provisão
  de multa rescisória (~4%) e, fora do Simples Nacional, INSS patronal (~20%) + RAT/
  terceiros (~5-9%). Contratar "achando" que custa só o salário é um erro clássico que
  aperta o caixa 2 ou 3 meses depois (nas férias/13º).
- **Dados/origem:** `Employee.salary` + `Employee.benefits` + `Business.fiscal`
  (regime tributário, pra saber se aplica INSS patronal separado ou se já está
  embutido no DAS do Simples Nacional — nuance que a maioria das calculadoras
  genéricas erra).
- **Como aparece pro leigo:** ao lado do salário no cadastro, badge automático: **"Esse
  funcionário custa de verdade R$ 5.100/mês pra empresa (não R$ 3.000)."** Com um
  detalhamento expansível (FGTS, férias, 13º...) só pra quem quiser abrir. Simulador
  "antes de contratar": "posso pagar R$ 3.000 de salário?" → mostra o custo total e
  compara com a margem de contribuição disponível (integra com 4.7: "seu ponto de
  equilíbrio sobe X vendas/dia se você contratar").

### 4.16 Carga tributária estimada (Simples Nacional / MEI)

- **O que é:** cálculo contínuo (não só no fechamento do mês) de quanto o negócio deve
  de imposto, considerando o regime real da empresa.
  - **MEI:** valor fixo por atividade — comércio/indústria R$76,90, serviços
    R$80,90, comércio+serviços R$81,90 (valores 2026, reajustados com o salário
    mínimo) — teto de faturamento R$81.000/ano (R$6.750/mês); alerta se estiver perto
    de estourar (>20% acima desenquadra retroativo a janeiro).
  - **Simples Nacional:** RBT12 (receita bruta 12 meses) × alíquota nominal do anexo
    (I a V) − parcela a deduzir, dividido por RBT12 = alíquota efetiva (fórmula da
    Resolução CGSN 140/2018) — `DasRecord` já implementa isso.
- **Por que importa:** imposto é a despesa que mais surpreende (porque não é
  visível dia a dia como aluguel) e que mais rápido vira dívida com multa/juros da
  Receita se atrasar. Saber o valor **antes** do vencimento, não no dia 19, é o que
  permite separar o dinheiro com antecedência.
- **Dados/origem:** `DasRecord` (já modela isso corretamente); falta a UI proativa e
  o **simulador de Fator R** — para empresas de serviço (Anexo III vs V): se a folha
  de salários dos últimos 12 meses (incluindo pró-labore + FGTS) for ≥ 28% do RBT12, a
  empresa cai pro Anexo III (alíquota inicial 6%) em vez do Anexo V (alíquota inicial
  15,5%) — diferença que pode representar milhares de reais por mês.
- **Como aparece pro leigo:** número simples no topo do fluxo de caixa projetado (já
  reservado, não "sobra" pro dono gastar): **"Reservado pra imposto: R$ 1.240"**. Pro
  Fator R: **"Se você formalizar R$ 400 a mais de pró-labore, seu imposto desse mês cai
  de Anexo V pra Anexo III — economia estimada de R$ 380/mês."** (com aviso "confirme
  com seu contador" — o sistema simula, não substitui contador).

### 4.17 "Quanto sobrou de verdade" (resumo de fim de mês)

- **O que é:** a pergunta mais importante do módulo, isolada em um único número — não é
  o lucro do DRE (que é competência) nem o saldo do caixa (que pode incluir dinheiro
  que já tem dono — fornecedor, imposto, parcela de empréstimo). É: **quanto dá pra
  tirar da empresa hoje sem sufocar o caixa amanhã.**
  Fórmula prática: `Saldo em caixa − contas a pagar nos próximos 15/30 dias − imposto
  já reservado (DAS/MEI do mês) − parcela de dívida/empréstimo do período − reserva
  mínima sugerida (ex: 1x custo fixo mensal)`.
- **Por que importa:** é a armadilha nº1 do pequeno empresário brasileiro — confundir
  faturamento, lucro contábil e dinheiro disponível, e tirar pró-labore/distribuição
  além do que o caixa aguenta, quebrando o negócio 2-3 meses depois quando o boleto
  grande chega. Também dá a base pra decidir pró-labore vs distribuição de lucro (tema
  de planejamento tributário: pró-labore isento até R$5 mil + distribuição de lucro
  isenta até R$50 mil/mês, mas isso é decisão pro contador — o sistema só mostra o
  número disponível).
- **Dados/origem:** `BankAccount.balance` (soma de contas ativas) + `transactions`
  pendentes de curto prazo + `DasRecord` do mês + recorrências de dívida
  (`TransactionRecurrence` categorizada como empréstimo/financiamento).
- **Como aparece pro leigo:** o card mais destacado do dashboard, sempre visível,
  em três linhas:
  ```
  Dinheiro no banco hoje:           R$ 8.400
  Já tem dono (contas + imposto):  -R$ 5.100
  ─────────────────────────────────────────
  Você pode tirar até:              R$ 3.300  ✅
  ```
  Com aviso educativo automático quando o número do "lucro" (DRE, 4.4) e o "pode
  tirar" (aqui) divergem muito: **"Seu DRE mostra lucro de R$6.000, mas só R$3.300
  estão livres pra tirar agora — o resto ainda está em vendas a prazo e imposto
  reservado."**

---

## 5. Priorização

### MVP obrigatório (o financeiro não sobrevive sem isso — construir primeiro)

| # | Feature | Por quê é MVP |
|---|---|---|
| 1 | Fluxo de caixa realizado (4.1) | Já existe, só precisa granularidade semanal |
| 2 | Contas a pagar e a receber (4.3) | Já existe integralmente — é o esqueleto de tudo |
| 3 | Categorias e centros de custo (4.11) | Pré-requisito de DRE/margem — já existe, precisa de categorias pré-cadastradas por vertical |
| 4 | Recorrências (4.12) | Já existe e é maduro — só conectar às projeções |
| 5 | Conciliação bancária básica (4.5, sem Open Finance ainda) | Já existe (OFX/CSV) — é o que garante que os outros números são confiáveis |
| 6 | Fluxo de caixa projetado (4.2) | Maior alavanca de sobrevivência; dados já existem, falta o motor de projeção |
| 7 | Alerta: conta vencendo + caixa negativo projetado (4.14, 2 dos 5) | Sem isso a projeção (item 6) é só um gráfico bonito que ninguém olha a tempo |
| 8 | "Quanto sobrou de verdade" (4.17) | O card que justifica a existência do módulo pro leigo — resposta que ele mais quer |
| 9 | DRE gerencial simplificado (4.4) | Sem isso não existe "o negócio dá lucro?" — mas pode nascer em versão enxuta (sem centro de custo detalhado ainda) |
| 10 | Carga tributária — DAS/MEI já reservado (4.16, sem simulador de Fator R ainda) | `DasRecord` já calcula; falta só mostrar de forma proativa e reservar no fluxo de caixa |

### Fase 2 (depois que o esqueleto está de pé e sendo usado)

| # | Feature | Por quê espera |
|---|---|---|
| 11 | Ponto de equilíbrio (4.6) | Depende de custos fixos/variáveis bem categorizados (item 3 rodando há 1-2 meses) |
| 12 | Margem de contribuição por produto/canal (4.7) | Depende de CMV confiável (ligação correta com `stockMovements`/BOM) |
| 13 | Custo por produto/serviço/canal (4.8) | Extensão direta do item 12 |
| 14 | Análise de rentabilidade — cliente/canal/setor (4.9) | Precisa de volume de dado histórico pra ranking fazer sentido |
| 15 | Regime de caixa vs competência lado a lado com explicação (4.10) | Enriquece o DRE do MVP; não bloqueia nada |
| 16 | Alertas restantes: queda de margem + cliente inadimplente (4.14, os outros 3 já cobertos parcialmente) | Depende de 12/13 (margem) e histórico de cliente (9) |
| 17 | Custo real de funcionário CLT (4.15) | Importante mas não bloqueia sobrevivência imediata — é mais "não erre ao crescer" |
| 18 | Conciliação via Open Finance (upgrade de 4.5) | Diferencial competitivo real, mas manual (OFX/CSV) já resolve o essencial no MVP |
| 19 | Régua de cobrança automática via agente/WhatsApp com tom personalizado (extensão de 4.14) | Hoje já existe versão template fixa — a versão com IA é incremento, não bloqueio |

### Futuro (visão de médio prazo, não crítico pra sobrevivência imediata)

| # | Feature | Por quê é futuro |
|---|---|---|
| 20 | Projeção/cenários "e se..." completo com sliders livres (4.13) | Requer 11-13 maduros e é uma feature de "otimização", não de "sobrevivência" — dono que já não está afogado quer isso |
| 21 | Simulador de Fator R (parte de 4.16) | Alto valor, mas é edge-case (só empresas de serviço perto do limiar de 28%) — não é universal como o resto |
| 22 | Distribuição de lucro entre sócios com sugestão pró-labore vs distribuição (extensão de 4.17) | Precisa de `Partner` maduro + é decisão que depende de contador — sistema deve simular, não decidir |
| 23 | Orçado vs realizado por categoria (ativar `Budget` com UI dedicada) | Ferramenta de gestão avançada — dono leigo raramente orça com precisão nos primeiros meses de uso |
| 24 | Benchmark de vertical ("restaurantes como o seu têm margem X%") | Precisa de massa de dados anonimizada entre tenants — só faz sentido com escala de usuários |
| 25 | Categorização automática por IA (sem regra prévia) | `ReconciliationRule` cobre o caso comum; IA é upgrade de conveniência, não de sobrevivência |

---

## 6. Padrões de UI para "leigo ver como consultor sênior"

- **Hierarquia de linguagem:** título em português de rua ("Quanto sobrou de
  verdade?"), subtítulo com o termo técnico entre parênteses só pra quem já conhece
  ("Resultado do mês (DRE)"). Nunca a ordem inversa.
- **Cor como semáforo, não decoração:** verde = sobra/saudável, amarelo = atenção,
  vermelho = ação necessária — consistente em todo o módulo (mesmo padrão de 🟢🟡🔴
  usado em 4.7).
- **Todo número tem uma frase ao lado.** Nunca um KPI solto sem contexto de "isso é bom
  ou ruim?" comparado ao período anterior ou a uma meta.
- **"i" (info) em vez de manual.** Um ícone de interrogação/info com 1 frase de
  explicação (não um link pra artigo de ajuda) em todo termo técnico inevitável (DRE,
  margem de contribuição, competência).
- **Modo detalhado é opt-in, nunca default.** Quem quer a tabela contábil clássica
  (ex: pra mandar pro contador) ativa "ver como o contador vê" — não é a tela inicial.
- **Alertas chegam onde o dono já está: WhatsApp.** Reaproveitar
  `/api/financial/notify` e o agente de IA pra notificar o **dono** (hoje só notifica
  o cliente) é o maior diferencial competitivo possível — nenhum concorrente
  pesquisado (Conta Azul, Omie, Granatum, QuickBooks) tem canal conversacional nativo
  dentro do próprio produto.
- **Exportação sempre disponível, nunca obrigatória.** PDF/Excel do DRE e do fluxo de
  caixa pro contador, mas o dono não deveria precisar abrir Excel pra entender o
  próprio negócio no dia a dia.

---

## 7. Notas de implementação (SDD — alinhado ao CLAUDE.md do repo)

Novas entidades/rotas propostas que precisam de contrato em `lib/contracts/` antes de
implementação (R2):

| Entidade/rota nova | Tipo de contrato | Observação de FSM/idempotência |
|---|---|---|
| `Scenario` (projeções "e se...", §4.13) | `lib/contracts/domain/scenario.ts` | Não persiste efeito real — mas se salvo como "cenário favorito", precisa `businessId` (R1) |
| `FinancialAlert` (se virar entidade persistida, não só notificação transiente) | `lib/contracts/domain/financialAlert.ts` + FSM (`pendente → visto → dispensado`) | Idempotência por `(businessId, alertType, referenceId, period)` — mesmo padrão de `birthdayCampaignLogs` (R3) |
| Cron de projeção de caixa diária | `lib/contracts/api/financial/cash-forecast.ts` | Idempotente por dia — não reenviar mesmo alerta 2x no mesmo dia (R3) |
| Cálculo de custo CLT (`Employee` → custo total) | `lib/contracts/domain/employeeCost.ts` (função pura derivada, não nova coleção) | Sem side-effect — é view calculada, não precisa FSM |
| DAS/Fator R simulador | Estende `lib/contracts/domain/dasRecord.ts` (se ainda não existe formalmente) | — |
| Notificação de alerta ao **dono** via WhatsApp (extensão de `/api/financial/notify`) | Evento cross-módulo: `financial.alert.triggered` → dispatch pro agente/conversas | Promover a `dispatchDomainEvent()` só quando houver 2+ subscribers (bell in-app + WhatsApp já contam como 2) — R5 |

Todos os relatórios (DRE, ponto de equilíbrio, margem, rentabilidade) devem ser
**views derivadas em memória/query**, não novas coleções gravadas — eles são
recomputados a partir de `transactions`/`sales`/`products`/`stockMovements` já
existentes, respeitando R1 (`businessId` em toda query) e R6 (validação só no
boundary; cálculo interno confia no tipo já validado na escrita original).

---

## Fontes consultadas (pesquisa web, julho/2026)

- Conta Azul — Dashboard de Fluxo de Caixa e DRE Gerencial: https://contaazul.com/funcionalidades/fluxo-caixa-diario/ , https://ajuda.contaazul.com/hc/pt-br/articles/44911365743501 , https://ajuda.contaazul.com/hc/pt-br/articles/115007764267
- Granatum — Ponto de equilíbrio, margem de contribuição, DRE automática: https://controlefinanceiro.granatum.com.br/empreendedorismo/o-que-e-ponto-de-equilibrio/ , https://www.granatum.com.br/blog/lucro-bruto-e-margem-de-contribuicao
- QuickBooks — Cash Flow Planner/Projector: https://quickbooks.intuit.com/money/cash-flow-management/ , https://quickbooks.intuit.com/learn-support/en-us/help-article/budget-forecast-reports/use-cash-flow-planner-quickbooks-online/L2l59mIqe_US_en_US
- Nubank PJ / Stone — conta empresarial e stack financeiro PME: https://nubank.com.br/empresas/conta , https://dinheirodaminhaempresa.com/comparativos/melhor-conta-pj-pme/
- Omie vs Bling — comparativo financeiro/DRE: https://multise.com.br/conta-azul-omie-nibo-ou-bling-comparativo-entre-os-erps-mais-usados-por-pmes/ , https://adrion.com.br/blog/tiny-bling-conta-azul-omie-comparativo-honesto-pme/
- Custo de funcionário CLT 2026: https://contaja.com.br/blog/quanto-custa-um-funcionario-para-empresa/ , https://valorfinal.com.br/guia/custo-funcionario
- Simples Nacional, anexos, Fator R, DAS 2026: https://contabilidade.com/blog/simples-nacional-2026-guia-completo-de-anexos-fator-r-limites-e-das/ , https://simplifique.contmatic.com.br/blogs/fator-r-simples-nacional-o-que-e-como-calcular
- MEI — limite e tabela DAS 2026: https://www.balancinho.com.br/tabela-mei , https://www8.receita.fazenda.gov.br/simplesnacional/Noticias/NoticiaCompleta.aspx?id=c3b2044c-ff97-432a-b33c-ecf2a3df6dc3
- Alertas inteligentes de caixa / estatística Sebrae: https://sebrae.com.br/sites/PortalSebrae/artigos/fluxo-de-caixa-o-que-e-e-como-implantar , https://nemala.com.br/blog/ia-previsao-fluxo-de-caixa
- Régua de cobrança automatizada: https://www.asaas.com/regua-de-cobranca , https://adrion.com.br/blog/ia-cobranca-boleto-inadimplencia-pme/
- Rentabilidade por produto/canal em delivery/restaurante: https://sisfood.com.br/saiba-mais/gestao-financeira/margem-lucro-restaurante , https://blog.deli.com.br/como-calcular-se-o-delivery-realmente-esta-dando-lucro-ou-prejudicando-a-operacao
- Open Finance Brasil — conciliação automática: https://brickup.app/conciliacao-bancaria-via-open-finance/ , https://www.pluggy.ai/blog/open-finance-para-empresas
- Pró-labore vs distribuição de lucro: https://bancodoempreendedor.org.br/pro-labore-como-calcular-a-retirada-mensal-dos-donos-da-empresa/
- Plano de contas simplificado para pequenas empresas/restaurantes: https://www.ledware.com.br/2026/04/10/como-montar-um-plano-de-contas-para-restaurante/ , https://agenciasebrae.com.br/economia-e-politica/descubra-como-montar-um-plano-de-contas-para-o-seu-pequeno-negocio/
