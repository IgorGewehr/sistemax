# SPED — EFD-ICMS/IPI e EFD-Contribuições no SistemaX (design-first, SDD)

> Stack-alvo: `src/Modules/Fiscal/{Domain,Application,Infrastructure}` (sub-área nova, `Sped/`),
> lendo dados que os módulos **Fiscal** e **Compras** já persistem — sem novo `IModule`. Mesmo
> padrão de `docs/fiscal/arquitetura.md` (design antes do código) e mesmo espírito de
> `docs/arquitetura/adr/0001-sincronizacao-local-first.md`/`0002-fiscal.md` (grounding contra um
> defeito real do `gestao-raiz`, decisão explícita do que muda, "por que NÃO" para as alternativas
> óbvias). **Status: design — nenhuma linha de `Sped/` existe ainda.** Este documento é o contrato
> antes da primeira linha.

**Grounding:** este design foi calibrado lendo `src/lib/fiscal/sped-generator.ts` (712 linhas),
`src/__tests__/sped-generator.test.ts` (738 linhas) e `src/services/accounting-export.service.ts`
do `gestao-raiz` — o único gerador de SPED que existe lá, em produção real. Achado central: o
gestao-raiz gera **um único arquivo** (EFD-ICMS/IPI) e resolve CST/CSOSN por **inferência a partir
do valor do imposto** (`inferCstIcms(icmsVal, regime)`, `inferCstPis`, `inferCstCofins`,
`inferCstIpi` — `sped-generator.ts:121-140`) porque o dado real já foi descartado rio acima
(`convertToFirestoreTaxes`, documentado em `docs/fiscal/arquitetura.md` linha 716). O Bloco E
(apuração) é **sempre zero, para todo tenant, em todo período** (`buildBlockE`,
`sped-generator.ts:537-549` — 14 campos monetários de `E110` são literais `'0,00'`). Não existe
EFD-Contribuições em nenhuma forma (`grep -ri "M100\|M200\|efd.contrib"` no repo não encontra
nada) — apesar de PIS/COFINS serem calculados por item (`tax-calculation.service.ts`) e
declarados no XML da NF-e, esse dado nunca vira o arquivo que a Receita realmente exige
mensalmente. As três seções abaixo (§2) detalham cada achado; o restante do documento é o desenho
do SistemaX que fecha os três.

---

## Pergunta que este documento responde

> "Como o SistemaX gera SPED (EFD-ICMS/IPI e EFD-Contribuições) a partir dos dados que Vendas,
> Compras, Estoque e Fiscal já produzem — sem reintroduzir o defeito do gestao-raiz de reinferir
> CST/CSOSN e sem inventar uma apuração fiscal (Bloco E) que hoje é zero mudo?"

**Resposta curta:** O SistemaX **já tem** o dado que o SPED de saída precisa — `TributoResolvidoItem`
grava CST/CSOSN real desde a resolução (`ItemDocumentoFiscal`, ver `docs/fiscal/arquitetura.md`
§2.6), então o gerador de SPED **lê**, nunca reinfere. O que falta é (1) o **gerador em si**
(zero linhas hoje), (2) um **gap real em Compras** — `ItemDeNotaDeCompra` não captura CST/base/
alíquota de ICMS "normal" nem PIS/COFINS por item, dado que o SPED de entrada (documentos
recebidos de fornecedor) precisa e que o gestao-raiz, apesar dos outros defeitos, **captura
corretamente** (`PurchaseNote.items[].taxes`) — e (3) uma **apuração real** de Bloco E (débito −
crédito ± saldo anterior), porque emitir zero para um tenant que efetivamente deve ICMS é pior do
que não emitir nada. EFD-Contribuições é **um arquivo e um design separados** (registros e blocos
diferentes; regimes diferentes ficam dispensados dele), nunca uma extensão do gerador de
EFD-ICMS/IPI.

---

## 1. Dois arquivos, uma fonte de dado — nunca dois motores

EFD-ICMS/IPI (Ato COTEPE/ICMS, obrigação estadual) e EFD-Contribuições (IN RFB 1.252/2012,
obrigação federal de PIS/COFINS/CPRB) são **arquivos textuais distintos**, com registros, blocos e
periodicidade de envio diferentes — mas **a mesma fonte de verdade** para o dado transacional: o
`DocumentoFiscal`/`ItemDocumentoFiscal` que o Fiscal já resolveu e persistiu para a venda, e a
`NotaDeCompra`/`ItemDeNotaDeCompra` que o Compras já persistiu para a entrada. Nenhum dos dois
motores de geração de arquivo **recalcula** tributo — o cálculo já aconteceu (na emissão da nota,
ou na captura da nota do fornecedor); gerar SPED é **projeção read-only** desse dado já resolvido
para um layout textual diferente, exatamente como um read-model do Financeiro é uma projeção do
log de eventos (ADR-0001 item 3). Isto é a mesma disciplina de "um único motor, nunca dois" da
decisão #4 do ADR-0002, aplicada aqui: **um único par (Domain function pura → Application use
case) por arquivo**, nunca um segundo caminho que poderia divergir do primeiro.

Regra dura desta seção, para nunca repetir o erro do gestao-raiz: **o gerador de SPED nunca infere
CST/CSOSN a partir do VALOR do imposto.** Se um item não tem `TributoResolvidoItem.SituacaoTributaria`
(saída) ou o correspondente capturado do XML de compra (entrada), o gerador **falha nomeado**
(`sped.item.situacao_tributaria_ausente`) e lista o documento/item pendente — nunca substitui por
um código "razoável" calculado a partir do valor. Mesma régua de `Result.Falhar` do
`MotorDeCalculoTributario` (docs/fiscal/arquitetura.md §3), aplicada à exportação em vez de à
emissão.

---

## 2. O que o `gestao-raiz` faz — grounding factual

