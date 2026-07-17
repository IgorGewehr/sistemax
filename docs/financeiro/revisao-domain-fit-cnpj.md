# Revisão de aderência do Financeiro ao CNPJ real (assistência técnica, 3 correntes de receita)

> Auditoria READ-ONLY de quant finance + domínio ERP, executada em 2026-07-17 sobre o estado atual
> de `src/Modules/Financeiro/**`, `src/Modules/{Vendas,Estoque,Compras,Fiscal}/**` e
> `src/Verticals/Assistencia/**`. Todo achado cita `arquivo:linha`. Nada de código foi alterado.
>
> **Contexto de negócio auditado**: o CNPJ que vai operar isto é uma assistência técnica com três
> correntes de receita — (1) assinaturas/recorrente, (2) Ordens de Serviço (mão de obra + peças),
> (3) venda de peças/periféricos no balcão (cartão-pesado). O dono é matemático e quer unit
> economics POR CORRENTE, com todo número rastreável até a fonte.

---

## 1. Veredito executivo

**O motor matemático é de qualidade alta e confiável no que ele calcula; o problema é que, para
este CNPJ, ele calcula sobre a corrente errada — ou sobre corrente nenhuma.**

Em uma frase por corrente:

| Corrente | Estado real hoje |
|---|---|
| **1. Assinaturas** | Domínio bonito (MRR normalizado, churn, painel) — mas **o gerador de cobrança não roda em produção** (só o DemoSeeder o chama) e **ignora o ciclo da assinatura** (uma anual seria cobrada todo mês pelo valor cheio). Sem dunning, sem expansão/contração, sem receita diferida. A receita de assinatura **não entra em nenhuma fact table** (`fato_receita_diaria`/`fato_recebiveis`/`fato_caixa_diario`), então RBT12, breakeven e bandas de caixa não a enxergam. |
| **2. OS / serviços** | O agregado `OrdemDeServico` é o melhor pedaço de domínio do repo (FSM, guarda de valor, desconto determinístico, eventos por linha de peça). Mas o vertical Assistência é **Domain-only**: `AssistenciaModule.Registrar` está vazio — **nada publica `OsFaturada` nem os 4 eventos de peça**. Hoje, fechar uma OS não gera um único centavo no Financeiro. E mesmo quando a torneira abrir: o evento perde forma de pagamento/técnico na tradução, e **não existe evento de custo de peça de OS** (só `CustoBaixadoPorVenda`), então margem por OS é incomputável. |
| **3. Peças / varejo** | **A única corrente ponta-a-ponta de verdade.** Venda → `VendaConcluida` + `VendaItensMovimentados` → baixa por custo médio (com BOM) → `CustoBaixadoPorVenda` → `fato_custo_diario` + `fato_margem_produto` (rateio Hamilton) → DRE com CMV por competência da venda; `fato_recebiveis` com MDR/lag por forma de pagamento. É o gabarito que as outras duas correntes precisam copiar. |

E o transversal que mais dói para o dono: **nenhuma estrutura do Financeiro tem a dimensão
"corrente de receita"**. `fato_receita_diaria` é um acumulador único por dia; o DRE soma tudo em
`ReceitaBruta`; os handlers gravam venda de balcão, OS e assinatura **todos** com a mesma categoria
`"servicos"`. Unit economics por corrente é hoje impossível por construção, não por falta de tela.

O **Radar do Simples** — a análise mais sensível ao CNPJ — só implementa o **Anexo I (comércio)** e
rejeita qualquer outro anexo com 400. Para uma assistência técnica (serviço = Anexo III/V com Fator
R, peças = Anexo I) o número de alíquota efetiva mostrado é estruturalmente errado, e a projeção de
"meses até o próximo degrau" tem um erro matemático de dimensão (~12× otimista sob crescimento
sustentado — ver P1-1).

**Resumo**: a fundação (ledger de eventos, fatos idempotentes, partida dobrada automática, centavos
inteiros, folds determinísticos com seed) está pronta para receber a matemática certa. O trabalho
não é refazer motor — é (a) dar dimensão de corrente aos fatos, (b) ligar as torneiras de OS e
assinatura, (c) ensinar o Radar a falar Anexo III/V + Fator R + mix.

---

## 2. Achados priorizados

Legenda: **P0** = quebra o modelo de negócio do CNPJ (número errado ou ausente numa decisão real).
**P1** = importante (distorção material ou ciclo incompleto). **P2** = melhoria/consistência.

### P0 — quebram o modelo do CNPJ

