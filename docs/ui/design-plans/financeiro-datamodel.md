# Modelo de Dados Lógico — Módulo Financeiro (ERP modular BR)

> Stack-agnóstico. Entidades e relações lógicas — não é DDL físico, não é schema Firestore/SQL.
> Objetivo: o financeiro é o **coração** do sistema. Todo outro módulo (PDV, estoque, compras,
> ordem de serviço, agenda, pedidos, clientes) **alimenta** o financeiro via eventos de domínio,
> nunca escreve direto nas tabelas financeiras.

Grounding: este design foi calibrado olhando o estado real do repo `saas-erp` (ServicePro) —
`lib/types/index.ts:Transaction`, `lib/contracts/fsm/transaction.ts`,
`lib/contracts/events/index.ts`, `lib/services/commission.ts`, `lib/services/reconciliation.ts`.
Onde relevante, aponto o gap entre "hoje" e o modelo-alvo.

---

## 1. Decisão: single-entry vs double-entry vs híbrido

### As três opções

**A) Partida simples (single-entry).** Cada fato financeiro é uma linha: tipo (receita/despesa),
valor, data, categoria, conta. É o que o ServicePro tem hoje (`Transaction` em
`lib/types/index.ts:1222`, `type: 'receita' | 'despesa'`).

- ✅ Trivial de entender para um leigo: "entrou X", "saiu Y".
- ✅ Rápido de implementar e de consultar (uma tabela, um filtro de data).
- ❌ Não tem checagem estrutural de integridade. Se um bug criar um lançamento de receita sem
  o débito correspondente em caixa, **nada no schema acusa isso** — só um relatório errado,
  descoberto tarde, por um humano.
- ❌ Não dá balanço patrimonial nem trial balance de verdade — só soma de linhas por categoria.
- ❌ Estornos, comissões e parcelamentos viram "mais uma linha solta na mesma tabela", sem
  vínculo estrutural que garanta que o estorno neutraliza exatamente o original.

**B) Partida dobrada (double-entry) pura.** Todo fato vira um `LancamentoContabil` com N
`PartidaContabil` (débito/crédito) contra um plano de contas, com invariante
`Σdébitos == Σcréditos` por lançamento.

- ✅ Auditável por construção: se o código está certo, o trial balance **sempre** fecha. Um bug
  que "cria dinheiro do nada" quebra a invariante e pode ser rejeitado no write (`.refine()` /
  transação atômica), não só detectado num relatório depois.
- ✅ Balanço patrimonial, DRE por competência e fluxo de caixa saem do mesmo motor, sem gambiarra.
- ❌ Débito/crédito é **opaco para leigo**. Ninguém que não é contador entende por que uma
  receita é lançada a crédito. Forçar o usuário do PDV a escolher conta débito/conta crédito
  manualmente é inviável para o público-alvo (PME brasileira, dono que não é contador).
- ❌ Migração cara: reescreve toda a base de código financeiro atual (Transaction, comissão,
  conciliação, recorrência) de uma vez.

**C) Híbrido pragmático (recomendado): single-entry na superfície, double-entry por baixo,
gerado automaticamente.**

O usuário e a UI só enxergam fatos econômicos simples (`ContaAPagar`, `ContaAReceber`,
`MovimentoFinanceiro`) — nunca digita débito/crédito. Por baixo, **todo** fato que nasce de um
evento de domínio gera **automaticamente** (nunca manualmente) um `LancamentoContabil` com
partidas dobradas, contra um plano de contas simplificado e pré-definido (Caixa/Bancos, Contas a
Receber, Contas a Pagar, Receita, Custo/Despesa, Impostos a recolher). O motor de geração é
código, não input do usuário — então a "partida dobrada" vira uma **checagem de integridade
automática e invisível**, não um fardo cognitivo.

### Justificativa da escolha

Dinheiro não pode ter bug, mas o público não é contador. A saída não é escolher um dos dois
extremos — é **desacoplar a experiência de entrada de dados da garantia de integridade**:

- A UI e os contratos de API expõem *fatos de negócio* (single-entry): "recebi R$ 500 do
  cliente X, categoria Serviços, conta Itaú, pago no PIX". Isso é o que aparece em toda tela,
  relatório e API pública.