| Achado | Onde | Como o SistemaX difere |
|---|---|---|
| Gera só EFD-ICMS/IPI; EFD-Contribuições nunca existiu | `services/accounting-export.service.ts:674-710` monta só `spedContent`/`spedFilename` do ICMS/IPI | Dois geradores desde o design (§7) |
| COD_VER (versão de leiaute) resolvido por ANO do período — 015/2024, 019/2025, 020/2026 | `resolveLayoutVersion(year)`, `sped-generator.ts:100-104` | Reaproveitado tal qual — é a única peça 100% correta e "dado, não hardcode" (§8) |
| IND_PERFIL (A/B/C) inferido do regime, com override explícito por tenant | `resolveIndPerfil`, `sped-generator.ts:114-119` | Reaproveitado — mapeia 1:1 em `RegimeTributario.UsaCsosn()`/`Crt()` já existentes |
| CST/CSOSN de ICMS/PIS/COFINS/IPI **reinferidos do valor do imposto** (`icmsVal > 0 ? '000' : '040'`) | `inferCstIcms`/`inferCstPis`/`inferCstCofins`/`inferCstIpi`, `sped-generator.ts:121-140` | **Nunca reinfere** — lê `TributoResolvidoItem.SituacaoTributaria` gravado na emissão (§3) |
| Perfil C (Simples) nunca gera C170 em nenhum documento — só C100/C190 agregado | `buildBlockC`, `isPerfilC` guards em `sped-generator.ts:257-524` | Mesma regra — `RegimeTributario.UsaCsosn()` já decide isso no Fiscal (§3) |
| NFC-e (mod. 65) nunca gera C170 nem tem COD_PART; campos de ST/IPI/PIS/COFINS vazios (proibido no layout) | `sped-generator.ts:262-320` (`isNfce` guards) | Mesma regra — layout federal fechado, não muda por tenant |
| Bloco E — **débito, crédito e saldo sempre zero**, para todo tenant/período | `buildBlockE`, `sped-generator.ts:537-549`, 14 campos monetários literais `'0,00'` | Apuração real a partir de `DocumentoFiscal`/entrada, com "nunca saldo anterior mudo" (§6) — **maior melhoria estrutural deste design** |
| Bloco E nunca abre `E200` (apuração de IPI), embora `IND_ATIV=1` (industrial) seja o default do tenant | Ausente de `buildBlockE` inteiro | Documentado como gap explícito, fase 2 (§10) — nunca declarar `IND_ATIV=1` sem a apuração correspondente é uma inconsistência que uma fiscalização notaria |
| Entrada (compra) carrega CST/base/alíquota/valor de ICMS **e** PIS/COFINS por item, capturados do XML | `PurchaseNote.items[].taxes.{icms,ipi,pis,cofins}` (fixture em `sped-generator.test.ts:159-175`) | **Não existe ainda no SistemaX** — `ItemDeNotaDeCompra` (Compras) não captura isso (§4); é regressão a fechar, não a repetir |
| Contagem cruzada de registros (Bloco 9 / `9900`) sempre bate com o arquivo real, inclusive vazio | `buildBlock9`, testado exaustivamente em `sped-generator.test.ts:632-695` | Reaproveitado tal qual — é a peça de maior valor de teste do gestao-raiz (§11) |
| CFOP de entrada nunca vazio — fallback `5102`/normalização 5xxx→1xxx / 6xxx→2xxx | `normalizeCfopForEntry`, `sped-generator.ts:142-147` | Não precisa de fallback — `IResolvedorDeCfop` (Fiscal) já resolve com falha nomeada em vez de fallback mudo (ADR-0002 decisão adicional); Compras herda o mesmo princípio (§4) |

---

## 3. O que já está pronto — Fiscal alimenta a saída (Bloco C) sem nada novo

O agregado `DocumentoFiscal` (`src/Modules/Fiscal/SistemaX.Modules.Fiscal.Domain/Documentos/`)
já persiste, por item, exatamente o que `C170`/`C190` precisam — porque `ItemDocumentoFiscal`
existe justamente para fechar o gap de reinferência do gestao-raiz (docs/fiscal/arquitetura.md
§2.6):

```csharp
// Já implementado — SistemaX.Modules.Fiscal.Domain.Documentos
public sealed record ItemDocumentoFiscal(
    string ProdutoId, string Descricao, string Ncm, string? Cest,
    OrigemMercadoria Origem, string Cfop, Quantidade Quantidade,
    Money PrecoUnitario, Money Desconto,
    IReadOnlyList<TributoResolvidoItem> Tributos);

public sealed record TributoResolvidoItem(
    TipoTributo Tipo, string? SituacaoTributaria, Money Base,
    Percentual Aliquota, Money Valor,
    Percentual? ReducaoBaseCalculo = null, Percentual? Mva = null);
```

Mapa direto (Fiscal → EFD-ICMS/IPI, saída):

| Campo SPED | Registro | Fonte no Fiscal |
|---|---|---|
| `COD_MOD` (55/65) | C100 | `DocumentoFiscal.Tipo` (`NFe`→55, `NFCe`→65) |
| `COD_SIT` (00/02) | C100 | `DocumentoFiscal.Status` (`Autorizado`→00, `Cancelado`→02) |
| `SER`/`NUM_DOC`/`CHV_NFE` | C100 | `Serie`/`Numero`/`ChaveDeAcesso` |
| `VL_DOC` | C100 | `DocumentoFiscal.Total` (soma de `ItemDocumentoFiscal.Subtotal`) |
| `COD_ITEM`/`UNID`/`QTD` | C170 | `ProdutoId`/`Quantidade` (`Quantidade.EmDecimal`) |
| `CST_ICMS`/`CSOSN` | C170 | `Tributos.First(t => t.Tipo == Icms).SituacaoTributaria` — **real, não inferido** |
| `VL_BC_ICMS`/`ALIQ_ICMS`/`VL_ICMS` | C170 | `Base`/`Aliquota.EmFracao*100`/`Valor` do `TributoResolvidoItem(Icms)` |
| `VL_BC_ICMS_ST`/`VL_ICMS_ST` | C170 | idem, `TributoResolvidoItem(IcmsSt)` — ausente do bag quando não há ST (nunca "0,00" fabricado, ver §3 de `MotorDeCalculoTributario`) |
| `CST_IPI`/`VL_BC_IPI`/`ALIQ_IPI`/`VL_IPI` | C170 | `TributoResolvidoItem(Ipi)` |
| `CST_PIS`/`VL_BC_PIS`/`ALIQ_PIS`/`VL_PIS` | C170 | `TributoResolvidoItem(Pis)` |
| `CST_COFINS`/análogos | C170 | `TributoResolvidoItem(Cofins)` |
| `CFOP` | C170/C190 | `ItemDocumentoFiscal.Cfop` — já resolvido por `IResolvedorDeCfop` na emissão (nunca recalculado aqui) |
| Agrupamento `C190` (CST, CFOP, alíquota) | C190 | Chave composta idêntica à do gestao-raiz (`sped-generator.ts:378`), só que a `SituacaoTributaria` agrupada é a real, não a reinferida |