| # | O quê | Evidência | Impacto | Recomendação |
|---|---|---|---|---|
| **P0-1** | **Nenhuma dimensão "corrente de receita" em lugar nenhum** — DRE de balde único; fact tables sem eixo de corrente; categorias colapsadas | `DreGerencialService.cs:33-52` (uma `ReceitaBruta`, um `CustoDireto`, sem quebra); `FatoReceitaDiariaProjection.cs:38-63` (venda, pedido e OS acumulam no MESMO contador diário, OS até soma serviço+peças em um número só na linha 61); `VendaConcluidaHandler.cs:36` (venda de balcão gravada como categoria `"servicos"`); `GeracaoRecorrenteUseCases.cs:90` (cobrança de assinatura também `CategoriaFinanceiraPadrao.Servicos`); `OsFaturadaHandler.cs:35` (OS idem); `CategoriaFinanceiraPadrao.cs:14-19` (só 6 slugs, nenhum distingue comércio × serviço × recorrente) | O dono quer MRR com MC ~100%, OS com margem serviço+peça, varejo com MC baixa e MDR — hoje o DRE devolve UM número que mistura os três. Qualquer decisão de mix (contratar técnico? empurrar assinatura? margem de peça?) fica sem base. Também bloqueia P0-4 (repartição de anexos) | Criar enum estável `CorrenteReceita { Recorrente, Servico, ComercioPecas }` propagado como STRING no evento (mesma regra de fronteira de `NaturezaOperacao`). Categorias novas: `receita-recorrente`, `receita-servico`, `receita-pecas-os`, `receita-comercio`. `fato_receita_diaria` e `fato_custo_diario` ganham coluna `corrente` (chave passa a `tenant+dia+corrente`); refold do ledger via `ResetarAsync` + catch-up já existente. DRE passa a devolver `PorCorrente[]` com `{ReceitaBruta, CustoDireto, MC, MC%}` cada — a soma bate com o total de hoje (invariante de teste) |
| **P0-2** | **A corrente de OS não chega ao Financeiro: o vertical Assistência é Domain-only** — ninguém publica `OsFaturada`/`PecaReservada`/`PecaConsumida`/`ReservaLiberada`/`ConsumoEstornado` | `AssistenciaModule.cs:30-45` (`Registrar` vazio: "Quando Assistencia.Application/.Infrastructure existirem…"); `OrdemDeServicoDomainEvents.cs:43-52` (os 4 eventos de peça nunca ganharam `ParaEventoDeIntegracao()`); consumidores prontos e órfãos: `OsFaturadaHandler.cs`, `FatoReceitaDiariaProjection.cs:31`, `PecaConsumidaHandler.cs` (Estoque) | Fechar uma OS hoje **não cria ContaAReceber, não entra no DRE, não entra em recebíveis, não baixa estoque**. Para uma assistência técnica isso é o negócio inteiro invisível ao Financeiro | Construir `Assistencia.Application/Infrastructure`: repositório SQLite, casos de uso (o agregado já está pronto), e publicação pós-commit dos 5 eventos — o gesto é idêntico a `ConcluirVendaUseCase` (`VendaUseCases.cs:89-112`). Prioridade máxima junto com P0-3 |
| **P0-3** | **Assinaturas não faturam em produção + gerador ignora o ciclo** — `GerarCobrancasAssinaturasUseCase` só é chamado pelo DemoSeeder; `Assinatura.GerarCobranca` não consulta `Ciclo`/`UltimaCobrancaGeradaEm` | Únicos call sites: `Hosts/SistemaX.Host.Desktop/Bridge/DemoSeeder.cs:63` e registro DI em `FinanceiroModule.cs:61-62`; nenhum endpoint (`FinanceiroEndpointsModule.cs` não mapeia POST de geração) e o único cron do módulo é `AvaliarParcelasVencidasBackgroundService.cs`; `Assinatura.cs:123-139` (`GerarCobranca` só checa `Status == Ativa`; idempotência é por `yyyyMM`, então **cada mês novo gera cobrança nova pelo `ValorPorCiclo` CHEIO, mesmo para ciclo trimestral/anual**); contraste com `Recorrencia.CalcularProximaOcorrencia` (`Recorrencia.cs:89-112`) e com a projeção de UI `AssinaturaDetalheService.cs:38-51`, que **respeitam** o ciclo — a tela diria "próxima cobrança em 3 meses" enquanto o gerador cobraria mês que vem | Corrente 1 inteira parada (nenhum recebível recorrente nasce sozinho); e no dia em que alguém ligar um cron mensal ingênuo, **toda assinatura não-mensal é superfaturada** (anual = 12× no ano ⇒ 12× o valor contratado) | (a) `GerarCobranca` calcula a próxima competência devida a partir de `UltimaCobrancaGeradaEm ?? DataInicio` + `Ciclo` (mesmo algoritmo de `AssinaturaDetalheService.CalcularProximaCobranca`) e só emite se `competencia >= próxima devida`, com catch-up em loop como `GerarContasRecorrentesUseCase` já faz; (b) criar `GerarCobrancasAssinaturasBackgroundService` no molde exato de `AvaliarParcelasVencidasBackgroundService.cs:28-55` (catch-up no boot + intervalo configurável + fail-open) |
| **P0-4** | **Radar do Simples inoperante para o mix do CNPJ**: só Anexo I, sem Fator R, sem repartição por corrente — e o RBT12 nem contém a receita de assinaturas | `RadarDoSimplesNacional.cs:31-39` (só `AnexoI` populado); `RadarDoSimplesService.cs:35-40` (qualquer anexo ≠ I → erro "anexo_nao_suportado"); `FinanceiroConsultorFactProvider.cs:56` (o Consultor chama **hardcoded** `AnexoSimplesNacional.I` — um tenant de serviço recebe alíquota de comércio como fato); nenhuma ocorrência de "FatorR/Fator R" no repo (grep em `src/`); RBT12 = soma de `fato_receita_diaria` (`RadarDoSimplesService.cs:43-46`), que não recebe assinaturas (P1-3/P0-1) | Para assistência técnica o DAS real é: mão de obra → Anexo III (Fator R ≥ 28%) ou V (< 28%); peças vendidas/NFC-e → Anexo I. O radar hoje mostra alíquota de comércio sobre um RBT12 incompleto — **um número de imposto errado nos dois eixos** para o CNPJ-alvo | Ver §4, Fatia 4 — tabelas oficiais III/V, Fator R com folha de 12 meses (o dado nasce de `FolhaLancada`, já no ledger), e repartição: `DAS_mês = Σ_corrente (receita_mês_corrente × alíquota_efetiva_do_anexo_da_corrente)`, onde **cada alíquota efetiva usa o RBT12 TOTAL da empresa** na fórmula `((RBT12×nominal − PD)/RBT12)` do anexo daquela corrente (LC 123/2006 art. 18 §§1º-A e 3º). Depende de P0-1 (receita dimensionada por corrente) |
| **P0-5** | **Custo de peça aplicada em OS não existe no Financeiro** — Estoque baixa pelo custo médio mas não publica o custo; não há `CustoBaixadoPorOs` | `PecaConsumidaHandler.cs:51-62` (registra `MovimentoDeEstoque` com `saldo.CustoMedio` e **não publica nada**); catálogo de eventos só tem `CustoBaixadoPorVenda` amarrado a `VendaId` (`IntegrationEvents.cs:362-366`); `FatoCustoDiarioProjection.cs:22-28` (só reage a `CustoBaixadoPorVenda`) | Mesmo com P0-2 resolvido, o DRE registraria receita de peças da OS **sem o CMV correspondente** → margem de serviço superestimada exatamente na corrente que o dono mais precisa medir (margem por OS = mão de obra + peças − custo peças − comissão) | Evento aditivo `CustoBaixadoPorOs(OrdemServicoId, TenantId, LinhaId, ProdutoId, CustoCentavos, OcorridoEm)` publicado por `PecaConsumidaHandler` ao final da baixa (mesmo padrão "um fato, dois eventos" de `VendaItensMovimentadosHandler.cs:57-60`); `fato_custo_diario` (corrente = Servico) e `fato_margem_produto` ganham o case. Estorno simétrico em `ConsumoEstornadoHandler` |