- Todo fato de negócio, ao ser persistido, dispara a geração determinística de um
  `LancamentoContabil` balanceado (código, não humano, escreve as partidas). Isso roda como uma
  invariante de escrita: se as partidas não batem, a transação inteira falha — vira o
  "circuit breaker" contra bug de dinheiro que o single-entry puro não tem.
- Custo de migração é incremental: dá para introduzir o motor de partida dobrada como uma
  **camada de auditoria paralela** (mesmo padrão de "audit-only" que o repo já usa para
  `domainEvents/{id}` em `lib/contracts/events/index.ts`) sem quebrar nada que já funciona hoje
  em `Transaction`. Fase 1: motor roda e loga divergências. Fase 2 (quando confiável): motor
  vira fonte de verdade para relatórios contábeis (DRE societário, balanço) — o
  `MovimentoFinanceiro`/`ContaAPagar`/`ContaAReceber` continuam sendo a fonte de verdade para a
  UI e para o dia a dia operacional.

Trade-off honesto: isso é mais código do que single-entry puro (dois modelos a manter
sincronizados) e menos "matematicamente elegante" que double-entry puro (o motor de mapeamento
evento→partidas é uma peça a mais que pode ter bug ela mesma). Mas é o único caminho que atende
as duas exigências simultâneas do enunciado — clareza para leigo **e** correção auditável —
sem sacrificar nenhuma das duas.

---

## 2. Entidades núcleo

### 2.1 Visão de competência (accrual) — "o que foi economicamente incorrido"

| Entidade | Papel |
|---|---|
| `ContaAReceber` | Obrigação de terceiro para com a empresa, nascida de um evento de negócio (venda, OS faturada, pedido). Tem `dataCompetencia` = quando o fato gerador ocorreu (não quando o dinheiro entra). |
| `ContaAPagar` | Obrigação da empresa para com terceiro (fornecedor, funcionário, comissão), mesma lógica. |
| `Parcela` | Fatia agendada de uma `ContaAPagar`/`ContaAReceber`: `vencimento`, `valor`, `status` (aberto/parcial/pago/atrasado/cancelado). 1 conta → N parcelas (parcelamento nativo). |
| `Categoria` | Classificação gerencial do lançamento ("Serviços", "Comissões", "Aluguel", "Marketing"...). N:1 com `LinhaDRE`. |
| `CentroDeCusto` | Dimensão analítica ortogonal à categoria — por filial, setor, projeto. Permite DRE por centro de custo sem duplicar categorias. |
| `LinhaDRE` | Nó de uma árvore fixa (Receita Bruta → Deduções → Receita Líquida → CMV/CPV → Lucro Bruto → Despesas Operacionais → EBITDA → Financeiro → Lucro Líquido). Todo `Categoria` mapeia para exatamente uma `LinhaDRE`. |
| `Recorrencia` | Template gerador de `ContaAPagar`/`ContaAReceber` futuras (frequência, dia fixo, data fim). Já existe como `TransactionRecurrence` em `lib/types/index.ts:1191` — mantém-se. |

### 2.2 Visão de caixa (cash) — "o que efetivamente moveu dinheiro"

| Entidade | Papel |
|---|---|
| `MovimentoFinanceiro` | O fato de caixa: dinheiro que de fato mudou de mão. Nasce da liquidação (total ou parcial) de uma `Parcela`, ou de um recebimento à vista sem conta a receber prévia (venda no PDV pago na hora). `dataMovimento` = data efetiva do caixa. |
| `ContaBancariaCaixa` | Onde o dinheiro fisicamente está (conta corrente, caixa físico da loja, carteira digital). Saldo é **derivado** (soma de `MovimentoFinanceiro`), nunca a fonte de verdade em si — evita drift entre saldo armazenado e histórico. |
| `FormaDePagamento` | Como o dinheiro se moveu (dinheiro, PIX, débito, crédito, boleto, transferência). Carrega metadados de liquidação: taxa (cartão), prazo de compensação (D+0 PIX, D+30 crédito) — usado para calcular `dataMovimento` projetada vs real. |
| `Conciliacao` | Vínculo entre um `MovimentoFinanceiro` (interno) e um `ExtratoBancarioItem` (linha importada de OFX/CSV, ver `lib/services/reconciliation.ts`). Guarda status: `nao_conciliado` / `conciliado_auto` / `conciliado_manual` / `ignorado`. |

