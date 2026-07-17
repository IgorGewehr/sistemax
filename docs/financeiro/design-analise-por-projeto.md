# Design — Análise por Projeto (linha de produto) no Financeiro

> Documento de DESIGN (read-only, nenhum código alterado), produzido em 2026-07-17 sobre o estado
> atual de `src/Modules/Financeiro/**` (pós-migrações V16–V20 da dimensão Corrente de Receita) e da
> auditoria `docs/financeiro/revisao-domain-fit-cnpj.md`. Todo mecanismo proposto aqui **espelha um
> padrão que já existe no repo** — a dimensão PROJETO é desenhada como a 2ª dimensão irmã de
> `CorrenteDeReceita` (mesmo padrão de coluna nullable + backfill + fold), o custo amortizado espelha
> a correção de CMV já feita no DRE ("compra é troca de ativo, custo nasce no uso"), e o gerador de
> amortização espelha `Assinatura.GerarCobranca` + `FaturarAssinaturasBackgroundService`.
>
> **Contexto de negócio**: assistência técnica / micro-SaaS reseller. O dono (matemático/quant) quer
> unit economics POR PROJETO (linha de produto): DigiSat (revenda de licenças com custo de aquisição
> amortizável), Aevo (automação WhatsApp com MRR e custo de IA recorrente), e apontamento de tempo de
> suporte para descobrir onde gasta tempo × onde ganha dinheiro.

---

## 0. TL;DR — o modelo em cinco linhas

1. **`Projeto`** é uma entidade leve por tenant; **`projetoId` nullable** entra como 2ª dimensão irmã
   de `Corrente` em `Assinatura`, `Recorrencia`, `ContaAReceber`, `ContaAPagar`,
   `MovimentoFinanceiro` e (fatia tardia) nas fact tables — `null` = "sem projeto" = comportamento
   de hoje, intacto.
2. **Custo amortizado** = novo agregado `AtivoAmortizavel`: o CAIXA continua sendo a `ContaAPagar`
   parcelada (7×R$985), mas a DESPESA vira um cronograma linear Hamilton de `total/vidaUtil` por
   competência (36×R$191,53), reconhecido por cron idempotente — mesmo mecanismo que a receita
   diferida (P1-5) vai reusar, só que no lado do custo.
3. **`ApontamentoDeTempo`** = lançamento leve (minutos, vínculo a projeto/cliente/assinatura,
   custo/hora opcional congelado no lançamento) que entra na margem GERENCIAL do projeto e no painel
   "onde vai meu tempo" — nunca no DRE (evita dupla contagem com `FolhaLancada`).
4. **`PainelDoProjeto`** (read-model + `GET /financeiro/projetos/{id}/painel`) entrega MRR, churn
   (hazard por exposição), LTV, margem em 3 camadas (variável / cheia / gerencial), payback
   (realizado por caixa + projetado por simulação determinística), ROI e utilização de capacidade
   (as 5 licenças) — toda métrica com fórmula fechada neste doc.
5. **Opt-in absoluto**: toggle `analisePorProjeto` em `ConfiguracaoFinanceiraTenant` (default
   `false`). Desligado, nenhuma tela mostra campo de projeto, nenhuma escrita aceita `projetoId`
   (422), e o DRE é byte-idêntico ao de hoje (invariante de teste).

---

## 1. Os casos-âncora que o modelo precisa representar

| Caso | O que exige do modelo |
|---|---|
| **DigiSat** — 5 licenças de 3 anos por 7 parcelas de R$985 (total R$6.895); 1 cliente fechado a R$280/mês | Separar **caixa** (7 parcelas) de **despesa econômica** (R$6.895 ÷ 36 meses); capacidade (5 licenças, 1 usada); payback (quando os R$280/mês recuperam os R$6.895) e ROI por projeto |
| **Aevo** — automação WhatsApp: MRR próprio + custo de IA recorrente | Custo recorrente tagueável no projeto (`Recorrencia` a pagar com `projetoId`) → margem = MRR − custo de IA, todo mês, sem digitação extra |
| **Tempo de suporte** — "30min no cliente X" | Lançamento em segundos; custo/hora opcional; agregação por cliente/assinatura/projeto; cruzamento tempo × margem para achar o gargalo |
| **Genérico** | MRR, custo específico, margem de contribuição, churn, LTV, payback, ROI — por projeto, com todo número rastreável à fonte |

---

## 2. Invariante de UX — opt-in não-intrusivo

### 2.1 O toggle: `ConfiguracaoFinanceiraTenant`

Mesmo padrão de `ConfiguracaoFiscalTenant` (`src/Modules/Fiscal/.../Regimes/ConfiguracaoFiscalTenant.cs`):
um `record` de configuração por tenant, uma linha por tenant, port + repositório SQLite/InMemory com
contract tests 2×. **Ausência de linha = tudo desligado** — zero seed necessário, tenant novo nasce
exatamente como hoje.

```csharp
// Domain/Comum (ou Domain/Configuracao) — record puro, sem I/O
public sealed record ConfiguracaoFinanceiraTenant(
    string TenantId,
    bool AnalisePorProjetoAtiva,          // default false
    long? CustoHoraPadraoCentavos,        // null = tempo sem custo (painel mostra só horas)
    bool TempoEntraNoDre)                 // default false — ver §5.3 e Decisão D4
{
    public static ConfiguracaoFinanceiraTenant Padrao(string tenantId)
        => new(tenantId, false, null, false);
}
```

### 2.2 A regra de gating (servidor)

| Estado do toggle | Leitura (`GET`) | Escrita com semântica de projeto |
|---|---|---|
| **Desligado** | Rotas de projeto respondem 200 com vazio (`GET /financeiro/projetos` → `[]`); painéis 404 | Qualquer `POST/PATCH` de projeto/ativo/apontamento, e qualquer request existente que carregue `projetoId != null` → **422 `financeiro.projetos.desativado`** |
| **Ligado** | Tudo disponível | Tudo disponível; `projetoId` continua **opcional** em toda escrita |

A regra "422 quando desligado" impede estado fantasma (dado tagueado que ninguém vê) e torna o
toggle um contrato, não uma cortina. Um único helper na Application
(`AnalisePorProjetoGuard.ExigirAtivaAsync(businessId)`) — nunca `if` espalhado por handler.

### 2.3 Por que as entidades de quem não usa não são poluídas

Exatamente a jogada da V16–V18 com `Corrente`:

- Todo campo novo em entidade existente é **nullable com default `null` no fim da assinatura** —
  `Assinatura.Criar(..., string? projetoId = null)`. Nenhum call site existente muda.
- Nenhum invariante de domínio passa a exigir projeto. `ContaAReceber.Criar` sem `projetoId` é o
  mesmo código de hoje.
- O DRE **não ganha termo por projeto** — projeto é uma LENTE (read-models próprios), nunca uma
  dimensão do demonstrativo oficial. A única mudança no DRE é a linha de amortização (§4.7), que é
  `Money.Zero` para quem não tem ativo amortizável → resultado byte-idêntico (invariante de teste
  de caracterização).