### P1 — importantes

| # | O quê | Evidência | Impacto | Recomendação |
|---|---|---|---|---|
| **P1-1** | **Projeção "meses até o próximo degrau" do Radar é matematicamente errada** — divide a distância pelo crescimento médio da receita MENSAL, mas o RBT12 é janela móvel de 12 meses: seu incremento mensal é `m_{t+1} − m_{t−11}`, não `g` | `RadarDoSimplesNacional.cs:82-90`: `deltas` = diferenças mês-a-mês da receita mensal; `mesesProjetados = distancia / média(deltas)` | Sob crescimento linear sustentado (`m_t = a + g·t`), o incremento real do RBT12 é `12g` — o radar diz "cruza em ~24 meses" quando cruza em ~2. **Direção do erro: otimista** — o alerta de sublimite chega tarde demais, que é exatamente o que o radar existe para evitar | Rolar a janela explicitamente: projete `m̂_{t+j} = m_t + g·j` e acumule `RBT12_{t+k} = RBT12_t + Σ_{j=1..k} (m̂_{t+j} − m_{t+j−12})` usando os 12 meses reais já carregados; menor `k` que cruza o teto é a resposta. Determinístico, sem dependência nova, e degrada corretamente para "não cruza" quando `g ≤ 0` |
| **P1-2** | **Breakeven usa MC% de UMA população (só produtos de estoque) aplicada sobre a receita TOTAL** — sem MC por corrente | `PontoDeEquilibrioService.cs:93-100` (MC% = agregado de `fato_margem_produto`, que só contém vendas de itens com `ControlaEstoque` — `FatoMargemProdutoProjection.cs:36-40`); aplicada em `BreakevenMensal.Calcular` sobre `fato_receita_diaria` inteira (`PontoDeEquilibrioService.cs:49-55`); `BreakevenMensal.cs:31-45` aceita um único `margemContribuicaoPercentual` | Para o CNPJ: recorrente tem MC ≈ 100%, mão de obra de OS MC altíssima, peça MC baixa. Usar a MC de peças (a menor) sobre a receita total **superestima** a receita necessária e empurra o "dia do equilíbrio" para frente; se o mix mudar no mês, o número mente na outra direção | MC blended por mix: `MC% = Σ_s w_s·MC_s`, com `w_s` = share de receita da corrente `s` na janela (vem de P0-1) e `MC_s` por corrente: comércio/peças-OS de `fato_margem_produto` + `CustoBaixadoPorOs`; serviço = 1 − (comissão%); recorrente ≈ 1. `BreakevenMensal` não precisa mudar de forma — muda o insumo (é o lugar certo: a fórmula clássica continua `CustosFixos ÷ MC%`) |
| **P1-3** | **`fato_caixa_diario` é unilateral e incompleto** → ruído do bootstrap enviesado para cima e burn EWMA ≈ 0 (runway bruto quase sempre `null`) | `FatoCaixaDiarioProjection.cs:28-37`: só `VendaConcluida` à vista (+), `PedidoPago` (+) e `VendaEstornada` (−). **Nenhuma saída de pagamento de conta** (folha, compras, fixas — baixadas via `BaixarParcelaUseCase` — nunca entram), nenhuma OS, nenhuma assinatura; `PrevisaoDeCaixaService.cs:50-58` alimenta `BandasDeFluxoDeCaixa` e `RunwayCalculator.CalcularBurnDiarioEwma` com essa série | A série histórica de "deltas de caixa" contém quase só entradas ⇒ (a) bandas P5/P50/P95 sistematicamente otimistas (o ruído nunca é negativo); (b) `burnDoDia = max(0, −delta)` ≈ 0 sempre ⇒ runway bruto vira `null` ("sem queima") mesmo com folha alta — o Consultor então anuncia "Seu caixa não está queimando" (`FinanceiroConsultorFactProvider.cs:96-101`) | Duas opções, em ordem de preferência: (1) publicar evento `ParcelaBaixada(…, tipo, valor)` no `BaixarParcelaUseCase` e foldar entrada/saída no `fato_caixa_diario` (mantém a regra "insumo é evento, nunca leitura de tabela alheia"); (2) interim: derivar a série densa de `MovimentoFinanceiro` (que já tem os dois lados — `ListarPorPeriodoAsync`) só para o insumo do bootstrap/EWMA, como `FluxoDeCaixaService.cs:33-47` já faz |
| **P1-4** | **Ciclo de vida de MRR incompleto: sem expansão/contração, sem dunning, e a conta de churn tem viés** | `Assinatura.cs:25` (`ValorPorCiclo` imutável — não existe troca de plano; upgrade real teria que ser cancelar+criar ⇒ infla churn E novo simultaneamente); `StatusAssinatura.cs:5-10` (só Ativa/Pausada/Cancelada — não há `Inadimplente`/`AguardandoPagamento`); nenhum retry/suspensão por falta de pagamento em lugar nenhum (o máximo é `ParcelaVencida` publicado pelo cron — `AvaliarParcelasVencidasUseCase.cs:53-63` — sem consumidor); `ReceitaRecorrenteService.cs:41-52`: `mrrNovo` só conta assinaturas AINDA ativas iniciadas no mês (uma que nasceu e cancelou no mesmo mês entra no churn mas não no novo, e `mrrInicioMes = mrr − novo + churn` a inclui como se existisse no dia 1); `Pausada` some do MRR sem aparecer em nenhuma métrica (churn silencioso) | Métricas de assinatura são a lente nº 1 da corrente recorrente; churn% enviesado e ausência de expansão/contração tornam o painel não-auditável para um dono matemático. Sem dunning, inadimplência de assinatura vira só uma parcela atrasada genérica | Contabilidade de movimentos por EVENTO, não por snapshot: `MRR_fim = MRR_início + Novo + Expansão − Contração − Churn + Reativação`, cada termo somado dos eventos do mês (o domínio já levanta `AssinaturaCriada/Cancelada` com MRR em centavos — `AssinaturaDomainEvents`; faltam `AssinaturaAlterada` (delta) e `AssinaturaReativada` com valor). Persistir `MRR_início` (ou derivá-lo do ledger) em vez de reconstruir por álgebra invertida. Dunning: estado `Inadimplente` + política (N dias após `ParcelaVencida` da cobrança da assinatura → marca; M dias → pausa/cancela), como consumidor do próprio `ParcelaVencida` |
| **P1-5** | **Sem receita diferida/pró-rata: cobrança anual/trimestral reconhece o ciclo inteiro em UMA competência** | `Assinatura.GerarCobranca` (`Assinatura.cs:128-138`) cria `ContaAReceber` de `ValorPorCiclo` cheio com `DataCompetencia = competencia`; DRE soma `ContaAReceber` por competência (`DreGerencialService.cs:35-39`) — um plano anual de R$12.000 vira R$12.000 de receita num único mês e zero nos outros 11 | DRE mensal da corrente recorrente vira serrote; churn/renovação anual distorce comparativos mês a mês; RBT12 por competência também serrilha (o Simples usa regime de competência ou caixa consistente — hoje seria competência concentrada) | Agenda de reconhecimento: cobrança de ciclo `k` meses gera k linhas de `V/k` (rateio Hamilton de `RateioProporcional.Alocar(V, pesos=1×k)` para fechar o total exato) em uma projeção `fato_receita_reconhecida` (corrente = Recorrente); a `ContaAReceber` continua integral (é o recebível), o DRE lê o schedule. Pró-rata de troca de plano usa a mesma agenda (crédito do não-consumido + débito do novo) |
| **P1-6** | **MDR não existe como despesa em nenhum demonstrativo** — só como visão analítica | O dado mora em `FormaDePagamento.TaxaPercentual` (`FormaDePagamento.cs:34-38`, seed com 1,39% débito / 3,49% crédito / 2% boleto em `FinanceiroBootstrapSeeder.cs:74-78`) e é usado por `FatoRecebiveisProjection.cs:76-98` (líquido correto, taxa sobre valor absoluto ✓) e `TaxasPorFormaService.cs:23-51` (painel). Mas: DRE (`DreGerencialService.cs:41-48`) não tem linha de despesa financeira/MDR; `BaixarParcelaUseCase`/`VendaConcluidaHandler` registram `MovimentoFinanceiro` pelo BRUTO; `FluxoDeCaixaService.cs:54-58` e `PrevisaoDeCaixaService.CarregarFluxoConhecidoAsync` (`:90-116`) projetam parcelas pelo bruto no vencimento | Num varejo cartão-pesado, ~3,5% do volume de crédito é custo real recorrente que **não aparece** no resultado nem na projeção de caixa conhecida — resultado operacional e "quanto sobrou" superestimados de forma sistemática e proporcional ao sucesso do varejo | (a) DRE: linha "despesas financeiras (MDR)" derivada de `fato_recebiveis` (`Σ bruto − líquido` do período) por corrente; (b) fluxo conhecido: para parcelas com forma resolvível, projetar `líquido` na `DataLiquidacaoPrevista` (o cálculo já existe pronto em `FatoRecebiveisProjection.ResolverTaxaELagAsync` — reusar, não duplicar); (c) conciliação: quando a adquirente credita o líquido, a baixa pelo bruto de hoje nunca vai bater com o extrato — antecipar isso na Fase de conciliação |
| **P1-7** | **`OsFaturada` perde os campos que o Financeiro precisa, e o handler nunca liquida** — mesmo com o cliente pagando na retirada | `OrdemDeServicoDomainEvents.cs:15-22` (GAP documentado: `ClienteId/ClienteNome/NumeroOs/FormaPagamento/TecnicoId` existem no evento de domínio e são **descartados** em `ParaEventoDeIntegracao()`, `:35-41`); `IntegrationEvents.cs:70-75` (contrato pobre); consequências no handler: `OsFaturadaHandler.cs:31-35` — parcela única com `vencimento = OcorridoEm`, **nunca liquidada** (na assistência paga-se na entrega — `OrdemDeServico.Entregar` exige `FormaPagamento`, `OrdemDeServico.cs:397`), sem `MovimentoFinanceiro`, sem `ClienteId` na conta, e comissão do técnico impossível (gap descrito no próprio handler, `:13-21`); `FatoRecebiveisProjection.cs:26-29` exclui OS por falta de forma de pagamento | Quando P0-2 ligar a torneira, cada OS entregue viraria um recebível aberto que o cron marca `Atrasado` no dia seguinte (`ContaFinanceiraBase.cs:117-127`) — inadimplência fantasma inflando a PDD (`InadimplenciaService`) e o aging; caixa da OS nunca registrado; comissão (parte do custo direto da corrente serviço) segue impossível | Estender `OsFaturada` ADITIVAMENTE (campos opcionais: `FormaPagamento?`, `ClienteId?`, `TecnicoId?`, `NumeroOs?` — retrocompatível com o ledger existente); handler passa a espelhar `VendaConcluidaHandler.cs:43-69`: à vista → liquida parcela + `MovimentoFinanceiro` + lançamento de caixa; `fato_recebiveis` ganha o case de OS com MDR/lag; comissão vira `ContaAPagar` categoria `comissoes` quando `TecnicoId` + percentual configurado existirem |

