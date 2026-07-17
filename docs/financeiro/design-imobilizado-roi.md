# Design — Imobilizado (depreciação linear) + Painel de ROI do Negócio

> Documento de DESIGN (read-only, nenhum código alterado), produzido em 2026-07-17 sobre o estado
> atual de `src/Modules/Financeiro/**` (migrações V1–V20 aplicadas; dimensão `CorrenteDeReceita`
> em produção) e sobre o design-irmão `docs/financeiro/design-analise-por-projeto.md`
> ("design-pai" daqui em diante — **ainda não implementado**: suas fatias P1–P6 e migrações
> indicativas V21–V32 são design-only, o que dá a este documento a liberdade de GENERALIZAR o
> `AtivoAmortizavel` do design-pai ANTES de ele nascer, sem refactor).
>
> **Contexto de negócio**: o dono (matemático/quant) está abrindo a assistência técnica agora e
> quer, desde o dia zero, todo o investimento no papel — equipamentos, móveis, placa/comunicação
> visual, reforma, computador — com depreciação linear no DRE e um painel que responda **"em
> quantos meses o negócio atinge o ROI completo?"** com payback simples, payback descontado, TIR
> e ROI% acumulado. Escopo deliberadamente ENXUTO: **sem módulo de sócios/split de capital**
> (decisão explícita do dono — este design não o desenha nem deixa gancho que o exija).

---

## 0. TL;DR — o modelo em cinco linhas

1. **`AtivoDeCapital`** é a GENERALIZAÇÃO do `AtivoAmortizavel` do design-pai (§3.3 de lá): o
   mesmo agregado, mesma FSM, mesmo cron idempotente e mesmo `CronogramaLinear`, ganhando
   `Natureza` (Tangível/Intangível), `Categoria` (equipamento/móveis/…), `ValorResidual` e
   `DataAquisicao` — **um lar único** para depreciação (bem tangível) E amortização (licença
   DigiSat), nunca duas máquinas paralelas.
2. **Depreciação linear** = `CronogramaLinear.Gerar((Custo − Residual), vidaUtilMeses, início)` —
   Hamilton centavo-exato, o MESMO helper da amortização e da futura receita diferida (P1-5) —
   virando a linha `DepreciacaoEAmortizacao` do DRE via cron idempotente
   (`amortizacao:{ativoId}:{yyyyMM}`), com a compra excluída do resultado (é balanço, conta
   `1.3 Ativos de Capital`), espelho exato da correção do CMV.
3. **`AporteDeCapital`** = entidade LEVE (valor, data, descrição) para capital de giro /
   investimento inicial — registro **gerencial**, fora de `MovimentoFinanceiro` (que exige
   `ParcelaId`) e fora da partida dobrada (creditaria 3.1 Receita e mentiria no DRE/RBT12).
   Conta no "total investido" do ROI e em nada mais. Sem sócio, sem percentual, sem patrimônio.
4. **`GET /financeiro/roi-negocio`** (read-model `RoiDoNegocioService`): fluxo líquido mensal
   `N_m = FluxoOperacional_m − Capex_m` → payback simples (1º mês com acumulado ≥ 0), payback
   descontado (taxa da config), TIR (bisseção sobre o VPL), ROI% acumulado e **meses até ROI
   completo** (projeção determinística pela trajetória conhecida — clone da simulação §9.5 do
   design-pai, extraída para `Application/Quant/MatematicaDePayback` e REUSADA pelos dois
   painéis).
5. **Opt-in absoluto**: toggle `imobilizadoRoiAtivo` em `ConfiguracaoFinanceiraTenant` (o record
   do design-pai §2.1, estendido; default `false`). Desligado: nenhum pixel, escrita → 422, DRE
   byte-idêntico (invariante de teste). Toda fórmula tem UM lar declarado (§10).

---

## 1. Os casos-âncora que o modelo precisa representar

| Caso | O que exige do modelo |
|---|---|
| **Reforma da loja R$25.000** | Bem tangível sem "unidades", vida útil longa (sugerida = prazo do contrato de aluguel), depreciação mensal no DRE desde o mês de conclusão — nunca R$25.000 de despesa num mês só |
| **Equipamentos/bancada R$12.000, computador R$6.000, móveis R$8.000, placa R$4.800** | Cadastro em segundos (nome, categoria, custo, data, vida útil), cada um com seu cronograma; painel lista valor contábil e depreciação acumulada por bem |
| **Licenças DigiSat R$6.895/36m** (caso do design-pai) | O MESMO agregado — intangível com `projetoId` — sem nenhuma divergência de mecânica; os números nominais de §9.5 do design-pai continuam válidos byte a byte |
| **Capital de giro R$20.000 na conta da empresa** | Registro de aporte em um gesto (valor+data), contando no total investido do ROI — sem virar módulo de sócios |
| **"Em quanto tempo recupero tudo?"** | Payback simples/descontado, TIR, ROI%, meses até ROI completo — cada número com fórmula fechada neste doc e rastreável à fonte (movimentos, bens, aportes) |

---

## 2. Invariante de UX — opt-in não-intrusivo

### 2.1 O toggle: extensão de `ConfiguracaoFinanceiraTenant`

O design-pai (§2.1) já define o record por tenant com repositório SQLite/InMemory + contract
tests 2× (espelho de `ConfiguracaoFiscalTenant`, `src/Modules/Fiscal/.../Regimes/ConfiguracaoFiscalTenant.cs`).
Como **nada disso foi implementado ainda**, a extensão é um merge de design, não uma migração:

```csharp
// Domain/Comum — record puro, sem I/O (AMENDA o §2.1 do design-pai; um record só, uma tabela só)
public sealed record ConfiguracaoFinanceiraTenant(
    string TenantId,
    bool AnalisePorProjetoAtiva,          // design-pai
    long? CustoHoraPadraoCentavos,        // design-pai
    bool TempoEntraNoDre,                 // design-pai
    bool ImobilizadoRoiAtivo,             // ESTE design — default false
    int? TaxaDescontoAnualBps,            // ESTE design — 1200 = 12% a.a.; null = payback descontado omitido
    DateOnly? InicioOperacao)             // ESTE design — null = m0 derivado do 1º fato (§7.2)
{
    public static ConfiguracaoFinanceiraTenant Padrao(string tenantId)
        => new(tenantId, false, null, false, false, null, null);
}
```

Dois toggles independentes de propósito: o dono pode querer Imobilizado+ROI no dia zero e a
análise por projeto (DigiSat/Aevo) só meses depois — ou vice-versa. Ausência de linha = tudo
desligado (zero seed; tenant novo nasce como hoje).

### 2.2 Gating (servidor) — mesmo contrato do design-pai §2.2

| Estado | Leitura | Escrita |
|---|---|---|
| **Desligado** | `GET /financeiro/imobilizado` → `[]`; `GET /financeiro/aportes` → `[]`; `GET /financeiro/roi-negocio` → 404 | Qualquer `POST` de bem/aporte/baixa → **422 `financeiro.imobilizado.desativado`** |
| **Ligado** | Tudo disponível | Tudo disponível |

Um único helper de gating para as DUAS features (nunca `if` espalhado): o `AnalisePorProjetoGuard`
do design-pai vira **`FinanceiroOptInGuard`** com dois métodos —
`ExigirAnalisePorProjetoAsync(businessId)` e `ExigirImobilizadoRoiAsync(businessId)` — mesmo
arquivo, mesma leitura de config, dois erros distintos. (Amenda o design-pai §2.2; ambos
pré-implementação, custo zero.)