- Na UI: o menu "Projetos" do Financeiro, os selects de projeto (form de assinatura, lançamento
  manual, recorrência) e o quick-action "Registrar atendimento" só renderizam com o toggle ligado
  (lido de `GET /financeiro/configuracoes` no load do Financeiro). Desligado, nenhum pixel muda.

---

## 3. Modelo de domínio

### 3.1 `Projeto` — agregado leve

```csharp
// Domain/Projetos/Projeto.cs
public sealed class Projeto : AggregateRoot<string>   // Id = ULID (R6)
{
    public string BusinessId { get; }
    public string Nome { get; private set; }          // único por tenant (case-insensitive)
    public string? Descricao { get; private set; }
    public StatusProjeto Status { get; private set; } // FSM abaixo
    public DateTimeOffset CriadoEm { get; }
    public DateTimeOffset? ArquivadoEm { get; private set; }
}

/// VALORES PINADOS (persistidos como INTEGER — nunca reordenar, mesma regra de CorrenteDeReceita).
public enum StatusProjeto { Ativo = 0, Arquivado = 1 }
```

FSM (R4 — `Fsm<StatusProjeto>` com mapa explícito):

```
Ativo ⇄ Arquivado
```

- `Arquivar()` **não** desvincula nada: assinaturas/contas/apontamentos mantêm o `projetoId`
  (histórico intacto, painel continua consultável); o projeto só some das listas default e dos
  selects de tagging.