### P2 — melhorias

| # | O quê | Evidência | Recomendação |
|---|---|---|---|
| P2-1 | RBT12 = 365 dias corridos incluindo o dia atual; a definição legal é os 12 meses FECHADOS anteriores ao período de apuração | `RadarDoSimplesService.cs:43-46` | Somar `fato_receita_diaria` de `início(mês atual − 12)` a `fim(mês anterior)`; hoje o valor oscila dentro do próprio mês |
| P2-2 | `VendaConcluida` carrega só o método principal — venda split (metade dinheiro, metade crédito) aplica MDR/lag do principal ao TOTAL em `fato_recebiveis` | `VendaDomainEvents.cs:38-48` (evolução proposta documentada, não implementada); `FatoRecebiveisProjection.cs:49-53` | Evoluir aditivamente com `Pagamentos[]` e foldar um `FatoRecebivel` por pagamento |
| P2-3 | A versão com matemática correta do sinal "conta grande antes de receber" (projeção ACUMULADA de caixa no vencimento) é **dead code**; o Consultor usa heurística inline mais fraca (compara só a maior parcela a receber) | `SinalContaGrandeAntesDoRecebimento.cs:36-67` (nenhuma referência fora do próprio arquivo); `FinanceiroConsultorFactProvider.cs:292-345` | Trocar o inline pelo `Detectar()` do Quant (mesmos insumos, resultado mais correto) e deletar a duplicação |
| P2-4 | Concentração de MRR só por SERVIÇO; o comentário promete "1 cliente/serviço carregando a receita" mas não agrupa por cliente | `ReceitaRecorrenteService.cs:54-63` | Adicionar `PorCliente` (o dado já está na assinatura) |
| P2-5 | `fato_margem_produto` não reage a `VendaEstornada` (margem por produto não é revertida) — limitação documentada | `FatoMargemProduto.cs:19-24` | Quando `VendaEstornada` carregar itens, adicionar handler simétrico (o próprio doc já prescreve) |
| P2-6 | Custos fixos do breakeven vêm SÓ de `Recorrencia` a pagar — folha lançada via `FolhaLancada` (evento) não conta como custo fixo se não houver template | `PontoDeEquilibrioService.cs:67-72`; `FolhaLancadaHandler.cs` | Incluir média das `ContaAPagar` categoria `despesa-com-pessoal` dos últimos meses, ou orientar cadastro de folha como Recorrencia |
| P2-7 | `ClassificadorFormaPagamento` binário (dinheiro/pix à vista; TODO o resto D+30) diverge da `FormaDePagamento` cadastrada (débito D+1, boleto D+2) — dois "lares" do prazo | `ClassificadorFormaPagamento.cs:18-20` vs `FinanceiroBootstrapSeeder.cs:74-78` | Fase 2 já prevista no próprio arquivo: resolver contra `FormaDePagamento` (o repositório e o nome do evento já casam — `ObterPorNomeAsync`) |
| P2-8 | PDD usa curva estática de perda por aging; `EstimarMatrizRollRate` (com Laplace, row-estocástica) pronta mas nunca chamada — e a FSM de `Parcela` não tem estado "perda confirmada" para treinar | `InadimplenciaRollRate.cs:20-27, 96-101` | Manter (escolha documentada e razoável para F1); na Fase 3, snapshots mensais de `fato_recebiveis` + estado `PerdaConfirmada` alimentam a matriz empírica |