**Exceção deliberada ao gating**: bens `AtivoDeCapital` criados pela análise por projeto (caso
DigiSat, toggle `analisePorProjetoAtiva` ligado) são válidos mesmo com `imobilizadoRoiAtivo`
desligado — o agregado é compartilhado; cada toggle governa a SUA superfície (telas/rotas), não a
existência do dado. A regra exata: `POST /financeiro/imobilizado` exige `imobilizadoRoiAtivo`;
`POST /financeiro/ativos` (rota do design-pai §8.3) exige `analisePorProjetoAtiva`; as duas rotas
criam a mesma entidade. O DRE mostra a linha `DepreciacaoEAmortizacao` sempre que existir ativo —
`Money.Zero` para quem não tem nenhum → byte-idêntico (invariante de teste, espelho do §4.7 do
design-pai).

### 2.3 UI

Menu "Investimento & ROI" do Financeiro, o quick-action "Registrar bem" e o card de ROI no
dashboard só renderizam com `imobilizadoRoiAtivo` (lido de `GET /financeiro/configuracoes` no
load). Desligado, nenhum pixel muda.

---

## 3. Modelo de domínio — a generalização de `AtivoAmortizavel`

### 3.1 Antes (design-pai §3.3) → Depois (este design)

O design-pai desenhou o agregado para o caso DigiSat (intangível, por projeto). Este design o
generaliza ANTES da implementação — a fatia P3 do design-pai já nasce na forma geral, e o
"imobilizado" não é uma segunda máquina: é o MESMO agregado com natureza tangível.

```csharp
// ANTES — design-pai §3.3 (Domain/Ativos/AtivoAmortizavel.cs, nunca implementado)
public sealed class AtivoAmortizavel : AggregateRoot<string>
{
    public string BusinessId { get; }
    public string? ProjetoId { get; }
    public string Descricao { get; }
    public Money ValorTotal { get; }
    public int VidaUtilMeses { get; }
    public DateTimeOffset InicioAmortizacao { get; }
    public MetodoAmortizacao Metodo { get; }            // { Linear = 0 }
    public int QuantidadeUnidades { get; }
    public string? ContaAPagarId { get; }
    public StatusAtivoAmortizavel Status { get; }       // { EmAmortizacao=0, Encerrado=1, Baixado=2 }
    public DateTimeOffset? UltimaCompetenciaReconhecida { get; }
    public DateTimeOffset? BaixadoEm { get; }
    public string? MotivoBaixa { get; }
}
```

```csharp
// DEPOIS — Domain/Ativos/AtivoDeCapital.cs (este design; SUBSTITUI o de cima 1:1 + campos novos)
public sealed class AtivoDeCapital : AggregateRoot<string>   // Id = ULID
{
    public string BusinessId { get; }
    public string? ProjetoId { get; }                 // nullable: reforma da loja não é de projeto nenhum
    public string Nome { get; }                       // "Bancada ESD", "Licenças DigiSat 5×36m" (era Descricao)
    public NaturezaAtivo Natureza { get; }            // NOVO — Tangivel deprecia, Intangivel amortiza (mesma máquina)
    public CategoriaAtivo Categoria { get; }          // NOVO — equipamento/moveis/… (relatório e vida útil sugerida)
    public Money CustoAquisicao { get; }              // era ValorTotal — o custo ECONÔMICO total
    public Money ValorResidual { get; }               // NOVO — default Zero; invariante: >= 0 e < CustoAquisicao
    public DateOnly DataAquisicao { get; }            // NOVO — o relógio do INVESTIMENTO (Capex_m do ROI, §7)
    public DateOnly InicioDepreciacao { get; }        // era InicioAmortizacao — competência (mês); default = mês da aquisição
    public int VidaUtilMeses { get; }                 // >= 1
    public MetodoDeCronograma Metodo { get; }         // era MetodoAmortizacao — { Linear = 0 }, pinado, extensível
    public int QuantidadeUnidades { get; }            // default 1 (capacidade — D2 do design-pai, só faz sentido p/ licença)
    public string? ContaAPagarId { get; }             // link ao trilho de CAIXA (parcelas), categoria ativo-de-capital
    public StatusAtivoDeCapital Status { get; private set; }
    public DateTimeOffset? UltimaCompetenciaReconhecida { get; private set; }  // cursor do cron (espelho Assinatura)
    public DateTimeOffset? EncerradoEm { get; private set; }
    public DateTimeOffset? BaixadoEm { get; private set; }
    public string? MotivoBaixa { get; private set; }
    public Money? ValorVenda { get; private set; }    // NOVO — preenchido só na transição p/ Vendido (fatia I4)
}

/// VALORES PINADOS — persistidos como INTEGER; nunca reordenar.
public enum NaturezaAtivo { Intangivel = 0, Tangivel = 1 }        // 0 = comportamento do design-pai
public enum CategoriaAtivo
{
    Equipamento = 0, Moveis = 1, ComunicacaoVisual = 2, Reforma = 3,
    Computador = 4, Veiculo = 5, LicencaSoftware = 6, Outro = 99
}
public enum MetodoDeCronograma { Linear = 0 }
public enum StatusAtivoDeCapital { EmUso = 0, Encerrado = 1, Baixado = 2, Vendido = 3 }
```

Renomes vs. design-pai (todos pré-implementação — zero refactor, o design-pai deve ser lido com
esta tabela ao lado):

| Design-pai | Este design | Racional |
|---|---|---|
| `AtivoAmortizavel` | `AtivoDeCapital` | "Amortizável" é tecnicamente errado para bem tangível (BR: deprecia); o nome neutro cobre os dois |
| `Descricao` | `Nome` | É o rótulo do bem, não um texto livre |
| `ValorTotal` | `CustoAquisicao` | Distingue do valor contábil corrente |
| `InicioAmortizacao` | `InicioDepreciacao` | Neutro na prática (doc do campo diz: "início do cronograma, depreciação OU amortização") |
| `MetodoAmortizacao` | `MetodoDeCronograma` | Casa com `CronogramaLinear` |
| `StatusAtivoAmortizavel.EmAmortizacao=0` | `StatusAtivoDeCapital.EmUso=0` | Mesmo valor pinado, semântica ampliada |
| tabela `ativos_amortizaveis` (V28 indicativa) | `ativos_de_capital` | §9 |
| categoria `ativo-diferido` | `ativo-de-capital` | §4.4 — um slug só para o desvio contábil |
| conta `1.3 Ativo Diferido` | `1.3 Ativos de Capital` | idem |

O que **não** muda vs. design-pai (herdado verbatim): FSM dirigida pelo cron, cursor
`UltimaCompetenciaReconhecida`, `SourceRef("amortizacao", "{id}:{yyyyMM}")`, evento de integração
`CustoAmortizadoReconhecido` (nome e chave de idempotência mantidos — o doc do record passa a
dizer "cobre depreciação e amortização"), gesto único de criação com `ContaAPagar` parcelada
opcional, `QuantidadeUnidades` para capacidade, invariantes de `Criar`.

### 3.2 FSM

```
EmUso → Encerrado    (automático: última competência do cronograma reconhecida pelo cron)
EmUso → Baixado      (write-off/impairment — §4.5)
EmUso → Vendido      (alienação com ValorVenda — fatia I4, §4.6)
Encerrado → Baixado | Vendido   (bem 100% depreciado ainda existe fisicamente e pode ser vendido)
```

Invariantes de `Criar`: `CustoAquisicao.EhPositivo`, `ValorResidual >= Zero`,
`ValorResidual < CustoAquisicao`, `VidaUtilMeses >= 1`, `QuantidadeUnidades >= 1`,
`InicioDepreciacao >= mês de DataAquisicao` (não se deprecia antes de existir).

### 3.3 `AporteDeCapital` — investimento inicial / capital de giro