### 2.3 Camada contábil (double-entry, gerada, não editável por humano)

| Entidade | Papel |
|---|---|
| `PlanoDeContas` | Catálogo fixo e enxuto de contas contábeis de controle: Caixa/Bancos, Contas a Receber, Contas a Pagar, Receita, Custo/Despesa, Impostos a Recolher. Não é exposto na UI operacional. |
| `LancamentoContabil` | Header: `id`, `businessId`, `data`, `origem` (ref ao evento/fato que gerou), `descricao`. |
| `PartidaContabil` | Linha filha: `contaContabilId`, `debitoOuCredito`, `valor`. Invariante por `LancamentoContabil`: `Σdébito == Σcrédito`. Gerado 1:1 automaticamente a partir de cada `ContaAPagar`/`ContaAReceber` (lançamento de competência) e de cada `MovimentoFinanceiro` (lançamento de caixa) — nunca digitado manualmente. |

### 2.4 Diagrama ER (ASCII)

```
                         ┌───────────────┐
                         │   LinhaDRE    │ (árvore fixa: Receita, CMV, Desp.Op, EBITDA...)
                         └───────▲───────┘
                                 │ N:1
                         ┌───────┴───────┐        ┌────────────────┐
                         │   Categoria   │        │  CentroDeCusto │
                         └───────▲───────┘        └───────▲────────┘
                                 │ N:1                    │ N:1
                                 │                         │
   ┌──────────┐     evento      │                         │
   │  Módulo  │  de domínio     │                         │
   │  fonte   ├─────────────────┤                         │
   │ (Venda,  │                 │                         │
   │ Compra,  │        ┌────────┴─────────────────────────┴──────┐
   │ OS, Pe-  │        │        ContaAReceber / ContaAPagar       │◄────┐
   │ dido,    │        │  (competência: dataCompetencia, valor,   │     │ template gera
   │ Folha)   │        │  status FSM, sourceRef{modulo,id})       │     │
   └──────────┘        └────────────────┬──────────────────────--┘     │
                                         │ 1:N                    ┌─────┴──────┐
                                         ▼                        │ Recorrencia│
                                  ┌─────────────┐                 └────────────┘
                                  │   Parcela   │
                                  │ vencimento, │
                                  │ valor,status│
                                  └──────┬──────┘
                                         │ 1:N (liquidação total ou parcial)
                                         ▼
                              ┌─────────────────────┐        ┌───────────────────┐
                              │  MovimentoFinanceiro │───────►│  FormaDePagamento │
                              │  (caixa: dataMovim., │        └───────────────────┘
                              │   valor efetivo)     │
                              └──────────┬───────────┘
                                         │ N:1
                                         ▼
                              ┌─────────────────────┐        ┌───────────────────┐
                              │ ContaBancariaCaixa   │        │    Conciliacao    │
                              └──────────┬───────────┘        │  (Movimento <->   │
                                         │ 1:N               ◄┤ ExtratoBancarioItem)
                                         └─────────────────────┴───────────────────┘

   ── camada contábil (gerada, não editável) ─────────────────────────────────────
   ContaAReceber/ContaAPagar ──► gera ──► LancamentoContabil ──1:N──► PartidaContabil
   MovimentoFinanceiro       ──► gera ──►        (mesmo par)    ──1:N──► PartidaContabil
                                                        Σdébito == Σcrédito (invariante)
```

---

## 3. Regime de caixa vs competência — mesmo fato, duas visões

O mesmo fato de negócio produz **dois registros distintos com datas diferentes**, nunca um só:

| | Nasce quando | Data usada | Alimenta |
|---|---|---|---|
| **Competência** (`ContaAReceber`/`ContaAPagar` + `Parcela`) | O fato gerador ocorre (venda concluída, NF de compra recebida, OS faturada) — independente de o dinheiro já ter trocado de mãos | `dataCompetencia` / `vencimento` | DRE gerencial, projeção de fluxo de caixa futuro |
| **Caixa** (`MovimentoFinanceiro`) | O dinheiro efetivamente muda de mãos (PIX confirmado, cartão aprovado, boleto compensado) | `dataMovimento` | Saldo de conta bancária, fluxo de caixa realizado |