---

## 3. O que já está ótimo (não mexer)

- **Dinheiro em centavos inteiros de ponta a ponta** — `Money.cs` (`readonly record struct`, wire só
  `{centavos, moeda}`, arredondamento bancário em `DeReais`); quantidades em milésimos no Estoque.
  Nenhum float toca cálculo em lugar nenhum que eu tenha encontrado.
- **Rateio de CMV por maior resto (Hamilton)** — `RateioProporcional.cs:24-54` garante por
  construção `Σ alocado = total`; exatamente o algoritmo certo para dividir centavos.
- **Percentil nomeado** — `BandasDeFluxoDeCaixa.cs:112-133` usa Hyndman & Fan tipo 7 e DIZ isso no
  doc; auditável por qualquer estatístico contra R/NumPy.
- **Block bootstrap com seed determinística por período** — `BandasDeFluxoDeCaixa.cs:75-95` preserva
  autocorrelação de dia-da-semana; `SeedDeterministico` + `PrevisaoDeCaixaService.cs:55` dão
  reprodutibilidade byte-a-byte no mesmo dia. A aproximação de independência ruído×conhecido está
  documentada no lugar certo. (O problema é o INSUMO — P1-3 — não o motor.)
- **EWMA de burn com conversão janela→α clássica** — `RunwayCalculator.cs:25-40`, `α = 2/(n+1)`,
  burn = só a fração negativa. Correto dado o insumo.