**Consulta de origem:** `IDocumentoFiscalRepository.ObterPorOrigemAsync`/uma nova query por
período (`ListarAutorizadosOuCanceladosNoPeriodoAsync(tenantId, dtIni, dtFin)`, a acrescentar ao
port — mesmo estilo de `ListarNumeroAlocadoAntesDeAsync` já existente) é **toda a integração
necessária do lado de Vendas/PDV**: o gerador de SPED nunca lê `Venda`/`ItemDeVenda` diretamente
(regra de fronteira — Fiscal já não referencia `Vendas.Domain`, docs/fiscal/arquitetura.md §7);
ele lê o `DocumentoFiscal` que o `VendaItensMovimentadosHandler` já materializou. Zero evento novo
do lado de Vendas.

---

## 4. O gap real — Compras não captura tributo de entrada por item

SPED de **entrada** (`C100` com `IND_OPER=0`, documento recebido de fornecedor) precisa do mesmo
nível de detalhe por item que a saída — CST/CSOSN, base, alíquota e valor de ICMS **e** PIS/COFINS,
além de CFOP. O gestao-raiz captura isso corretamente
(`PurchaseNote.items[].taxes.{icms,ipi,pis,cofins}.{cst,baseCalc,rate,value}`, confirmado pelo
fixture de teste `sped-generator.test.ts:168-174`) — é uma das poucas partes do módulo fiscal de
lá auditadas como **certas**. O SistemaX, hoje, **não tem esse dado**:

```csharp
// ATUAL — SistemaX.Modules.Compras.Domain.Notas.ItemDeNotaDeCompra
// (src/Modules/Compras/SistemaX.Modules.Compras.Domain/Notas/ItemDeNotaDeCompra.cs:22-36)
public string? Ncm { get; init; }
public Money VProd { get; init; }
public Money VIpi { get; init; }       // total de IPI da linha — sem CST/base/alíquota
public Money VIcmsSt { get; init; }    // só ICMS-ST — SEM o ICMS "normal" (base/alíquota/valor/CST)
// PIS/COFINS por item: AUSENTE. CFOP por item: AUSENTE.
```

Isto **não é** um defeito do desenho atual de Compras — `ItemDeNotaDeCompra` foi desenhado para o
que Compras precisa hoje (rateio de custo de entrada, `CustoDeEntrada.Ratear`, que só usa
`VProd`/`VFrete`/`VSeguro`/`VOutro`/`VIpi`/`VIcmsSt`). É um gap **novo, do SPED**, que este
documento tem que nomear em vez de esconder atrás de um `0,00` (a mesma escolha que o gestao-raiz
fez errado no Bloco E). Correção proposta — **objeto de valor aditivo**, capturado verbatim do XML
no momento de `NotaDeCompra.Importar` (nunca calculado; é o mesmo racional de
"`OrigemMercadoria` nunca inferida" do ADR-0002 §2.4, aplicado à entrada):

```csharp
// PROPOSTO — SistemaX.Modules.Compras.Domain.Notas.ImpostoRecebidoItem
// Campo aditivo de ItemDeNotaDeCompra: `public ImpostoRecebidoItem? Impostos { get; init; }`
// Nullable porque nota Manual (sem XML) pode não ter esse detalhe — SPED trata ausência como
// "sem C170 para esta linha" (mesma regra de "falha nomeada, nunca 0,00 fabricado" do §1).
public sealed record ImpostoRecebidoItem(
    string Cfop,
    string? SituacaoTributariaIcms,   // CST OU CSOSN, como veio do XML do fornecedor — nunca resolvido pelo motor do Fiscal (o Fiscal calcula SAÍDA; entrada é o que o FORNECEDOR já calculou)
    Money BaseIcms, Percentual AliquotaIcms, Money ValorIcms,
    Money BaseIcmsSt, Percentual AliquotaIcmsSt,  // ValorIcmsSt já existe (VIcmsSt) — não duplicar
    string? CstIpi, Money BaseIpi, Percentual AliquotaIpi,   // ValorIpi já existe (VIpi)
    string? CstPis, Money BasePis, Percentual AliquotaPis, Money ValorPis,
    string? CstCofins, Money BaseCofins, Percentual AliquotaCofins, Money ValorCofins);
```

Nota de fronteira: `ImpostoRecebidoItem` vive em `Compras.Domain`, **não** em `Fiscal.Domain` —
é dado que o **fornecedor** calculou e o XML carrega, capturado no parse da nota, o mesmo lugar
onde `Ncm`/`VProd`/`VIpi` já são capturados hoje. O Fiscal nunca recalcula imposto de entrada (não
é dele o cálculo — só o de saída, via `MotorDeCalculoTributario`); o Fiscal só **lê** esse dado já
pronto para montar `C170` de entrada, exatamente como lê `NcmPorProduto` do Estoque hoje sem
recalcular NCM.

---

## 5. Como pluga no modular monolith

### 5.1 Ponte Compras → Fiscal — mesmo padrão de `ProdutoFiscalAtualizado`