Exemplo concreto (venda a prazo, 30 dias):

```
Dia 01 — venda.concluida (R$ 300, cliente compra a prazo)
  → ContaAReceber criada:  dataCompetencia = 01/mês, categoria "Serviços"
  → Parcela única:         vencimento = 31/mês, status = aberto
  → DRE de competência do mês 1 já reconhece R$ 300 de receita.
  → Fluxo de caixa REALIZADO do mês 1: R$ 0 (nada mudou de mão ainda).

Dia 31 — cliente paga via PIX
  → Parcela.status: aberto → pago
  → MovimentoFinanceiro criado: dataMovimento = 31, contaBancaria = Itaú, R$ 300
  → Fluxo de caixa realizado do mês 1 (ou 2, dependendo do corte) agora reconhece R$ 300.
```

Se a venda fosse à vista (dinheiro no PDV), os dois fatos nascem **no mesmo instante** — o
handler do evento cria a `ContaAReceber`/`Parcela` já com `status: pago` **e** o
`MovimentoFinanceiro` correspondente, atomicamente, na mesma escrita. Nunca existe
`MovimentoFinanceiro` sem uma `Parcela` (mesmo que liquidada no mesmo milissegundo) — isso
preserva a regra "toda entrada de caixa tem uma origem de competência rastreável", inclusive
para venda à vista.

Nota de alinhamento com o repo atual: hoje `Transaction` (`lib/types/index.ts:1222`) já meio
que modela isso com `dueDate` (competência) vs `paymentDate` (caixa) no **mesmo** registro — é
um single-entry pragmático que funciona para volume atual. O modelo-alvo separa isso em duas
entidades (`ContaAPagar/Receber+Parcela` vs `MovimentoFinanceiro`) porque parcelamento parcial
(pagar 2 de 3 parcelas) e conciliação bancária (N movimentos batendo com 1 parcela, ex.: taxa de
cartão descontada) não cabem bem num único registro com um único `paymentDate`.

---

## 4. Contrato de integração por eventos de domínio

### 4.1 Anti-padrão a evitar (estado observado no repo)

Hoje, efeitos financeiros rodam **inline** no código do módulo de origem, com guards de
idempotência ad-hoc espalhados (`commissionTransactionId`, `stockDeductedAt`, `paidAt`,
`transactionReversedAt` — ver comentário em `lib/contracts/events/index.ts:20-27`). O bus de
eventos (`dispatchDomainEvent`) hoje é **audit-only**: persiste `domainEvents/{id}` para
trilha, mas só tem *um* handler realmente plugado (`appointment.completed`). Para o resto —
principalmente os eventos de pagamento do cardápio (`payment.approved`, `deliveryOrder.confirmed`)
— o evento serve só de auditoria; o efeito de dinheiro real roda inline no caller, com guard CAS.

Isso é exatamente o anti-padrão citado no enunciado: **lógica financeira inline + side-effect
ad-hoc**. Funciona hoje porque o volume é baixo e cada caller foi revisado manualmente, mas não
escala e não é auditável de forma centralizada — para saber "o que gera dinheiro no sistema"
seria preciso ler o corpo de todo handler de UI/API, não um catálogo único.

**Direção recomendada:** promover cada evento financeiramente relevante a ter exatamente **um**
subscriber real (`lib/services/financial/subscribers/{evento}.ts`), registrado no
`dispatchDomainEvent`, e **parar de confiar em "audit-only"** para qualquer evento que mexa com
dinheiro — a regra README do próprio repo já autoriza isso ("promova a `dispatchDomainEvent()`
quando dois ou mais subscribers existirem"; para financeiro, o critério deveria ser mais estrito:
promova assim que o efeito for monetário, mesmo com um único subscriber, porque é onde bug
custa dinheiro real).

### 4.2 Catálogo de eventos

