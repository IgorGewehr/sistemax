# Assistência Técnica — Vertical (Domain)

Vertical do SistemaX para quem vende serviço de assistência técnica. Este pacote é só a camada
**Domain**: um agregado (`OrdemDeServico`), seus objetos de valor, sua FSM e os eventos de
domínio que ela levanta. Não há Application/Infrastructure/UI aqui ainda — ver Roteiro abaixo.

Plano completo (contexto, wireframes, decisões em aberto):
`scratchpad/design/os-plano.md` (arquivo de planejamento, fora do repo de código).

## O agregado

`OrdemDeServico` modela o ciclo de vida completo: equipamento → defeito → diagnóstico →
orçamento (peças previstas **e** mão de obra, aprovados juntos) → aprovação → execução → pronta
→ entrega. A entrega **fatura e entrega no mesmo ato** — no MVP o cliente paga quando retira o
equipamento, então separar "faturada" de "entregue" criaria um limbo inexistente na operação
real e violaria a regra de "fato financeiro = uma transação local única".

### FSM

```
Aberta → EmDiagnostico → AguardandoAprovacao ⟲ (reenvio substitui) → Aprovada → EmExecucao → Pronta → Entregue (T)
                                              └→ Reprovada → DevolvidaSemReparo (T)
Cancelada (T) — de qualquer estado pré-entrega
```

Terminais: `Entregue`, `DevolvidaSemReparo`, `Cancelada`. Nenhum método escreve em `Status` sem
passar por `Fsm<StatusOrdemServico>.ValidarTransicao` — ver o mapa `TransicoesPermitidas` no
final de `OrdemDeServico.cs`, único ponto de verdade.

### Guarda de valor

Peça extra (fora do orçamento) ou aumento de mão de obra acima do orçado **exigem**
`clienteAvisado: true` explícito no comando (`AdicionarPecaExtra`, `AjustarMaoDeObraFinal`).
Reduzir é sempre livre. Isso torna estruturalmente impossível uma OS fechar cobrando mais do que
o cliente aprovou sem rastro de que ele foi avisado — a soma nunca ultrapassa
`Orcamento.Total` a não ser por um caminho já confirmado.

### Timestamps explícitos

Todo método que muda estado recebe `DateTimeOffset agora` como parâmetro — o domínio nunca lê o
relógio do sistema por conta própria (mesmo padrão de `Venda.RegistrarPagamento`). Mantém o
agregado determinístico e testável sem mock de tempo.

## Interação com o Estoque (contrato, sem módulo ainda)

Não existe módulo Estoque no repo. Em vez de inventar um acoplamento provisório, a OS já
levanta 4 eventos de **domínio** com chave de idempotência estável por linha
(`os.reserva/baixa/libera/estorno:{osId}:{linhaId}`, nunca por timestamp):

| Evento (domínio) | Quando | 
|---|---|
| `PecaReservadaDomainEvent` | `RegistrarAprovacao`, uma por peça orçada com `ProdutoId` |
| `PecaConsumidaDomainEvent` | `AplicarPeca` / `AdicionarPecaExtra`, peça a peça |
| `ReservaLiberadaDomainEvent` | `ConcluirExecucao` (sobra do orçamento) e `Cancelar` |
| `ConsumoEstornadoDomainEvent` | `Cancelar` durante `EmExecucao`, por peça já baixada |

Peça "sob encomenda" (`ProdutoId` nulo no orçamento) nunca gera evento de estoque — não há o que
reservar/baixar.

**Gap documentado (não resolvido aqui de propósito):** este módulo está proibido de alterar
assinaturas de `Modules.Abstractions` na execução atual. Os 4 eventos acima são eventos de
DOMÍNIO privados ao vertical — ainda não têm equivalente de **integração** catalogado (como
`OsFaturada` já tem para o Financeiro). No dia em que o módulo Estoque nascer e alguém com
permissão em Abstractions catalogar `PecaReservada`/`PecaConsumida`/`ReservaLiberada`/
`ConsumoEstornado`, cada evento de domínio ganha um `ParaEventoDeIntegracao()` — exatamente o
gesto que `OsFaturadaDomainEvent` já faz.