```csharp
// Domain/Ativos/AporteDeCapital.cs — registro LEVE, gerencial
public sealed class AporteDeCapital : AggregateRoot<string>
{
    public string BusinessId { get; }
    public Money Valor { get; }               // invariante: EhPositivo
    public DateOnly Data { get; }
    public string Descricao { get; }          // "Capital de giro inicial", "Cobertura da folha de setembro"
    public DateTimeOffset CriadoEm { get; }
}
```

Sem FSM, sem lançamento contábil, **delete físico permitido** — mesmo racional do
`ApontamentoDeTempo` do design-pai (§3.4 de lá): registro gerencial que alimenta UMA lente (o
total investido do ROI), não fato contábil. Errou, apaga e relança.

**Formas alternativas avaliadas e rejeitadas** (a pergunta 2 do escopo — "qual a forma mais
limpa?"):

| Alternativa | Por que NÃO |
|---|---|
| Tipo/categoria novo de `MovimentoFinanceiro` (`aporte`) | `MovimentoFinanceiro.Registrar` **exige `ParcelaId`** (invariante "toda entrada de caixa tem origem de competência", `MovimentoFinanceiro.cs:75-78`) — um aporte não liquida parcela nenhuma. Além disso contaminaria TODA a série de caixa operacional: bandas P5/P50/P95, burn EWMA, fluxo projetado e o próprio ROI leriam o aporte como "entrada do negócio", exatamente o que o payback não pode fazer (o aporte é FUNDING, não retorno — §7.3) |
| `ContaAReceber` categoria especial + baixa | `LancamentoContabilFactory.DeContaAReceber` credita **3.1 Receita** — aporte viraria receita no razão; e a conta entraria em `ListarPorCompetenciaAsync` inflando a `ReceitaBruta` do DRE. Corrigir isso exigiria desvios em três lugares — pior que uma entidade de 5 campos |
| Conta de Patrimônio Líquido + partida dobrada (D-1.1/C-2.3) | Exigiria `TipoContaContabil.Patrimonio` novo E um débito em 1.1 Caixa **sem** `MovimentoFinanceiro` correspondente — dessincronizando o razão de caixa da série de movimentos (hoje 1.1 é alimentado exclusivamente por movimentos). É o primeiro passo de um módulo de capital/sócios — **explicitamente fora do escopo** |
| **Entidade leve `AporteDeCapital`** (escolhida) | 5 campos, zero acoplamento, conta no ROI e em nada mais. Se um dia existir módulo de sócios, ganha `socioId?` aditivo — sem retrabalho |

Nota de honestidade ao quant: o aporte declarado **não altera a data do payback** (prova em
§7.4 — invariância); ele muda o denominador do ROI% e a leitura de funding. Declarar de menos não
adianta o payback; declarar de mais não o atrasa.

### 3.4 Resumo das entidades

| Entidade | Tabela | Chave | Repositórios |
|---|---|---|---|
| `AtivoDeCapital` | `ativos_de_capital` | `id` ULID | Sqlite + InMemory + contract tests 2× (padrão `AssinaturaRepositoryContractTests`) |
| `AporteDeCapital` | `aportes_de_capital` | `id` ULID | idem |
| `ConfiguracaoFinanceiraTenant` (estendida) | `configuracoes_financeiras` | `tenant_id` | design-pai, colunas novas |

---

## 4. Depreciação — mecânica e matemática

### 4.1 Caixa ≠ competência (o mesmo desenho do design-pai §4.1, agora para bem tangível)

```
CAIXA       ContaAPagar "Bancada ESD" R$12.000 em 3×R$4.000 (categoria ativo-de-capital)
            → BaixarParcela → 3 MovimentoFinanceiro de Saída          ← ROI (Capex_m) lê DAQUI
            (bem pago fora do sistema: sem conta — o custo entra no ROI pela DataAquisicao)

COMPETÊNCIA AtivoDeCapital (12.000 − residual 0) / 60 meses
            → 60 reconhecimentos mensais de R$200,00                  ← DRE lê DAQUI
```

Sem o ativo, o DRE mostraria R$12.000 de despesa no mês da compra — a MESMA distorção do CMV
pré-correção, resolvida pelo MESMO princípio ("comprar capacidade é troca de ativo; a despesa
nasce com o uso do tempo").

### 4.2 A fórmula (lar único: `Application/Quant/CronogramaLinear.cs`)

O helper do design-pai §4.2, intacto — este design só define a BASE:

```
B  = CustoAquisicao − ValorResidual                     (base depreciável, em centavos)
cronograma = CronogramaLinear.Gerar(B.Centavos, VidaUtilMeses, InicioDepreciacao)
           = RateioProporcional.Alocar(B, pesos = 1×n)  (Hamilton — Σ = B EXATO; com restos
             iguais o centavo extra vai às PRIMEIRAS competências, desempate ThenBy(índice))

D_m               = valor da competência m no cronograma
DepAcum(T)        = Σ_{m ≤ T} D_m
ValorContabil(T)  = CustoAquisicao − DepAcum(T)         (≥ ValorResidual sempre; = Residual no fim,
                                                         por construção — invariante de teste)
```

O cronograma **não é persistido** (convenção do repo: derivado recomputa da fonte); DRE, painel e
cron recomputam `Gerar(...)` do agregado — determinismo do Hamilton garante que todos veem os
mesmos centavos, e cron atrasado nunca = número errado (o cron só materializa o rastro contábil e
o evento, exatamente como no design-pai §4.5).

### 4.3 Números de sanidade (os bens da abertura)

```
Reforma      R$25.000, 60m, residual 0:  2.500.000÷60 = 41.666,67 → 40× 41.667 + 20× 41.666  (Σ ✓)
Equipamento  R$12.000, 60m:              1.200.000÷60 = 20.000 exato        → R$200,00/mês
Placa        R$ 4.800, 48m:                480.000÷48 = 10.000 exato        → R$100,00/mês
Computador   R$ 6.000, 60m:                600.000÷60 = 10.000 exato        → R$100,00/mês
Móveis       R$ 8.000, 120m:               800.000÷120 = 6.666,67 → 80× 6.667 + 40× 6.666    (Σ ✓)

Capex total = R$55.800     DepreciacaoEAmortizacao do 1º mês = 41.667+20.000+10.000+10.000+6.667
                          = 88.334 centavos = R$883,34/mês
```

O DRE do mês 1 da assistência já mostra R$883,34 de custo econômico da estrutura — mesmo com o
caixa do capex parcelado ou pago do bolso. (E o DigiSat do design-pai §4.3 continua: residual 0 ⇒
números idênticos, 28×191,53 + 8×191,52.)

### 4.4 Partida dobrada — conta `1.3 Ativos de Capital`

Generalização 1:1 do §4.4 do design-pai (só nomes):

`PlanoDeContasPadrao` ganha `AtivosDeCapital = ContaContabil.Criar("1.3", "Ativos de Capital",
TipoContaContabil.Ativo)`; `CategoriaFinanceiraPadrao` ganha `AtivoDeCapital = "ativo-de-capital"`.

| Fato | Débito | Crédito |
|---|---|---|
| `ContaAPagar` categoria `ativo-de-capital` criada | **1.3 Ativos de Capital** (não 4.1!) | 2.1 Contas a Pagar |
| Baixa de cada parcela (mecanismo existente, intocado) | 2.1 Contas a Pagar | 1.1 Caixa e Bancos |
| Reconhecimento mensal (cron) | 4.1 Custo/Despesa | **1.3 Ativos de Capital** |
| Baixa antecipada / write-off (§4.5) | 4.1 (valor contábil restante) | 1.3 |

O desvio por categoria entra no ÚNICO lar do mapeamento (`LancamentoContabilFactory.DeContaAPagar`,
hoje incondicional D-4.1/C-2.1 — `LancamentoContabilFactory.cs:34-43`): um `switch` pela
categoria, coerente com "é código, não input". Ao fim da vida, o saldo de 1.3 relativo ao bem =
`ValorResidual` (zera na baixa/venda) — invariante de teste.

### 4.5 Baixa antecipada (impairment) — herdada do design-pai §4.6, com residual

`Baixar(motivo, competencia)`: `EmUso|Encerrado → Baixado`; reconhece **de uma vez** o
`ValorContabil` na competência da baixa (`SourceRef("amortizacao-baixa", ativoId)`) — note que é o
valor CONTÁBIL (inclui o residual, que nunca entraria no cronograma), não só o restante do
cronograma; para residual 0 as duas definições coincidem com o design-pai. D-4.1 / C-1.3.

### 4.6 Venda do bem (fatia I4 — não bloqueia o painel)

`Baixar(motivo, competencia, valorVenda)`: `→ Vendido`, guarda `ValorVenda`;
`ResultadoAlienacao = ValorVenda − ValorContabil(T)` (ganho ou perda). Default (decisão DI6):
linha **informativa fora do `ResultadoOperacional`** do DRE — vender a bancada usada não é
performance operacional. O dinheiro da venda, se entrar no sistema, entra como `ContaAReceber`
categoria nova `alienacao-de-ativo`, **excluída da `ReceitaBruta`** do DRE (mesmo padrão da
exclusão de `cmv-fornecedor` na despesa) e naturalmente fora do Radar do Simples (venda de
imobilizado não compõe RBT12; e `fato_receita_diaria` só é alimentada por eventos de
venda/OS/assinatura — lançamento avulso nunca entra lá). A baixa dessa conta gera
`MovimentoFinanceiro` de Entrada → o ROI captura o proceeds automaticamente em `F_m` (§7.2).
Contabilmente: C-1.3 pelo valor contábil, D-4.1 pela perda (ou crédito residual em 3.1 pelo
ganho) — detalhar no PR da fatia I4.

### 4.7 Efeito no DRE (a única mudança no demonstrativo — amenda §4.7 do design-pai)

1. `CategoriaFinanceiraPadrao.AtivoDeCapital` entra na lista de exclusão de `DespesaOperacional`
   do `DreGerencialService` (junto de `cmv-fornecedor` e `comissoes` — `DreGerencialService.cs:73-76`).
2. `DreResultado` ganha campo aditivo **`DepreciacaoEAmortizacao`** (Money) — era `Amortizacao`
   no design-pai; UMA linha para as duas naturezas, computada de `CronogramaLinear` sobre os
   ativos com competência na janela (função pura; não depende do cron).
3. `ResultadoOperacional = ReceitaBruta − CustoDireto − DespesaOperacional − DepreciacaoEAmortizacao`.
   (De carona, `ResultadoOperacional + DepreciacaoEAmortizacao` é o EBITDA gerencial — exposto
   como campo derivado no endpoint do DRE se o dono quiser; não é requisito.)
4. `PorCorrente` **não muda** — D&A é custo de capacidade, não custo variável (mesma justificativa
   do design-pai: não contaminar o insumo do breakeven).

Invariante de caracterização: tenant sem nenhum `AtivoDeCapital` e sem as categorias novas →
`DreResultado` byte-idêntico ao atual.

Radar do Simples: **intocado** — depreciação é lente gerencial, DAS incide sobre receita.

---

## 5. O gerador (cron) — herdado, um rename

Trio do design-pai §4.5, renomeado: `AtivoDeCapital.ProximaCompetenciaDevida` +
`ReconhecerCompetencia(...)` (Domain) → **`ReconhecerCronogramaDeAtivosUseCase`** (Application,
catch-up em loop, dupla idempotência: cursor no domínio + `BuscarPorOrigemAsync`) → rodando como
3º use case no ciclo do cron EXISTENTE (`FaturarAssinaturasBackgroundService.ExecutarUmCicloFailOpenAsync`,
que já encadeia assinaturas + recorrências — mesmo escopo por rodada, mesmo fail-open; recomendação
do design-pai mantida: **não** criar um 4º `BackgroundService`). Publica `CustoAmortizadoReconhecido`
pós-commit (chave `amortizacao:{ativoId}:{yyyyMM}` — inalterada). Ao reconhecer a última
competência, FSM `EmUso → Encerrado`.

---

## 6. "Abertura como Projeto especial" vs. painel dedicado — avaliação

A pergunta legítima: o painel de projeto (design-pai §9) já tem payback/ROI — por que não modelar
"a abertura do negócio" como um `Projeto` e reusar a tela?

| Critério | Projeto "Abertura" | Painel dedicado (**recomendado**) |
|---|---|---|
| Fonte do fluxo de retorno | Só o que estiver TAGUEADO com `projetoId` — exigiria taguear TODA receita e TODO custo do negócio, para sempre | Fluxo TOTAL de `MovimentoFinanceiro` — zero tagging, zero gesto extra |
| Opt-in não-intrusivo | Violado: obrigaria o toggle de projeto + disciplina de tagging universal | Preservado: liga o toggle e o painel funciona com o que já existe |
| Aportes de caixa | Não existem no modelo de projeto | Nativos (`AporteDeCapital`) |
| Semântica | Projeto = LENTE sobre uma linha de produto; "o negócio inteiro" como projeto quebra o invariante "Σ receita dos painéis ≤ receita total" (design-pai §13.3) — a abertura conteria os outros projetos | Negócio ⊇ projetos: o ROI do negócio soma TODOS os ativos (com e sem `projetoId`); cada painel de projeto soma só os seus |
| Matemática | A mesma | A mesma — e é AQUI que o reuso acontece de verdade |

**Decisão de design: painel dedicado, operadores compartilhados.** O reuso não é da tela, é da
matemática: a simulação de payback do design-pai §9.5 (realizado por caixa + projeção
determinística mês a mês) é EXTRAÍDA para `Application/Quant/MatematicaDePayback.cs`, e o
`PainelDoProjetoService` (fatia P3 do design-pai) já nasce chamando esse helper — um lar único
para "primeiro mês em que um acumulado cruza zero", usado pelas duas lentes. TIR e desconto
(novidades deste design) nascem em `Quant` ao lado, disponíveis para o painel de projeto no
futuro sem retrabalho (amenda o design-pai §9.5: a fórmula muda de lar, não de forma).

---

## 7. Painel de ROI do negócio — read-model e a matemática de cada métrica

### 7.0 Fontes (nenhuma métrica inventa dado)

| Insumo | Fonte | Observação |
|---|---|---|
| Fluxo de caixa operacional | `IMovimentoFinanceiroRepository.ListarPorPeriodoAsync` (bilateral por construção — mesmo insumo de `FluxoDeCaixaService.cs:33-47`) | **Não depende** de `fato_caixa_diario` (unilateral, P1-3) nem da Fatia 6 da auditoria. O burn só aparece se as despesas passarem por contas+baixa — disciplina operacional, não gap de modelo |
| Capex | `AtivoDeCapital` (custo, `DataAquisicao`, `ContaAPagarId`) + parcelas das contas categoria `ativo-de-capital` | §7.2 — anti-dupla-contagem |
| Aportes | `AporteDeCapital` | — |
| Depreciação (lente competência) | `CronogramaLinear.Gerar` (função pura) | — |
| Lucro por competência | `DreGerencialService.CalcularAsync` mês a mês (com a linha D&A) | mesma fonte do demonstrativo — nunca uma segunda conta de lucro |

### 7.1 Shape do `GET /financeiro/roi-negocio`

```jsonc
{
  "marcoInicial": "2026-07",                       // m0 — §7.2
  "taxaDescontoAnualBps": 1200,                    // null = payback descontado omitido
  "investimento": {
    "capexCentavos": 5580000,                      // Σ CustoAquisicao dos bens (aquisição ≤ hoje)
    "aportesCentavos": 2000000,
    "totalCentavos": 7580000,
    "giroConsumidoObservadoCentavos": 1200000,     // diagnóstico: max drawdown de Σ F_m (§7.3)
    "bens": 5,
    "porCategoria": [ { "categoria": "reforma", "custoCentavos": 2500000, "valorContabilCentavos": 2458333 } ]
  },
  "recuperacao": {
    "fluxoOperacionalAcumuladoCentavos": 6000000,  // F(T)
    "recuperadoCentavos": 8000000,                 // Aportes + F(T)
    "faltamCentavos": 0,                           // max(0, Investido − Recuperado)
    "percentRecuperado": 105.5
  },
  "payback": {
    "simplesRealizadoEm": "2027-11",               // null enquanto não cruzar
    "descontadoRealizadoEm": null,
    "projetadoMeses": 16,                          // null se margem ≤ 0 e não cruza em 120m
    "descontadoProjetadoMeses": 18,
    "metodo": "simulacao-fluxo-conhecido"
  },
  "tir": { "mensalPercent": 3.1, "anualizadaPercent": 44.2, "motivoIndefinida": null },
                                                    // ou { null, null, "sem-mudanca-de-sinal" }
  "roi": {
    "caixaPercent": 5.5,                           // 100·Acum/Investido
    "competenciaPercent": -12.3,                   // 100·LucroOperAcum/Investido
    "mesesAteRoiCompleto": 0                       // = payback.projetadoMeses (0 = já atingido)
  },
  "serie": [                                        // rastreio mês a mês — o quant audita AQUI
    { "competencia": "2026-07", "fluxoOperacionalCentavos": -300000, "capexCentavos": 5580000,
      "aporteCentavos": 2000000, "liquidoCentavos": -5880000, "acumuladoCentavos": -5880000,
      "acumuladoDescontadoCentavos": -5880000 }
  ]
}
```

### 7.2 As séries mensais (definições exatas)

**Marco `m0`** = `ConfiguracaoFinanceiraTenant.InicioOperacao` se configurado; senão o mês do
primeiro fato de investimento: `min(min DataAquisicao dos bens, min Data dos aportes)`.
Movimentos anteriores a `m0` ficam fora da série (relevante só para tenant legado que ligar o ROI
tarde — para o dia-zero é vazio; decisão DI4).

**Fluxo operacional do mês** (a série do negócio, sem investimento e sem funding):

```
F_m = Σ_{mov ∈ MovimentoFinanceiro, mês(DataMovimento)=m} (+Valor se Entrada, −Valor se Saída)
      EXCLUINDO movimentos cuja ContaOrigemId pertença a conta com categoria ativo-de-capital
```

- Estornos entram com o tipo invertido automaticamente (imutabilidade + `GerarEstorno`).
- A exclusão é o **anti-dupla-contagem**: o caixa do capex já está em `Capex_m`; contá-lo também
  em `F_m` cobraria o investimento duas vezes do payback.
- Aportes não estão em `F_m` por construção (não são `MovimentoFinanceiro` — §3.3).

**Capex do mês** (caixa quando rastreado, competência da aquisição quando não — decisão DI7):

```
Capex_m = Σ parcelas PAGAS no mês m de contas categoria ativo-de-capital        (bem COM conta vinculada)
        + Σ CustoAquisicao dos bens SEM ContaAPagarId com mês(DataAquisicao)=m  (bem pago fora do sistema)
```

Invariante (testável): cada bem entra por EXATAMENTE um dos dois trilhos, decidido por
`ContaAPagarId != null` — nunca pelos dois, nunca por nenhum.

**Fluxo líquido e acumulado:**

```
N_m       = F_m − Capex_m
Acum(T)   = Σ_{m=m0..T} N_m
Investido(T) = Capex(≤T) + Aportes(≤T)          // Capex(≤T) = Σ CustoAquisicao dos bens adquiridos até T
Recuperado(T) = Aportes(≤T) + F(m0..T)
```

(Para `Investido`/`Recuperado` — os números-manchete — o capex entra pelo CUSTO na aquisição,
independente do parcelamento: "quanto foi comprometido". Para `N_m`/TIR/desconto — as séries
temporais — entra pelo trilho de DI7 acima: "quando o dinheiro saiu".)

### 7.3 Payback simples e a leitura de capital de giro

```
PaybackSimples = menor T tal que Acum(T) ≥ 0, tendo existido t < T com Acum(t) < 0
                 (null enquanto não cruzar — o campo simplesRealizadoEm)

GiroConsumidoObservado = max(0, − min_{T} FluxoOperacionalAcum(T))      // pico de burn operacional
                          onde FluxoOperacionalAcum(T) = Σ_{m ≤ T} F_m
```

O capital de giro queimado **não entra como parcela separada da fórmula de payback** — ele já
está dentro de `Acum(T)` via os meses negativos de `F_m` (é exatamente por isso que o payback
"não mente pra menos": cada mês de burn empurra o cruzamento pra frente). O
`GiroConsumidoObservado` é o diagnóstico exibido ao lado dos aportes declarados: se
`aportes < giroConsumido`, o dono aportou menos do que o negócio queimou (dinheiro entrou por
fora sem registro — o painel avisa); se `>`, há folga de caixa.

### 7.4 A invariância que fecha a conta (por que não há dupla contagem)

```
Recuperado(T) − Investido(T) = [Aportes + F(T)] − [Capex + Aportes] = F(T) − Capex(≤T) ≈ Acum(T)
```

Logo `Recuperado ≥ Investido ⇔ Acum(T) ≥ 0`: a data do payback **independe de quanto se declara
de aporte** (os aportes se cancelam nos dois lados), e "quanto falta"
(`Investido − Recuperado = Capex − F`) também. O aporte declarado afeta APENAS o denominador do
ROI% e a leitura de funding (§7.3) — propriedade que vira property test (§12.3): registrar um
aporte extra de qualquer valor não move `simplesRealizadoEm` nem `mesesAteRoiCompleto` em um
único mês. É a resposta formal ao medo do dono: o burn conta via `F_m`, o aporte NÃO é contado de
novo por cima.

### 7.5 Payback descontado

```
i_a = TaxaDescontoAnualBps / 10.000                 (ex.: 1200 bps → 12% a.a.)
d   = (1 + i_a)^(1/12) − 1                          (taxa mensal equivalente, composta)
AcumDesc(T) = Σ_{m=m0..T} N_m · (1+d)^{−(m−m0)}
PaybackDescontado = menor T com AcumDesc(T) ≥ 0     (mesma regra de cruzamento)
```

Com `TaxaDescontoAnualBps = null` o bloco é omitido (`descontadoRealizadoEm/descontadoProjetadoMeses
= null`) — nunca um default silencioso inventado pelo sistema. Para o padrão canônico
(investimento primeiro, retornos depois) e `d > 0`, `PaybackDescontado ≥ PaybackSimples` sempre —
invariante de teste com fixtures canônicas.

### 7.6 TIR

```
VPL(r) = Σ_{m=m0..T} N_m · (1+r)^{−(m−m0)}          (r = taxa MENSAL)
TIR mensal r* : VPL(r*) = 0
TIR anualizada = (1+r*)^12 − 1
```

Resolução: **bisseção** em `r ∈ (−0,99, 10]` (−99% a +1000% a.m.), até `|VPL| < 0,5 centavo` ou
200 iterações — determinística, sem dependência nova, auditável contra `numpy.irr`/Excel.
Pré-condições de existência (senão `null` + `motivoIndefinida`, o padrão de honestidade do LTV do
design-pai §9.4):

- `∃ N_m < 0` e `∃ N_m > 0` (`"sem-mudanca-de-sinal"` — negócio que nunca investiu ou nunca
  retornou não tem TIR);
- `VPL(−0,99) · VPL(10) < 0` (`"sem-raiz-no-intervalo"`).

Caveat documentado no código: com UMA troca de sinal na série (o padrão canônico — negativos do
investimento, depois positivos), a raiz em `r > −1` é única (Descartes); séries patológicas com
múltiplas trocas podem ter múltiplas raízes — a bisseção devolve uma e o campo `metodo` declara
isso. Para o negócio-alvo (capex no início) o caso patológico não ocorre.

### 7.7 ROI% e "meses até ROI completo"

```
roiCaixaPercent        = 100 · Acum(T) / Investido(T)                       (T = hoje)
roiCompetenciaPercent  = 100 · LucroOperAcum(T) / Investido(T)
    onde LucroOperAcum(T) = Σ_{m=m0..T} DreResultado(m).ResultadoOperacional   (JÁ líquido de D&A)
```

**Payback não tem lente de competência** — deliberado (coerente com D5 do design-pai): o
`ResultadoOperacional` já devolve o capital via D&A dentro do lucro; comparar lucro-após-D&A com
o capex de novo cobraria o capital duas vezes. Payback é pergunta de caixa; a lente competência
entrega ROI% (retorno SOBRE o capital, líquido do consumo dele — a definição ROIC-style que um
quant espera) e a margem mensal.

**Projeção determinística** (clone da simulação §9.5 do design-pai — mesmo helper, §6):

```
margemCaixaMensal = média(F_m das 3 últimas competências FECHADAS)
fluxoFuturo(k)    = margemCaixaMensal − CapexComprometido(k)
    CapexComprometido(k) = Σ parcelas EM ABERTO de contas ativo-de-capital com vencimento no mês k
AcumProj(0) = Acum(hoje);   AcumProj(k) = AcumProj(k−1) + fluxoFuturo(k)
mesesAteRoiCompleto = menor k com AcumProj(k) ≥ 0     (horizonte 120 meses; null se não cruza;
                                                       0 se Acum(hoje) já ≥ 0)
descontadoProjetadoMeses = mesma simulação com cada fluxoFuturo(k)·(1+d)^{−(T−m0+k)}
```

Nenhum parâmetro exógeno além da taxa de desconto: as parcelas restantes do capex entram sozinhas
(estão em aberto nas contas tagueadas), a margem vem da trajetória real.

### 7.8 Sanidade nominal (o dia-zero da assistência — teste ponta-a-ponta)

Capex R$55.800 à vista no mês 0 (bens sem conta vinculada); aporte R$20.000; burn de R$3.000/mês
nos meses 1–4; depois F = +R$6.000/mês:

```
N_0 = −55.800;  N_1..4 = −3.000;  N_5.. = +6.000
Acum(4)  = −67.800
Acum(15) = −67.800 + 11×6.000 = −1.800 < 0
Acum(16) = −67.800 + 12×6.000 = +4.200 ≥ 0        → PaybackSimples = mês 16
Checagem pela outra face (§7.4): Recuperado(16) = 20.000 + F(16) = 20.000 + 60.000 = 80.000
                                 ≥ Investido = 55.800 + 20.000 = 75.800 ✓ (mesmo mês, como provado)
GiroConsumidoObservado = 12.000 (≤ aporte de 20.000 — funding ok)
ROI aos 24 meses = 100 · (−67.800 + 20×6.000) / 75.800 = 100 · 52.200/75.800 ≈ 68,9%
TIR: VPL(0) = +52.200 > 0 no horizonte 24m ⇒ r* > 0; o teste NÃO pina um valor mágico — verifica
     a propriedade |VPL(r*)| < 0,5 centavo e r* > 0 (o número exato sai da bisseção e é conferível
     em qualquer ferramenta externa)
DRE mês 1: DepreciacaoEAmortizacao = R$883,34 (§4.3), reduzindo o ResultadoOperacional — a lente
     competência mostra o custo da estrutura DESDE o primeiro mês, enquanto a lente caixa mostra o
     buraco de −R$58.800 sendo escavado e preenchido. As duas juntas são o pedido do dono.
```

---

## 8. Endpoints (rota + shape camelCase)

Todos em `FinanceiroEndpointsModule.MapearEndpoints`, tenant só da sessão, permissões
`RequerPermissao(Modulo.Financeiro, Acao.Ver|Editar)`, DTOs de fio, gating §2.2 via
`FinanceiroOptInGuard`.

### 8.1 Imobilizado

```
GET  /financeiro/imobilizado?status=&categoria=              (Acao.Ver)
→ [ { "id": "01J…", "nome": "Bancada ESD", "categoria": "equipamento", "natureza": "tangivel",
      "custoAquisicaoCentavos": 1200000, "valorResidualCentavos": 0,
      "dataAquisicao": "2026-07-10", "inicioDepreciacao": "2026-07", "vidaUtilMeses": 60,
      "status": "EmUso", "depreciacaoMensalAtualCentavos": 20000,
      "depreciacaoAcumuladaCentavos": 20000, "valorContabilCentavos": 1180000,
      "competenciasRestantes": 59, "projetoId": null, "contaAPagarId": "01J…" } ]

POST /financeiro/imobilizado                                  (Acao.Editar)
{ "nome": "Bancada ESD", "categoria": "equipamento", "natureza": "tangivel",
  "custoAquisicaoCentavos": 1200000, "valorResidualCentavos": 0, "dataAquisicao": "2026-07-10",
  "vidaUtilMeses": 60, "inicioDepreciacao": "2026-07", "projetoId": null,
  "contaAPagar": { "parcelas": [ { "vencimento": "2026-08-05", "valorCentavos": 400000 }, …×3 ] } }
  // OU "contaAPagarId": "…"; OU nenhum (pago fora do sistema — o custo entra no ROI pela dataAquisicao)

POST /financeiro/imobilizado/{id}/baixar                      (Acao.Editar)
{ "motivo": "Sucateado", "competencia": "2028-03", "valorVendaCentavos": null }   // != null ⇒ Vendido (I4)
```

(`POST /financeiro/ativos` do design-pai §8.3 permanece como alias da MESMA criação sob o toggle
de projeto — §2.2; um handler só, dois gates.)

### 8.2 Aportes

```
POST   /financeiro/aportes    { "valorCentavos": 2000000, "data": "2026-07-01", "descricao": "Capital de giro inicial" }
GET    /financeiro/aportes    → [ { "id": "…", "valorCentavos": …, "data": "…", "descricao": "…" } ]
DELETE /financeiro/aportes/{id}
```

### 8.3 ROI e configuração

```
GET /financeiro/roi-negocio                → §7.1 (404 com toggle desligado)
GET /financeiro/configuracoes              → shape do design-pai §8.1 + { "imobilizadoRoiAtivo": false,
                                             "taxaDescontoAnualBps": null, "inicioOperacao": null }
PUT /financeiro/configuracoes              (Acao.Editar — liga/desliga e define a taxa)
```

---

## 9. Migrações — retrocompatibilidade nos dois cenários

A numeração real usa o próximo livre na hora (hoje o topo implementado é V20; V21–V32 são
indicativas do design-pai). Duas rotas, conforme a ordem de implementação:

**Cenário A (recomendado — fatia P3 do design-pai ainda não rodou):** a migração indicativa V28
do design-pai **já nasce generalizada** — nenhuma migração extra para o agregado:

```sql
CREATE TABLE ativos_de_capital (
    id TEXT PRIMARY KEY, business_id TEXT NOT NULL, projeto_id TEXT NULL,
    nome TEXT NOT NULL, natureza INTEGER NOT NULL, categoria INTEGER NOT NULL,
    custo_aquisicao_centavos INTEGER NOT NULL, moeda TEXT NOT NULL,
    valor_residual_centavos INTEGER NOT NULL DEFAULT 0,
    data_aquisicao TEXT NOT NULL, inicio_depreciacao TEXT NOT NULL,
    vida_util_meses INTEGER NOT NULL, metodo INTEGER NOT NULL,
    quantidade_unidades INTEGER NOT NULL DEFAULT 1, conta_a_pagar_id TEXT NULL,
    status INTEGER NOT NULL, ultima_competencia_reconhecida TEXT NULL,
    encerrado_em TEXT NULL, baixado_em TEXT NULL, motivo_baixa TEXT NULL,
    valor_venda_centavos INTEGER NULL
);
CREATE INDEX ix_ativos_de_capital_business ON ativos_de_capital (business_id, status);
```

E a V22 indicativa (config) já nasce com `imobilizado_roi INTEGER NOT NULL DEFAULT 0`,
`taxa_desconto_anual_bps INTEGER NULL`, `inicio_operacao TEXT NULL`.

**Cenário B (fallback — P3 já rodou com o schema do design-pai):** ALTERs aditivos, espelho V16:

```sql
ALTER TABLE ativos_amortizaveis ADD COLUMN natureza INTEGER NOT NULL DEFAULT 0;   -- 0 = Intangivel (comportamento antigo)
ALTER TABLE ativos_amortizaveis ADD COLUMN categoria INTEGER NOT NULL DEFAULT 99; -- Outro
ALTER TABLE ativos_amortizaveis ADD COLUMN valor_residual_centavos INTEGER NOT NULL DEFAULT 0;
ALTER TABLE ativos_amortizaveis ADD COLUMN data_aquisicao TEXT NULL;              -- backfill: substr(inicio_amortizacao,1,10)
ALTER TABLE ativos_amortizaveis ADD COLUMN encerrado_em TEXT NULL;
ALTER TABLE ativos_amortizaveis ADD COLUMN valor_venda_centavos INTEGER NULL;
-- config: ALTER TABLE configuracoes_financeiras ADD COLUMN imobilizado_roi / taxa_desconto_anual_bps / inicio_operacao
```

Residual default 0 e natureza 0 fazem TODO dado existente se comportar byte-idêntico (cronograma
sobre custo−0, mesma chave de idempotência) — retrocompat por construção.

**Nova em qualquer cenário:**

```sql
CREATE TABLE aportes_de_capital (
    id TEXT PRIMARY KEY, business_id TEXT NOT NULL,
    valor_centavos INTEGER NOT NULL, moeda TEXT NOT NULL,
    data TEXT NOT NULL, descricao TEXT NOT NULL, criado_em TEXT NOT NULL
);
CREATE INDEX ix_aportes_business ON aportes_de_capital (business_id, data);
```

Todos os repositórios (Sqlite **e** InMemory) no mesmo PR da migração, contract tests 2× — o
padrão da casa (`tests/.../Contracts/{X}RepositoryContractTests` + `InMemory{X}...`).

---

## 10. Organização — o lar único de cada fórmula (requisito duro)

Regra de revisão: **um cálculo, um arquivo**. Se um service precisar de uma fórmula daqui, ele
CHAMA o lar — nunca reimplementa. PR que duplicar fórmula é rejeitado por definição.

| Cálculo | Lar ÚNICO | Quem chama |
|---|---|---|
| Espalhar total exato em n competências (Hamilton) | `Application/Quant/RateioProporcional.cs` (existe) | `CronogramaLinear` |
| Cronograma linear (depreciação = amortização = receita diferida futura) | `Application/Quant/CronogramaLinear.cs` (design-pai §4.2) | DRE, painel ROI, painel de projeto, cron, Fatia 7 da auditoria |
| Base depreciável, valor contábil, cursor, FSM do ativo | `Domain/Ativos/AtivoDeCapital.cs` | use cases e read-models (nunca recalculam vida útil por fora) |
| Payback simples/descontado + projeção determinística de cruzamento | `Application/Quant/MatematicaDePayback.cs` (NOVO — extrai §9.5 do design-pai) | `RoiDoNegocioService` E `PainelDoProjetoService` |
| TIR (bisseção sobre VPL) | `Application/Quant/TaxaInternaDeRetorno.cs` (NOVO) | `RoiDoNegocioService` (painel de projeto no futuro) |
| Séries `F_m`/`Capex_m`/`N_m` e agregados Investido/Recuperado | `Application/ReadModels/RoiDoNegocioService.cs` — SÓ montagem de fontes + chamadas ao Quant; zero fórmula própria além de Σ | endpoint |
| Linha D&A do DRE e exclusões de categoria | `Application/ReadModels/DreGerencialService.cs` | endpoint do DRE, lente competência do ROI |
| Mapeamento contábil (desvio 1.3) | `Domain/Contabil/LancamentoContabilFactory.cs` | use cases |
| Gating opt-in | `Application/.../FinanceiroOptInGuard.cs` | todos os endpoints novos |

O roteiro de auditoria do matemático cabe numa linha: *depreciação? `CronogramaLinear`. Payback?
`MatematicaDePayback`. TIR? `TaxaInternaDeRetorno`. De onde vem cada número do painel? A `serie`
mensal do response (§7.1) reproduz `N_m` termo a termo contra movimentos/bens/aportes.*

---

## 11. Impacto no que existe

| Peça | Mudança | Quem não usa |
|---|---|---|
| `DreGerencialService` | Linha `DepreciacaoEAmortizacao` + exclusão de `ativo-de-capital` (e, na I4, `alienacao-de-ativo` da receita) | Zero ativos → byte-idêntico (teste de caracterização) |
| `PlanoDeContasPadrao` / `LancamentoContabilFactory` | Conta 1.3 + desvio por categoria (§4.4) | Nunca aciona o desvio |
| `CategoriaFinanceiraPadrao` | `AtivoDeCapital = "ativo-de-capital"` (+ `AlienacaoDeAtivo` na I4) | Slug nunca usado |
| Cron `FaturarAssinaturasBackgroundService` | 3º use case no ciclo (`ReconhecerCronogramaDeAtivosUseCase` — §5) | Loop vazio |
| `FinanceiroConsultorFactProvider` | Fatos novos, fail-quiet com toggle off: "faltam R$X (~N meses) para o ROI completo", "TIR atual do negócio: Y% a.a.", "depreciação mensal da estrutura: R$Z" | Nenhum fato |
| Design-pai (pré-implementação) | Amendas: renames §3.1; `Amortizacao`→`DepreciacaoEAmortizacao` no DRE; guard unificado §2.2; §9.5 movido para `MatematicaDePayback` | — |
| Radar do Simples / breakeven / bandas / `fato_caixa_diario` | **Intocados** — o ROI lê `MovimentoFinanceiro` direto | — |

---

## 12. Plano de implementação — fatias dotnet-gated

Cada fatia termina com `dotnet build && dotnet test` verdes e é útil sozinha. **Encaixe**: as
fatias I dependem SÓ de dois pedaços do design-pai — `ConfiguracaoFinanceiraTenant` (P1) e o
ativo + `CronogramaLinear` + conta 1.3 + cron (P3). P2/P4/P5/P6 (painel de projeto, tempo, fact
tables) são ortogonais e podem vir antes ou depois.

> **Caminho rápido do dia-zero** (se o dono quiser o ROI antes da análise por projeto): extrair de
> P1 apenas o "P1-lite" — `ConfiguracaoFinanceiraTenant` + migração de config + `GET/PUT
> /financeiro/configuracoes` + `FinanceiroOptInGuard` (sem tagging de `projetoId`, sem CRUD de
> projeto) — e implementar P3 já na forma generalizada (Cenário A de §9). Sequência mínima:
> **P1-lite → P3(geral) → I2 → I3**, com I1 diluída dentro de P3.

**I1 — Ativo de capital generalizado + DRE + endpoints de imobilizado.**
Se P3 ainda não rodou: P3 nasce com o agregado de §3.1 (Cenário A); senão: ALTERs do Cenário B.
Domain: `Natureza`/`Categoria`/`ValorResidual`/`DataAquisicao`, FSM `{EmUso, Encerrado, Baixado,
Vendido}` (Vendido só declarado; transição na I4). Contábil: conta 1.3 + categoria
`ativo-de-capital` + desvio na factory. DRE: linha `DepreciacaoEAmortizacao` + exclusão.
Endpoints §8.1 (sem `valorVendaCentavos`). Repos Sqlite+InMemory + contract tests 2×.
Testes-chave: Σ cronograma = custo − residual (property test com custo/vida/residual sortidos);
`ValorContabil(fim) = Residual`; números nominais de §4.3; DigiSat §9.5 do design-pai intacto;
DRE byte-idêntico sem ativos; replay do cron não duplica.

**I2 — Aportes + toggle ROI.**
`AporteDeCapital` + migração `aportes_de_capital` + repos + contract tests. Campos
`imobilizadoRoiAtivo`/`taxaDescontoAnualBps`/`inicioOperacao` na config (+ colunas). Gating 422
nos endpoints de I1 e nos de aporte (§8.2). Testes: toggle off → 422 em escrita e `[]`/404 em
leitura; aporte inválido (valor ≤ 0) rejeitado.

**I3 — Painel de ROI do negócio.**
Quant: `MatematicaDePayback` (extraindo a simulação §9.5 do design-pai — se P2/P3 do design-pai
já existirem, o `PainelDoProjetoService` passa a chamá-la NESTE PR, deletando a versão inline) +
`TaxaInternaDeRetorno`. Read-model: `RoiDoNegocioService` (séries §7.2, métricas §7.3–§7.7) +
`GET /financeiro/roi-negocio`. Port: `IContaAPagarRepository` ganha
`ListarPorCategoriaAsync(businessId, categoriaId)` (Sqlite+InMemory+contract). Consultor: fatos
novos. Testes: invariância de aportes (§7.4) como property test; `|VPL(TIR)| < 0,5` centavo; TIR
null sem mudança de sinal; descontado ≥ simples em fixtures canônicas; anti-dupla-contagem de
capex (bem com conta × sem conta); nominal §7.8 ponta-a-ponta; `mesesAteRoiCompleto = 0` quando
`Acum ≥ 0`.

**I4 — Alienação e refinamentos.**
Transição `→ Vendido` com `valorVenda`; categoria `alienacao-de-ativo` + exclusão da
`ReceitaBruta`; `ResultadoAlienacao` informativo no DRE; lançamentos contábeis da venda; painel
por categoria de bem enriquecido. Testes: razão 1.3 zera na venda/baixa; venda não infla
`ReceitaBruta` nem o Radar; proceeds aparecem em `F_m`.

---

## 13. Decisões que precisam do dono (com recomendação default)

Nenhuma reintroduz sócios/split de capital.

| # | Decisão | Opções | **Recomendação (default do design)** |
|---|---|---|---|
| **DI1** | Forma da generalização | (a) um agregado `AtivoDeCapital` com `Natureza`; (b) `BemImobilizado` separado do `AtivoAmortizavel` | **(a)** — um lar de cronograma, um cron, uma conta 1.3; (b) criaria duas máquinas paralelas com a mesma matemática (proibido pelo requisito de organização) |
| **DI2** | Taxa de desconto do payback descontado | Número fixo do dono; CDI-like; nenhum | **Config explícita em bps, sem default do sistema** (`null` = bloco omitido). Sugestão de UI: o custo de oportunidade pessoal do dono (ex.: o que o dinheiro renderia no CDI). É input do quant, não palpite do ERP |
| **DI3** | Vidas úteis default por categoria | Livres; tabela fixa; sugeridas | **Sugeridas na UI, livres no domínio**: computador 60m, equipamento 60–120m, móveis 120m, comunicação visual 60m, veículo 60m, reforma = prazo do contrato de aluguel (gerencial ≈ tabela RFB, sem pretensão fiscal) |
| **DI4** | Marco `m0` do ROI | Sempre derivado; sempre configurado | **Derivado do 1º fato de investimento, com override `inicioOperacao` na config** (necessário só para tenant legado que ligar o ROI tarde) |
| **DI5** | Aporte deletável? | Delete físico; imutável+estorno | **Delete físico** — registro gerencial sem lançamento contábil (mesmo racional do `ApontamentoDeTempo`). Vira imutável apenas se um dia ganhar partida dobrada (fora do escopo) |
| **DI6** | Resultado da venda de bem no DRE | Dentro do `ResultadoOperacional`; linha informativa fora | **Fora** (linha informativa) — vender a bancada usada não é performance operacional; impairment (baixa sem venda) continua DENTRO, como no design-pai §4.6 (perda real de capacidade) |
| **DI7** | Capex nas séries temporais (TIR/desconto) | Caixa (parcelas pagas); competência da aquisição | **Caixa quando rastreado** (conta vinculada), custo integral na `dataAquisicao` quando não — timing verdadeiro do dinheiro, determinístico e documentado no campo `metodo` |

---

## 14. Invariantes de teste (o contrato deste design)

1. **Conservação de centavos**: `Σ cronograma = CustoAquisicao − ValorResidual` para qualquer
   (custo, vida, residual); `ValorContabil(fim) = ValorResidual` (property tests).
2. **Não-intrusividade**: toggle off ⇒ 422 em escrita, `[]`/404 em leitura, DRE/fluxo/consultor
   byte-idênticos ao baseline sem ativos.
3. **Invariância de aporte** (§7.4): `simplesRealizadoEm`, `descontadoRealizadoEm` e
   `mesesAteRoiCompleto` não mudam ao registrar aporte de qualquer valor; `roiCaixaPercent` muda
   só pelo denominador.
4. **Anti-dupla-contagem de capex**: bem com conta vinculada entra em `Capex_m` pelas parcelas e
   seus movimentos são excluídos de `F_m`; bem sem conta entra pelo custo na aquisição; em nenhum
   cenário um centavo de capex aparece duas vezes em `N_m`.
5. **TIR**: `|VPL(r*)| < 0,5` centavo; `null` + motivo sem mudança de sinal; anualização
   `(1+r*)^12 − 1`.
6. **Descontado ≥ simples** para `d > 0` em fluxo canônico (investimento antes dos retornos).
7. **Idempotência**: replay de `ReconhecerCronogramaDeAtivos` e do cron nunca duplica
   lançamento/evento (chave `amortizacao:{ativoId}:{yyyyMM}`).
8. **Fechamento contábil**: saldo de 1.3 relativo ao bem = `ValorResidual` ao fim da vida; zero
   após baixa/venda.
9. **Regressões cruzadas**: cenário DigiSat (design-pai §9.5) intacto; nominal do dia-zero (§7.8:
   payback mês 16, giro observado 12.000, ROI 68,9% aos 24m, D&A R$883,34) como ponta-a-ponta.
10. **Estorno**: movimento estornado inverte o sinal em `F_m` no mês do estorno (nunca retroage).

---

*Divergência entre este doc e o código após as fatias entrarem = bug de documentação; atualizar no
PR que mudar o comportamento citado (mesma regra do design-pai e da auditoria). Amendas ao
design-pai listadas em §3.1/§11 valem como erratas dele enquanto ambos forem design-only.*