- **Tabela do Anexo I e fórmula da alíquota efetiva** — `RadarDoSimplesNacional.cs:31-39` confere
  linha a linha com a LC 123/2006 (redação LC 155/2016): tetos 180k/360k/720k/1,8M/3,6M/4,8M,
  nominais 4/7,3/9,5/10,7/14,3/19%, parcelas a deduzir 0/5.940/13.860/22.500/87.300/378.000;
  `CalcularAliquotaEfetiva` (`:60-65`) é a fórmula oficial do art. 18 §1º-A com clamps sensatos.
- **CMV economicamente correto no DRE do varejo** — a decisão de tirar `CompraRecebida` do custo do
  período e usar `CustoBaixadoPorVenda` foldado (`DreGerencialService.cs:17-27` +
  `FatoCustoDiarioProjection`) é a correção clássica "compra é troca de ativo, custo nasce na
  venda". Rara de ver certa em ERP pequeno.
- **Partida dobrada automática e invisível** — `LancamentoContabilFactory.cs:23-71` mapeia cada
  fato para débito/crédito correto (CAR: D-Recebíveis/C-Receita; baixa: D-Caixa/C-Recebíveis etc.);
  estorno espelha o original preservando vínculo.
- **Idempotência disciplinada em TODOS os handlers** — `SourceRef` determinística derivada do id do
  fato (`VendaConcluidaHandler.cs:26-28`, `GerarCobranca` `assinatura:{id}:{yyyyMM}`,
  `recorrencia:{id}:{yyyyMMdd}`, chaves por LINHA no Estoque). Replay nunca duplica.