- Sem "excluir": projeto com qualquer vínculo é histórico imutável — mesma filosofia de
  `MovimentoFinanceiro`. (Projeto sem nenhum vínculo pode ser deletado fisicamente — caso "criei
  errado"; o repositório confere os vínculos antes.)
- Eventos de domínio: `ProjetoCriado`, `ProjetoArquivado`, `ProjetoReativado` (records privados ao
  módulo; nenhum vira evento de integração — nada cross-módulo acontece).

### 3.2 Tagging `projetoId` nas entidades existentes — a 2ª dimensão irmã

Mesma semântica de `Corrente` (`ContaFinanceiraBase.Corrente`): **nullable, aditivo, `null` = fora
da lente**, parâmetro opcional no fim das factories, propagado — nunca re-inferido — rio abaixo.

| Entidade | Como recebe o `projetoId` | Propagação |
|---|---|---|
| `Assinatura` | `Criar(..., string? projetoId = null)` + método `VincularProjeto(string? projetoId)` (re-tag permitido — tagging é classificação gerencial, não fato contábil) | `GerarCobranca` copia para a `ContaAReceber` gerada — hardcoded como a `Corrente` já é (`Assinatura.cs:158-163`): cobrança de assinatura de projeto P é, por definição, receita do projeto P |
| `Recorrencia` | `Criar(..., string? projetoId = null)` — **o lar do custo de IA do Aevo**: template a pagar mensal tagueado uma vez, custo cai no projeto todo mês sozinho | `GerarContasRecorrentesUseCase` copia para a conta materializada (junto do `corrente` que já copia) |
| `ContaAReceber` / `ContaAPagar` | Campo em `ContaFinanceiraBase` (irmão de `Corrente`, mesmo ponto dos construtores/`Reconstituir`); `LancarContaComando` ganha `string? ProjetoId = null` | — |
| `MovimentoFinanceiro` | `Registrar(..., string? projetoId = null)`; **estorno herda** (`GerarEstorno` copia, igual já copia `Corrente`) | `BaixarParcelaUseCase.ProcessarLiquidacaoAsync` passa `conta.ProjetoId` (e, de carona, corrige o gap observado: hoje ele **também não propaga `conta.Corrente`** para o movimento — `BaixarParcelaUseCase.cs:66-68` chama `Registrar` sem o parâmetro; a fatia P1 conserta os dois juntos) |
| `ApontamentoDeTempo` (novo) | Nato com vínculos — §3.4 | — |
| `fato_receita_diaria` / `fato_custo_diario` | Fatia tardia (P5): `projeto_id TEXT NOT NULL DEFAULT ''` na CHAVE (`''` = sem projeto — SQLite permite NULL em PK por quirk histórico; sentinela explícita é mais segura que depender disso), rebuild espelhando V19/V20 | Folds leem `ProjetoId?` dos eventos que o carregarem |

**Backfill**: nenhum. Projeto é conceito novo — não existe pista no dado histórico (diferente de
`Corrente`, que era inferível pelo `SourceRef.Modulo`). Linhas antigas ficam `NULL` ("sem projeto").
Classificação retroativa é gesto de UI: `POST /financeiro/projetos/{id}/vincular-assinaturas`
(por `servicoId` — o caso 1:1 comum "o serviço DigiSat É o projeto DigiSat") re-tagueia assinaturas
em lote; cobranças **futuras** herdam. Re-taguear contas passadas fica fora do MVP (o painel aceita
janela "desde o vínculo").

### 3.3 `AtivoAmortizavel` — o custo diferido (caso DigiSat)

```csharp
// Domain/Ativos/AtivoAmortizavel.cs
public sealed class AtivoAmortizavel : AggregateRoot<string>
{
    public string BusinessId { get; }
    public string? ProjetoId { get; }                 // nullable: ativo sem projeto é legal (qualquer compra grande)
    public string Descricao { get; }                  // "Licenças DigiSat 5×36m"
    public Money ValorTotal { get; }                  // R$ 6.895,00 — o custo ECONÔMICO total
    public int VidaUtilMeses { get; }                 // 36 — Decisão D1
    public DateTimeOffset InicioAmortizacao { get; }  // granularidade de MÊS (competência)
    public MetodoAmortizacao Metodo { get; }          // { Linear = 0 } — pinado, extensível (D1)
    public int QuantidadeUnidades { get; }            // 5 licenças — capacidade (D2); default 1
    public string? ContaAPagarId { get; }             // link ao fluxo de CAIXA (as 7 parcelas)
    public StatusAtivoAmortizavel Status { get; private set; }
    public DateTimeOffset? UltimaCompetenciaReconhecida { get; private set; }  // espelho de Assinatura.UltimaCobrancaGeradaEm
    public DateTimeOffset? BaixadoEm { get; private set; }
    public string? MotivoBaixa { get; private set; }
}

/// VALORES PINADOS — persistidos como INTEGER.
public enum StatusAtivoAmortizavel { EmAmortizacao = 0, Encerrado = 1, Baixado = 2 }
public enum MetodoAmortizacao { Linear = 0 }
```

FSM:

```
EmAmortizacao → Encerrado   (automático: última competência do cronograma reconhecida)
EmAmortizacao → Baixado     (write-off antecipado — §4.6)
```

Invariantes de `Criar`: `ValorTotal.EhPositivo`, `VidaUtilMeses >= 1`, `QuantidadeUnidades >= 1`.

O gesto de criação é **um só** na UI: "Registrar investimento" cria o ativo E (opcionalmente, num
único caso de uso) a `ContaAPagar` parcelada do caixa — request carrega `parcelas[]` (7×98500) ou um
`contaAPagarId` já existente. A conta nasce com a nova categoria
`CategoriaFinanceiraPadrao.AtivoDiferido = "ativo-diferido"` e o mesmo `projetoId` — é essa
categoria que a tira do DRE (§4.7).

### 3.4 `ApontamentoDeTempo`

```csharp
// Domain/Tempo/ApontamentoDeTempo.cs
public sealed class ApontamentoDeTempo : AggregateRoot<string>
{
    public string BusinessId { get; }
    public string? ProjetoId { get; }
    public string? ClienteId { get; }
    public string? AssinaturaId { get; }              // se veio, ProjetoId/ClienteId são derivados dela na criação
    public string? OrdemServicoId { get; }            // ponte futura com o vertical Assistência (pós Fatia 3 da auditoria)
    public int Minutos { get; }                       // > 0; granularidade de minuto basta
    public DateTimeOffset Data { get; }
    public string OperadorId { get; }
    public string OperadorNome { get; }               // audit-field com nome junto — padrão MovimentoDeSessaoCaixa
    public string? Descricao { get; }
    public long? CustoHoraCentavosSnapshot { get; }   // CONGELADO na criação — §5.2
}
```

- **Invariante**: ao menos UM vínculo (`ProjetoId`/`ClienteId`/`AssinaturaId`/`OrdemServicoId`) —
  apontamento sem destino não agrega a nada.
- **Sem FSM, sem lançamento contábil, delete físico permitido**: apontamento é registro
  operacional/gerencial, não fato contábil (não toca `LancamentoContabil`, não toca DRE por default
  — §5.3). Errou, apaga e relança. Se o dono ligar `TempoEntraNoDre` (D4), aí sim delete vira
  bloqueado após o fechamento da competência — regra só nasce junto com essa decisão.
- Custo derivado (nunca persistido além do snapshot da taxa):
  `Custo = Money(round_banker(Minutos × CustoHoraCentavosSnapshot / 60))`.

### 3.5 Resumo das entidades novas

| Entidade | Tabela | Chave | Repositórios |
|---|---|---|---|
| `Projeto` | `projetos` | `id` ULID | Sqlite + InMemory + contract tests 2× (padrão `AssinaturaRepositoryContractTests`) |
| `AtivoAmortizavel` | `ativos_amortizaveis` | `id` ULID | idem |
| `ApontamentoDeTempo` | `apontamentos_de_tempo` | `id` ULID | idem |
| `ConfiguracaoFinanceiraTenant` | `configuracoes_financeiras` | `tenant_id` | idem (espelho de `SqliteConfiguracaoFiscalTenantRepository`) |

---

## 4. Custo amortizado — mecânica e matemática

### 4.1 Caixa ≠ competência: os dois trilhos do caso DigiSat

O modelo híbrido do módulo já separa os dois trilhos (`ContaFinanceiraBase` = competência,
`MovimentoFinanceiro` = caixa). O ativo amortizável só estica a competência no tempo:

```
CAIXA       ContaAPagar "Licenças DigiSat" R$6.895 em 7 parcelas de R$985
            (categoria ativo-diferido, projetoId=digisat)
            → BaixarParcela mês a mês → 7 MovimentoFinanceiro de Saída  ← payback lê DAQUI

COMPETÊNCIA AtivoAmortizavel R$6.895 / 36 meses
            → 36 reconhecimentos mensais de ~R$191,53                   ← DRE/margem lê DAQUI
```

Sem o ativo, o DRE de hoje mostraria R$6.895 de despesa concentrada na competência da compra (na
prática, distorção idêntica à do CMV pré-correção — e a solução é a MESMA: "comprar capacidade é
troca de ativo; a despesa nasce com o uso do tempo, não com a compra").

### 4.2 O cronograma: `CronogramaLinear` (compartilhado com a receita diferida P1-5)

Helper puro em `Application/Quant` (ao lado de `RateioProporcional`, que ele usa):

```csharp
public static class CronogramaLinear
{
    /// Devolve VidaUtilMeses pares (competência yyyy-MM, valorCentavos) com
    /// Σ valores == totalCentavos EXATO — RateioProporcional.Alocar(total, pesos = 1×n),
    /// o mesmo Hamilton de FatoMargemProdutoProjection. Determinístico: com restos iguais,
    /// o desempate ThenBy(índice) dá o centavo extra às PRIMEIRAS competências.
    public static IReadOnlyList<(DateOnly Competencia, long ValorCentavos)> Gerar(
        long totalCentavos, int meses, DateOnly inicio);
}
```

- **Amortização** (este design): despesa `V/n` por competência, projeto no lado do custo.
- **Receita diferida** (P1-5 / Fatia 7 da auditoria): reconhecimento `V/k` de cobrança anual — o
  MESMO helper, lado da receita. Um lar só para "espalhar um total exato em n competências";
  quando a Fatia 7 chegar, ela **reusa** `CronogramaLinear`, não reimplementa.

O cronograma **não é persistido** (convenção do repo: totais/derivados são computados da fonte,
nunca cacheados): qualquer leitor — DRE, painel, cron — recomputa `CronogramaLinear.Gerar(...)` do
próprio agregado. Determinismo do Hamilton garante que todos veem os mesmos centavos.

### 4.3 Números do DigiSat (sanidade)

```
689.500 ÷ 36 = 19.152,77… → floor 19.152, resto 28
→ meses 1–28: 19.153 (R$191,53)   meses 29–36: 19.152 (R$191,52)
→ Σ = 28×19.153 + 8×19.152 = 689.500 ✓ (centavo-exato por construção)

Custo por licença (D2): Alocar(689.500, [1,1,1,1,1]) = 5 × 137.900 = R$1.379,00/licença
```

### 4.4 Partida dobrada — a conta `1.3 Ativo Diferido`

`PlanoDeContasPadrao` ganha `AtivoDiferido = ContaContabil.Criar("1.3", "Ativo Diferido",
TipoContaContabil.Ativo)`. O circuito fecha assim:

| Fato | Débito | Crédito |
|---|---|---|
| `ContaAPagar` categoria `ativo-diferido` criada | **1.3 Ativo Diferido** (não 4.1!) | 2.1 Contas a Pagar |
| Baixa de cada parcela (mecanismo existente, intocado) | 2.1 Contas a Pagar | 1.1 Caixa e Bancos |
| Reconhecimento mensal de amortização (novo) | 4.1 Custo/Despesa | **1.3 Ativo Diferido** |

`LancamentoContabilFactory.DeContaAPagar` ganha o desvio por categoria (`ativo-diferido` → débito
em 1.3) — um `switch` no único lar do mapeamento, coerente com a filosofia "é código, não input".
Ao fim dos 36 meses, o saldo de 1.3 referente ao ativo zera sozinho (Σ cronograma = total).

### 4.5 O gerador: `ReconhecerAmortizacoesUseCase` + cron

Clone estrutural do trio `Assinatura.GerarCobranca` / `GerarCobrancasAssinaturasUseCase` /
`FaturarAssinaturasBackgroundService` — mesmo catch-up, mesma idempotência dupla:

- **Domain**: `AtivoAmortizavel.ProximaCompetenciaDevida` (a partir de
  `UltimaCompetenciaReconhecida ?? InicioAmortizacao`, +1 mês) e
  `ReconhecerCompetencia(competencia)` que valida contra o cronograma, avança o cursor e devolve o
  valor do mês; ao reconhecer a última, transiciona `EmAmortizacao → Encerrado` pela FSM.
- **Application**: loop de catch-up por ativo (`while ProximaCompetenciaDevida <= hoje`), cada
  competência com `SourceRef("amortizacao", $"{ativoId}:{yyyyMM}")`; grava o `LancamentoContabil`
  (§4.4) e publica `CustoAmortizadoReconhecido` (§6) **após o commit**; dupla rede: checagem de
  cursor no domínio + `BuscarPorOrigemAsync` antes de persistir.
- **Infrastructure**: `AmortizarAtivosBackgroundService` no molde exato de
  `FaturarAssinaturasBackgroundService` (rodada imediata no boot, intervalo em
  `FinanceiroCronOptions`, fail-open, escopo por rodada). Pode inclusive rodar DENTRO do mesmo ciclo
  do cron existente (terceiro use case no `ExecutarUmCicloFailOpenAsync`) — menos um
  `BackgroundService`; recomendação: mesmo cron.
- **Importante**: DRE e painel **não dependem do cron** para o número — leem
  `CronogramaLinear.Gerar` diretamente (função pura sobre os ativos, filtrada pela janela). O cron
  só materializa o rastro contábil + o evento no ledger. Cron atrasado nunca = número errado.

### 4.6 Baixa antecipada (impairment)

Cancelou a DigiSat no mês 14? `Baixar(motivo, competencia)`:
`EmAmortizacao → Baixado`; o valor ainda não reconhecido (Σ competências restantes do cronograma) é
reconhecido **de uma vez** na competência da baixa (`SourceRef("amortizacao-baixa", ativoId)`,
D-4.1/C-1.3 do restante). O painel marca o ROI final do projeto com o prejuízo real — exatamente o
número que um quant quer ver, não um ativo fantasma amortizando para sempre.

### 4.7 Efeito no DRE (a ÚNICA mudança no demonstrativo)

Espelho exato do tratamento do CMV (`DreGerencialService.cs:17-27`):

1. `CategoriaFinanceiraPadrao.AtivoDiferido` entra na lista de exclusão de `DespesaOperacional`
   (junto de `cmv-fornecedor` e `comissoes`) — a compra é balanço, não resultado.
2. `DreResultado` ganha campo aditivo `Amortizacao` (Money), computado de
   `CronogramaLinear` dos ativos com competência na janela.
3. `ResultadoOperacional = ReceitaBruta − CustoDireto − DespesaOperacional − Amortizacao`.
4. `PorCorrente` **não muda** — amortização é custo de capacidade, não custo variável; misturá-la
   na MC por corrente contaminaria o insumo do breakeven (P1-2). A alocação fina por projeto vive
   no painel (§9), não no DRE.

Invariante de caracterização: tenant sem nenhum `AtivoAmortizavel` e sem categoria `ativo-diferido`
→ `DreResultado` byte-idêntico ao atual (teste com fixture dos testes existentes de DRE).

Nota fiscal: **nada disso toca o Radar do Simples** — DAS incide sobre receita (RBT12); amortização
é lente gerencial. Nenhuma interação com P0-4/Fatia 5.

---

## 5. Apontamento de tempo — custo/hora e fronteira com o DRE

### 5.1 Fluxo de lançamento (o gesto de 5 segundos)

Quick-action "Registrar atendimento" (visível só com o toggle): cliente OU assinatura (busca) →
minutos → descrição opcional → salvar. Se veio `assinaturaId`, o servidor deriva
`clienteId`/`projetoId` dela — o dono nunca classifica duas vezes.

### 5.2 Custo/hora — resolução e snapshot

Resolução na CRIAÇÃO do apontamento (nunca no read):

```
custoHora = custo_hora_operador[operadorId]      (override por técnico — tabela própria, opcional)
          ?? ConfiguracaoFinanceiraTenant.CustoHoraPadraoCentavos
          ?? null                                 (sem custo — apontamento vale só como horas)
```

O valor resolvido é **congelado** em `CustoHoraCentavosSnapshot` — mesmo racional do custo médio
capturado no instante da baixa de estoque (`VendaItensMovimentadosHandler`): mudar a config amanhã
não reescreve o custo de ontem. Painéis somam `round_banker(minutos × snapshot / 60)` por grupo.

### 5.3 Por que tempo NÃO entra no DRE (default)

O salário/pró-labore do técnico **já entra** no DRE via `FolhaLancada` → `ContaAPagar`
(`despesa-com-pessoal`). Somar custo/hora dos apontamentos como despesa contaria o mesmo dinheiro
duas vezes. O custo de tempo é uma **alocação gerencial da folha** (activity-based costing): vive na
margem gerencial do projeto (MC3, §9.3) e no painel "onde vai meu tempo" — nunca no demonstrativo.
`TempoEntraNoDre` existe como escape na config (D4), default `false`, e mesmo ligado entraria como
linha informativa fora do `ResultadoOperacional`.

---

## 6. Eventos

### 6.1 Domínio (privados ao módulo, `record : DomainEvent`)

`ProjetoCriado/Arquivado/Reativado`, `AtivoAmortizavelCriado`, `AmortizacaoReconhecida(AtivoId,
Competencia, ValorCentavos)`, `AtivoBaixadoAntecipadamente(AtivoId, ValorRestanteCentavos)`,
`ApontamentoDeTempoRegistrado`, `AssinaturaVinculadaAProjeto(AssinaturaId, ProjetoId?)`.

### 6.2 Integração (catálogo `IntegrationEvents.cs` — ADITIVO, um record novo)

```csharp
/// <summary>Amortização de competência reconhecida (Análise por Projeto,
/// docs/financeiro/design-analise-por-projeto.md §4) → despesa econômica do mês no ledger,
/// insumo dos folds por projeto (fase P5).</summary>
public sealed record CustoAmortizadoReconhecido(
    string AtivoId, string TenantId, string? ProjetoId, string Competencia /* yyyy-MM */,
    long ValorCentavos, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"amortizacao:{AtivoId}:{Competencia}";  // derivada do id do fato (R3), nunca timestamp
}
```

Publicado pós-commit pelo use case do cron (§4.5). Nenhum outro evento de integração é necessário
nesta feature: tagging e apontamentos são internos ao Financeiro (não há side-effect cross-módulo).
Quando a Fatia 3 da auditoria estender `OsFaturada` aditivamente, `ProjetoId?` pode pegar carona no
mesmo PR aditivo — anotar lá, não bloquear aqui.

---

## 7. Migrações — espelho de V16–V20

Numeração indicativa a partir de V21 (usar o próximo livre na hora); uma migração por tabela, mesmo
grão de V16/V17/V18. Nenhum backfill de `projeto_id` (conceito novo — §3.2).

| # | Tipo | SQL (essência) |
|---|---|---|
| V21 | CREATE | `projetos (id TEXT PK, business_id TEXT NOT NULL, nome TEXT NOT NULL, descricao TEXT NULL, status INTEGER NOT NULL, criado_em TEXT NOT NULL, arquivado_em TEXT NULL)` + `ix_projetos_business (business_id, status)` + índice único `(business_id, lower(nome))` |
| V22 | CREATE | `configuracoes_financeiras (tenant_id TEXT PK, analise_por_projeto INTEGER NOT NULL DEFAULT 0, custo_hora_padrao_centavos INTEGER NULL, tempo_no_dre INTEGER NOT NULL DEFAULT 0)` |
| V23 | ALTER | `assinaturas ADD COLUMN projeto_id TEXT NULL` |
| V24 | ALTER | `recorrencias ADD COLUMN projeto_id TEXT NULL` |
| V25 | ALTER | `contas_a_receber ADD COLUMN projeto_id TEXT NULL` (espelho V16, sem o CASE de backfill) |
| V26 | ALTER | `contas_a_pagar ADD COLUMN projeto_id TEXT NULL` (espelho V17) |
| V27 | ALTER | `movimentos_financeiros ADD COLUMN projeto_id TEXT NULL` (espelho V18) |
| V28 | CREATE | `ativos_amortizaveis (id TEXT PK, business_id, projeto_id TEXT NULL, descricao, valor_centavos INTEGER, moeda TEXT, vida_util_meses INTEGER, inicio_amortizacao TEXT, metodo INTEGER, quantidade_unidades INTEGER, conta_a_pagar_id TEXT NULL, status INTEGER, ultima_competencia_reconhecida TEXT NULL, baixado_em TEXT NULL, motivo_baixa TEXT NULL)` + `ix (business_id, status)` |
| V29 | CREATE | `apontamentos_de_tempo (id TEXT PK, business_id, projeto_id TEXT NULL, cliente_id TEXT NULL, assinatura_id TEXT NULL, ordem_servico_id TEXT NULL, minutos INTEGER, data TEXT, operador_id, operador_nome, descricao TEXT NULL, custo_hora_centavos INTEGER NULL)` + `ix (business_id, data)`, `ix (business_id, projeto_id)` |
| V30 | CREATE | `custo_hora_operador (business_id TEXT, operador_id TEXT, custo_hora_centavos INTEGER NOT NULL, PRIMARY KEY (business_id, operador_id))` — fatia P4 |
| V31/V32 | DROP+CREATE | `fato_receita_diaria`/`fato_custo_diario` com `projeto_id TEXT NOT NULL DEFAULT ''` na PK `(tenant_id, dia, corrente, projeto_id)` + reset do cursor via `ResetarCursorSeExistirAsync` — **espelho byte-a-byte da estratégia V19/V20** (projeção descartável → rebuild+replay, nunca ALTER). Fatia P5; coordenar com a Fatia 7 da auditoria: se `fato_receita_reconhecida` nascer antes, já nasce com `projeto_id` na chave |

Todos os repositórios tocados (Sqlite **e** InMemory) ganham a coluna/entidade no mesmo PR da
migração, com os contract tests 2× atualizados — o padrão da casa.

---

## 8. Endpoints (rota + shape camelCase)

Todos em `FinanceiroEndpointsModule.MapearEndpoints`, tenant SÓ da sessão (`ObterBusinessId()`),
permissões `RequerPermissao(Modulo.Financeiro, Acao.Ver|Editar)`, DTOs de fio (nunca agregado cru).
Serialização minimal-API default já entrega camelCase.

### 8.1 Configuração

```
GET /financeiro/configuracoes
→ { "analisePorProjetoAtiva": false, "custoHoraPadraoCentavos": null, "tempoEntraNoDre": false }

PUT /financeiro/configuracoes            (Acao.Editar — é aqui que o toggle liga/desliga)
← mesmo shape
```

### 8.2 Projetos

```
GET  /financeiro/projetos?incluirArquivados=false
→ [ { "id": "01J...", "nome": "DigiSat", "status": "Ativo", "criadoEm": "...",
      "mrrCentavos": 28000, "assinaturasAtivas": 1,
      "margemMesCentavos": 8847, "paybackProjetadoMeses": 18, "roiRealizadoPercent": -71.6 } ]

POST /financeiro/projetos                          { "nome": "DigiSat", "descricao": null }
POST /financeiro/projetos/{id}/arquivar            (e /reativar)
POST /financeiro/projetos/{id}/vincular-assinaturas { "servicoId": "..." }   → { "vinculadas": 3 }
GET  /financeiro/projetos/{id}/painel?de=2026-01-01&ate=2026-07-31           → §9 (shape completo)
```

### 8.3 Ativos amortizáveis

```
POST /financeiro/ativos
{ "projetoId": "01J...", "descricao": "Licenças DigiSat 5×36m",
  "valorTotalCentavos": 689500, "vidaUtilMeses": 36, "inicioAmortizacao": "2026-07",
  "quantidadeUnidades": 5,
  "contaAPagar": { "parcelas": [ { "vencimento": "2026-08-05", "valorCentavos": 98500 }, … ×7 ] } }
  // OU "contaAPagarId": "…" para vincular uma conta já lançada; OU nenhum dos dois
  // (investimento pago fora do sistema — só a competência entra)

GET  /financeiro/ativos?projetoId=…
POST /financeiro/ativos/{id}/baixar    { "motivo": "Contrato DigiSat encerrado", "competencia": "2027-09" }
```

### 8.4 Tempo

```
POST   /financeiro/apontamentos
{ "assinaturaId": "01J...", "minutos": 30, "data": "2026-07-17T14:00:00-03:00",
  "operadorId": "…", "operadorNome": "Igor", "descricao": "Suporte impressora fiscal" }
→ { "id": "…", "projetoId": "…(derivado)", "custoCentavos": 5000 }

GET    /financeiro/apontamentos?projetoId=&clienteId=&de=&ate=
DELETE /financeiro/apontamentos/{id}
GET    /financeiro/tempo/resumo?de=&ate=          → §9.6 (o painel de gargalo cross-projeto)
```

### 8.5 Tagging aditivo em requests existentes

`LancarContaComando` (+`ProjetoId?`), request de criar assinatura (+`projetoId?`),
`POST /financeiro/assinaturas/{id}/projeto { "projetoId": "…" | null }` (vincular/desvincular uma),
request de criar recorrência (+`projetoId?`). Todos opcionais; com toggle desligado, `projetoId`
presente → 422 (§2.2).

---

## 9. Read-models e a matemática de cada métrica

### 9.0 Fontes (nenhuma métrica inventa dado)

| Métrica | Fonte |
|---|---|
| MRR/churn/LTV | `IAssinaturaRepository` filtrado por `ProjetoId` (reusa `Assinatura.Mrr` normalizado) |
| Receita/custo por competência | `ContaAReceber`/`ContaAPagar` por competência com `ProjetoId` (mesmo caminho do DRE) |
| Amortização | `CronogramaLinear.Gerar` sobre os `AtivoAmortizavel` do projeto (função pura) |
| Payback (realizado) | `MovimentoFinanceiro` com `ProjetoId` (caixa real) |
| Tempo | `ApontamentoDeTempo` agregado |

### 9.1 Shape do `GET /financeiro/projetos/{id}/painel`

```jsonc
{
  "projeto": { "id": "…", "nome": "DigiSat", "status": "Ativo", "criadoEm": "…" },
  "janela": { "de": "2026-01-01", "ate": "2026-07-31" },
  "receita": {
    "mrrCentavos": 28000, "arrCentavos": 336000, "assinaturasAtivas": 1,
    "ticketMedioCentavos": 28000, "receitaMesCentavos": 28000,
    "receitaReconhecidaAcumuladaCentavos": 196000
  },
  "churn": { "cancelamentos12m": 0, "exposicaoAssinaturaMeses12m": 7.0,
             "churnMensalPercent": 0.0, "vidaEsperadaMeses": null },
  "ltv": { "ltvCentavos": null, "limiteInferiorCentavos": 196000,
           "metodo": "mcVariavel/churn", "observacao": "churn=0 na janela — LTV indefinido; mostrado o piso realizado" },
  "custos": {
    "diretoMesCentavos": 0,               // ContaAPagar tagueadas, excl. ativo-diferido
    "amortizacaoMesCentavos": 19153,
    "tempoMesCentavos": 5000,             // null se custo/hora não configurado
    "acumuladoCentavos": 139071,
    "breakdown": [ { "categoriaId": "custo-ia", "valorCentavos": 0 } ]
  },
  "margem": {
    "mc1VariavelMesCentavos": 28000,  "mc1Percent": 100.0,
    "mc2CheiaMesCentavos": 8847,      "mc2Percent": 31.6,
    "mc3GerencialMesCentavos": 3847,  "mc3Percent": 13.7
  },
  "capacidade": { "unidadesTotais": 5, "unidadesUtilizadas": 1, "utilizacaoPercent": 20.0,
                  "custoOciosidadeMesCentavos": 15322 },
  "payback": { "investimentoTotalCentavos": 689500, "fluxoCaixaAcumuladoCentavos": -493500,
               "paybackRealizadoEm": null, "paybackProjetadoMeses": 18,
               "metodo": "simulacao-fluxo-conhecido" },
  "roi": { "realizadoPercent": -71.6, "roiSobreInvestimentoPercent": -71.6,
           "runRateAnualizadoPercent": 46.2, "base": "competencia" },
  "tempo": { "minutosJanela": 210, "custoJanelaCentavos": 35000,
             "porCliente": [ { "clienteId": "…", "clienteNome": "…", "minutos": 210, "custoCentavos": 35000 } ] }
}
```

### 9.2 Receita e MRR

- `MRR_P = Σ Assinatura.Mrr` das assinaturas **ativas** com `ProjetoId = P` (a normalização
  ciclo→mensal já existe e é o único lar — `Assinatura.NormalizarParaMensal`).
- `receitaMes` / `receitaReconhecidaAcumulada` = Σ `ContaAReceber` não-canceladas do projeto por
  `DataCompetencia` (mês corrente / desde o início). Quando a Fatia 7 (receita diferida) chegar,
  esta soma passa a ler o cronograma de reconhecimento — o painel herda de graça porque lê a mesma
  fonte do DRE.

### 9.3 Margem de contribuição em 3 camadas (declaradas, nunca misturadas)

```
MC1 (variável)  = Receita_mês − CustoDiretoVariável_mês
                  CustoDiretoVariável = ContaAPagar do projeto na competência,
                  EXCLUINDO categoria ativo-diferido (é balanço)      ← ex.: custo de IA do Aevo
MC2 (cheia)     = MC1 − Amortização_mês (Σ cronogramas dos ativos do projeto na competência)
MC3 (gerencial) = MC2 − CustoTempo_mês (Σ round_banker(minutos × custoHoraSnapshot/60))
```

Cada camada responde uma pergunta diferente: MC1 = "vale vender mais uma unidade?" (insumo do LTV);
MC2 = "o projeto paga a capacidade que comprei?"; MC3 = "o projeto paga até o meu tempo?".

### 9.4 Churn e LTV (hazard por exposição — correto para n pequeno)

Snapshot mensal (o método do `ReceitaRecorrenteService`) é ruidoso demais para um projeto com 3
assinaturas. O painel usa taxa de risco por exposição:

```
W  = min(12 meses, idade do projeto)
λ  = cancelamentos_no_W / Σ assinatura-meses ativos no W        (exposição fracionária por dias)
churnMensalPercent = 100·λ
vidaEsperadaMeses  = 1/λ                    (sobrevivência geométrica)
LTV = MC1_por_assinatura_mensal × (1/λ)     onde MC1_por_assinatura = MC1_mês / assinaturasAtivas
```

- `λ = 0` (nenhum cancelamento ainda): LTV **indefinido** — o painel devolve `null` +
  `limiteInferiorCentavos` = margem acumulada realizada por assinatura ("o LTV já é ≥ isso"),
  nunca um número inventado. (Sem prior bayesiano no MVP — honestidade > esperteza; é uma extensão
  natural se o dono quiser um shrinkage com o churn global do tenant.)
- Quando a Fatia 7 da auditoria (P1-4, movimentos de MRR por evento) chegar, os termos
  novo/expansão/contração/churn do projeto saem do MESMO ledger de eventos — este painel troca a
  fonte sem mudar o shape.

### 9.5 Payback e ROI

**Payback realizado (caixa, o número "de verdade"):**

```
FluxoAcum(T) = Σ_{mov ∈ MovimentoFinanceiro do projeto, data ≤ T} (+valor se Entrada, −valor se Saída)
paybackRealizadoEm = primeiro T com FluxoAcum(T) ≥ 0 tendo existido FluxoAcum < 0 antes
                     (null enquanto não cruzar; estornos já entram com o tipo invertido)
```

**Payback projetado (simulação determinística mês a mês — estilo da casa, não fórmula de bolso):**

```
margemCaixaMensal    = MRR_P − CustoRecorrenteMensal_P
CustoRecorrenteMensal_P = média das 3 últimas competências FECHADAS de ContaAPagar do projeto
                          (excl. ativo-diferido)
fluxo(m) = margemCaixaMensal − Σ parcelas EM ABERTO de contas do projeto com vencimento no mês m
Acum(0)  = FluxoAcum(hoje);  Acum(m) = Acum(m−1) + fluxo(m)
paybackProjetadoMeses = menor m com Acum(m) ≥ 0   (horizonte 120 meses; null se não cruza)
```

As parcelas restantes do investimento (as 7×985 ainda não pagas) entram automaticamente — estão em
aberto na `ContaAPagar` tagueada. Nenhum parâmetro exógeno.

**ROI (competência):**

```
CustoEconAcum(T)          = CustoDiretoAcum + AmortizaçãoReconhecidaAcum (+ CustoTempoAcum, se configurado)
roiRealizadoPercent       = 100 × (ReceitaAcum − CustoEconAcum) / CustoEconAcum
roiSobreInvestimentoPercent = 100 × (ReceitaAcum − CustoEconAcum) / InvestimentoTotal   (Σ ativos do projeto)
runRateAnualizadoPercent  = 100 × (MC2_mês × 12) / (Amortização_mês × 12)  − 100
                            (= a margem corrente sobre o custo de capacidade corrente — "no ritmo
                             de hoje, o retorno anual sobre a capacidade é X%")
```

**Sanidade DigiSat (1 assinatura de R$280, investimento R$6.895 em 7×R$985):**

- Caixa: meses 1–7 acumulam `280−985 = −705`/mês → `−4.935` no mês 7; depois `+280`/mês →
  cruza zero em `7 + ⌈4935/280⌉ = 25` meses. Estático confere: `6895/280 = 24,6 → 25`.
- Com as 5 licenças vendidas (R$1.400/mês): `1400 > 985` → o fluxo nunca fica negativo — payback
  imediato; o painel mostra `paybackRealizadoEm = null` + acumulado sempre ≥ 0 (documentado no
  campo `metodo`).
- Competência: MC2 = `280 − 191,53 = R$88,47`/mês com 20% de utilização — o projeto já é
  economicamente positivo POR MÊS mesmo com o caixa ainda no vermelho. As duas lentes juntas são
  exatamente o que o dono pediu ("o retorno levando em conta o custo específico").
- Vida inteira (36m, 1 licença): `10.080 − 6.895 = +3.185` → ROI 46,2%; com 5: +631%.

### 9.6 Capacidade e ociosidade (as 5 licenças — D2)

```
unidadesTotais       = Σ QuantidadeUnidades dos ativos EmAmortizacao do projeto
unidadesUtilizadas   = assinaturas ativas do projeto           (1 assinatura ↔ 1 unidade — D2)
utilizacaoPercent    = 100 × utilizadas / totais
custoOciosidadeMes   = Amortização_mês × (1 − utilização)      → DigiSat: 191,53 × 0,8 = R$153,22/mês
custoPorUnidade      = Alocar(ValorTotal, 1×unidades)          → R$1.379,00/licença
paybackPorUnidade    = custoPorUnidade / MC1_por_assinatura    → 1379/280 ≈ 4,9 meses/licença
```

A amortização corre sobre o TOTAL independente da utilização (licença parada também queima dinheiro
— é o insight, não um bug); a ociosidade é exibida, nunca abatida.

### 9.7 `GET /financeiro/tempo/resumo` — onde vai meu tempo

Agrega apontamentos da janela por projeto e por cliente, cruzando com margem:

```jsonc
{ "janela": { "de": "…", "ate": "…" }, "minutosTotais": 1240, "custoTotalCentavos": 206700,
  "porProjeto": [ { "projetoId": "…", "nome": "Aevo", "minutos": 840, "custoCentavos": 140000,
                    "margemMc1Centavos": 92000, "indiceGargalo": 1.52 } ],
  "porCliente": [ { "clienteId": "…", "clienteNome": "…", "minutos": 300, "custoCentavos": 50000,
                    "mrrCentavos": 28000, "indiceGargalo": 1.79 } ] }
```

`indiceGargalo = custoTempo / margem (MC1)` do mesmo período — ordenado desc, é literalmente a lista
"quem come meu tempo sem pagar por ele". `> 1` = o atendimento consome mais do que a margem entrega;
`null` quando não há custo/hora configurado (ordena por minutos nesse caso).

### 9.8 `ProjetosResumoService` (a lista de `GET /financeiro/projetos`)

Uma linha por projeto com o sub-conjunto barato do painel (MRR, ativas, MC2 do mês, payback
projetado, ROI realizado) — mesmas fórmulas, janela fixa "mês corrente", calculado em lote.

---

## 10. Impacto no que existe

| Peça | Mudança | Quem não usa projeto |
|---|---|---|
| `DreGerencialService` | Linha `Amortizacao` + exclusão de `ativo-diferido` da despesa (§4.7) | Zero ativos → DRE byte-idêntico (teste) |
| `LancamentoContabilFactory` / `PlanoDeContasPadrao` | Conta `1.3 Ativo Diferido` + desvio por categoria (§4.4) | Nunca aciona o desvio |
| `BaixarParcelaUseCase` | Propaga `conta.ProjetoId` **e** `conta.Corrente` ao `MovimentoFinanceiro` (fecha o gap existente de corrente — §3.2) | Propaga `null`, como hoje |
| `GerarContasRecorrentesUseCase` / `GerarCobrancasAssinaturasUseCase` | Copiam `projetoId` do template/assinatura para a conta | Copiam `null` |
| Cron (`FaturarAssinaturasBackgroundService`) | 3º use case no ciclo: `ReconhecerAmortizacoesUseCase` | Loop vazio |
| `FinanceiroConsultorFactProvider` | Fatos novos, fail-quiet quando toggle off/sem dados: "payback da DigiSat projetado em N meses", "ociosidade custa R$X/mês", "cliente Y tem índice de gargalo Z" | Nenhum fato emitido |
| Radar do Simples / breakeven / bandas | **Intocados** — amortização não é receita tributável nem entra na MC por corrente | — |
| Fatias da auditoria em andamento | Sem conflito: colunas aditivas ortogonais. Coordenação em 2 pontos: `OsFaturada` aditiva (Fatia 3) pode levar `ProjetoId?` de carona; `fato_receita_reconhecida` (Fatia 7) deve nascer com `projeto_id` na chave e **reusar `CronogramaLinear`** | — |

---

## 11. Plano de implementação — fatias dotnet-gated

Cada fatia termina com `dotnet build && dotnet test` verdes e é útil sozinha.

**P1 — Projeto, toggle e tagging (a fundação).**
Domain: `Projeto` + FSM; `ProjetoId?` em `Assinatura` (com `VincularProjeto`), `Recorrencia`,
`ContaFinanceiraBase`, `MovimentoFinanceiro` (estorno herda). `ConfiguracaoFinanceiraTenant`.
Application: propagação em `GerarCobranca`/`GerarContasRecorrentes`/`BaixarParcela` (com o fix da
corrente), `AnalisePorProjetoGuard`, CRUD de projeto + config endpoints + vincular-assinaturas.
Infrastructure: migrações V21–V27, repos Sqlite+InMemory, contract tests 2×.
Testes-chave: toggle off → 422 em escrita com projeto; estorno herda projeto; cobrança de
assinatura tagueada nasce tagueada; DRE inalterado (caracterização).

**P2 — Painel do Projeto v1 (sem ativo, sem tempo).**
`PainelDoProjetoService` + `ProjetosResumoService`: MRR, churn λ/exposição, LTV (com o `null`
honesto), receita, MC1, custo direto tagueado. `GET /financeiro/projetos` +
`GET /financeiro/projetos/{id}/painel`. Testes: fórmulas de §9.2/9.3/9.4 com fixtures nominais;
Σ receita dos painéis ≤ receita total do DRE da mesma janela.

**P3 — Ativo amortizável (o caso DigiSat completo).**
Domain: `AtivoAmortizavel` + FSM + `CronogramaLinear` (Hamilton). Contábil: conta 1.3 + desvio da
factory + categoria `ativo-diferido`. Application: criar ativo (com `ContaAPagar` opcional no mesmo
gesto), `ReconhecerAmortizacoesUseCase` idempotente, baixa antecipada; DRE ganha `Amortizacao`;
painel ganha MC2, payback (realizado + simulação), ROI, capacidade/ociosidade. Infrastructure:
V28, evento `CustoAmortizadoReconhecido` no catálogo, cron no ciclo existente.
Testes: Σ cronograma = total (property test com totais/vidas sortidos); replay do cron não duplica;
números do DigiSat de §9.5 como teste nominal ponta-a-ponta; baixa antecipada reconhece o resto
exato; DRE de tenant sem ativos byte-idêntico.

**P4 — Apontamento de tempo.**
Domain+Infra: `ApontamentoDeTempo`, V29/V30, resolução+snapshot de custo/hora, derivação de
vínculos via assinatura. Endpoints de apontamento + `tempo/resumo` (índice de gargalo). Painel
ganha MC3 e bloco `tempo`. Testes: snapshot congelado (mudar config não muda custo passado);
arredondamento bancário de minutos×taxa; invariante "ao menos um vínculo".

**P5 — Projeto nas fact tables + Consultor.**
V31/V32 (rebuild com `projeto_id` na chave, espelho V19/V20), folds atualizados
(`CustoAmortizadoReconhecido` → `fato_custo_diario`? **não** — fato novo `fato_custo_amortizado`
mensal OU coluna dedicada; decidir no PR mantendo `fato_custo_diario` = CMV puro para não
contaminar breakeven), série diária do painel, fatos do Consultor por projeto.

**P6 — Convergência com a receita diferida (quando a Fatia 7 da auditoria rodar).**
`fato_receita_reconhecida` usa `CronogramaLinear` e nasce com `projeto_id`; o painel troca a fonte
de `receitaMes` para o reconhecimento; churn do projeto passa a ler os movimentos de MRR por evento.

---

## 12. Decisões que precisam do dono (com recomendação)

| # | Decisão | Opções | **Recomendação (default do design)** |
|---|---|---|---|
| **D1** | Método e vida de amortização | Linear vs. acelerada; vida fixa vs. escolhida por ativo; valor residual | **Linear, sem valor residual, vida escolhida por ativo na criação** (default sugerido pela UI = duração contratual — DigiSat 36m). Linear + Hamilton é centavo-exato, auditável de cabeça, e SOTA para intangível de vida contratual definida; `MetodoAmortizacao` fica extensível se um dia quiser saldo-decrescente |
| **D2** | Como as 5 licenças viram capacidade | (a) 1 ativo com `quantidadeUnidades=5`; (b) 5 ativos de R$1.379; (c) ignorar capacidade | **(a)** — a compra foi UMA transação; amortiza o total independente da utilização (ociosidade visível como custo, não escondida); custo/licença via Hamilton (R$1.379,00) só para métricas unitárias (payback/licença ≈ 4,9 meses a R$280). (b) só se as licenças puderem ser baixadas individualmente no futuro — dá para migrar depois via baixa parcial |
| **D3** | Custo/hora do tempo | Global; por técnico; nenhum | **Global primeiro** (`custoHoraPadraoCentavos`), com override por técnico já previsto (V30). Sugerir como default: pró-labore mensal ÷ 160h. Sempre snapshot no lançamento |
| **D4** | Tempo entra no DRE? | Sim como despesa; sim como linha informativa; não (só painel) | **Não** — o salário já entra via `FolhaLancada`; custo/hora no DRE dupla-contaria a folha. Tempo é alocação gerencial (MC3 + painel de gargalo). O toggle `tempoEntraNoDre` existe como escape, default `false`, e mesmo ligado seria linha informativa fora do `ResultadoOperacional` |
| **D5** | Lente primária de payback | Caixa (movimentos reais) vs. competência | **Caixa como número-manchete** (payback é pergunta de caixa por natureza), com o ROI por competência ao lado — o painel mostra os dois (§9.5); para o DigiSat as duas lentes divergem por ~18 meses e é exatamente essa divergência que informa a decisão de vender as outras 4 licenças |

---

## 13. Invariantes de teste (o contrato deste design)

1. **Não-intrusividade**: toggle off ⇒ DRE, fluxo, previsão e consultor byte-idênticos ao baseline;
   toda escrita com `projetoId` ⇒ 422.
2. **Conservação de centavos**: Σ `CronogramaLinear` = `ValorTotal` para qualquer (total, vida);
   Σ custo/licença = total do ativo (property tests).
3. **Lente ⊆ total**: Σ receita dos painéis de projeto ≤ `ReceitaBruta` do DRE na mesma janela
   (igual quando tudo tagueado — espelho do invariante `PorCorrente`).
4. **Idempotência**: replay de `ReconhecerAmortizacoes` e re-execução do cron nunca duplicam
   lançamento/evento (chave `amortizacao:{ativoId}:{yyyyMM}`).
5. **Herança em estorno**: estorno de movimento tagueado carrega o mesmo `projetoId` e `corrente`.
6. **Fechamento contábil**: ao fim da vida (ou na baixa), saldo da conta 1.3 relativo ao ativo = 0.
7. **Snapshot de tempo**: alterar custo/hora na config não altera custo de apontamento existente.
8. **DigiSat nominal**: o cenário de §9.5 (payback mês 25 com 1 licença; fluxo nunca-negativo com
   5; MC2 = R$88,47; ociosidade = R$153,22) como teste ponta-a-ponta de regressão.

---

*Divergência entre este doc e o código após as fatias entrarem = bug de documentação; atualizar no
PR que mudar o comportamento citado (mesma regra da auditoria).*