Convenção de nome mantém o padrão já usado em `lib/contracts/events/index.ts`
(`modulo.fato_no_passado`, envelope com `businessId`, `occurredAt`, `actorType`, `actorId`).
Onde já existe evento equivalente no repo, indico o mapeamento.

| Evento | Origem (módulo) | Efeito de competência | Efeito de caixa | Chave de idempotência |
|---|---|---|---|---|
| `venda.concluida` (≈ `sale.finalized` já existente) | PDV / Vendas | Cria `ContaAReceber` + `Parcela`(s) conforme forma de pagamento; categoria por tipo de item vendido | Se à vista/pago no ato: cria `MovimentoFinanceiro` atômico junto | `sourceRef = {modulo:'sale', id: saleId}` — 1 conta a receber por venda; handler faz upsert idempotente checando existência antes de criar |
| `venda.estornada` | PDV / Vendas | Se `ContaAReceber` ainda aberta: cancela (transição FSM `aberto→cancelado`). Se já paga: cria fato de **estorno** vinculado (`reversalOfId`), nunca edita/apaga o original | Cria `MovimentoFinanceiro` negativo (reembolso) datado no dia do estorno, vinculado à conta original | `sourceRef = {modulo:'sale-reversal', id: saleId}` — reprocessar o mesmo `saleId` não duplica o estorno (handler checa se já existe reversal para aquele saleId) |
| `compra.recebida` (nota fiscal de fornecedor importada) | Compras / NF-e | Cria `ContaAPagar` com `dataCompetencia` = emissão da NF, categoria CMV/Custo, `Parcela`(s) conforme condição de pagamento do XML | Nenhum até liquidação (compra a prazo é o caso comum) | `sourceRef = {modulo:'purchaseNote', id: purchaseNoteId}` |
| `os.faturada` (ordem de serviço/agendamento concluído e faturado; ≈ `appointment.completed`) | Agenda / OS | Cria `ContaAReceber` (categoria Serviços) **e**, se profissional tem comissão configurada, dispara evento derivado `comissao.devida` → cria `ContaAPagar` categoria Comissões | Se pagamento já capturado no ato (ex: cartão na hora do serviço): `MovimentoFinanceiro` atômico | `sourceRef = {modulo:'appointment', id: appointmentId}` — replica o guard já existente `commissionTransactionId` como campo de dedupe na `ContaAPagar` de comissão |
| `pedido.pago` (pagamento de pedido do cardápio confirmado por gateway; ≈ `payment.approved` / `deliveryOrder.confirmed`) | Pedidos / Cardápio | Cria `ContaAReceber` já nascida quitada (pagamento online é sempre resolvido no evento) | Cria `MovimentoFinanceiro` imediato, categoria Delivery | `sourceRef = {modulo:'order-payment', id: `${orderId}_${gatewayPaymentId}`}` — usa o id do PSP para dedupar contra retry de webhook, não o `orderId` sozinho |
| `parcela.vencida` (evento de tempo, não de negócio — cron) | Cron financeiro | **Não cria fato novo de dinheiro.** Só transiciona `Parcela.status: aberto→atrasado` (FSM) e, se configurado, gera um fato econômico **separado** de multa/juros (nunca altera o valor original da parcela) | Nenhum (não há caixa envolvido em vencer) | Idempotente por natureza: é uma transição de estado condicionada ao estado atual (`if (parcela.status === 'aberto' && hoje > vencimento) transicionar`); rodar o cron 2x no mesmo dia não duplica nada porque a segunda rodada já encontra `atrasado` |
| `folha.lancada` | RH / Folha de pagamento | Cria N `ContaAPagar` (uma por funcionário ou consolidada), categoria "Despesa com Pessoal", `dataCompetencia` = mês de referência, `vencimento` = dia de pagamento configurado | Nenhum até liquidação (folha é paga depois de lançada) | `sourceRef = {modulo:'payroll', id: `${periodo}_${employeeId}`}` — mesmo padrão de chave composta já usado em `birthdayCampaignLogs/{campaignId}_{clientId}_{year}` |

### 4.3 Regras gerais de idempotência