- **Ledger persist-then-dispatch** — `InProcessIntegrationEventBus.cs:39-73` grava antes de
  despachar, serializa pelo tipo concreto, e a ordem `VendaItensMovimentados` →
  `CustoBaixadoPorVenda` explorada por `FatoMargemProdutoProjection.cs:15-26` está corretamente
  garantida pelo cursor aninhado.
- **O agregado `OrdemDeServico`** — FSM com mapa único de transições (`OrdemDeServico.cs:501-522`),
  guarda de valor (peça extra/aumento de mão de obra exigem `clienteAvisado`), desconto
  determinístico abatendo mão de obra primeiro (`:412-414`), garantia com OS de retorno sem estado
  novo, total sempre recalculado. Pronto para produção — só falta a Application (P0-2).
- **Custo médio + expansão de BOM na baixa** — `VendaItensMovimentadosHandler.cs:38-49, 67-99`:
  custo capturado no instante da baixa, ficha técnica expandida, alerta de mínimo na transição.
- **`fato_recebiveis` com MDR/lag resolvido no domínio** — `FatoRecebiveisProjection.cs:73-108`:
  taxa sobre valor absoluto (estorno nunca inverte sinal da taxa), `FormaDePagamento` como lar
  único, fallback conservador 0%/D+0 documentado. **Responde SIM à pergunta 7 para o varejo** — só
  falta OS/assinatura entrarem (P1-7/P0-3).
- **Cron de vencidas idempotente por transição** — `AvaliarParcelasVencidasUseCase` +
  `ContaFinanceiraBase.AvaliarVencimento` (`:117-127`) só levantam evento na transição
  Aberto→Atrasado; o BackgroundService é fail-open com catch-up no boot.
- **PDD por aging com taxas explícitas** — `InadimplenciaRollRate.CalcularProvisao` é a conta
  padrão de provisionamento, com arredondamento bancário e faixas clássicas.
- **`AssinaturaDetalheService.CalcularProximaCobranca`** (`:38-51`) — a projeção da próxima
  cobrança respeita ciclo, dia de cobrança e catch-up; é o algoritmo que `GerarCobranca` deveria
  usar (P0-3 é copiar daqui, não inventar).

---

## 4. Plano de implementação sugerido (fatias dotnet-gated)

Ordem escolhida para maximizar valor por fatia mantendo `dotnet build && dotnet test` verdes ao fim
de cada uma. Fatias 1–2 destravam a corrente recorrente real; 3–4 destravam a OS; 5 conserta o
imposto; 6+ refinam o quant.

**Fatia 1 — Assinatura fatura certo e sozinha (P0-3).**
Domain: `Assinatura.GerarCobranca` respeita `Ciclo` (próxima competência devida a partir de
`UltimaCobrancaGeradaEm ?? DataInicio`, algoritmo de `AssinaturaDetalheService.CalcularProximaCobranca`
movido para o agregado e reusado pelo read-model — um lar só). Application: catch-up em loop no
use case (gera todas as competências devidas até `ate`, cada uma idempotente por `yyyyMM`).
Infrastructure: `GerarCobrancasAssinaturasBackgroundService` no molde de
`AvaliarParcelasVencidasBackgroundService` (+ `GerarContasRecorrentesUseCase` no mesmo cron — hoje
também órfão). Testes: anual gera 1/ano com valor cheio; mensal faz catch-up de N meses; replay não
duplica; `AssinaturaDetalhe` e gerador concordam sempre (property test com ciclos sorteados).