`Fiscal.Application` já mantém uma cópia local denormalizada de dado de outro módulo
(`NcmPorProduto`/`fiscal_dados_produto_cache`, alimentada por `ProdutoFiscalAtualizado(EmLote)` do
Estoque — docs/fiscal/arquitetura.md §4). A mesma receita se aplica a Compras: um evento de
integração NOVO, companion de `CompraItensRecebidos` (que continua "pobre" — Estoque/Financeiro
não precisam do detalhe fiscal), carregando o que §4 propõe:

```csharp
// PROPOSTO — Modules.Abstractions/IntegrationEvents.cs (aditivo, catálogo Fiscal)
/// <summary>Companion de CompraItensRecebidos com o detalhe fiscal por item (CST/CSOSN/base/
/// alíquota/valor de ICMS/PIS/COFINS + CFOP), capturado do XML do fornecedor em Compras — o
/// Fiscal só CONSOME para montar C100/C170 de entrada do SPED, nunca recalcula (§4).</summary>
public sealed record CompraFiscalRecebida(
    string CompraId, string TenantId, string FornecedorId, DateTimeOffset DataEmissao,
    string Numero, string Serie, string? ChaveDeAcesso,
    IReadOnlyList<ItemFiscalRecebido> Itens, DateTimeOffset OcorridoEm) : IIntegrationEvent
{
    public string ChaveIdempotencia => $"compra.fiscal:{CompraId}";
}

public sealed record ItemFiscalRecebido(
    string ProdutoId, string? Ncm, string Cfop, string? SituacaoTributariaIcms,
    long BaseIcmsCentavos, long AliquotaIcmsMilionesimos, long ValorIcmsCentavos,
    long ValorIcmsStCentavos, string? CstIpi, long ValorIpiCentavos,
    string? CstPis, long ValorPisCentavos, string? CstCofins, long ValorCofinsCentavos);
```

`NotaDeCompra.ConfirmarRecebimento` publica os **três** eventos lado a lado a partir do mesmo
`NotaDeCompraRecebidaDomainEvent` (`CompraRecebida` para Financeiro, `CompraItensRecebidos` para
Estoque, `CompraFiscalRecebida` para Fiscal) — mesmo desenho "um fato, N eventos publicados lado a
lado, pós-commit" que `VendaConcluidaDomainEvent`/`NotaDeCompraRecebidaDomainEvent` já demonstram
(docs/fiscal/arquitetura.md §6). `Fiscal.Application` ganha
`EventosDeIntegracao/Handlers/CompraFiscalRecebidaHandler.cs` (idempotente por
`ChaveIdempotencia`, mesmo gesto de `VendaItensMovimentadosHandler`), que persiste em
`fiscal_compras_recebidas_cache` (nova tabela, `FiscalSchemaMigrationV2`):

```sql
CREATE TABLE IF NOT EXISTS fiscal_compras_recebidas_cache (
    compra_id        TEXT PRIMARY KEY,
    tenant_id        TEXT NOT NULL,
    fornecedor_id    TEXT NOT NULL,
    data_emissao     INTEGER NOT NULL,
    numero           TEXT NOT NULL,
    serie            TEXT NOT NULL,
    chave_acesso     TEXT
);
CREATE TABLE IF NOT EXISTS fiscal_itens_compra_recebida_cache (
    compra_id TEXT NOT NULL REFERENCES fiscal_compras_recebidas_cache(compra_id) ON DELETE CASCADE,
    produto_id TEXT NOT NULL, ncm TEXT, cfop TEXT NOT NULL,
    situacao_tributaria_icms TEXT,
    base_icms_centavos INTEGER, aliquota_icms_milionesimos INTEGER, valor_icms_centavos INTEGER,
    valor_icms_st_centavos INTEGER,
    cst_ipi TEXT, valor_ipi_centavos INTEGER,
    cst_pis TEXT, valor_pis_centavos INTEGER,
    cst_cofins TEXT, valor_cofins_centavos INTEGER
);
CREATE INDEX IF NOT EXISTS ix_fiscal_itens_compra_recebida_cache_compra
    ON fiscal_itens_compra_recebida_cache (compra_id);
```

`FiscalModule.DependeDe` continua **sem** listar `"compras"` — mesma régua já fixada no ADR-0002
(§6): assinar um evento de integração não exige o módulo emissor fisicamente presente. Instalação
sem Compras habilitado → cache de entrada fica vazio → SPED sai sem `C100`/`C170` de entrada
(arquivo ainda válido, só sem NF-e de compra) — zero superfície de falha nova.

### 5.2 Onde o gerador de SPED vive — sub-área de `Fiscal`, não `IModule` novo

SPED **não** é um agregado com ciclo de vida (não tem FSM, não tem invariante de negócio própria
além de "não inventar dado ausente") — é uma **exportação read-only** sobre dado que `Fiscal` e o
cache de `Compras` já persistem. Não justifica um módulo novo (`SistemaX.Modules.Sped.*`) só para
um relatório textual; vive como sub-área de `Fiscal`, mesmo argumento que já vale para
`ResolvedorDeCfop` estar em `Fiscal.Application` em vez de módulo próprio:

```
src/Modules/Fiscal/SistemaX.Modules.Fiscal.Domain/
  Sped/
    Comum/SpedRecordWriter.cs        (função pura — join por '|' + CRLF, mesmo r(...) do gestao-raiz)
    Comum/SpedFormatadores.cs        (fv/fd/str — Money→"0,00" BR, Date→DDMMYYYY, truncar+upper)
    EfdIcmsIpi/BlocosBuilder.cs      (Bloco0/BlocoC/BlocoE/Bloco1/Bloco9 — funções puras, sem I/O)
    EfdContribuicoes/BlocosBuilder.cs
    ApuracaoIcmsMensal.cs            (§6 — o registro de saldo anterior/apurado)

src/Modules/Fiscal/SistemaX.Modules.Fiscal.Application/
  Sped/
    CasosDeUso/GerarSpedEfdIcmsIpiUseCase.cs
    CasosDeUso/GerarSpedEfdContribuicoesUseCase.cs
    CasosDeUso/ConfirmarApuracaoIcmsUseCase.cs   (§6 — fecha o período, nunca automático)
    Ports/IApuracaoIcmsMensalRepository.cs
    Ports/IComprasRecebidasParaSpedRepository.cs

src/Modules/Fiscal/SistemaX.Modules.Fiscal.Infrastructure/
  Sqlite/SqliteApuracaoIcmsMensalRepository.cs
  Sqlite/SqliteComprasRecebidasParaSpedRepository.cs   (lê o cache de §5.1)
  EventosDeIntegracao/Handlers/CompraFiscalRecebidaHandler.cs (registrado em FiscalModule.Application, mesma régua de VendaItensMovimentadosHandler)
```