## Interação com o Financeiro

`Entregar()` (e `DevolverSemReparo()` com taxa > 0) levantam `OsFaturadaDomainEvent`, traduzido
para `OsFaturada` (já catalogado em `Modules.Abstractions`, consumido por
`OsFaturadaHandler` no Financeiro). Desconto na entrega abate **primeiro** a mão de obra —
decisão determinística, documentada em `Entregar`.

**Gap documentado:** `OsFaturadaDomainEvent` carrega `ClienteId`, `ClienteNome`, `NumeroOs`,
`FormaPagamento` e `TecnicoId` — os campos que o próprio `OsFaturadaHandler` já pede em comentário
(contraparte real da ContaAReceber, baixa imediata da parcela, fechamento do gap de comissão).
Eles são **perdidos** na tradução para `OsFaturada` porque o evento de integração ainda não foi
estendido em `Modules.Abstractions` (fora do escopo deste módulo — regra de execução vigente
proíbe alterar Abstractions). Quando alguém estender `OsFaturada` com esses campos, atualizar
`OsFaturadaDomainEvent.ParaEventoDeIntegracao()` para parar de descartá-los, e o
`OsFaturadaHandler` (Financeiro) para os consumir (kind `"os"` no `SourceRef`, hoje herdado de
`"appointment"` — outra correção que cabe a quem mantém o Financeiro).

## OS de garantia

Retorno com o mesmo defeito dentro da garantia é uma **nova OS** com `OsOrigemId` apontando para
a original (`EhRetornoDeGarantia`). Nenhum estado novo na FSM. Se o orçamento nasce zerado (peça
em garantia com preço 0), `Entregar()` **não** emite `OsFaturada` — não há nada a receber; o
consumo real de peça já foi registrado via `PecaConsumidaDomainEvent` em `AplicarPeca`.

## O que é stub / fora deste pacote

- **Application/Infrastructure** (repositório, handlers de comando, sequência de `Numero` por
  tenant, publicação pós-commit dos eventos de integração) — Fase B do roteiro do plano.
- **UI** (lista + detalhe) — protótipo estático em
  `scratchpad/mockups/ordem-servico.html`, ainda não conectado a este domínio. Fase C.
- **Impressão térmica e WhatsApp** — Fase D.
- **Módulo Estoque de fato** (assinantes dos 4 eventos acima) — Fase E, só quando o módulo
  nascer.
- **Extensão de `OsFaturada` em Abstractions** e correção do `OsFaturadaHandler` — cabem a quem
  mantém esses projetos, não a este módulo (ver gaps documentados acima).

## Testes

`tests/SistemaX.Verticals.Assistencia.Tests/` — xUnit, 71 casos:

- `OrdemDeServicoFsmTests` — toda transição válida e inválida, `Reprovada` não-terminal,
  auto-loop de reenvio de orçamento, cancelamento em cada estado pré-entrega.
- `OrdemDeServicoOrcamentoEExecucaoTests` — orçamento com peças + mão de obra, aplicar peça
  orçada, peça extra e aumento de mão de obra com/sem `clienteAvisado`.
- `OrdemDeServicoFaturamentoTests` — `Entregar`/`DevolverSemReparo`, tradução para `OsFaturada`,
  split de desconto (mão de obra primeiro), OS de garantia com total zero não fatura.
- `OrdemDeServicoEstoqueEventosTests` — reserva/consumo/liberação/estorno, peça sob encomenda
  sem efeito de estoque, cancelamento em cada ponto do fluxo.
- `OrdemDeServicoInvariantesTests` — senha do equipamento nunca em texto plano, campos
  derivados (`EstaAtrasada`, `OrcamentoVencido`, `TempoNaEtapaAtual`), histórico de transições.