**Fatia 2 — Dimensão de corrente nos fatos e no DRE (P0-1).**
`CorrenteReceita` como string estável no contrato; categorias novas em `CategoriaFinanceiraPadrao`;
handlers passam a gravar a categoria da sua corrente (venda→comércio, OS→serviço/peças-OS,
assinatura→recorrente); migração Vn: coluna `corrente` em `fato_receita_diaria`/`fato_custo_diario`
+ refold do ledger (mecanismo `ResetarAsync`/catch-up já existe). `DreGerencialService` devolve
total + `PorCorrente[]` (receita, custo direto, MC, MC%) com invariante `Σ correntes = total`
testada. Eventos antigos do ledger sem corrente → classificar pelo TIPO do evento no fold (venda =
comércio, os = serviço, é derivável sem migrar payload).

**Fatia 3 — OS chega ao Financeiro (P0-2 + P1-7).**
`Assistencia.Application/Infrastructure`: repositório SQLite + casos de uso (o agregado está
pronto) + publicação pós-commit dos 5 eventos (gesto de `ConcluirVendaUseCase`). Estender
`OsFaturada` aditivamente (`FormaPagamento?`, `ClienteId?`, `TecnicoId?`, `NumeroOs?`).
`OsFaturadaHandler`: à vista → liquidar parcela + `MovimentoFinanceiro` + lançamento de caixa
(espelho de `VendaConcluidaHandler`); descrição usa `NumeroOs` ("OS-0042", não ULID);
`fato_recebiveis` ganha o case de OS. Comissão: `ContaAPagar` categoria `comissoes` quando houver
`TecnicoId` + percentual (config por tenant). Testes de wiring ponta-a-ponta: Entregar → CAR
liquidada + caixa + receita nas duas correntes (serviço e peças-OS separadas — o evento já separa
`ValorServicoCentavos`/`ValorPecasCentavos`; NÃO somar mais os dois no fold).

**Fatia 4 — CMV de peça de OS (P0-5).**
Evento `CustoBaixadoPorOs` publicado por `PecaConsumidaHandler` (custo médio × quantidade, mesma
conta de `VendaItensMovimentadosHandler.cs:98`); estorno simétrico em `ConsumoEstornadoHandler`;
folds de `fato_custo_diario` (corrente Servico) e `fato_margem_produto`. Com Fatias 3+4, "margem
por OS" fecha: mão de obra + peças − custo peças − comissão, tudo rastreável ao ledger.

**Fatia 5 — Radar do Simples multi-anexo com Fator R e mix (P0-4 + P1-1 + P2-1).**
Tabelas oficiais III (6/11,2/13,5/16/21/33% — PD 0/9.360/17.640/35.640/125.640/648.000) e V
(15,5/18/19,5/20,5/23/30,5% — PD 0/4.500/9.900/17.100/62.100/540.000) no mesmo formato de `AnexoI`.
Fator R = folha 12 meses ÷ RBT12 (folha foldada de `FolhaLancada`, que já está no ledger, para uma
`fato_folha_mensal`); `≥ 0,28 → III`, `< 0,28 → V`, recalculado por apuração. Repartição:
alíquota efetiva de CADA anexo calculada com o RBT12 TOTAL; DAS estimado =
`receita_mês_comercio × ef_I + receita_mês_servico_recorrente × ef_{III|V}` (correntes da Fatia 2).
RBT12 por 12 meses fechados (P2-1). Projeção de degrau rolando a janela (fórmula em P1-1).
`FinanceiroConsultorFactProvider` deixa de hardcodar Anexo I e passa a reportar o mix.

**Fatia 6 — Caixa bilateral e MDR no resultado (P1-3 + P1-6).**
`ParcelaBaixada` como evento (ou fold interino de `MovimentoFinanceiro`) para `fato_caixa_diario`
ter os dois lados → bandas honestas e burn EWMA real. DRE ganha linha "despesas financeiras (MDR)"
derivada de `fato_recebiveis`; fluxo conhecido projeta líquido na data de liquidação usando o
resolvedor que já existe.

**Fatia 7 — Ciclo de vida de MRR e receita diferida (P1-4 + P1-5).**
Eventos `AssinaturaAlterada` (delta de valor) e `AssinaturaReativada`; painel de movimentos por
evento com identidade `MRR_fim = MRR_início + Novo + Expansão − Contração − Churn + Reativação`
testada como invariante; estado `Inadimplente` + política de dunning consumindo `ParcelaVencida`;
agenda de reconhecimento `V/k` por mês (Hamilton) para ciclos > mensal, lida pelo DRE recorrente.

**Fatia 8 — Higiene (P2-2..P2-8).** Split payments no evento de venda; substituir o sinal inline do
Consultor pelo `SinalContaGrandeAntesDoRecebimento.Detectar`; concentração por cliente; estorno em
`fato_margem_produto`; prazo/compensação resolvidos só em `FormaDePagamento`.

---

*Gerado por auditoria de código em 2026-07-17. Divergência entre este doc e o código após novas
fatias = bug de documentação; atualizar no PR que mudar o comportamento citado.*