O builder de blocos (`Fiscal.Domain.Sped.*`) é **função pura** — recebe listas de
`ItemDocumentoFiscal`/cache de entrada/`ApuracaoIcmsMensal` já carregadas pela Application e
devolve `string[]` de linhas, exatamente como `MotorDeCalculoTributario` é puro e testável sem
banco. `GerarSpedEfdIcmsIpiUseCase` (Application) é só orquestração: busca dado nos três
repositórios, chama o builder, escreve o arquivo (via porta de armazenamento local já existente no
repo, não nova).

---

## 6. Bloco E — apuração real, nunca zero mudo

O gestao-raiz emite `E110` com os 14 campos monetários zerados para **todo** tenant e período —
inclusive Lucro Presumido/Real, que efetivamente apuram e recolhem ICMS mensalmente. Um arquivo
SPED com apuração zerada para um contribuinte que deve imposto é um **risco de autuação real**,
não um "falta polimento" — é a mesma classe de defeito que motivou a regra "nunca CSOSN 400
mudo" do ADR-0002: **um zero fabricado é pior que a ausência do dado**, porque parece uma resposta
válida.

Correção: `E110` (Perfil A/B) é **calculado** a partir do que já existe —

```
Débito do período   = Σ VL_ICMS de C190 SAÍDA (agrupado, já calculado em §3)
Crédito do período   = Σ VL_ICMS de C170/C190 ENTRADA elegível a crédito (regra por regime —
                        Simples Nacional/MEI não aproveitam crédito da mesma forma; a elegibilidade
                        é uma linha de RegraFiscalPorOperacao/config, não um `if` solto aqui)
Saldo Credor Anterior = ApuracaoIcmsMensal do período ANTERIOR (mesmo tenant), campo
                        SaldoApuradoCentavos quando negativo (credor) — OU confirmação manual se
                        for a primeira apuração do tenant no sistema
Saldo Apurado         = SaldoCredorAnterior + Débito − Crédito
```

`ApuracaoIcmsMensal` é o registro que fecha a lacuna — chave (`TenantId`, `Ano`, `Mes`), nunca
sobrescrito automaticamente sem confirmação explícita (mesma disciplina de "documento autorizado é
imutável" — aqui, "apuração confirmada é imutável", corrigir é uma linha NOVA de período, não um
`UPDATE`):

```csharp
// PROPOSTO — SistemaX.Modules.Fiscal.Domain.Sped.ApuracaoIcmsMensal
public sealed record ApuracaoIcmsMensal(
    string TenantId, int Ano, int Mes,
    Money SaldoCredorAnterior, Money DebitoTotal, Money CreditoTotal, Money SaldoApurado,
    bool Confirmada, string? ConfirmadaPorId, DateTimeOffset? ConfirmadaEm)
{
    /// <summary>Regra dura: gerar o SPED de um período cujo ANTERIOR nunca foi confirmado
    /// (`Confirmada == false`) é bloqueado — nunca assume saldo anterior = 0 silenciosamente
    /// (mesma régua de `Result.Falhar` nunca-default-mudo do MotorDeCalculoTributario). A
    /// primeira apuração de um tenant novo no sistema exige UMA confirmação manual explícita de
    /// abertura (`SaldoCredorAnterior` = o que o contador informar, inclusive zero — mas
    /// DECLARADO, não assumido) via `ConfirmarApuracaoIcmsUseCase`.</summary>
    public static Result<ApuracaoIcmsMensal> Calcular(
        string tenantId, int ano, int mes, Money saldoCredorAnterior, Money debitoTotal, Money creditoTotal)
    {
        var saldo = Money.DeReais(saldoCredorAnterior.EmReais + debitoTotal.EmReais - creditoTotal.EmReais);
        return Result.Ok(new ApuracaoIcmsMensal(tenantId, ano, mes, saldoCredorAnterior, debitoTotal,
            creditoTotal, saldo, Confirmada: false, ConfirmadaPorId: null, ConfirmadaEm: null));
    }
}
```

`GerarSpedEfdIcmsIpiUseCase`, ao montar Bloco E: busca `ApuracaoIcmsMensal(tenantId, ano, mês-1)`;
se não existir e `mês-1` for anterior ao primeiro documento fiscal do tenant no sistema, trata como
"tenant novo" e **exige** que `ConfirmarApuracaoIcmsUseCase` tenha sido chamado antes (saldo de
abertura, ainda que zero) — se não foi, `Result.Falhar("sped.apuracao.saldo_anterior_nao_confirmado")`,
e o SPED **não é gerado** até o contador confirmar. Isto é deliberadamente mais rígido que o
gestao-raiz (que gera sempre, com zero) — é a mesma troca já aceita em ADR-0001/0002: mais
disciplina de processo do que "sempre gera algo", porque aqui "algo" pode estar legalmente errado.

Para Perfil C (Simples Nacional/MEI): `E110` continua sendo emitido com os 14 campos zerados —
isso **é** correto (ICMS não é apurado pelo regime normal, é recolhido embutido no DAS), o
gestao-raiz acerta esse caso especificamente; o SistemaX preserva essa regra via
`RegimeTributario.UsaCsosn()` (mesmo booleano que já decide CSOSN vs CST).

`E200`/apuração de IPI: fora da Fase 1 (§10) — `IND_ATIV=1` (industrial, já no registro `0000`)
sem `E200` correspondente é uma inconsistência que o design reconhece e adia, nunca esconde.

---

