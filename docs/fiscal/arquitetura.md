# Arquitetura do módulo Fiscal (design-first, SDD)

> Stack-alvo: `src/Modules/Fiscal/` (Domain/Application/Infrastructure), mesmo padrão de
> `docs/arquitetura/COMO-CRIAR-UM-MODULO.md`. Este documento é o CONTRATO antes do código —
> nenhuma linha de `SistemaX.Modules.Fiscal.*` deveria existir sem que a forma abaixo já tenha
> sido revisada. Complementa a decisão registrada em
> `docs/arquitetura/adr/0002-fiscal.md` (leia primeiro o ADR para o "porquê" resumido; aqui é o
> "como", em detalhe).
>
> **Status: implementado.** `src/Modules/Fiscal/{Domain,Application,Infrastructure}` existe e
> compila (`dotnet build` verde), registrado em `SistemaXHost`. Escopo desta primeira
> implementação: o CORE tributário (regime, perfil por NCM, override por produto, resolução de
> CFOP/CST/CSOSN/ICMS/PIS/COFINS, `DocumentoFiscal` com FSM, `SequenciaFiscal`) — a integração
> SEFAZ/certificado (`IGatewayEmissaoSefaz`) continua fora de escopo (§9). O gap de CFOP
> (produção própria vs. revenda) citado abaixo como "lacuna documentada" foi FECHADO — ver nota na
> própria seção §2.3.

**Grounding:** este design foi calibrado contra uma auditoria linha-a-linha do módulo fiscal do
`gestao-raiz` (repo irmão, mesmo domínio de negócio, produção real). Achado central de lá: **o
CSOSN sai hardcoded (`'400'`) do motor que realmente emite a nota**, porque existem **dois
motores de cálculo tributário desconectados** — um "correto" (`tax-calculation.service.ts`,
nunca chamado pela emissão real) e um inline dentro de `fiscal.service.ts` (o que roda de
verdade), e a configuração fiscal que o usuário cadastra por produto **nunca chega** ao payload
de emissão. Cada seção abaixo referencia o defeito equivalente que ela fecha; a tabela completa
de rastreamento está em §11.

**Nota de revisão (2ª passada, pós-primeira redação):** uma revisão crítica desta primeira versão
encontrou 8 lacunas/erros — um deles um erro tributário real (CSOSN indevido para
`SimplesNacionalSublimite`, a mesma classe de defeito que este documento existe para evitar), os
demais estruturais (FSM sem `Fsm<TStatus>`, `IModule` misturando Application/Infrastructure,
`Origem da Mercadoria`/DIFAL/FCP ausentes do modelo, override de ICMS por produto incompleto,
CFOP sem distinguir produção própria de revenda). Todos foram corrigidos diretamente nas seções
abaixo — cada correção traz uma nota inline ("correção aplicada/achada nesta revisão") no ponto
exato onde foi aplicada, para que o histórico do que mudou e por quê fique junto do código, não
num changelog separado.

---

## 1. Princípio central: separar "O QUE É regime" de "COMO o regime tributa"

A causa raiz do defeito do gestao-raiz não é falta de cuidado — é **confundir duas coisas que
mudam em velocidades completamente diferentes**:

| | Muda... | Onde vive neste design |
|---|---|---|
| **Qual regime tributário uma empresa está** | Raramente (migração de faixa de faturamento, opção anual pelo Simples, mudança societária) | `RegimeTributario` — **enum fechado**, fato estável, 1 valor por tenant por período |
| **Como cada regime tributa uma operação concreta** (qual CSOSN/CST, qual alíquota, se tem ST, qual CFOP) | Com frequência real: convênio ICMS novo, ato COTEPE, mudança de alíquota por UF, correção de cadastro, e — a partir de 2026 — a **transição para IBS/CBS da Reforma Tributária** ano a ano até 2033 | `PerfilFiscalNCM` + `RegraFiscalPorOperacao` — **DADOS**, seedáveis e editáveis em runtime, nunca `const`/`switch` no código |

O gestao-raiz tratou a segunda coisa como se fosse a primeira: `defaultIcmsCSOSN`, `ICMS_ALIQUOTA_INTERNA`,
`ALIQUOTAS_IPI_NCM` são todos `const`/tabela hardcoded em arquivo TypeScript — corrigir uma
alíquota de UF ou cadastrar um NCM novo exige **deploy de código**. Neste design, a única coisa
que é código é o **motor de resolução** (a função que sabe COMO combinar regime + operação + NCM
para chegar num CSOSN) — os *valores* em si (quais CSOSN, quais alíquotas, para qual UF) são
linhas de tabela que um contador/admin da loja edita sem tocar em C#. Isso é o que a pergunta do
Igor pede como "arquitetura adaptável" e é também a resposta a "preparado pra Lucro Real": Lucro
Real não exige nenhuma mudança de código — é um novo valor de enum + novas linhas de regra.

---

## 2. Modelo de domínio

### 2.1 `RegimeTributario` — o fato estável

```csharp
namespace SistemaX.Modules.Fiscal.Domain.Regimes;

/// <summary>
/// Classificação tributária da empresa perante o Fisco — muda raramente (migração de
/// faixa/opção anual). NÃO carrega alíquota nem CSOSN nenhum — só identifica QUAL conjunto de
/// regras (RegraFiscalPorOperacao) se aplica. Fechado de propósito: os 5 regimes são um fato
/// legal do Brasil, não um conceito de negócio que o tenant "inventa" — estender para um regime
/// novo é adicionar um valor de enum + popular RegraFiscalPorOperacao para ele, nunca reescrever
/// o motor de cálculo.
/// </summary>
public enum RegimeTributario
{
    Mei,
    SimplesNacional,
    /// <summary>Excesso de sublimite de receita bruta — ainda optante do Simples Nacional (continua
    /// recolhendo IRPJ/CSLL/PIS/COFINS/CPP unificados no DAS), mas ICMS/ISS são recolhidos "por
    /// fora" do DAS, PELAS REGRAS DO REGIME NORMAL (CRT=2 na NF-e — mas o CAMPO ICMS do item usa
    /// CST, tabela B, igual a LucroPresumido/LucroReal — NUNCA CSOSN, ver
    /// <see cref="RegimeTributarioExtensions.UsaCsosn"/> logo abaixo e a nota de correção após este
    /// bloco). É a família de regra de <b>tributação de ICMS/ISS</b> que muda aqui, não a família de
    /// enquadramento perante o Simples como um todo.</summary>
    SimplesNacionalSublimite,
    LucroPresumido,
    /// <summary>Preparado, não operante nesta fase (nenhum PerfilFiscalNCM/RegraFiscalPorOperacao
    /// semeada para ele ainda) — mas o modelo já comporta sem refatoração: mesmo enum, mesma
    /// tabela de regras, só faltam as linhas de dado quando o primeiro tenant precisar.</summary>
    LucroReal
}

/// <summary>
/// Única função "hardcoded" aceitável do regime: o Código de Regime Tributário (CRT) do
/// cabeçalho da NF-e/NFC-e é um FATO FECHADO do layout SEFAZ (3 valores, ponto — não é regra de
/// negócio editável). Ainda assim vive num único lugar, nunca duplicado inline (era exatamente
/// o que o gestao-raiz fazia em 3 pontos diferentes de fiscal.service.ts com pequenas variações).
/// </summary>
public static class RegimeTributarioExtensions
{
    public static string Crt(this RegimeTributario regime) => regime switch
    {
        RegimeTributario.Mei or RegimeTributario.SimplesNacional => "1",
        RegimeTributario.SimplesNacionalSublimite => "2",
        RegimeTributario.LucroPresumido or RegimeTributario.LucroReal => "3",
        _ => throw new ArgumentOutOfRangeException(nameof(regime))
    };

    /// <summary>Só o Simples Nacional "pleno" (MEI e SimplesNacional, CRT=1) usa CSOSN.
    /// <see cref="RegimeTributario.SimplesNacionalSublimite"/> (CRT=2) usa CST — <b>tabela B,
    /// igual ao regime Normal</b> — porque o excesso de sublimite tira exatamente o ICMS/ISS do
    /// tratamento simplificado (é o motivo de existir "por fora do DAS" nesse regime). É esta
    /// função — não um switch solto em cada lugar que monta um item — que decide isso, e é
    /// também o ponto exato que a correção abaixo fecha.</summary>
    public static bool UsaCsosn(this RegimeTributario regime) =>
        regime is RegimeTributario.Mei or RegimeTributario.SimplesNacional;
}
```

**Correção específica embutida aqui (1):** o gestao-raiz tratava `lucro_presumido` e
`simples_nacional` como equivalentes para PIS/COFINS (`getPisCofinsAliquota` retornava 0,65/3,0%
para os dois). Isso é tributariamente errado: Simples Nacional **não destaca** PIS/COFINS por
item na NF-e (está embutido no DAS unificado — CST 07/08/99 na prática, sem cálculo de valor);
0,65%/3,0% cumulativo é especificamente do **Lucro Presumido**. Como aqui cada regime tem sua
própria linha em `RegraFiscalPorOperacao` (§2.3), esse tipo de "os dois se parecem, deve ser a
mesma conta" nunca mais acontece por acidente de código — é uma linha de dado a menos, não um
`if` a mais.

**Correção específica embutida aqui (2) — achada nesta revisão, mesma classe de erro que (1):**
uma primeira versão deste desenho agrupava `SimplesNacionalSublimite` junto de `SimplesNacional`
em `UsaCsosn()`, sob a lógica de "ainda é Simples, então ainda é CSOSN". **Isso está tributariamente
errado e é exatamente o tipo de acidente que este documento existe para impedir.** A regra real
(Manual de Orientação do Contribuinte da NF-e, validação sobre o campo CRT): quando `CRT = 2`
("excesso de sublimite de receita bruta"), o contribuinte **perde o direito de recolher ICMS/ISS
pelas regras do Simples** — precisa apurar pelas regras do regime Normal, o que na NF-e significa
preencher o grupo `ICMS` com **CST** (tabela B), não o grupo `ICMSSN` com CSOSN. O CRT no
cabeçalho continua "2" (não vira "3" — a empresa segue optante do Simples para IRPJ/CSLL/PIS/COFINS/CPP),
mas o campo de situação tributária de cada item de ICMS segue a família do regime Normal. Se este
agrupamento não fosse corrigido, `SituacaoTributariaIcms.ParaCst(RegimeTributario.SimplesNacionalSublimite, ...)`
falharia sempre (guarda logo abaixo, em `ParaCst`) — bloqueando a ÚNICA combinação tributariamente
correta para esse regime e deixando como único caminho possível um CSOSN que a legislação não
permite para ele.
Reflita este ajuste em `RegraFiscalPorOperacao`: as linhas seedadas para `SimplesNacionalSublimite`
usam `SituacaoTributariaIcms.ParaCst`, nunca `ParaCsosn`.

### 2.2 `OperacaoFiscal` — o contexto da transação

```csharp
namespace SistemaX.Modules.Fiscal.Domain.Operacoes;

public enum TipoOperacaoFiscal
{
    VendaMercadoria,
    DevolucaoDeVenda,
    TransferenciaEntreEstabelecimentos,
    RemessaEmComodato,
    RemessaParaConserto,
    PrestacaoDeServico   // reserva de extensão — ver §12 (NFS-e)
}

/// <summary>
/// Contexto de uma operação concreta — o que, junto do NCM (via PerfilFiscalNCM/TributacaoProduto)
/// e do regime do tenant, alimenta a resolução de CFOP e de CSOSN/CST (§2.3). Nunca confundir com
/// <c>DocumentoFiscal</c>: uma <c>OperacaoFiscal</c> é o "tipo de fato" (venda interna? devolução?
/// pra fora do estado?), o documento é o "registro" desse fato já com número/chave.
/// </summary>
public sealed record OperacaoFiscal(
    TipoOperacaoFiscal Tipo,
    string UfOrigem,
    string UfDestino,
    bool DestinatarioConsumidorFinal,
    bool DestinatarioContribuinteIcms,
    bool OperacaoPresencial)
{
    public bool EhInterestadual => !string.Equals(UfOrigem, UfDestino, StringComparison.OrdinalIgnoreCase);
}
```