1. **Toda `ContaAPagar`/`ContaAReceber` carrega `sourceRef` único** (módulo de origem + id do
   fato gerador). Antes de criar, o handler consulta por esse `sourceRef` — se existir, é
   replay do mesmo evento, não cria de novo (mesma filosofia de R3 do `CLAUDE.md`: toda POST que
   cria recurso é idempotente; aqui o "recurso" é o fato financeiro, e a "idempotency key" é o
   `sourceRef` derivado do evento, não um header HTTP).
2. **Cron/agendado idempotente por estado, não por execução.** `parcela.vencida` não é "rode uma
   vez por dia com lock" — é "toda execução re-avalia o estado atual e só transiciona se ainda
   fizer sentido". Isso já é o padrão usado em `birthdayCampaignRunner` no repo.
3. **Webhook de pagamento dedupla pelo id do provedor** (`gatewayPaymentId`/`wamid`-equivalente),
   nunca só pelo id interno do pedido — um retry de webhook do PSP tem o mesmo
   `gatewayPaymentId` e deve ser reconhecido como duplicata mesmo que o pedido interno já tenha
   mudado de estado entre as duas chamadas.

### 4.4 Como o estorno flui de volta

Regra dura: **fato financeiro confirmado nunca é editado ou apagado.** Corrigir é sempre
**lançar um novo fato** que referencia o original:

1. Módulo de origem emite `X.estornada`/`X.cancelada`.
2. Handler financeiro localiza a `ContaAPagar`/`ContaAReceber` original por `sourceRef`.
3. Se ainda **aberta** (nenhum `MovimentoFinanceiro` associado): transição FSM
   `aberto → cancelado`. Não há caixa a reverter — é anulação pura.
4. Se **já liquidada** (tem `MovimentoFinanceiro`): cria um novo `MovimentoFinanceiro` de sinal
   invertido, com `reversalOfId` apontando pro original, datado **na data do estorno** (não
   retroage para o período de competência original, a menos que esse período contábil ainda
   esteja aberto — período fechado nunca é reaberto, o estorno sempre cai no período corrente
   com nota explicando a referência).
5. Na camada contábil (double-entry), o mesmo vale: nunca edita `PartidaContabil` existente —
   gera um `LancamentoContabil` de estorno com as partidas espelhadas (débito↔crédito invertidos).
6. Efeitos derivados (ex.: comissão vinculada a uma OS estornada) recebem o mesmo evento de
   estorno e aplicam a mesma regra — nunca um `DELETE`, sempre um fato de reversão auditável.

### 4.5 Envelope de evento (proposta de shape)

```
EventoFinanceiro {
  businessId: string
  type: string              // "venda.concluida", "venda.estornada", ...
  occurredAt: ISO datetime
  actorType: 'user' | 'api' | 'agent' | 'system'
  actorId?: string
  sourceRef: { modulo: string, id: string }   // chave de idempotência
  payload: { ... específico do evento ... }
}
```

Cada handler financeiro é uma função pura de `(EventoFinanceiro, estado atual) → efeitos`,
testável sem UI — mesmo espírito do `lib/contracts/fsm/transaction.ts` já existente, estendido
para cobrir a criação do fato (não só a transição de status).

---

## 5. Resumo da decisão de modelagem

- **Single-entry na superfície** (`ContaAPagar`, `ContaAReceber`, `Parcela`,
  `MovimentoFinanceiro`) para manter a experiência simples ao leigo — é isso que aparece em
  toda tela e API.
- **Double-entry gerado automaticamente por baixo** (`LancamentoContabil`/`PartidaContabil`),
  nunca editado por humano, funcionando como checagem estrutural de integridade — é isso que dá
  a garantia de "dinheiro não pode ter bug" sem exigir que o usuário entenda débito/crédito.
- **Competência e caixa são duas entidades ligadas, não um campo dentro da mesma linha** —
  permite parcelamento parcial, conciliação bancária N:1 e estorno independente sem overload de
  semântica num único registro.
- **Eventos de domínio são o único canal de entrada** para o financeiro — todo módulo-fonte
  emite fato, nunca escreve direto em `ContaAPagar`/`ContaAReceber`/`MovimentoFinanceiro`. Isso
  substitui o padrão atual de efeito inline + guard CAS espalhado por um catálogo único,
  testável e auditável.