## 7. EFD-Contribuições — arquivo separado, dispensa por regime

Layout e periodicidade diferentes de EFD-ICMS/IPI (IN RFB 1.252/2012): Bloco 0 (abertura,
registros próprios — `0000`/`0001`/`0100`/`0110`/`0140`/`0150`/`0190`/`0200`, parcialmente
equivalentes mas **não** intercambiáveis com os do EFD-ICMS/IPI), Bloco C (`C100`/`C170`/`C180`/
`C181`/`C185`/`C190` — campos de PIS/COFINS por item, não de ICMS/IPI), Bloco M (apuração —
`M200`/`M210` para PIS, `M600`/`M610` para COFINS, regime cumulativo; `M100`/`M105` para créditos
do regime não-cumulativo), Bloco F (receitas/despesas não classificadas em C/D), Bloco P (CPRB,
quando aplicável), Bloco 1 (complementares), Bloco 9 (fechamento — mesmo princípio de contagem
cruzada do §11).

**Dispensa por regime — fato legal, não decisão de produto:** Simples Nacional (inclusive
`SimplesNacionalSublimite`, cujo PIS/COFINS continua embutido no DAS mesmo com ICMS/ISS "por
fora" — ver `RegimeTributarioExtensions.UsaCsosn`/nota do ADR-0002 §2.1) e MEI **não entregam**
EFD-Contribuições (IN RFB 1.252/2012, art. 5º §3º). Só Lucro Presumido/Real são obrigados.
Proposta de extensão simétrica a `UsaCsosn()`:

```csharp
// PROPOSTO — RegimeTributarioExtensions (aditivo)
/// <summary>Fato legal fechado (IN RFB 1.252/2012 art. 5º §3º), mesma classe de "hardcode
/// aceitável" que Crt()/UsaCsosn() — nunca dado editável por tenant.</summary>
public static bool DispensadoDeEfdContribuicoes(this RegimeTributario regime) =>
    regime is RegimeTributario.Mei or RegimeTributario.SimplesNacional or RegimeTributario.SimplesNacionalSublimite;
```

`GerarSpedEfdContribuicoesUseCase.ExecutarAsync` checa isso **primeiro** — para um tenant
dispensado, retorna `Result.Falhar("sped.contribuicoes.regime_dispensado")` em vez de gerar um
arquivo vazio (mesma régua de nunca produzir artefato sem sentido: um arquivo "vazio mas válido"
convida a ser enviado por engano).

### Fase 1 do EFD-Contribuições — só o caminho comum, cumulativo

Dado o tamanho do layout completo, a Fase 1 cobre **só** o cenário mais comum do público-alvo do
SistemaX — Lucro Presumido, regime **cumulativo** de PIS/COFINS (0,65%/3,0% sobre a receita, sem
apropriação de crédito): Bloco 0 mínimo (abertura + participantes + itens, reaproveitando a mesma
extração de `DocumentoFiscal`/cache de compra do EFD-ICMS/IPI), Bloco C (`C100`/`C170` de
mercadoria — usa os MESMOS `TributoResolvidoItem(Pis)`/`(Cofins)` já gravados, nenhum cálculo
novo), `M200`/`M210`/`M600`/`M610` (apuração cumulativa simples — débito do período agregado de
`C170`/`C190` equivalente, sem crédito, porque regime cumulativo não aproveita), Bloco 1 mínimo,
Bloco 9. Lucro Real (regime não-cumulativo, com créditos `M100`/`M105`), Bloco F, Bloco P (CPRB) e
Bloco A (serviços/NFS-e, que nem existe ainda no Fiscal — TipoDocumentoFiscal.NFSe é reserva não
operante) ficam para Fase 2 (§10) — `RegimeTributario.LucroReal` já está preparado no enum sem
nenhuma linha semeada (docs/fiscal/arquitetura.md §9), mesma situação de EFD-ICMS/IPI.

---

## 8. Formatação — funções puras, mesmo formato do gestao-raiz (peça correta, reaproveitada)

O formato textual do SPED (pipe-delimited, CRLF, vírgula decimal, DDMMYYYY) é um fato fechado do
layout federal — não há "melhoria" possível aqui, só reaproveitar o que o gestao-raiz já acerta,
adaptado aos tipos do SistemaX (`Money`/`Percentual`/`Quantidade` em vez de `number` cru):

```csharp
namespace SistemaX.Modules.Fiscal.Domain.Sped.Comum;

/// <summary>Funções puras de formatação SPED — nenhuma tem I/O, todas são testáveis por valor.
/// Espelham fv/fd/str/r do gestao-raiz (sped-generator.ts:56-88), adaptadas para nunca aceitar
/// double/string cru onde o SistemaX já tem um tipo com invariante (Money/Percentual).</summary>
public static class SpedFormatadores
{
    /// <summary>Money → "1234,56" (nunca "1234.56") — decimal.ToString com CultureInfo pt-BR
    /// fixo, não a cultura do processo (mesma razão de Percentual.Formatado/Quantidade.Formatado
    /// já usarem CultureInfo explícito).</summary>
    public static string Fv(Money valor) => valor.EmReais.ToString("0.00", CULTURA_PT_BR).Replace('.', ',');

    /// <summary>Percentual → "12,00" (fração × 100, vírgula BR) — nunca Milionesimos cru.</summary>
    public static string Fv(Percentual aliquota) => (aliquota.EmFracao * 100).ToString("0.00", CULTURA_PT_BR).Replace('.', ',');

    public static string Fd(DateOnly data) => data.ToString("ddMMyyyy", CULTURA_PT_BR);

    /// <summary>Trunca e maiuscula — mesmo contrato de str() do gestao-raiz.</summary>
    public static string Str(string? valor, int max = 60) => (valor ?? "").ToUpperInvariant()[..Math.Min((valor ?? "").Length, max)];

    private static readonly CultureInfo CULTURA_PT_BR = CultureInfo.GetCultureInfo("pt-BR");
}

/// <summary>Monta uma linha de registro SPED — join por '|' com CRLF, mesmo contrato de r(...)
/// do gestao-raiz (sped-generator.ts:81-83), delimitadores nas duas pontas.</summary>
public static class SpedRecordWriter
{
    public static string Registro(params string?[] campos) =>
        "|" + string.Join('|', campos.Select(c => c ?? "")) + "|\r\n";
}
```

`resolveLayoutVersion(year)`/`resolveIndPerfil(tenant)` do gestao-raiz (`sped-generator.ts:100-119`)
são **reaproveitados quase tal qual** — são as duas peças do gerador de lá que já seguem a
disciplina certa (dado por ano/regime, não `const` fixo), só traduzidas para `RegimeTributario`/
`RegimeTributarioExtensions` já existentes em vez de `TaxRegime` string do TS.

---

## 9. Fase 1 — blocos/registros concretos

| Arquivo | Bloco/Registro | Conteúdo | Fonte |
|---|---|---|---|
| EFD-ICMS/IPI | `0000`/`0001`/`0005`/`0100` | Abertura, período, empresa, contabilista (vazio, como hoje) | `ConfiguracaoFiscalTenant` |
| EFD-ICMS/IPI | `0150` | Participantes (destinatário NF-e não-NFC-e + fornecedor) | `DocumentoFiscal`/`Compras.Fornecedor` |
| EFD-ICMS/IPI | `0190`/`0200` | Unidades e itens referenciados em `C170` | `ItemDocumentoFiscal`/cache de entrada |
| EFD-ICMS/IPI | `C001`/`C100`/`C170`/`C190`/`C990` — saída | NF-e/NFC-e autorizadas/canceladas do período | `IDocumentoFiscalRepository` (§3) |
| EFD-ICMS/IPI | `C001`/`C100`/`C170`/`C190`/`C990` — entrada | Notas de compra recebidas do período | `fiscal_compras_recebidas_cache` (§5.1) |
| EFD-ICMS/IPI | `B001`/`B990`, `D001`/`D990`, `G001`/`G990`, `H001`/`H990`, `K001`/`K990` | Abertura/fechamento vazios (mesma regra do gestao-raiz — não usados nesta fase) | — |
| EFD-ICMS/IPI | `E001`/`E100`/`E110`/`E990` | Apuração REAL de ICMS (débito/crédito/saldo), com bloqueio se saldo anterior não confirmado | `ApuracaoIcmsMensal` (§6) |
| EFD-ICMS/IPI | `1001`/`1010`/`1990` | Indicadores complementares (leiaute ≥020) | Config estática por leiaute |
| EFD-ICMS/IPI | `9001`.. `9999` | Contagem cruzada de todo o arquivo | `SpedBloco9Builder` (idêntico ao `buildBlock9` do gestao-raiz) |
| EFD-Contribuições | `0000`/`0001`/`0100`/`0110`/`0140`/`0150`/`0190`/`0200` | Abertura + cadastros (regime cumulativo, código de incidência tributária) | `ConfiguracaoFiscalTenant` + mesma extração de itens/participantes |
| EFD-Contribuições | `C001`/`C100`/`C170`/`C190`(análogo)/`C990` — mercadoria | Mesmos documentos de saída, campos de PIS/COFINS | `TributoResolvidoItem(Pis)`/`(Cofins)` |
| EFD-Contribuições | `M001`/`M200`/`M210`/`M600`/`M610`/`M990` | Apuração cumulativa (débito agregado, sem crédito) | Agregação de `C170`/`C190` do próprio arquivo |
| EFD-Contribuições | `1001`.. fechamento mínimo, `9001`..`9999` | Complementares + contagem cruzada | Igual ao EFD-ICMS/IPI |

---

## 10. Fase 2 — explicitamente adiado

| Item | Por que fica de fora agora |
|---|---|
| `E200`/`E210`.. (apuração de IPI) | Exige regra de crédito de IPI por insumo (industrial) — modelo de crédito ainda não desenhado no Fiscal |
| Saldo credor anterior calculado automaticamente entre períodos sem confirmação | Fase 1 exige confirmação manual explícita (§6) — automatizar o rollover é seguro só depois de meses de uso real validando o cálculo |
| Regra de elegibilidade de crédito de ICMS por regime (quais entradas geram crédito) | Hoje é citado em prosa no cálculo de "Crédito do período" (§6) — precisa virar linha de `RegraFiscalPorOperacao`/config nova antes de Fase 1 fechar de verdade; até lá, Fase 1 assume crédito integral de toda entrada tributada e marca isso explicitamente no relatório de conferência (não no arquivo SPED, que não tem campo para "assumido") |
| EFD-Contribuições — regime não-cumulativo (`M100`/`M105`, créditos) | Só Lucro Real usa; `RegimeTributario.LucroReal` ainda sem linha semeada em nenhuma tabela do Fiscal |
| EFD-Contribuições — Bloco F (receitas/despesas fora de C/D) | Sem fonte de dado modelada ainda (Financeiro não classifica lançamentos nessa taxonomia) |
| EFD-Contribuições — Bloco P (CPRB) | Regime de desoneração da folha — não aplicável ao público-alvo inicial do SistemaX |
| EFD-Contribuições/EFD-ICMS-IPI — Bloco A (NFS-e/serviços) | `TipoDocumentoFiscal.NFSe`/`TipoTributo.Iss` são reserva não operante (docs/fiscal/arquitetura.md §9) |
| MDF-e no SPED | MDF-e não é escriturado em EFD-ICMS/IPI (é controle de transporte, não mercadoria/serviço) — não se aplica |
| Reforma Tributária (IBS/CBS) — layout SPED novo/substituto | Cronograma 2026-2033 ainda não publicado pela RFB em leiaute definitivo; `TipoTributo.Ibs`/`Cbs` já reservados no enum do Fiscal, sem uso aqui ainda |
| Envio/protocolo automático (PVA/Receitanet) | Fora de escopo de qualquer fase — o SistemaX gera o arquivo `.txt`; upload no ambiente da Receita continua manual/contador, mesma fronteira que o gestao-raiz já assume (`accounting-export.service.ts` só gera, nunca transmite) |

---

## 11. Invariantes (checklist para quando `Sped/` for implementado)

Mesmo formato de checklist do §7 de `docs/fiscal/arquitetura.md` — a verificar contra o código real
quando existir, não só contra a intenção deste documento:

- [ ] Todo `CST`/`CSOSN`/`SituacaoTributaria` que aparece em `C170` vem de
      `TributoResolvidoItem.SituacaoTributaria` (saída) ou `ImpostoRecebidoItem.SituacaoTributariaIcms`/
      `Cst*` (entrada) — nunca de uma função `Inferir*` calculada a partir do valor do imposto.
- [ ] Item sem situação tributária resolvida bloqueia a geração do arquivo com erro nomeado —
      nunca gera `C170` com campo vazio nem "chuta" um CST.
- [ ] `E110` de um tenant Lucro Presumido/Real com `ApuracaoIcmsMensal` do período anterior
      ausente e não confirmada bloqueia a geração — nunca assume saldo anterior = 0 silenciosamente.
- [ ] `E110` de Perfil C (Simples/MEI) é sempre zerado (regra correta, preservada do gestao-raiz).
- [ ] Toda linha de `9900` do Bloco 9 reflete a contagem REAL do registro no arquivo gerado —
      mesmo teste de `sped-generator.test.ts:635-660`, portado para xUnit.
- [ ] Arquivo gerado sem nenhum documento no período (Vendas/Compras vazios) ainda é um arquivo
      válido — `0000`..`9999` presentes, blocos de documento vazios, contagens batendo (mesmo caso
      "Arquivo sem notas" testado em `sped-generator.test.ts:605-629`).
- [ ] `GerarSpedEfdContribuicoesUseCase` recusa (`Result.Falhar`) tenant com
      `RegimeTributario.DispensadoDeEfdContribuicoes() == true` — nunca gera arquivo vazio "só
      porque foi pedido".
- [ ] Toda linha termina em CRLF (`\r\n`), nunca `\n` bare — mesmo teste estrutural do gestao-raiz.
- [ ] `FiscalModule.DependeDe` continua sem listar `"compras"` mesmo após `CompraFiscalRecebidaHandler`
      existir — consumir evento de integração não exige o módulo emissor presente (mesma régua do
      ADR-0002 §6).

---

## 12. Rastreamento — defeito do `gestao-raiz` → correção deste design

| Defeito no `gestao-raiz` | Onde este design fecha o gap |
|---|---|
| `inferCstIcms`/`inferCstPis`/`inferCstCofins`/`inferCstIpi` reconstroem CST a partir do VALOR do imposto (`sped-generator.ts:121-140`) | Lê `TributoResolvidoItem.SituacaoTributaria` (saída, já gravado real na emissão) e `ImpostoRecebidoItem` (entrada, capturado verbatim do XML) — nunca reinfere (§3, §4) |
| Bloco E — débito/crédito/saldo sempre `'0,00'`, para todo tenant/período (`sped-generator.ts:537-549`) | `ApuracaoIcmsMensal` calcula de verdade a partir de C190 saída/entrada + saldo anterior confirmado; bloqueia em vez de fabricar zero quando o anterior não existe (§6) |
| EFD-Contribuições nunca implementado | Arquivo/design próprio, dispensa por regime explícita, Fase 1 cumulativo (§7) |
| Entrada (compra) sem captura de tributo por item no SistemaX (`ItemDeNotaDeCompra` atual) | `ImpostoRecebidoItem` (Compras, capturado no import do XML) + `CompraFiscalRecebida`/cache no Fiscal (§4, §5.1) |
| `IND_ATIV=1` declarado mas Bloco E nunca abre `E200` (apuração de IPI) para indústria | Gap nomeado explicitamente como Fase 2, nunca escondido atrás de um bloco ausente sem explicação (§2, §10) |
| Gerador de SPED como função solta em `lib/fiscal/`, sem separação Domain/Application (mistura formatação, agregação e persistência de config no mesmo arquivo de 712 linhas) | Builder de blocos é `Fiscal.Domain.Sped.*` (função pura, sem I/O); orquestração/persistência é `Fiscal.Application.Sped.*` — mesma separação de camada que o resto do Fiscal já segue (docs/fiscal/arquitetura.md §6) |
| `accounting-export.service.ts` gera SPED inline dentro de um "pacote de fechamento contábil" maior (XMLs, PDFs, Excel, SPED — tudo num service de 700+ linhas) | `GerarSpedEfdIcmsIpiUseCase`/`GerarSpedEfdContribuicoesUseCase` são casos de uso dedicados, um artefato por vez — compor um "pacote de fechamento" (se o produto precisar) é responsabilidade de uma camada acima que CHAMA os dois, nunca um service monolítico que os mistura |

---

## 13. Documentos relacionados

| Arquivo | Conteúdo |
|---|---|
| `docs/fiscal/arquitetura.md` | Modelo de domínio do Fiscal (`DocumentoFiscal`/`TributoResolvidoItem`/`MotorDeCalculoTributario`) que este documento consome como fonte de dado |
| `docs/arquitetura/adr/0002-fiscal.md` | Decisão resumida do Fiscal — "nunca CSOSN mudo", "um único motor" — princípios que este documento estende à exportação |
| `docs/arquitetura/adr/0001-sincronizacao-local-first.md` | "Reconciliação autoritativa onde há invariante dura" — mesma régua aplicada aqui à apuração de ICMS (nunca saldo anterior mudo) |
| `docs/arquitetura/ARCHITECTURE.md` | Regras de camada/IModule — justifica por que SPED é sub-área de `Fiscal`, não módulo novo |
| `docs/persistencia/persistencia-sqlite.md` | Convenção de migração/schema que `FiscalSchemaMigrationV2` (§5.1) segue |
| `docs/robustez/robustez-hardware-licoes.md` | "Unidade de crash-safety é a transação do banco" — aplica-se a `ConfirmarApuracaoIcmsUseCase` gravar `ApuracaoIcmsMensal` |