`DestinatarioContribuinteIcms` + `EhInterestadual` importam porque mudam a **partilha de ICMS**
(EC 87/2015 — DIFAL) e o CFOP; `OperacaoPresencial` importa porque separa NFC-e (balcão) de NF-e
(e-commerce/B2B) mesmo com o mesmo tipo de operação.

**Lacuna fechada nesta revisão — DIFAL precisa de um lugar para existir, não só de um comentário:**
a versão anterior deste documento citava DIFAL na prosa acima mas não dava a ele nenhum campo em
`TipoTributo` (§2.6) nem um passo no fluxo de resolução (§3) — ficava "importante" sem ser
modelado. Quando `EhInterestadual && DestinatarioConsumidorFinal && !DestinatarioContribuinteIcms`
(venda interestadual para consumidor final não-contribuinte — o caso clássico de e-commerce/B2C
cross-UF), a operação gera **dois** lançamentos de ICMS sobre o mesmo item, não um: o ICMS de
origem (alíquota interestadual, ex. 7%/12%/4%) E o ICMS de partilha para o destino (diferença entre
a alíquota interna do UF de destino e a interestadual, 100% para o destino desde 2019 — cronograma
fixado pelo Convênio ICMS 93/2015, com o fundamento de validade hoje assentado em LC 190/2022 após
o STF julgar o convênio insuficiente sozinho, ADI 5469). `TipoTributo` (§2.6)
ganha `IcmsDifal` e `Fcp` (Fundo de Combate à Pobreza — adicional de 1-2% que a maioria dos UFs
cobra sobre a mesma base do DIFAL) como valores de primeira classe do bag, resolvidos pela mesma
`RegraFiscalPorOperacao` (chave já inclui `UfDestino`, então a alíquota interna do destino já tem
onde morar) — nunca um `if` especial fora da tabela.

### 2.3 `RegraFiscalPorOperacao` — onde CSOSN/CST deixam de ser hardcode

Esta é a peça que resolve literalmente o requisito "CSOSN configurável por regime/operação/CFOP".
É uma **tabela de decisão**, não uma classe com lógica de `if`:

```csharp
namespace SistemaX.Modules.Fiscal.Domain.Regras;

/// <summary>
/// Uma linha da matriz de decisão fiscal. Chave composta = (RegimeTributario, TipoOperacaoFiscal,
/// UfOrigem, UfDestino nullable = "qualquer", IndicadorSt). O valor é o CSOSN OU CST (nunca os
/// dois — <see cref="SituacaoTributariaIcms"/> garante isso na invariante) + as alíquotas
/// vigentes para essa combinação. TenantId é nullable: linha sem TenantId é DEFAULT DO SISTEMA
/// (seed curada, editável em runtime pelo suporte); linha COM TenantId sobrepõe o default só
/// para aquele tenant (ex.: benefício fiscal estadual específico daquela empresa).
/// </summary>
public sealed record RegraFiscalPorOperacao(
    string? TenantId,
    RegimeTributario Regime,
    TipoOperacaoFiscal TipoOperacao,
    string UfOrigem,
    string? UfDestino,          // null = vale para qualquer UF de destino
    bool IndicadorSt,
    SituacaoTributariaIcms SituacaoIcms,
    Percentual? AliquotaInterna,
    Percentual? AliquotaInterestadual,
    Percentual? ReducaoBaseCalculo = null,
    Percentual? Mva = null)
{
    /// <summary>Especificidade da linha — usada pelo resolvedor para desempatar quando mais de
    /// uma linha bate (tenant-específica vence default; UfDestino exata vence "qualquer").</summary>
    public int Especificidade =>
        (TenantId is not null ? 2 : 0) + (UfDestino is not null ? 1 : 0);
}

/// <summary>
/// CSOSN (Simples) OU CST (Normal) — nunca os dois ao mesmo tempo. As factories impõem a
/// compatibilidade com o regime; não existe construtor público que deixe montar um CSOSN para
/// regime Normal ou vice-versa.
/// </summary>
public sealed record SituacaoTributariaIcms
{
    public string Codigo { get; }
    public bool EhCsosn { get; }

    private SituacaoTributariaIcms(string codigo, bool ehCsosn) => (Codigo, EhCsosn) = (codigo, ehCsosn);

    public static Result<SituacaoTributariaIcms> ParaCsosn(RegimeTributario regime, string codigo)
    {
        if (!regime.UsaCsosn())
            return Result.Falhar<SituacaoTributariaIcms>(new Error(
                "fiscal.situacao.csosn_fora_do_simples", $"CSOSN não se aplica ao regime '{regime}'."));
        return Result.Ok(new SituacaoTributariaIcms(codigo, true));
    }

    public static Result<SituacaoTributariaIcms> ParaCst(RegimeTributario regime, string codigo)
    {
        if (regime.UsaCsosn())
            return Result.Falhar<SituacaoTributariaIcms>(new Error(
                "fiscal.situacao.cst_fora_do_normal", $"CST não se aplica ao regime '{regime}' (use CSOSN)."));
        return Result.Ok(new SituacaoTributariaIcms(codigo, false));
    }
}
```

`Percentual` é o "`Money` das alíquotas" — mesmo espírito de `Money`/`Quantidade` já estabelecido
no SharedKernel/Estoque: nunca `decimal` cru carregando alíquota por várias camadas, fixa em
milionésimos (6 casas decimais, cobre MVA/ICMS-ST tranquilamente):

```csharp
namespace SistemaX.Modules.Fiscal.Domain.Comum;

public readonly record struct Percentual(long Milionesimos) // 1_000_000 = 100%
{
    public static readonly Percentual Zero = new(0);
    public decimal EmFracao => Milionesimos / 1_000_000m;

    public static Percentual DePorcentagem(decimal percentual) =>
        new((long)Math.Round(percentual / 100m * 1_000_000m, MidpointRounding.ToEven));

    /// <summary>Aplica a alíquota sobre uma base monetária — único ponto do módulo onde uma
    /// alíquota vira Money, com o MESMO critério de arredondamento bancário de Money.DeReais.</summary>
    public Money AplicarSobre(Money base_) =>
        Money.DeReais(Math.Round(base_.EmReais * EmFracao, 2, MidpointRounding.ToEven));
}
```

**Resolução de CFOP — DECISÃO FECHADA (Igor) e IMPLEMENTADA.** A tentação óbvia seria uma tabela
`RegraCfop(TipoOperacaoFiscal, EhInterestadual, DestinatarioContribuinteIcms) → Cfop` só — mas essa
chave sozinha não resolve os dois CFOPs mais comuns do varejo brasileiro: `5101`/`6101` (venda de
produção própria) e `5102`/`6102` (venda de mercadoria adquirida de terceiros para revenda) têm
exatamente o mesmo `TipoOperacaoFiscal`/`EhInterestadual`/`DestinatarioContribuinteIcms` — o que
diferencia é se o **produto** foi fabricado pelo próprio tenant ou comprado para revenda. A decisão
(rota (a) da versão anterior deste documento, agora implementada) foi:

1. **CFOP tem 3 níveis de override, resolvidos nesta ordem: emissão > produto > padrão-config.**
   Nunca hardcode em nenhum dos 3.
2. **"Produto"** — `NaturezaOperacaoProduto` (`ProducaoPropria | RevendaDeTerceiros | ImportacaoPropria`,
   default `RevendaDeTerceiros`) e `CfopOverride` (string, opcional) viraram campos de
   `DadosFiscaisProduto` no módulo **Estoque** (`Produto.Fiscal`) — é lá que o gesto de cadastro faz
   sentido (mesma fronteira de NCM/CEST). `Produto.AtualizarDadosFiscais(...)` (o gap "só existe no
   construtor" documentado abaixo, em §4, foi fechado) muda esses campos e a Application publica
   `ProdutoFiscalAtualizado`/`ProdutoFiscalAtualizadoEmLote` (Modules.Abstractions) já carregando
   `NaturezaOperacao` (como STRING estável — `"producao_propria"|"revenda_terceiros"|"importacao_propria"`,
   nunca ordinal de enum) e `CfopOverride`. Fiscal assina e mantém a cópia local
   `DadosFiscaisProdutoCache` (mesmo padrão de `NcmPorProduto` já desenhado abaixo).
3. **"Padrão-config"** — `RegraCfop(TenantId?, TipoOperacaoFiscal, EhInterestadual,
   DestinatarioContribuinteIcms, NaturezaOperacaoProduto) → Cfop`, dado seedável/editável em
   runtime (viria de Settings→Fiscal, como as demais tributações), com o MESMO critério de
   especificidade (tenant-específica vence default) de `RegraFiscalPorOperacao`.
4. **"Emissão"** — override explícito passado no momento de emitir (parâmetro opcional do caso de
   uso/preview de UI) — vence sobre tudo, é o escape hatch de última instância.
5. `IResolvedorDeCfop.ResolverAsync(tenantId, operacao, produtoId, cfopDaEmissao)` (Application,
   `SistemaX.Modules.Fiscal.Application.Cfop.ResolvedorDeCfop`) implementa a cadeia — falha nomeada
   (`fiscal.cfop.nao_encontrado`) se nenhum dos 3 níveis resolve, nunca um CFOP "chutado".

Implementado em `src/Modules/Fiscal/` (`Domain/Regras/RegraCfop.cs`,
`Domain/Produtos/NaturezaOperacaoProduto.cs`, `Application/Cfop/ResolvedorDeCfop.cs`,
`Application/Motor/ResolvedorDeItemFiscalService.cs`) e em `src/Modules/Estoque/`
(`Domain/Catalogo/Produto.cs` — `DadosFiscaisProduto.NaturezaOperacao`/`CfopOverride` + método
`AtualizarDadosFiscais`; `Application/CasosDeUso/AtualizarDadosFiscaisProdutoUseCase.cs`).

### 2.4 `PerfilFiscalNCM` — a tributação PADRÃO por NCM/regime

```csharp
namespace SistemaX.Modules.Fiscal.Domain.Ncm;

/// <summary>
/// Origem da mercadoria — tabela oficial fechada do Manual de Orientação do Contribuinte da NF-e
/// (grupo ICMS, tag &lt;orig&gt; do XML), campo OBRIGATÓRIO em todo item, independente de regime.
/// Nunca inferido: entra por cadastro (NCM ou produto) e é carregado até
/// <see cref="TributoResolvidoItem"/> sem re-inferência. Fechado por norma federal do layout NF-e
/// (mesma classe de "hardcode aceitável" que <see cref="RegimeTributarioExtensions.Crt"/>) — não é
/// dado que o tenant inventa. Confirmar o número exato do convênio/ajuste vigente com o contador
/// antes de fechar o seed inicial — o QUE a tabela representa (8 códigos fechados) não muda; o
/// instrumento legal que a define pode ser atualizado por CONFAZ.
/// </summary>
public enum OrigemMercadoria
{
    Nacional = 0,
    EstrangeiraImportacaoDireta = 1,
    EstrangeiraAdquiridaMercadoInterno = 2,
    NacionalConteudoImportacaoSuperior40 = 3,
    NacionalProcessoProdutivoBasico = 4,
    NacionalConteudoImportacaoAteQuarenta = 5,
    EstrangeiraImportacaoDiretaSemSimilarNacional = 6,
    EstrangeiraAdquiridaMercadoInternoSemSimilarNacional = 7,
    NacionalConteudoImportacaoSuperior70 = 8
}

/// <summary>
/// Única regra "hardcoded" aceitável de <see cref="OrigemMercadoria"/>: a Resolução do Senado
/// Federal 13/2012 fixa em 4% a alíquota de ICMS interestadual para mercadoria importada (direta
/// ou com conteúdo de importação &gt; 40%), <b>substituindo</b> a alíquota interestadual que
/// `RegraFiscalPorOperacao` traria para aquela UF — fato fechado de lei federal, nunca dado editável
/// por UF. <see cref="MotorDeCalculoTributario"/> (§3) checa isto ANTES de aplicar
/// `AliquotaInterestadual` da regra resolvida.
/// </summary>
public static class OrigemMercadoriaExtensions
{
    public static bool ForcaAliquotaInterestadual4Pct(this OrigemMercadoria origem) =>
        origem is OrigemMercadoria.EstrangeiraImportacaoDireta
               or OrigemMercadoria.EstrangeiraAdquiridaMercadoInterno
               or OrigemMercadoria.NacionalConteudoImportacaoSuperior40
               or OrigemMercadoria.EstrangeiraImportacaoDiretaSemSimilarNacional
               or OrigemMercadoria.EstrangeiraAdquiridaMercadoInternoSemSimilarNacional;
}

/// <summary>
/// Tributação DEFAULT de um NCM sob um regime — chave (TenantId, Regime, Ncm). Populada em
/// massa (§4) e editável linha a linha. TenantId aqui (diferente de RegraFiscalPorOperacao) NÃO
/// é opcional: perfil fiscal de NCM é sempre do tenant (o contador da empresa é quem decide se
/// aquele NCM tem ICMS-ST, qual CEST, qual IPI — ainda que o sistema possa SUGERIR valores
/// iniciais a partir de uma tabela de referência ao criar a linha pela primeira vez).
/// </summary>
public sealed record PerfilFiscalNCM(
    string TenantId,
    RegimeTributario Regime,
    string Ncm,
    OrigemMercadoria Origem,
    bool ExigeIcmsSt,
    string? Cest,
    Percentual? AliquotaIpi,
    string CstOuCsosnPisCofins,
    Percentual? AliquotaPis,
    Percentual? AliquotaCofins,
    DateTimeOffset AtualizadoEm)
{
    public static Result<PerfilFiscalNCM> Criar(
        string tenantId, RegimeTributario regime, string ncm, OrigemMercadoria origem, bool exigeIcmsSt, string? cest,
        Percentual? aliquotaIpi, string cstOuCsosnPisCofins, Percentual? aliquotaPis, Percentual? aliquotaCofins)
    {
        if (!NcmValido(ncm))
            return Result.Falhar<PerfilFiscalNCM>(new Error("fiscal.ncm.formato_invalido", $"NCM '{ncm}' não tem 8 dígitos."));
        if (exigeIcmsSt && string.IsNullOrWhiteSpace(cest))
            return Result.Falhar<PerfilFiscalNCM>(new Error("fiscal.ncm.cest_obrigatorio", "NCM com ICMS-ST exige CEST."));

        return Result.Ok(new PerfilFiscalNCM(tenantId, regime, ncm, origem, exigeIcmsSt, cest, aliquotaIpi,
            cstOuCsosnPisCofins, aliquotaPis, aliquotaCofins, DateTimeOffset.UtcNow));
    }

    private static bool NcmValido(string ncm) => ncm.Length == 8 && ncm.All(char.IsDigit);
}
```

`Origem` é `OrigemMercadoria` (não nullable) — diferente de `ExigeIcmsSt`/`Cest`/alíquotas, não há
"não se aplica": todo NCM tem uma origem, `Nacional` (0) é o default são para o caso comum e deve
ser o valor pré-preenchido pela UI ao criar a linha (nunca um `null` escondido que o motor
interprete como "não sei").

Nota deliberada: `PerfilFiscalNCM` **não guarda o CSOSN/CST de ICMS** — o DEFAULT vem de
`RegraFiscalPorOperacao` (§2.3), porque a situação tributária de ICMS depende também da
*operação* (venda normal vs devolução vs transferência), não só do NCM. O que é
NCM-intrínseco (exige ST? qual CEST? qual IPI? qual tratamento padrão de PIS/COFINS? qual origem
da mercadoria?) fica aqui; o que depende de contexto de operação fica na matriz. Essa separação é
o que impede a segunda falha do gestao-raiz — lá, `MVA_PADRAO`/`ALIQUOTAS_IPI_NCM` (fato do NCM) e
`ICMS_ALIQUOTA_INTERNA`/`defaultIcmsCSOSN` (fato de operação+regime) viviam misturados no mesmo
arquivo de constantes, sem essa fronteira, o que tornou fácil aplicar a tabela errada no lugar
errado. Isso **não** significa que um produto específico nunca possa ter uma situação de ICMS
diferente da matriz — ver a correção logo abaixo em `TributacaoProduto`.

### 2.5 `TributacaoProduto` — override por `productId`

**Correção aplicada nesta revisão — faltava exatamente o override que fecha o defeito central do
gestao-raiz.** A primeira versão deste record permitia override por produto de ST/CEST/IPI/PIS/
COFINS, mas **não** do próprio CSOSN/CST de ICMS — o único caminho para uma situação de ICMS
diferente da matriz era uma linha `RegraFiscalPorOperacao` com `TenantId` (§2.3), que vale para
**todos** os produtos daquele tenant/regime/operação/UF, nunca para um único SKU. Um caso real e
comum — um produto específico com benefício fiscal individual (redução de base pontual, isenção
por programa estadual restrito àquele item, ICMS-ST que só aquele SKU tem dentro de um NCM
predominantemente sem ST) — não tinha modelagem nenhuma. `SituacaoTributariaIcmsOverride` +
`AliquotaIcmsOverride`/`ReducaoBaseCalculoOverride`/`MvaOverride` abaixo fecham isso, com o mesmo
`Motivo` obrigatório dos demais campos:

```csharp
namespace SistemaX.Modules.Fiscal.Domain.Produtos;

/// <summary>
/// Override PONTUAL por produto, campo a campo — cada campo nulo herda de
/// <see cref="PerfilFiscalNCM"/>/<see cref="RegraFiscalPorOperacao"/>; campo preenchido vence.
/// ProdutoId é o Id (ULID) do <c>Produto</c> do módulo Estoque, referenciado só como STRING —
/// Fiscal.Domain nunca importa um tipo de Estoque.Domain (mesma regra de fronteira que já vale
/// para SourceRef em cada módulo). <see cref="Motivo"/> é OBRIGATÓRIO quando qualquer campo é
/// preenchido — override sem justificativa escrita é o tipo de coisa que 8 meses depois ninguém
/// lembra por que existe.
///
/// <see cref="SituacaoTributariaIcmsOverride"/> (+ alíquota/redução/MVA de ICMS) é a peça que
/// faltava na primeira versão deste desenho: sem ela, um produto com benefício fiscal individual
/// (não compartilhado por todo o NCM/tenant) não tinha como divergir da matriz de
/// <see cref="RegraFiscalPorOperacao"/> — reintroduzia, por omissão, o mesmo tipo de "cadastro que
/// não chega na emissão" que motivou este documento inteiro, só que por um caminho ainda não
/// coberto.
/// </summary>
public sealed record TributacaoProduto(
    string TenantId,
    string ProdutoId,
    OrigemMercadoria? OrigemOverride,
    bool? ExigeIcmsStOverride,
    string? CestOverride,
    string? SituacaoTributariaIcmsOverride,   // CSOSN ou CST — compatibilidade com o regime validada no resolvedor (§3), não aqui
    Percentual? AliquotaIcmsOverride,
    Percentual? ReducaoBaseCalculoOverride,
    Percentual? MvaOverride,
    Percentual? AliquotaIpiOverride,
    string? CstOuCsosnPisCofinsOverride,
    Percentual? AliquotaPisOverride,
    Percentual? AliquotaCofinsOverride,
    string Motivo,
    DateTimeOffset AtualizadoEm)
{
    public static Result<TributacaoProduto> Criar(
        string tenantId, string produtoId, string motivo,
        OrigemMercadoria? origem = null, bool? exigeIcmsSt = null, string? cest = null,
        string? situacaoTributariaIcms = null, Percentual? aliquotaIcms = null,
        Percentual? reducaoBaseCalculo = null, Percentual? mva = null, Percentual? aliquotaIpi = null,
        string? cstOuCsosnPisCofins = null, Percentual? aliquotaPis = null, Percentual? aliquotaCofins = null)
    {
        var algumCampo = origem is not null || exigeIcmsSt is not null || cest is not null
            || situacaoTributariaIcms is not null || aliquotaIcms is not null || reducaoBaseCalculo is not null
            || mva is not null || aliquotaIpi is not null
            || cstOuCsosnPisCofins is not null || aliquotaPis is not null || aliquotaCofins is not null;

        if (algumCampo && string.IsNullOrWhiteSpace(motivo))
            return Result.Falhar<TributacaoProduto>(new Error(
                "fiscal.tributacao_produto.motivo_obrigatorio", "Override fiscal por produto exige motivo registrado."));

        return Result.Ok(new TributacaoProduto(tenantId, produtoId, origem, exigeIcmsSt, cest,
            situacaoTributariaIcms, aliquotaIcms, reducaoBaseCalculo, mva, aliquotaIpi,
            cstOuCsosnPisCofins, aliquotaPis, aliquotaCofins, motivo, DateTimeOffset.UtcNow));
    }
}
```

**Isto é a correção direta do gap mais grave encontrado no gestao-raiz**: lá,
`product.impostos` era gravado pela tela de cadastro fiscal do produto e **nunca lido** pela
emissão (`fiscal/emit/page.tsx` só copiava `ncm`/`cfop`, nunca `icmsSituacaoTributaria`/`csosn`/
`cest`). Aqui não existe um "payload de emissão" que o chamador preenche com CST/CSOSN — o
`DocumentoFiscal` **resolve** a tributação de cada item a partir de `TributacaoProduto` +
`PerfilFiscalNCM` + `RegraFiscalPorOperacao` (§3) no momento em que o item é adicionado, então não
há como "esquecer" de propagar o cadastro: ele é a única fonte, não um campo opcional do payload —
e agora essa fonte cobre também o caso "este produto específico tem uma situação de ICMS diferente
de todo o resto do NCM/tenant", que era exatamente a lacuna fechada acima.

### 2.6 `DocumentoFiscal` — o agregado, com FSM

```csharp
namespace SistemaX.Modules.Fiscal.Domain.Documentos;

public enum TipoDocumentoFiscal { NFe, NFCe, NFSe, MDFe }

/// <summary>
/// <code>
///   Rascunho ──ResolverItens() falha──► BloqueadoPorConfiguracaoFiscal ──corrige cadastro──► Rascunho
///   Rascunho ──ResolverItens() ok + AlocarNumero()──► NumeroAlocado
///   NumeroAlocado ──Transmitir()──► Autorizado | Denegado | Rejeitado
///   Rejeitado ──Retransmitir() (mesmo número)──► Autorizado | Denegado | Rejeitado
///   NumeroAlocado ──Desistir()──► Inutilizado   (nunca chegou a transmitir)
///   Rejeitado ──Desistir()──► Inutilizado        (desistiu sem nunca autorizar)
///   Autorizado ──Cancelar()──► Cancelado
/// </code>
/// Denegado, Cancelado e Inutilizado são terminais. Note que NÃO existe transição de "corrigir e
/// reemitir com o MESMO status Autorizado" — um documento autorizado é imutável (mesma filosofia
/// de "razão contábil imutável" do ADR-0001); qualquer correção pós-autorização é OUTRO documento
/// (Carta de Correção para erro leve, ou nova nota + cancelamento/devolução para erro grave).
/// </summary>
public enum StatusDocumentoFiscal
{
    Rascunho,
    BloqueadoPorConfiguracaoFiscal,
    NumeroAlocado,
    Autorizado,
    Denegado,
    Rejeitado,
    Cancelado,
    Inutilizado
}

public sealed class DocumentoFiscal : AggregateRoot<string>
{
    private readonly List<ItemDocumentoFiscal> _itens = new();

    /// <summary>
    /// Mapa explícito de transições — a MESMA disciplina de <c>Venda.TransicoesPermitidas</c>
    /// (ver `docs/arquitetura/COMO-CRIAR-UM-MODULO.md`, checklist "Status só muda via
    /// Fsm&lt;TStatus&gt;.ValidarTransicao contra um mapa explícito"). Correção aplicada nesta
    /// revisão: a versão anterior fazia a guarda de cada transição com um `if (Status is not (...))`
    /// solto por método — funciona, mas **não é** `Fsm&lt;TStatus&gt;` (a própria tabela de
    /// rastreamento em §11 já afirmava "mesma disciplina de Fsm&lt;TStatus&gt; do resto do repo",
    /// o que não era verdade no código mostrado). Centralizar aqui é o que faz a alegação da §11
    /// ser factual, e o que impede um método novo de esquecer uma guarda (o compilador não ajuda
    /// com `if` solto, mas o mapa é a fonte única consultada por todos).
    /// </summary>
    private static readonly IReadOnlyDictionary<StatusDocumentoFiscal, StatusDocumentoFiscal[]> TransicoesPermitidas =
        new Dictionary<StatusDocumentoFiscal, StatusDocumentoFiscal[]>
        {
            [StatusDocumentoFiscal.Rascunho] = [StatusDocumentoFiscal.Rascunho, StatusDocumentoFiscal.BloqueadoPorConfiguracaoFiscal, StatusDocumentoFiscal.NumeroAlocado],
            [StatusDocumentoFiscal.BloqueadoPorConfiguracaoFiscal] = [StatusDocumentoFiscal.Rascunho],
            [StatusDocumentoFiscal.NumeroAlocado] = [StatusDocumentoFiscal.Autorizado, StatusDocumentoFiscal.Denegado, StatusDocumentoFiscal.Rejeitado, StatusDocumentoFiscal.Inutilizado],
            [StatusDocumentoFiscal.Rejeitado] = [StatusDocumentoFiscal.Autorizado, StatusDocumentoFiscal.Denegado, StatusDocumentoFiscal.Rejeitado, StatusDocumentoFiscal.Inutilizado],
            [StatusDocumentoFiscal.Autorizado] = [StatusDocumentoFiscal.Cancelado],
        };

    public string TenantId { get; private set; } = string.Empty;
    public TipoDocumentoFiscal Tipo { get; private set; }
    public SourceRef Origem { get; private set; } = null!;      // ex.: SourceRef("vendas", vendaId)
    public StatusDocumentoFiscal Status { get; private set; }
    public string? Serie { get; private set; }
    public long? Numero { get; private set; }
    public string? ChaveDeAcesso { get; private set; }
    public IReadOnlyList<ItemDocumentoFiscal> Itens => _itens.AsReadOnly();
    public Money Total => _itens.Aggregate(Money.Zero, static (acc, i) => acc + i.Subtotal);
    public string? MotivoBloqueioOuRejeicaoOuDenegacao { get; private set; }

    private DocumentoFiscal() { }

    public static DocumentoFiscal Abrir(string tenantId, TipoDocumentoFiscal tipo, SourceRef origem)
        => new() { Id = Ulid.NewUlid().ToString(), TenantId = tenantId, Tipo = tipo, Origem = origem, Status = StatusDocumentoFiscal.Rascunho };

    private Result Transicionar(StatusDocumentoFiscal para) =>
        Fsm<StatusDocumentoFiscal>.ValidarTransicao(Status, para, TransicoesPermitidas);

    /// <summary>
    /// Chamado pela Application com o resultado já calculado pelo MotorDeCalculoTributario (§3) —
    /// o agregado não calcula imposto sozinho (isso seria Domain fazendo orquestração cross-NCM),
    /// só valida a invariante "documento com item sem ICMS resolvido não avança" e acumula.
    /// </summary>
    public Result AdicionarItemResolvido(ItemDocumentoFiscal item)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.Rascunho);
        if (transicao.Falha) return transicao;

        if (item.Tributos.All(t => t.Tipo != TipoTributo.Icms))
            return Result.Falhar(new Error("fiscal.item.icms_nao_resolvido",
                $"Item '{item.ProdutoId}' (NCM {item.Ncm}) não tem ICMS resolvido — configure PerfilFiscalNCM/TributacaoProduto antes de emitir."));

        _itens.Add(item);
        Status = StatusDocumentoFiscal.Rascunho;
        return Result.Ok();
    }

    /// <summary>Chamado pela Application quando a resolução de tributação de 1+ itens FALHOU —
    /// nunca emite com um default silencioso; bloqueia e nomeia o motivo.</summary>
    public Result Bloquear(string motivo)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.BloqueadoPorConfiguracaoFiscal);
        if (transicao.Falha) return transicao;

        Status = StatusDocumentoFiscal.BloqueadoPorConfiguracaoFiscal;
        MotivoBloqueioOuRejeicaoOuDenegacao = motivo;
        return Result.Ok();
    }

    /// <summary>Consome o próximo número da SequenciaFiscal (já alocado atomicamente pela
    /// Infrastructure — ver §5) — a partir daqui o número está COMPROMETIDO, mesmo que a
    /// transmissão falhe (por isso existe Desistir()/Inutilizar(), nunca "voltar" para Rascunho).</summary>
    public Result AlocarNumero(string serie, long numero)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.NumeroAlocado);
        if (transicao.Falha) return transicao;
        if (_itens.Count == 0)
            return Result.Falhar(new Error("fiscal.documento.sem_itens", "Documento sem itens não pode alocar número."));

        Serie = serie;
        Numero = numero;
        Status = StatusDocumentoFiscal.NumeroAlocado;
        Raise(new NumeroFiscalAlocadoDomainEvent(Id, TenantId, Tipo, serie, numero));
        return Result.Ok();
    }

    public Result RegistrarAutorizacao(string chaveDeAcesso, DateTimeOffset autorizadoEm)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.Autorizado);
        if (transicao.Falha) return transicao;

        ChaveDeAcesso = chaveDeAcesso;
        Status = StatusDocumentoFiscal.Autorizado;
        Raise(new DocumentoFiscalAutorizadoDomainEvent(Id, TenantId, Tipo, chaveDeAcesso, Serie!, Numero!.Value, Total, autorizadoEm));
        return Result.Ok();
    }

    public Result RegistrarDenegacao(string motivo)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.Denegado);
        if (transicao.Falha) return transicao;

        Status = StatusDocumentoFiscal.Denegado;
        MotivoBloqueioOuRejeicaoOuDenegacao = motivo;
        return Result.Ok();
    }

    /// <summary>Erro de schema/preenchimento — o MESMO número pode ser reenviado depois de
    /// corrigido (número já está comprometido desde AlocarNumero, rejeição não o libera).</summary>
    public Result RegistrarRejeicao(string motivo)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.Rejeitado);
        if (transicao.Falha) return transicao;

        Status = StatusDocumentoFiscal.Rejeitado;
        MotivoBloqueioOuRejeicaoOuDenegacao = motivo;
        return Result.Ok();
    }

    public Result Cancelar(string justificativa)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.Cancelado);
        if (transicao.Falha) return transicao;
        if (justificativa.Length < 15)
            return Result.Falhar(new Error("fiscal.documento.justificativa_curta", "Justificativa de cancelamento exige ao menos 15 caracteres (layout SEFAZ)."));
        Status = StatusDocumentoFiscal.Cancelado;
        Raise(new DocumentoFiscalCanceladoDomainEvent(Id, TenantId, Total));
        return Result.Ok();
    }

    /// <summary>Fecha formalmente um número que foi alocado mas nunca chegou a autorizar —
    /// vira insumo do evento de Inutilização de Numeração que a Application deve protocolar na
    /// SEFAZ dentro do prazo legal (ver §5). Sem isto, o número fica "aberto" para sempre, o que
    /// quebra o invariante de numeração única/sem lacuna não-justificada.</summary>
    public Result Desistir(string motivo)
    {
        var transicao = Transicionar(StatusDocumentoFiscal.Inutilizado);
        if (transicao.Falha) return transicao;

        Status = StatusDocumentoFiscal.Inutilizado;
        MotivoBloqueioOuRejeicaoOuDenegacao = motivo;
        Raise(new NumeroFiscalInutilizadoDomainEvent(Id, TenantId, Tipo, Serie!, Numero!.Value, motivo));
        return Result.Ok();
    }
}
```

Nota sobre a tabela `TransicoesPermitidas` acima: `Rascunho → Rascunho` é auto-transição deliberada
(cada `AdicionarItemResolvido` sucessivo mantém o documento em `Rascunho`) — `Fsm<TStatus>` permite
por construção (o destino só precisa estar na lista, nada impede que `de == para`). `NumeroAlocado`
e `Rejeitado` têm a mesma lista de destinos possíveis (ambos podem seguir para
Autorizado/Denegado/Rejeitado/Inutilizado) porque, do ponto de vista de "o que a SEFAZ pode
responder a uma transmissão", os dois estados são equivalentes — a diferença entre eles é só
histórica (se já foi rejeitado uma vez antes).

```csharp
public enum TipoTributo
{
    Icms, IcmsSt,
    /// <summary>Partilha de ICMS para o UF de destino em operação interestadual a consumidor final
    /// não-contribuinte (EC 87/2015/DIFAL) — ver nota em §2.2. Resolvido pela MESMA
    /// `RegraFiscalPorOperacao` da operação (a chave já carrega `UfDestino`), nunca um cálculo à
    /// parte fora da tabela.</summary>
    IcmsDifal,
    /// <summary>Fundo de Combate à Pobreza — adicional de 1-2% que a maioria dos UFs cobra sobre a
    /// mesma base do DIFAL em operação interestadual a consumidor final. Dado por UF de destino,
    /// nunca hardcoded (cada UF tem seu próprio percentual e sua própria lista de NCMs sujeitos).</summary>
    Fcp,
    Ipi, Pis, Cofins, Iss
    /* Ibs, Cbs — reserva Reforma Tributária, ver §12 */
}

/// <summary>Bag genérico e extensível — cada linha é um tributo incidente sobre o item, snapshot
/// IMUTÁVEL do que foi efetivamente calculado. Isto é o que o gestao-raiz descartava
/// (<c>convertToFirestoreTaxes</c> jogava fora cst/csosn do XML parseado, obrigando o SPED a
/// reinventar via <c>inferCstIcms</c>) — aqui a situação tributária REAL fica gravada junto do
/// valor, para sempre, sem re-inferência futura.</summary>
public sealed record TributoResolvidoItem(
    TipoTributo Tipo,
    string? SituacaoTributaria,   // CST ou CSOSN; null para ISS/IcmsDifal/Fcp (não têm CST próprio)
    Money Base,
    Percentual Aliquota,
    Money Valor,
    Percentual? ReducaoBaseCalculo = null,
    Percentual? Mva = null);

public sealed record ItemDocumentoFiscal(
    string ProdutoId,
    string Descricao,
    string Ncm,
    string? Cest,
    OrigemMercadoria Origem,     // tag <orig> do XML — ver §2.4; carregada até aqui sem re-inferência
    string Cfop,
    Quantidade Quantidade,
    Money PrecoUnitario,
    Money Desconto,
    IReadOnlyList<TributoResolvidoItem> Tributos)
{
    public Money Subtotal => (PrecoUnitario * (int)Quantidade.EmDecimal) - Desconto; // esboço — Quantidade fracionária usa multiplicação em Money por decimal controlado, não solto
}
```

### 2.7 `SequenciaFiscal` — autoridade de numeração, não CRDT

Ver §5 (dedicado, porque é a peça mais sensível legalmente — ADR-0001 já fixa a decisão de
"alocação autoritativa, nunca contador CRDT" para numeração fiscal; aqui está o desenho
concreto).

---

## 3. Fluxo de cálculo de tributo por item

Um único `MotorDeCalculoTributario` (função pura, sem I/O, em `Fiscal.Domain`) — substitui os
**dois motores divergentes** do gestao-raiz por um caminho só, chamado tanto pela emissão real
quanto por qualquer preview de UI (nunca dois caminhos que podem divergir):

```
ResolverItem(tenantId, regime, operacaoFiscal, produtoId, ncm, valores) -> Result<ItemDocumentoFiscal>

1.  cfop        = IResolvedorDeCfop.Resolver(operacaoFiscal, regime)                       [falha -> Result.Falhar]
2.  perfil      = IPerfilFiscalNcmRepository.Buscar(tenantId, regime, ncm)                 [pode ser null]
3.  override    = ITributacaoProdutoRepository.Buscar(tenantId, produtoId)                [pode ser null]
4.  origem      = override?.OrigemOverride ?? perfil?.Origem
                    ?? FALHA("fiscal.ncm.sem_perfil")     — Origem é obrigatória, nunca assume Nacional por omissão
5.  exigeSt     = override?.ExigeIcmsStOverride ?? perfil?.ExigeIcmsSt
                    ?? FALHA("fiscal.ncm.sem_perfil")     — nunca assume false silenciosamente
6.  situacaoIcms = override?.SituacaoTributariaIcmsOverride is { } sit
                    ? (sit, override.AliquotaIcmsOverride, override.ReducaoBaseCalculoOverride, override.MvaOverride)  — override GANHA da matriz, é o escape hatch por-SKU (§2.5)
                    : IRegraFiscalPorOperacaoRepository.Resolver(tenantId, regime, operacaoFiscal.Tipo,
                        operacaoFiscal.UfOrigem, operacaoFiscal.UfDestino, exigeSt)
                        -> desempate por Especificidade (§2.3)  [falha -> Result.Falhar, NUNCA CSOSN 400 default]
7.  aliquotaIcms = origem.ForcaAliquotaInterestadual4Pct() && operacaoFiscal.EhInterestadual
                    ? Percentual.DePorcentagem(4)          — Resolução Senado 13/2012, fato de lei, SOBRESCREVE a alíquota da regra (§2.4)
                    : situacaoIcms.AliquotaInterna ?? situacaoIcms.AliquotaInterestadual (conforme EhInterestadual)
8.  cest        = override?.CestOverride ?? perfil?.Cest
9.  tributoIcms = calcular Base/Aliquota/Valor a partir de situacaoIcms + aliquotaIcms + valores da linha
10. tributoIcmsDifalEFcp = SE operacaoFiscal.EhInterestadual && operacaoFiscal.DestinatarioConsumidorFinal
                    && !operacaoFiscal.DestinatarioContribuinteIcms
                    ENTÃO resolve 2ª linha de RegraFiscalPorOperacao chaveada por UfDestino (alíquota interna do
                    destino) e monta TributoResolvidoItem(IcmsDifal)/(Fcp) — SENÃO omite os dois (não gera linha
                    vazia; ausência no bag = "não se aplica a esta operação", nunca zero explícito)
11. tributoIpi, tributoPis, tributoCofins = análogo, usando override ?? perfil, cascata igual ao passo 5
12. monta ItemDocumentoFiscal com Origem = origem, Tributos = [tributoIcms, tributoIcmsSt?, tributoIcmsDifal?,
      tributoFcp?, tributoIpi?, tributoPis?, tributoCofins?]
13. Result.Ok(item)
```

**Regra de ouro que fecha o defeito central do gestao-raiz:** todo passo que não encontra
configuração retorna `Result.Falhar` com um código de erro nomeado
(`fiscal.ncm.sem_perfil`, `fiscal.regra_operacao.nao_encontrada`) — **nunca** um valor-padrão
mudo tipo `CSOSN = '400'`. A Application, ao receber a falha, chama `DocumentoFiscal.Bloquear(motivo)`
(§2.6) em vez de deixar o documento avançar. Isso desacopla "a venda aconteceu" (Vendas conclui
normalmente, é um módulo independente) de "a nota foi emitida corretamente" — a venda nunca é
bloqueada por falta de cadastro fiscal, mas a nota fica visível como pendente de configuração até
alguém completar o `PerfilFiscalNCM`/`TributacaoProduto` daquele NCM, nunca sai com um código
tributário inventado.

**Passos 6-7 e 10 são as correções desta revisão** (o override de ICMS por produto de §2.5, a
alíquota de 4% para mercadoria importada de §2.4, e o DIFAL/FCP de §2.2/§2.6) — nenhum deles existia
na primeira versão deste fluxo, e os três eram exatamente os pontos onde "parece configurável mas
na verdade uma peça do quebra-cabeça está faltando" se escondia.

---

## 4. Preenchimento de NCM em massa

Ponto de design importante, confirmado ao ler o código real do Estoque
(`src/Modules/Estoque/SistemaX.Modules.Estoque.Domain/Catalogo/Produto.cs:8`): o campo NCM em si
**já é do módulo Estoque** —
`DadosFiscaisProduto(Ncm, Cest)` vive em `Produto.Fiscal`, com o comentário do próprio autor:
*"Mantido leve de propósito — o dono do cálculo fiscal em si é o módulo Fiscal."* Isto está
certo e este design preserva a fronteira: **cadastro de NCM/CEST é Estoque; cálculo/resolução
tributária é Fiscal.** O preenchimento em massa, portanto, é um caso de uso do **Estoque**, não
do Fiscal:

```
PreencherNcmEmMassaUseCase (Estoque.Application)
  Modo A — planilha/CSV: linhas (Sku|ProdutoId, Ncm, Cest?)
  Modo B — por categoria: 1 Ncm aplicado a todos os produtos de uma Categoria
  Modo C — por lista de ProdutoIds selecionados na UI

  1. Parse + valida formato de cada Ncm (8 dígitos) — dry-run, NUNCA aplica direto.
  2. Preview: para cada linha, retorna (ProdutoId, NcmAtual, NcmNovo,
     TemPerfilFiscalNCM(regimeDoTenant, NcmNovo)?) — sinaliza em destaque quais NCMs recém-
     atribuídos AINDA NÃO têm PerfilFiscalNCM configurado no regime do tenant (aviso, não bloqueio
     — o cadastro de NCM pode vir antes da configuração tributária; só a EMISSÃO exige o perfil).
  3. Commit: dentro de uma única transação local, chama produto.AtualizarDadosFiscais(ncm, cest)
     por produto (idempotente — reaplicar o mesmo arquivo é set, não append) e acumula o evento
     de domínio correspondente.
  4. Após commit, Application publica UM evento de integração agregando o lote (companion event,
     mesmo padrão de VendaItensMovimentados/CompraItensRecebidos) — nunca N publicações
     individuais para uma operação em massa.
```

**FECHADO** (era gap documentado): `Produto.AtualizarDadosFiscais(DadosFiscaisProduto)` existe
(`Estoque.Domain/Catalogo/Produto.cs`) — mutação simples, sem FSM, mesmo gesto de
`Ativar()`/`Inativar()`. `AtualizarDadosFiscaisProdutoUseCase` (Estoque.Application) chama o
método, salva e publica `ProdutoFiscalAtualizado` (bus direto, sem indireção de DomainEvent —
mesmo padrão que `RegistrarPerdaUseCase` já usa no Estoque) DEPOIS do commit local.

### Como o Fiscal enxerga o NCM sem acoplar Domain-a-Domain

Fiscal nunca importa `Estoque.Domain` (regra de fronteira do repo: cada módulo só referencia
`SharedKernel` + `Modules.Abstractions`). A ponte é o **mesmo canal de eventos** já usado para
tudo o mais cross-módulo — dois eventos NOVOS a catalogar em `Modules.Abstractions/IntegrationEvents.cs`:

```csharp
/// <summary>Estoque publica sempre que Produto.Fiscal (Ncm/Cest) muda — individual.</summary>
public sealed record ProdutoFiscalAtualizado(
    string ProdutoId, string TenantId, string? Ncm, string? Cest, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"produto.fiscal:{ProdutoId}:{Ncm}:{Cest}";
}

/// <summary>Companion do preenchimento em massa (§4) — um evento por LOTE, não um por produto.</summary>
public sealed record ProdutoFiscalAtualizadoEmLote(
    string TenantId, IReadOnlyList<(string ProdutoId, string? Ncm, string? Cest)> Itens, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"produto.fiscal.lote:{TenantId}:{OcorridoEm.ToUnixTimeMilliseconds()}";
}
```

`Fiscal.Infrastructure` assina os dois e mantém uma **cópia local denormalizada**
(`NcmPorProduto(TenantId, ProdutoId) -> (Ncm, Cest)`, eventualmente consistente) — o mesmo
padrão que o Estoque já usa para consumir eventos de Vendas/Compras. `MotorDeCalculoTributario`
lê essa cópia local, nunca faz uma chamada síncrona cross-módulo. Isso também significa que
Fiscal pode resolver tributação mesmo se o Estoque, por algum motivo, estiver temporariamente
indisponível — mais um reflexo do princípio local-first do ADR-0001 aplicado a leituras
cross-módulo, não só a dados do próprio módulo.

---

## 5. `SequenciaFiscal` — numeração única, sem lacuna não-justificada, sem CRDT

ADR-0001 já fixa a regra geral: *"Numeração fiscal (NF-e/NFC-e gapless e única) → alocação
autoritativa de faixas por nó (ou central). Um contador CRDT criaria buracos/duplicatas —
inaceitável legalmente."* Esta seção fixa o desenho concreto, e também resolve uma tensão
aparente com uma lição registrada em `docs/robustez/robustez-hardware-licoes.md` §3
("numeração fiscal/sequencial é local-first com renumeração server-side em caso de colisão,
nunca bloqueando a venda") — **essa lição vale para o `sale_number` interno (identificador de
venda no PDV), não para o número de NF-e/NFC-e**. A diferença é legal, não técnica: um
`sale_number` pode ser renumerado depois porque é só um identificador interno; um número de
NF-e **não pode**, porque no momento em que aparece num XML autorizado pela SEFAZ ele vira um
fato jurídico — não existe "renumerar uma nota já autorizada". Por isso aqui a estratégia
correta é **nunca deixar a colisão acontecer**, não "detectar e corrigir depois".

### Mecanismo — dois casos concretos, cada um com autoridade única e sem coordenação distribuída

| Documento | Onde é emitido | Autoridade da `SequenciaFiscal` | Por que não há colisão |
|---|---|---|---|
| **NFC-e** (mod. 65) | Síncrono, no próprio PDV, no fechamento da venda | **Série dedicada e exclusiva por terminal** (Loja 1/Terminal 1 = série "1", Terminal 2 = série "2"...) | Cada linha `(TenantId, Modelo=65, Serie=<terminal>)` só é escrita por UM processo (aquele PDV) — zero concorrência por construção, não por detecção |
| **NF-e** (mod. 55) / **MDF-e** | Back-office / `Store.Server` (LAN) | **Uma série por loja, autoridade única no `Store.Server`** | Só um processo (o `Store.Server` daquela loja) aloca daquela série — se um PDV eventualmente precisar emitir NF-e direto, pede o número ao `Store.Server` via chamada síncrona LAN (baixa latência), nunca gera localmente |

`Cloud.Api` **nunca** aloca número fiscal — cada loja é autônoma para sua própria numeração
(mesma independência que o resto do local-first); a nuvem só consolida depois que o documento já
foi autorizado localmente. Isso também significa que a numeração continua funcionando com a
internet fora do ar — o único requisito é a LAN da loja estar de pé (para NF-e) ou nem isso
(NFC-e, cuja série pertence ao próprio terminal).

### Alocação — atômica na MESMA transação local que persiste o documento

```csharp
namespace SistemaX.Modules.Fiscal.Application.Ports;

public interface ISequenciaFiscalRepository
{
    /// <summary>Aloca o PRÓXIMO número via UPDATE...RETURNING atômico (ex.: SQLite
    /// <c>UPDATE sequencias_fiscais SET proximo_numero = proximo_numero + 1
    /// WHERE tenant_id=@t AND modelo=@m AND serie=@s RETURNING proximo_numero - 1</c>),
    /// executado DENTRO da mesma transação local que grava o DocumentoFiscal em NumeroAlocado —
    /// mesma lição do Supermarket-OS (§1 de robustez-hardware-licoes.md): a unidade de
    /// crash-safety é a transação do banco local, nunca uma sequência de chamadas de app.
    /// Falha (processo morre no meio) = rollback do WAL = nem número nem documento avançam;
    /// nunca "número consumido, documento não gravado".</summary>
    Task<Result<long>> AlocarProximoAsync(string tenantId, string modelo, string serie, CancellationToken ct = default);
}
```

`SequenciaFiscal` **não é** um `AggregateRoot` — de propósito. Não tem FSM, não tem ciclo de
vida próprio, é uma linha de contador por chave natural `(TenantId, Modelo, Serie)`. Modelá-la
como agregado ULID adicionaria uma camada de indireção sem benefício (o mesmo raciocínio já
aplicado a `ISaldoRepository`/`IMovimentoRepository` no Estoque — nem tudo que é dado persistente
é um agregado).

### O que fecha o gap de lacuna: `Desistir()` → `Inutilizado`

A lei brasileira não exige *zero* lacuna — exige **número único, sequencial, e toda lacuna
formalmente justificada** via evento de "Inutilização de Numeração" protocolado na SEFAZ dentro
do prazo legal. Por isso `DocumentoFiscal.Desistir()` (§2.6) existe como transição de primeira
classe: sempre que um número é alocado (`NumeroAlocado`) mas o documento nunca chega a
`Autorizado` (rascunho abandonado, venda cancelada antes da transmissão, crash seguido de nova
tentativa com item diferente), a Application deve rotear para `Desistir(motivo)` — nunca deixar
o documento "pairando" nem, pior, tentar reaproveitar aquele número para outro documento. O
evento de domínio resultante (`NumeroFiscalInutilizadoDomainEvent`) alimenta um job periódico
(Application) que agrega os números pendentes de inutilização e protocola o evento oficial na
SEFAZ dentro da janela legal.

---

## 6. Plugagem no modular monolith

```
src/Modules/Fiscal/
  SistemaX.Modules.Fiscal.Domain/
    Regimes/RegimeTributario.cs
    Operacoes/OperacaoFiscal.cs
    Regras/RegraFiscalPorOperacao.cs, SituacaoTributariaIcms.cs
    Ncm/PerfilFiscalNCM.cs
    Produtos/TributacaoProduto.cs
    Documentos/DocumentoFiscal.cs, ItemDocumentoFiscal.cs, StatusDocumentoFiscal.cs,
               DocumentoFiscalDomainEvents.cs
    Comum/Percentual.cs, Quantidade.cs (cópia local, mesma regra de fronteira), SourceRef.cs (idem),
          MotorDeCalculoTributario.cs (função pura de resolução — §3)
  SistemaX.Modules.Fiscal.Application/
    CasosDeUso/EmitirDocumentoFiscalUseCase.cs, CancelarDocumentoFiscalUseCase.cs,
               DesistirDeNumeroUseCase.cs, InutilizarNumeracaoPendenteUseCase.cs
    Ports/ISequenciaFiscalRepository.cs, IPerfilFiscalNcmRepository.cs,
          ITributacaoProdutoRepository.cs, IRegraFiscalPorOperacaoRepository.cs,
          IResolvedorDeCfop.cs, IGatewayEmissaoSefaz.cs (assinatura/transmissão — detalhe de
          Infrastructure por trás; ver §12 sobre reaproveitar padrões do gestao-raiz aqui)
    EventosDeIntegracao/Handlers/VendaItensMovimentadosHandler.cs,
                        ProdutoFiscalAtualizadoHandler.cs, ProdutoFiscalAtualizadoEmLoteHandler.cs
    FiscalModule.cs
  SistemaX.Modules.Fiscal.Infrastructure/
    Sqlite/... (sequencias_fiscais, perfis_fiscais_ncm, tributacoes_produto, regras_fiscais_operacao,
                documentos_fiscais, ncm_por_produto — cache local do Estoque)
    InMemory/... (adapters para teste — ver correção abaixo)
    FiscalInfrastructureModule.cs
```

**Correção estrutural aplicada nesta revisão.** A primeira versão deste documento mostrava um
`FiscalModule` único registrando handlers/use-cases (Application) **e** `SqliteSequenciaFiscalRepository`
(Infrastructure) no mesmo `IModule`, e sem nenhum adapter in-memory. Isso diverge do padrão que
`Financeiro`/`Vendas`/`Estoque`/`Compras` já seguem à risca no repo (`FinanceiroModule.cs`,
`VendasInfrastructureModule.cs`, `EstoqueInfrastructureModule.cs`) e que o próprio comentário do
`FinanceiroModule` explica: *"o grafo de referência de projeto da solução é
`Infrastructure → Application → Domain`, nunca o inverso — este assembly não pode referenciar os
adapters concretos de `...Infrastructure` sem criar uma dependência circular."* Um `FiscalModule`
em `Fiscal.Application` que referencia `SqliteSequenciaFiscalRepository` (que vive em
`Fiscal.Infrastructure`) não compila nesse grafo. A correção é a mesma partição em DOIS `IModule`
que todo módulo já implementado usa:

```csharp
// Fiscal.Application/FiscalModule.cs — só o que Application enxerga: handlers e casos de uso.
public sealed class FiscalModule : IModule
{
    public string Codigo => "fiscal";
    public string Nome => "Fiscal";
    // SEM DependeDe em "estoque"/"vendas" — ver nota abaixo: assinar um evento de integração não
    // exige o módulo emissor presente, só o TIPO do evento (que vive em Modules.Abstractions,
    // referenciado por todo módulo). Mesmo padrão que EstoqueModule já usa hoje (assina
    // VendaItensMovimentados/CompraItensRecebidos/4 eventos da OS sem declarar DependeDe em
    // "vendas"/"compras"/"assistencia").

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        services.AddScoped<IIntegrationEventHandler<VendaItensMovimentados>, VendaItensMovimentadosHandler>();
        services.AddScoped<IIntegrationEventHandler<ProdutoFiscalAtualizado>, ProdutoFiscalAtualizadoHandler>();
        services.AddScoped<IIntegrationEventHandler<ProdutoFiscalAtualizadoEmLote>, ProdutoFiscalAtualizadoEmLoteHandler>();

        services.AddScoped<EmitirDocumentoFiscalUseCase>();
        services.AddScoped<CancelarDocumentoFiscalUseCase>();
        services.AddScoped<DesistirDeNumeroUseCase>();
        services.AddScoped<InutilizarNumeracaoPendenteUseCase>();
    }
}

// Fiscal.Infrastructure/FiscalInfrastructureModule.cs — segundo IModule, no mesmo espírito de
// EstoqueInfrastructureModule/VendasInfrastructureModule: só quem enxerga port E adapter ao mesmo
// tempo registra o adapter concreto.
public sealed class FiscalInfrastructureModule : IModule
{
    public string Codigo => "fiscal.infra";
    public string Nome => "Fiscal — Infraestrutura";
    public IReadOnlyCollection<string> DependeDe => ["fiscal"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        if (contexto.Camada == CamadaExecucao.Nuvem)
        {
            // Cloud nunca aloca número (§5) — registra só os repositórios de leitura/consolidação
            // de DocumentoFiscal, nunca ISequenciaFiscalRepository (não existe autoridade de
            // numeração na Nuvem — ver §5).
            return;
        }

        if (contexto.Configuracao["persistencia"] == "sqlite")
        {
            services.AddScoped<ISequenciaFiscalRepository, SqliteSequenciaFiscalRepository>();
            services.AddScoped<IPerfilFiscalNcmRepository, SqlitePerfilFiscalNcmRepository>();
            services.AddScoped<ITributacaoProdutoRepository, SqliteTributacaoProdutoRepository>();
            services.AddScoped<IRegraFiscalPorOperacaoRepository, SqliteRegraFiscalPorOperacaoRepository>();
            services.AddModuleSchemaMigration<FiscalSchemaMigrationV1>();
        }
        else
        {
            // Default (config ausente, todo teste hoje) — mesma convenção de Estoque/Vendas.
            services.AddSingleton<ISequenciaFiscalRepository, InMemorySequenciaFiscalRepository>();
            services.AddSingleton<IPerfilFiscalNcmRepository, InMemoryPerfilFiscalNcmRepository>();
            services.AddSingleton<ITributacaoProdutoRepository, InMemoryTributacaoProdutoRepository>();
            services.AddSingleton<IRegraFiscalPorOperacaoRepository, InMemoryRegraFiscalPorOperacaoRepository>();
        }
    }
}
```

**Nota sobre `DependeDe`:** a versão anterior declarava `DependeDe => ["estoque", "vendas"]` em
`FiscalModule`. Isso forçaria toda instalação que queira habilitar Fiscal a também habilitar
Estoque **e** Vendas — `ModuleRegistry.RegistrarTodos` lança na hora do boot se uma dependência
declarada não estiver presente (`ModuleRegistry.cs`), o que contradiz o princípio central de
`IModule` ("habilitar um vertical = registrar seu `IModule`; desabilitar = não registrar; módulo
desligado não carrega nada — zero superfície de falha para os demais"). Fiscal **consome** eventos
de Vendas/Estoque, mas consumir um evento de integração não exige o módulo emissor fisicamente
presente — só o tipo do evento, que vive em `Modules.Abstractions` (kernel compartilhado que todo
módulo já referencia). O próprio `EstoqueModule` de hoje assina 4 eventos da OS + eventos de
Vendas/Compras sem declarar `DependeDe` em nenhum deles — este design segue a mesma convenção. Se
Vendas/Estoque nunca publicarem nada (instalação sem esses módulos), Fiscal simplesmente nunca
recebe evento — cache `NcmPorProduto` fica vazio, nenhum `DocumentoFiscal` é aberto por venda,
sem erro de boot.

### Evento de integração de entrada: `VendaConcluida`/`VendaItensMovimentados` → rascunho de `DocumentoFiscal`

Igual ao Estoque (mesmo evento, dois assinantes independentes — o barramento já suporta
fan-out, ver `InProcessIntegrationEventBus.PublishAsync`, que resolve TODOS os handlers
registrados para o tipo do evento), Fiscal assina `VendaItensMovimentados` (precisa do detalhe
por item — `VendaConcluida` sozinho só tem o total, insuficiente para montar
`ItemDocumentoFiscal`). `VendaItensMovimentadosHandler` **já existe e está implementado** em
`Fiscal.Application/EventosDeIntegracao/Handlers/VendaItensMovimentadosHandler.cs`, registrado em
`FiscalModule` e coberto por teste. `Vendas.Application` também já existe (`VendaUseCases.cs`) e
publica os eventos de integração (`VendaConcluida` via `ConcluirVendaUseCase`, `VendaEstornada`
via `EstornarVendaUseCase`).

**FECHADO nesta revisão** (o texto anterior descrevia este gap como remanescente): agora
`ConcluirVendaUseCase` publica **os dois** eventos de integração lado a lado, a partir do mesmo
`VendaConcluidaDomainEvent` — `evento.ParaEventoDeIntegracao()` (→ `VendaConcluida`, Financeiro) e
`evento.ParaVendaItensMovimentados()` (→ `VendaItensMovimentados`, Estoque + Fiscal), sempre
pós-commit, na mesma ordem "salva local, depois publica" (R3). `Venda.Concluir()` agora monta
`ItemVendaParaEstoque` (novo, em `VendaDomainEvents.cs`) a partir de `Itens` do agregado —
`QuantidadeMilesimos = Quantidade * 1000` (Vendas só vende unidades inteiras hoje; fracionário/KG
fica para quando o PDV precisar pesar). Mesmo desenho "um fato, dois eventos publicados lado a
lado" que `NotaDeCompraRecebidaDomainEvent.ParaCompraRecebida()`/`ParaCompraItensRecebidos()` já
demonstra em Compras. `grep -rn "new VendaItensMovimentados("` em `src/` agora encontra o
publisher em `VendaDomainEvents.cs`; `VendaConcluidaEventoTests` cobre a publicação dos dois
eventos e o formato do item (`ProdutoId`/`QuantidadeMilesimos`/`PrecoUnitarioCentavos`).

```
VendaItensMovimentadosHandler.HandleAsync(evento):
  1. idempotência: já existe DocumentoFiscal com Origem = SourceRef("vendas", evento.VendaId)? → NO-OP.
  2. doc = DocumentoFiscal.Abrir(evento.TenantId, TipoDocumentoFiscal.NFCe, SourceRef("vendas", evento.VendaId))
  3. para cada item do evento: MotorDeCalculoTributario.ResolverItem(...) → doc.AdicionarItemResolvido(item)
     ou, se falhar: doc.Bloquear(motivo) e PARA (não itera os demais com erro parcial).
  4. persiste doc (repositório Fiscal.Infrastructure) — commit local.
  5. se Status == Rascunho (todos os itens resolveram): enfileira EmitirDocumentoFiscalUseCase
     (aloca número dentro da mesma transação de persistência — §5 — e então transmite).
```

### Eventos NOVOS que Fiscal adiciona ao catálogo (`Modules.Abstractions/IntegrationEvents.cs`)

| Evento | Quem assina (hoje/futuro) |
|---|---|
| `NumeroFiscalAlocado` | Auditoria/observabilidade — nunca é gatilho de outro fato de negócio |
| `DocumentoFiscalAutorizado` | Notificações (link do PDF/XML), futura Contabilidade/SPED |
| `DocumentoFiscalCancelado` | Idem — nunca dispara reversão financeira sozinho (a `VendaEstornada` já cobre o lado financeiro; este evento é só o lado fiscal do mesmo fato) |
| `NumeroFiscalInutilizado` | Job de protocolo de Inutilização na SEFAZ (§5) |

---

## 7. Invariantes (checklist)

> **Passada de verificação desta revisão**: os 17 itens abaixo marcados `[x]` foram checados
> contra o código real (Domain/Application/Infrastructure, não só a intenção do design) — não é
> só "parece certo pela leitura do doc". O único item que continua `[ ]` é uma invariante de
> PROCESSO que depende do gateway inexistente (§9/§1 da lista de fixes desta revisão), não um bug:
> não há como o repo satisfazê-la enquanto `IGatewayEmissaoSefaz` não tiver implementação.

- [x] Todo `PerfilFiscalNCM`/`TributacaoProduto`/`DocumentoFiscal`/linha de `SequenciaFiscal` carrega `TenantId`; toda query filtra por ele. *(confirmado: `TenantId` no construtor/factory dos 3 records de Domain; `SequenciaFiscal` não é agregado — a chave `fiscal:{tenantId}:{modelo}:{serie}` embute o tenant em `SqliteSequenciaFiscalRepository`; `ObterPorOrigemAsync`/`ListarNumeroAlocadoAntesDeAsync` filtram `WHERE tenant_id = $tenantId` em `SqliteDocumentoFiscalRepository`.)*
- [x] Nunca existe um `CSOSN`/`CST` literal fora de dado (`RegraFiscalPorOperacao`/override) — zero `const string Csosn = "..."` em código de Application/Domain. *(confirmado: `grep -rn "const string.*Cs[oO]sn\|const string.*Cst"` em `Fiscal.Domain`/`Fiscal.Application` não encontra nenhuma ocorrência.)*
- [x] `SituacaoTributariaIcms` só nasce via `ParaCsosn`/`ParaCst`, nunca construtor solto — impede CST em regime Simples ou CSOSN em regime Normal por acidente de digitação. *(confirmado: construtor `private`, só as duas factories em `SituacaoTributariaIcms.cs`.)*
- [x] Resolução de tributação sem configuração suficiente é `Result.Falhar` (→ `DocumentoFiscal.Bloquear`), nunca um valor-padrão silencioso. *(confirmado: todo caminho sem `Perfil`/`RegraIcms`/`RegraIcmsDestino` em `MotorDeCalculoTributario.ResolverItem` retorna `Falhar(...)` com código nomeado, nunca um default mudo.)*
- [x] `DocumentoFiscal.Autorizado` é imutável — nenhuma correção pós-autorização edita o agregado; é sempre um documento novo (Carta de Correção/cancelamento/nova nota). *(confirmado: `TransicoesPermitidas[Autorizado] = [Cancelado]` — nenhum outro método muda o agregado a partir daí; `AdicionarItemResolvido`/`AlocarNumero` exigem `Transicionar(Rascunho/NumeroAlocado)`, que falha vindo de `Autorizado`.)*
- [ ] Todo número alocado (`NumeroAlocado`) termina em exatamente um de: `Autorizado`, `Denegado`, `Inutilizado` — nunca fica pendurado sem transição terminal. **NÃO confirmado hoje** — é o inverso do fix #1 desta revisão: sem `IGatewayEmissaoSefaz` implementado, nenhum código de Application chama `RegistrarAutorizacao`/`RegistrarRejeicao`/`RegistrarDenegacao`, então todo documento que chega em `NumeroAlocado` fica ali indefinidamente até alguém rodar `DesistirDeNumeroUseCase` manualmente. Só volta a ser uma invariante de fato quando o gateway (ou o job de timeout que dispara `DesistirDeNumeroUseCase`) existir.
- [x] `SequenciaFiscal.AlocarProximoAsync` roda dentro da MESMA transação local que persiste o `DocumentoFiscal` em `NumeroAlocado` — nunca dois passos separados. *(confirmado: `EmitirDocumentoFiscalUseCase.ExecutarAsync` abre `IUnidadeDeTrabalhoFiscal` antes de chamar `sequencias.AlocarProximoAsync`, e só comita depois de `documentos.SalvarAsync` — rollback nos dois `if (...Falha)` intermediários.)*
- [x] Cada `TipoTributo` calculado carrega `SituacaoTributaria` (CST/CSOSN) junto do valor — nunca só o número, para nunca precisar reinferir depois (SPED, auditoria). *(confirmado: todo `new TributoResolvidoItem(...)` em `MotorDeCalculoTributario` recebe a situação tributária resolvida junto com base/alíquota/valor, nunca só o valor.)*
- [x] `Fiscal.Domain` não referencia `Estoque.Domain` nem `Vendas.Domain` — toda leitura cross-módulo é via evento de integração + cópia local (`NcmPorProduto`). *(confirmado: `SistemaX.Modules.Fiscal.Domain.csproj` só referencia `SharedKernel`/`Modules.Abstractions`; zero `using SistemaX.Modules.Estoque`/`.Vendas` no projeto.)*
- [x] Dinheiro sempre `Money`; alíquota sempre `Percentual` — nenhum `decimal`/`double` cru carregando base ou alíquota entre camadas. *(confirmado: os únicos `decimal` públicos em Domain/Application são as conversões internas de `Percentual.EmFracao`/`DePorcentagem`/`Quantidade.EmDecimal`/`DeDecimal` — não atravessam fronteira de camada como campo solto.)*
- [x] Toda operação de emissão é idempotente por `SourceRef` (`Origem.Chave`) — reprocessar o mesmo `VendaItensMovimentados` nunca cria um segundo `DocumentoFiscal` para a mesma venda. *(confirmado: primeira linha de `EmitirDocumentoFiscalUseCase.ExecutarAsync` é `ObterPorOrigemAsync(tenantId, origem.Chave)` → retorna o existente sem duplicar; reforçado por `UNIQUE(tenant_id, origem_modulo, origem_id)` no schema SQLite, §8.)*
- [x] `RegimeTributario.SimplesNacionalSublimite` resolve ICMS via `SituacaoTributariaIcms.ParaCst` (nunca `ParaCsosn`) — `UsaCsosn()` só é `true` para `Mei`/`SimplesNacional`. *(confirmado: `UsaCsosn()` em `RegimeTributario.cs` retorna `true` só para `Mei`/`SimplesNacional`; `MotorDeCalculoTributario.ResolverSituacaoIcms` decide `ParaCsosn`/`ParaCst` a partir desse booleano.)*
- [x] Toda transição de `StatusDocumentoFiscal` passa por `Fsm<StatusDocumentoFiscal>.ValidarTransicao` contra `TransicoesPermitidas` — nenhum `if (Status is not (...))` solto por método. *(confirmado: `grep -c "Transicionar("` em `DocumentoFiscal.cs` = 10 chamadas, uma por método de transição; zero ocorrência de `if (Status is`.)*
- [x] `OrigemMercadoria` é preenchida em todo `PerfilFiscalNCM`/`ItemDocumentoFiscal` (nunca null/omitida) — item interestadual com origem importada (1/2/3/6/7) usa a alíquota fixa de 4% (Resolução Senado 13/2012), nunca a alíquota de `RegraFiscalPorOperacao`. *(confirmado: `Origem`/`OrigemMercadoria` é `OrigemMercadoria` não-nullable em ambos os records; `ForcaAliquotaInterestadual4Pct()` decide a alíquota de 4% antes de cair na regra padrão em `MotorDeCalculoTributario`.)*
- [x] `TributacaoProduto.SituacaoTributariaIcmsOverride` existe como escape hatch por-SKU — a resolução de ICMS não depende só de uma linha `RegraFiscalPorOperacao` com `TenantId` (que vale para todos os produtos daquele tenant/regime/operação/UF). *(confirmado: campo existe em `TributacaoProduto.cs` e é consultado antes da matriz padrão em `ResolverSituacaoIcms`.)*
- [x] Venda interestadual a consumidor final não-contribuinte gera `TributoResolvidoItem(IcmsDifal)`/`(Fcp)` além do ICMS de origem — nunca só o ICMS interestadual sozinho. *(confirmado: bloco `if (input.Operacao.GeraPartilhaDifal)` em `MotorDeCalculoTributario` adiciona `IcmsDifal` sempre, e `Fcp` quando `AliquotaFcp > 0`, em cima do `Icms` de origem já adicionado antes.)*
- [x] `FiscalModule` (Application) não referencia nenhum adapter concreto de `Fiscal.Infrastructure`; quem registra `Sqlite*Repository`/`InMemory*Repository` é `FiscalInfrastructureModule` (`DependeDe: ["fiscal"]`) — mesmo grafo `Infrastructure → Application → Domain` do resto do repo. *(confirmado: `FiscalModule.cs` só registra handlers/casos de uso/`ResolvedorDeCfop`; os `Sqlite*`/`InMemory*` concretos só aparecem em `FiscalInfrastructureModule.cs`.)*
- [x] `FiscalModule.DependeDe` não lista `"estoque"`/`"vendas"` — assinar evento de integração de outro módulo nunca exige esse módulo fisicamente presente na instalação. *(confirmado: `FiscalModule` não sobrescreve `DependeDe` — usa o default `Array.Empty<string>()` de `IModule`.)*

---

## 8. Persistência (esboço SQLite)

Mesmo padrão do resto do repo (`docs/persistencia/persistencia-sqlite.md`): migração própria do
módulo (`FiscalSchemaMigrationV1`), tabelas:

```
sequencias_fiscais       (tenant_id, modelo, serie) PK, proximo_numero
regras_fiscais_operacao  (tenant_id NULLABLE, regime, tipo_operacao, uf_origem, uf_destino NULLABLE,
                          indicador_st, situacao_tributaria, eh_csosn, aliquota_interna, aliquota_interestadual,
                          reducao_base, mva)
perfis_fiscais_ncm       (tenant_id, regime, ncm) PK, origem_mercadoria, exige_icms_st, cest, aliquota_ipi,
                          cst_csosn_pis_cofins, aliquota_pis, aliquota_cofins, atualizado_em
tributacoes_produto      (tenant_id, produto_id) PK, origem_mercadoria_override,
                          situacao_tributaria_icms_override, aliquota_icms_override,
                          reducao_base_calculo_override, mva_override, overrides nullable (ST/CEST/IPI/PIS/COFINS)...,
                          motivo, atualizado_em
documentos_fiscais       (id ULID PK, tenant_id, tipo, origem_modulo, origem_id, status, serie, numero,
                          chave_acesso, total_centavos, motivo, ...) + UNIQUE(tenant_id, origem_modulo, origem_id)
itens_documento_fiscal   (documento_fiscal_id FK, produto_id, ncm, cest, origem_mercadoria, cfop,
                          quantidade_milesimos, preco_unitario_centavos, desconto_centavos)
tributos_item_documento  (item_id FK, tipo_tributo, situacao_tributaria, base_centavos, aliquota_milionesimos,
                          valor_centavos, reducao_base_milionesimos, mva_milionesimos)
                          -- tipo_tributo agora inclui 'IcmsDifal'/'Fcp' (§2.6) além de Icms/IcmsSt/Ipi/Pis/Cofins/Iss
ncm_por_produto          (tenant_id, produto_id) PK — cache local do Estoque, populado por evento (§4)
```

`UNIQUE(tenant_id, origem_modulo, origem_id)` em `documentos_fiscais` é o que garante a
idempotência de `VendaItensMovimentadosHandler` no nível de banco, não só em memória.

`origem_mercadoria`/`origem_mercadoria_override` são `INTEGER NOT NULL DEFAULT 0` (Nacional) em
`perfis_fiscais_ncm` — nunca nullable ali, mesma razão de §2.4 (todo NCM tem uma origem, o default
são é o caso comum, nunca um `NULL` que o motor confunda com "não sei"); já em
`tributacoes_produto` o override é nullable (herda do perfil quando ausente), como os demais
campos de override.

---

## 9. Fora de escopo desta fase (gaps documentados, não bloqueiam o design acima)

- **NFS-e/ISS** — `TipoDocumentoFiscal.NFSe` e `TipoTributo.Iss` já existem no enum (extensão
  aditiva quando o vertical de serviço precisar), mas o cálculo de ISS por município (alíquota
  por serviço via LC 116, retenção, código de tributação municipal) não tem nenhum equivalente
  aproveitável do gestao-raiz (lá é stub 501) — é modelo net-new, fora desta fase.
- **SPED EFD-ICMS/IPI/Contribuições** — o modelo de `DocumentoFiscal`/`TributoResolvidoItem`
  já persiste CST/CSOSN reais (fecha o risco de divergência SPED×NF-e do gestao-raiz), mas o
  *gerador* de SPED em si (blocos 0/C/1/9) é um projeto separado, a construir sobre este modelo.
  O esqueleto de blocos do gestao-raiz (`sped-generator.ts`) é reaproveitável como referência de
  estrutura, não como fonte de CST (que passa a vir do dado real, não de `inferCstIcms`).
- **Gateway de assinatura/transmissão SEFAZ** — `IGatewayEmissaoSefaz` **já existe como arquivo de
  porta** (`Fiscal.Application/Ports/IGatewayEmissaoSefaz.cs`, adicionado nesta revisão — antes só
  era citado em comentário), declarando a assinatura `TransmitirAsync(DocumentoFiscal) →
  Result<ResultadoTransmissaoSefaz>` que fecha o caminho `NumeroAlocado → Autorizado/Rejeitado/
  Denegado`. O que este documento continua **não decidindo** é a IMPLEMENTAÇÃO: gateway terceiro
  pago (padrão do gestao-raiz, `emissao.tensorroot.com`) ou emissão própria via mTLS+XMLDSig
  direto com a SEFAZ (o padrão de `sefaz-distribuicao.ts` do gestao-raiz mostra que é tecnicamente
  viável) — decisão de produto/custo, não de arquitetura, a ser tomada como ADR separado quando
  chegar a vez de implementar `Fiscal.Infrastructure`. Até essa implementação existir,
  `EmitirDocumentoFiscalUseCase` continua nunca chamando `RegistrarAutorizacao`/
  `RegistrarRejeicao`/`RegistrarDenegacao` (ver nota no próprio use case) — ver também o item
  correspondente, ainda `[ ]`, em §7.
- **Lucro Real** — enum já existe (`RegimeTributario.LucroReal`), zero linha de
  `RegraFiscalPorOperacao`/`PerfilFiscalNCM` semeada para ele ainda — popular quando o primeiro
  tenant desse regime precisar, sem tocar em nenhum tipo/agregado.
- **Reforma Tributária (IBS/CBS, transição 2026–2033)** — `TipoTributo` já reserva espaço para
  `Ibs`/`Cbs` (comentário no enum); o período de coexistência com ICMS/PIS/COFINS é, na prática,
  "mais linhas de `RegraFiscalPorOperacao`/`PerfilFiscalNCM` com um `TipoTributo` novo", não uma
  reescrita do motor — é exatamente o cenário que motivou separar regra-como-dado de
  regra-como-código em §1.
- ~~**CFOP não distingue produção própria de revenda de terceiros**~~ — **FECHADO** (decisão de
  Igor, implementada). Ver §2.3: `NaturezaOperacaoProduto`/`CfopOverride` em `DadosFiscaisProduto`
  (Estoque) + `RegraCfop` (padrão-config) + override na emissão, cadeia
  `emissão > produto > padrão-config` resolvida por `IResolvedorDeCfop`.

---

## 10. Distribuição DFe e MDF-e — reaproveitamento explícito

A auditoria do gestao-raiz identificou `sefaz-distribuicao.ts` (cliente SOAP + mTLS + XMLDSig +
trust store ICP-Brasil para baixar NF-e de terceiros contra o CNPJ do tenant) como a peça mais
sofisticada e correta do módulo de lá, sem equivalente fácil de recriar. Este design não a
substitui — `DocumentoFiscal` (§2.6) é sobre documentos **emitidos** pelo tenant; distribuição de
documentos **recebidos** (compra de fornecedor) é um caso de uso separado, adjacente, que deveria
consumir o mesmo `IGatewayEmissaoSefaz`/porta de infraestrutura fiscal por trás, mas alimenta o
módulo **Compras** (`CompraRecebida`/`CompraItensRecebidos`, já catalogados), não gera um
`DocumentoFiscal` novo. MDF-e usa o mesmo agregado `DocumentoFiscal`
(`TipoDocumentoFiscal.MDFe`) com uma FSM mais rica (inclusão de condutor, encerramento) que não
está detalhada aqui por não fazer parte do pedido original — extensão aditiva quando precisar.

---

## 11. Rastreamento — defeito do gestao-raiz → correção deste design

| Defeito encontrado no `gestao-raiz` | Onde este design fecha o gap |
|---|---|
| `defaultIcmsCSOSN = crt !== '3' ? '400' : undefined` hardcoded em `fiscal.service.ts:1032-1045` | CSOSN nunca é literal de código — só valor de `RegraFiscalPorOperacao` (dado); falta de linha é `Result.Falhar`, não `'400'` (§2.3, §3) |
| Dois motores de cálculo divergentes (`tax-calculation.service.ts` correto e não usado vs lógica inline em `fiscal.service.ts` que roda de verdade) | Um único `MotorDeCalculoTributario`, chamado por emissão E preview (§3) |
| `product.impostos` gravado na tela de cadastro, nunca lido pela emissão (`emit/page.tsx` só copia `ncm`/`cfop`) | `DocumentoFiscal` RESOLVE a tributação a partir de `TributacaoProduto`/`PerfilFiscalNCM` — não existe payload de emissão que "esqueça" de copiar campo (§2.5, §3) |
| `getPisCofinsAliquota` trata `lucro_presumido` e `simples_nacional` como iguais (0,65/3,0%) | Cada `RegimeTributario` tem sua própria linha de regra — Simples nem calcula PIS/COFINS por item (embutido no DAS); só Presumido/Real usam a alíquota destacada (§2.1, §2.3) |
| `convertToFirestoreTaxes` descarta `cst`/`csosn` do XML parseado; SPED reinfere via `inferCstIcms` | `TributoResolvidoItem.SituacaoTributaria` é gravado como parte imutável do item no momento da resolução — nunca reinferido depois (§2.6) |
| Tabelas `NCM_TABLE`/`CFOP_TABLE`/`ALIQUOTAS_IPI_NCM`/`ICMS_ALIQUOTA_INTERNA` hardcoded ("valores exemplo", comentário do próprio autor) | Viram linhas de `PerfilFiscalNCM`/`RegraFiscalPorOperacao`/tabela de CFOP — dado seedável e editável em runtime, sem deploy (§1, §2.3, §2.4) |
| NFS-e stub 501 | Fora de escopo desta fase, mas `TipoDocumentoFiscal`/`TipoTributo` já reservam extensão aditiva (§9) |
| Numeração implícita, sem autoridade explícita documentada | `SequenciaFiscal` com autoridade dedicada (série por terminal para NFC-e; `Store.Server` único para NF-e) — nunca CRDT, nunca "detectar e renumerar" pós-fato (§5) |
| `Invoice.status` como string livre, sem FSM formal | `StatusDocumentoFiscal` + transições explícitas, mesma disciplina de `Fsm<TStatus>` do resto do repo (§2.6) |
| Preflight "grava antes de chamar o gateway" (bom padrão, preservar) | `DocumentoFiscal` em `NumeroAlocado` é persistido ANTES de `Transmitir()` — mesmo racional, formalizado como estado da FSM em vez de um campo solto (§2.6, §5) |

### 11.1 Correções da revisão crítica (2ª passada) — achadas nesta própria primeira versão

| Lacuna/erro achado | Correção aplicada |
|---|---|
| `UsaCsosn()` agrupava `SimplesNacionalSublimite` com `SimplesNacional` — CRT=2 tributariamente exige CST (tabela B), não CSOSN | `UsaCsosn()` só retorna `true` para `Mei`/`SimplesNacional`; comentário do enum e nota dedicada explicam o porquê (§2.1) |
| `Origem da Mercadoria` (0-8, tag `<orig>`, obrigatória em todo item de ICMS) ausente do modelo inteiro | `OrigemMercadoria` + `PerfilFiscalNCM.Origem`/`TributacaoProduto.OrigemOverride`/`ItemDocumentoFiscal.Origem`; força alíquota interestadual de 4% p/ importados (Resolução Senado 13/2012) (§2.4, §2.6, §3) |
| DIFAL/FCP citados na prosa (§2.2) mas sem campo em `TipoTributo` nem passo no fluxo de resolução | `TipoTributo.IcmsDifal`/`Fcp` + passo 10 do fluxo, resolvidos pela mesma `RegraFiscalPorOperacao` chaveada por `UfDestino` (§2.6, §3) |
| `TributacaoProduto` não permitia override do CSOSN/CST de ICMS por produto — só `RegraFiscalPorOperacao` com `TenantId` (tenant inteiro, não por SKU) | `SituacaoTributariaIcmsOverride` + alíquota/redução/MVA override, consultado antes da matriz no passo 6 do fluxo (§2.5, §3) |
| `RegraCfop` não distingue produção própria (5101/6101) de revenda de terceiros (5102/6102) — os 2 CFOPs mais comuns do varejo BR | **FECHADO** — `NaturezaOperacaoProduto`/`CfopOverride` em `DadosFiscaisProduto` (Estoque) + `RegraCfop` (padrão-config) + override na emissão; cadeia `emissão > produto > padrão-config` via `IResolvedorDeCfop` (§2.3, §9) |
| `DocumentoFiscal` mudava `Status` com `if (Status is not (...))` solto por método — não usava `Fsm<TStatus>`, apesar da §11 (linha acima) alegar que usava | `TransicoesPermitidas` centralizado + `Fsm<StatusDocumentoFiscal>.ValidarTransicao` em todo método (§2.6) |
| `FiscalModule` único registrava handlers (Application) e `SqliteSequenciaFiscalRepository` (Infrastructure) — não compila no grafo `Infrastructure → Application → Domain` do repo; sem adapter in-memory para teste | Partido em `FiscalModule` (Application) + `FiscalInfrastructureModule` (Infrastructure, `DependeDe: ["fiscal"]`, switch InMemory/Sqlite) — mesmo padrão de `Estoque`/`Vendas`/`Financeiro` (§6) |
| `FiscalModule.DependeDe => ["estoque", "vendas"]` forçaria essas duas instalações sempre que Fiscal fosse habilitado, contradizendo "desabilitar módulo = zero superfície de falha" de `IModule` | `DependeDe` removido — assinar evento de integração não exige o módulo emissor presente, mesmo padrão que `EstoqueModule` já demonstra hoje (§6) |

---

## 12. Documentos relacionados

| Arquivo | Conteúdo |
|---|---|
| `docs/arquitetura/adr/0002-fiscal.md` | Decisão resumida (formato ADR) que este documento detalha |
| `docs/arquitetura/adr/0001-sincronizacao-local-first.md` | Fixa "numeração fiscal = alocação autoritativa, nunca CRDT" — premissa deste documento |
| `docs/arquitetura/ARCHITECTURE.md` | Regras de camada, IModule, evento de domínio vs integração — convenções que este módulo segue |
| `docs/arquitetura/COMO-CRIAR-UM-MODULO.md` | Passo a passo genérico de módulo — Fiscal segue à risca quando for implementado |
| `docs/robustez/robustez-hardware-licoes.md` §1 e §3 | Transação atômica local + distinção sale_number (renumerável) vs número fiscal (não-renumerável) |
| `docs/persistencia/persistencia-sqlite.md` | Convenção de migração/schema local que §8 segue |
