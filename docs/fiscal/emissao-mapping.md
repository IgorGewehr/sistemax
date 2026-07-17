# Mapeamento de emissão — `DocumentoFiscal` → payloads do serviço de emissão (sefaz-gateway)

> **Status: rascunho para implementação.** Este documento é o CONTRATO antes do código do
> adapter `IGatewayEmissaoSefaz` — nenhuma linha de `Fiscal.Infrastructure.Sefaz.*` deveria
> existir sem que o mapeamento abaixo já tenha sido revisado. Complementa
> `docs/fiscal/arquitetura.md` (modelo de domínio) e `docs/arquitetura/adr/0002-fiscal.md`
> (decisão resumida); aqui é a peça que os dois deixam explicitamente em aberto: **"como o
> `DocumentoFiscal` já resolvido vira o JSON que um gateway de emissão de verdade aceita".**
>
> **Escopo:** monta o payload HTTP a partir de `DocumentoFiscal`/`ItemDocumentoFiscal`/
> `TributoResolvidoItem` (SistemaX, C#) para o contrato de serviço já em produção descrito em
> `saas-erp/lib/services/sefaz-gateway.ts` (endpoints `/nfe/emitir`, `/nfe/nfce/emitir`,
> `/nfe/nfce/contingencia/{prepare,transmit}`, `/nfse/emitir`, `/nfe/{cancelar,consultar,
> inutilizar,carta-correcao}`, `/nfe/status`). Esse serviço (`emissao.tensorroot.com` — um
> gateway terceiro que assina XML, fala SOAP com a SEFAZ e devolve JSON) já é consumido pelo
> `saas-erp` em produção; a decisão de PRODUTO sobre continuar usando ele ou trocar por emissão
> própria continua em aberto (`docs/fiscal/arquitetura.md` §9) — mas o **formato do payload**
> que ele aceita é um fato observável hoje, é o que este documento fixa como alvo de mapeamento,
> e a interface `IGatewayEmissaoSefaz` já declarada em `Fiscal.Application/Ports/` foi desenhada
> para não mudar de assinatura qualquer que seja a escolha.
>
> **Referências funcionais espelhadas (e onde este design melhora sobre elas):** NFS-e do
> `saas-erp` (`app/api/fiscal/emit/route.ts`, branch `type === 'nfse'`) para o payload de
> serviço; NFC-e/NF-e do `gestao-raiz` (`src/services/fiscal.service.ts`) para o fluxo
> funcional de contingência/idempotência/preflight — **não** para a resolução tributária, que é
> exatamente o código auditado como defeituoso em `docs/fiscal/arquitetura.md` (CSOSN
> hardcoded, dois motores). A melhoria central deste mapeamento sobre as duas referências está
> no §1.

---

## Pergunta que este documento responde

> "`DocumentoFiscal` já tem tudo resolvido (CST/CSOSN, CFOP, alíquotas, DIFAL/FCP) — como isso
> vira exatamente o JSON que `/nfe/emitir`, `/nfe/nfce/emitir` e `/nfse/emitir` esperam, campo a
> campo, sem reintroduzir a resolução-dentro-do-payload que é o defeito que `arquitetura.md`
> inteiro existe para evitar? E o que falta no domínio hoje pra isso ser possível de verdade?"

**Resposta curta:** o mapeamento é uma **projeção pura e mecânica** — `TributoResolvidoItem` →
bloco de imposto, sem `if`/precedência nenhuma, porque a precedência já aconteceu em
`MotorDeCalculoTributario` antes do agregado existir. Mas o mapeamento **também revela** que
`DocumentoFiscal` hoje não carrega várias coisas que o JSON exige (emitente cadastral,
destinatário/consumidor/tomador, forma de pagamento, certificado, referência de devolução,
unidade/EAN comercial do item) — nenhuma delas é bug do desenho existente (são propositalmente
fora do agregado, que é sobre tributo, não sobre cadastro), mas são inputs que o **adapter** e o
**caso de uso de transmissão** (ainda não escrito) precisam buscar em outro lugar. A tabela de
gaps em §11 é a lista concreta do que falta antes deste mapeamento rodar de ponta a ponta.

---

## 1. Princípio central — mapeamento é PROJEÇÃO, nunca RESOLUÇÃO

No `saas-erp` (`app/api/fiscal/emit/route.ts`) e no `gestao-raiz` (`fiscal.service.ts`), a
resolução tributária e a montagem do JSON acontecem **na mesma função**, entrelaçadas: o `pick()`
do saas-erp (`item.X ?? product.X ?? default`) decide CSOSN/CST **enquanto** monta o objeto que
vai pro gateway; o `defaultIcmsCSOSN = crt !== '3' ? '400' : undefined` do gestao-raiz é a mesma
mistura, só que sem a cascata de override — é exatamente o defeito documentado em
`docs/fiscal/arquitetura.md` (Grounding, topo do arquivo) e em `adr/0002-fiscal.md` decisão #2.

Aqui isso é estruturalmente impossível, não só evitado por disciplina: quando
`EmitirDocumentoFiscalUseCase` chama `IGatewayEmissaoSefaz.TransmitirAsync(documento)`, o
`documento.Itens[i].Tributos` **já é o resultado final** — CST ou CSOSN escolhidos por
`SituacaoTributariaIcms.ParaCst`/`ParaCsosn` (nunca os dois ao mesmo tempo, por construção do
tipo), alíquota já calculada, DIFAL/FCP já decididos por `OperacaoFiscal.GeraPartilhaDifal`. O
adapter HTTP deste port **não tem overrides pra aplicar** — só tem `record`s imutáveis pra
serializar. Isso significa duas coisas concretas para quem for implementar:

1. **O mapper nunca lê `TributacaoProduto`/`PerfilFiscalNCM`/`RegraFiscalPorOperacao` — só lê o
   `DocumentoFiscal` já resolvido.** Se o mapper "precisar" consultar uma dessas tabelas para
   decidir um campo do JSON, isso é sinal de que o campo deveria ter sido resolvido no
   `MotorDeCalculoTributario` e não foi — voltar pra lá, nunca resolver no adapter.
2. **Todo campo do JSON que não tem fonte em `DocumentoFiscal`/`ItemDocumentoFiscal`/
   `TributoResolvidoItem` é, por definição, uma dependência externa ao agregado fiscal** (dado
   cadastral do emitente, dado do cliente, forma de pagamento, certificado) — nunca "resolvido
   na hora" pelo mapper como um valor mágico. A tabela de gaps (§11) nomeia cada um desses e onde
   ele deveria vir de fora.

---

## 2. `IGatewayEmissaoSefaz` — adapter HTTP concreto proposto

`Fiscal.Application/Ports/IGatewayEmissaoSefaz.cs` já existe (ver `docs/fiscal/arquitetura.md`
§9) e não muda de assinatura:

```csharp
public interface IGatewayEmissaoSefaz
{
    Task<Result<ResultadoTransmissaoSefaz>> TransmitirAsync(DocumentoFiscal documento, CancellationToken ct = default);
}
```

O adapter concreto vive em `Fiscal.Infrastructure/Sefaz/` (novo diretório, ao lado de
`Sqlite/`/`InMemory/` — registrado por `FiscalInfrastructureModule`, nunca por `FiscalModule`,
mesma disciplina de grafo `Infrastructure → Application → Domain` já fixada em §6 de
`arquitetura.md`):

```csharp
namespace SistemaX.Modules.Fiscal.Infrastructure.Sefaz;

public sealed class SefazGatewayOptions
{
    public required string BaseUrl { get; init; }          // ex.: https://emissao.tensorroot.com
    public required string ApiKey { get; init; }            // Bearer — nunca logado, nunca no XML de erro
    public int TimeoutSeconds { get; init; } = 60;           // mesmo valor do TIMEOUT_MS do sefaz-gateway.ts
    public int MaxRetries { get; init; } = 3;                // idem MAX_RETRIES
}

/// <summary>
/// Implementação HTTP de <see cref="IGatewayEmissaoSefaz"/> contra o mesmo contrato que
/// `saas-erp/lib/services/sefaz-gateway.ts` já fala em produção. Único ponto de I/O de rede do
/// módulo Fiscal — <c>MotorDeCalculoTributario</c> nunca importa isto (regra de fronteira:
/// Domain não faz I/O).
/// </summary>
public sealed class HttpGatewayEmissaoSefaz(
    HttpClient http,                                   // BaseAddress = options.BaseUrl, header Authorization já setado
    ICertificadoDigitalRepository certificados,         // Gap #2 (§11) — ainda não existe, ver proposta ali
    ICadastroFiscalEmitenteRepository emitentes,        // Gap #4 (§11) — idem
    IDestinatarioDocumentoFiscalRepository destinatarios, // Gap #1 (§11) — idem
    IFormaPagamentoDocumentoFiscalRepository pagamentos)  // Gap #3 (§11) — idem
    : IGatewayEmissaoSefaz
{
    public async Task<Result<ResultadoTransmissaoSefaz>> TransmitirAsync(
        DocumentoFiscal documento, CancellationToken ct = default)
    {
        var payload = documento.Tipo switch
        {
            TipoDocumentoFiscal.NFe  => await MontarPayloadNFeAsync(documento, ct),
            TipoDocumentoFiscal.NFCe => await MontarPayloadNFCeAsync(documento, ct),
            TipoDocumentoFiscal.NFSe => await MontarPayloadNFSeAsync(documento, ct),
            _ => throw new NotSupportedException($"Tipo '{documento.Tipo}' sem endpoint de emissão mapeado."),
        };
        if (payload.Falha) return Result.Falhar<ResultadoTransmissaoSefaz>(payload.Erro);

        var endpoint = documento.Tipo switch
        {
            TipoDocumentoFiscal.NFe  => "/nfe/emitir",
            TipoDocumentoFiscal.NFCe => "/nfe/nfce/emitir",
            TipoDocumentoFiscal.NFSe => "/nfse/emitir",
            _ => throw new NotSupportedException(),
        };

        return await PostComRetryAsync(endpoint, payload.Valor, ct);
    }

    // PostComRetryAsync replica o algoritmo de sefazRequest() (sefaz-gateway.ts linhas 167-292):
    //   - até MaxRetries tentativas (default 3), backoff exponencial 1s/2s/4s
    //   - 401/403/400/429 => propaga IMEDIATAMENTE (Result.Falhar), nunca retry
    //   - 422 => devolve o corpo AS-IS pro caller interpretar como rejeição de negócio
    //     (ver §7) — nunca Result.Falhar (422 não é falha de infra, é a SEFAZ respondendo)
    //   - 503 / 5xx / timeout => guarda como lastError e tenta de novo
    //   - esgotadas as tentativas => Result.Falhar com a última falha
}
```

**Por que retry/backoff replica exatamente o TS e não é reinventado:** o serviço do outro lado é
o MESMO — replicar o algoritmo linha por linha (mesmos códigos HTTP tratados igual, mesmo
backoff 1/2/4s, mesmo timeout 60s) evita que o `saas-erp` e o SistemaX tenham comportamentos
diferentes contra o mesmo gateway (ex.: um insistindo em 429 enquanto o outro já desistiu) — bug
de coordenação sutil que só apareceria em produção, sob carga.

---

## 3. O que falta no domínio para montar `emitente` (cadastro do estabelecimento)

`ConfiguracaoFiscalTenant` (`Fiscal.Domain/Regimes/ConfiguracaoFiscalTenant.cs`) — a única fonte
de dado por-tenant que `Fiscal.Domain` já tem — carrega **só** `TenantId`, `Regime`, `UfOrigem`,
`SerieNfce`, `SerieNfe`. Não tem CNPJ, Razão Social, Nome Fantasia, Inscrição Estadual/Municipal,
nem endereço. O `Crt` (Código de Regime Tributário) **não** é um gap — já é derivado de
`Regime` por `RegimeTributarioExtensions.Crt()` (§2.1 de `arquitetura.md`), nunca precisa vir de
fora.

O restante do bloco `emitente` (CNPJ/Razão Social/Nome Fantasia/IE/IM/endereço completo) é dado
**cadastral da empresa**, não tributário — não deveria morar em `Fiscal.Domain` por definição
(mesma razão que NCM/CEST moram no Estoque, não no Fiscal — ver §4 de `arquitetura.md`). A
pergunta em aberto é **de qual módulo** ele vem (`Empresa`/`Tenant`, ainda não mapeado neste
repo) e como chega ao Fiscal sem acoplamento Domain-a-Domain. Proposta, mesma receita já usada
para `NcmPorProduto` (§4 de `arquitetura.md`):

```csharp
namespace SistemaX.Modules.Fiscal.Application.Ports;

public interface ICadastroFiscalEmitenteRepository
{
    Task<CadastroFiscalEmitente?> ObterAsync(string tenantId, CancellationToken ct = default);
}

public sealed record CadastroFiscalEmitente(
    string TenantId, string Cnpj, string RazaoSocial, string? NomeFantasia,
    string InscricaoEstadual, string? InscricaoMunicipal,
    string Logradouro, string Numero, string? Complemento, string Bairro,
    string CodigoMunicipio, string Municipio, string Cep, string? Telefone);
```

populado por evento de integração do módulo dono do cadastro (`EmpresaAtualizada` ou
equivalente — a catalogar em `Modules.Abstractions/IntegrationEvents.cs` quando esse módulo
existir), mesmo padrão fan-out de `ProdutoFiscalAtualizado`. Até esse módulo existir,
`InMemoryCadastroFiscalEmitenteRepository`/`SqliteCadastroFiscalEmitenteRepository` cobrem o
mesmo papel dos demais adapters "seed manual via Settings" já usados por `PerfilFiscalNCM`.

---

## 4. Mapeamento campo-a-campo — NF-e / NFC-e

Payload-alvo confirmado lendo `saas-erp/app/api/fiscal/emit/route.ts` (o único código real que
monta esse JSON hoje) — **não** é o XML da NF-e (`ide`/`dest`/`det`), é o schema JSON amigável
que o gateway `sefaz-api` aceita e traduz para XML internamente.

### 4.1 Envelope

| Campo JSON | Fonte SistemaX | Nota |
|---|---|---|
| `emitente` | `ICadastroFiscalEmitenteRepository` (§3) + `RegimeTributarioExtensions.Crt(regime)` | Objeto completo — CNPJ sem máscara, `crt` como string "1"/"2"/"3" |
| `numero` | `DocumentoFiscal.Numero` | Já `long` — sem cast |
| `serie` | `DocumentoFiscal.Serie` | String — série já é string em `ConfiguracaoFiscalTenant.SerieNfe`/`SerieNfce` |
| `ufEmitente` | `ConfiguracaoFiscalTenant.UfOrigem` (ou `OperacaoFiscal.UfOrigem` — devem ser o mesmo valor; usar o da config, é o fato estável) | 2 letras maiúsculas |
| `ambiente` | Config de ambiente do tenant (produção/homologação) — **não existe em `ConfiguracaoFiscalTenant` hoje**, precisa de campo novo (`Ambiente` enum) ali, mesmo padrão do `fiscal.environment`/`fiscal.nfeConfig.environment` do saas-erp | Sem esse campo, toda emissão cai em homologação por default de segurança até ser adicionado |
| `naturezaOperacao` | Texto derivado de `OperacaoFiscal.Tipo` — ver tabela 4.1.1 abaixo | Texto livre exigido pelo XSD (`xNatOp`), nunca um enum cru |
| `tipoOperacao` | `'1'` (saída) sempre, exceto `TipoOperacaoFiscal.DevolucaoDeVenda` → `'0'` (entrada) | Mesma regra do saas-erp (linha 1240-1242 do `emit/route.ts`) |
| `finalidade` | `'1'` normal; `DevolucaoDeVenda` → `'4'`; complemento/ajuste (fora do enum atual) → `'2'`/`'3'` quando existirem | `TipoOperacaoFiscal` de hoje não distingue complemento/ajuste — só mapeia normal/devolução |
| `consumidorFinal` | `OperacaoFiscal.DestinatarioConsumidorFinal` → `'1'`/`'0'` | Direto — já é bool no domínio |
| `presencaComprador` | `OperacaoFiscal.OperacaoPresencial` → `'1'` (presencial) / `'9'` (não presencial, default do saas-erp) | Domínio só tem bool; os 9 códigos SEFAZ (1-9, presencial/internet/teleatendimento/...) colapsam pra 2 — se granularidade fina importar, `OperacaoFiscal` precisa de um campo próprio em vez de derivar do bool |

**4.1.1 — `naturezaOperacao` por `TipoOperacaoFiscal`:**

| `TipoOperacaoFiscal` | `naturezaOperacao` (texto XSD) | `finalidade` |
|---|---|---|
| `VendaMercadoria` | `"VENDA DE MERCADORIA"` (NF-e) / `"VENDA AO CONSUMIDOR FINAL"` (NFC-e) | `'1'` |
| `DevolucaoDeVenda` | `"DEVOLUCAO DE VENDA"` | `'4'` |
| `TransferenciaEntreEstabelecimentos` | `"TRANSFERENCIA ENTRE ESTABELECIMENTOS"` | `'1'` |
| `RemessaEmComodato` | `"REMESSA EM COMODATO"` | `'1'` |
| `RemessaParaConserto` | `"REMESSA PARA CONSERTO"` | `'1'` |

### 4.2 Destinatário (NF-e) / Consumidor (NFC-e)

**Gap #1 (§11) — bloqueante.** `DocumentoFiscal` não tem NENHUM campo de cliente/destinatário
hoje — nem para NF-e (`destinatario`), nem para NFC-e (`consumidor`), nem para NFS-e
(`tomador`). Assumindo que um novo value object `DestinatarioDocumentoFiscal` (nullable — NFC-e
frequentemente não identifica o consumidor) é adicionado ao agregado ou passado como parâmetro
extra em `EmitirDocumentoFiscalUseCase`/`TransmitirAsync`, o mapeamento campo-a-campo seria:

| Campo JSON (NF-e `destinatario`) | Fonte proposta | Nota |
|---|---|---|
| `cnpj` / `cpf` | `DestinatarioDocumentoFiscal.Documento` (14 ou 11 dígitos, disambiguado pelo tamanho — mesmo truque do saas-erp linha 1178-1185) | Nunca os dois preenchidos |
| `nome` | `.Nome` | |
| `email` | `.Email` (opcional) | |
| `inscricaoEstadual` | `.InscricaoEstadual` (opcional) | |
| `indicadorIE` | `'1'` se tem IE, senão `'9'` (auto-resolvido, nunca pedido explicitamente — mesma regra do saas-erp linha 1170-1174) | |
| `endereco.*` | `.Endereco` (opcional) | Obrigatório só quando `finalidade='4'` ou operação interestadual a contribuinte |

| Campo JSON (NFC-e `consumidor`) | Fonte proposta | Nota |
|---|---|---|
| `cpf` | `DestinatarioDocumentoFiscal.Documento` quando 11 dígitos | Opcional — NFC-e pode sair sem identificar consumidor |
| `nome` | `.Nome` | Só enviado se `cpf` também presente (schema exige os dois juntos ou nenhum) |

### 4.3 Item — bloco comercial (`produto`)

| Campo JSON | Fonte SistemaX | Nota |
|---|---|---|
| `numero` | Índice do item na lista (1-based) | Puramente posicional, sem fonte de domínio |
| `codigo` | `ItemDocumentoFiscal.ProdutoId` | Livre no XML (`cProd`) — usar o Id já é suficiente, sem precisar de SKU dedicado |
| `cEAN` | **Gap #6 (§11)** — `ItemDocumentoFiscal` não carrega GTIN | `NcmPorProduto` (cache local do Estoque, §4 de `arquitetura.md`) hoje só guarda `(Ncm, Cest)` — precisa estender para incluir `Gtin`/`UnidadeComercial`, ou aceitar `"SEM GTIN"` como valor fixo enquanto o gap não fecha (mesmo fallback do saas-erp linha 503) |
| `descricao` | `ItemDocumentoFiscal.Descricao` | Direto |
| `ncm` | `ItemDocumentoFiscal.Ncm` | Direto — já validado 8 dígitos por `PerfilFiscalNCM.Criar` |
| `cest` | `ItemDocumentoFiscal.Cest` | Opcional — omitir a chave se null (nunca enviar `null` literal, mesmo cuidado do `stripEmpty` do saas-erp) |
| `cfop` | `ItemDocumentoFiscal.Cfop` | Já resolvido pela cadeia emissão>produto>padrão-config (§2.3 `arquitetura.md`) — mapper NUNCA reajusta 5xxx/6xxx por interestadualidade aqui, isso já é responsabilidade de `IResolvedorDeCfop`/`RegraCfop`, diferente do saas-erp que faz esse ajuste tardio no route (linha 682-692) por não ter resolução de CFOP própria |
| `unidade` / `unidadeTrib` | **Gap #6** — mesma origem do `cEAN` | Fallback `"UN"` enquanto o gap não fecha |
| `quantidade` / `quantidadeTrib` | `ItemDocumentoFiscal.Quantidade.EmDecimal` | `decimal`, nunca reconverter pra `double` no mapper |
| `valorUnitario` / `valorUnitarioTrib` | `ItemDocumentoFiscal.PrecoUnitario.EmReais` | `Money.EmReais` já é `decimal` |
| `valorTotal` | `ItemDocumentoFiscal.PrecoUnitario.EmReais * Quantidade.EmDecimal` (bruto, ANTES do desconto — schema exige `vProd` bruto e `vDesc` separado, nunca líquido) | Repetir a mesma conta de `Subtotal` sem subtrair `Desconto` |
| `valorDesconto` | `ItemDocumentoFiscal.Desconto.EmReais` | Omitir a chave (não enviar `0`) quando `Desconto.EhZero` |
| `cEANTrib` | Igual a `cEAN` salvo quando o cadastro tiver unidade tributável diferente da comercial (Gap #6) | |
| `indTot` | `'1'` sempre (item soma no total da nota) | Fixo — não há caso de uso hoje para `'0'` |

### 4.4 Item — bloco tributário (`imposto`)

Este é o bloco onde a projeção pura (§1) mais importa. `TributoResolvidoItem` é um bag —
`ItemDocumentoFiscal.Tributos` é uma lista de 0-N linhas, uma por `TipoTributo` incidente. O
mapper faz **um `switch` de agrupamento por `Tipo`**, nunca uma decisão de negócio:

```csharp
// Pseudocódigo do mapper — cada bloco é OMITIDO (chave ausente) quando o tributo
// correspondente não está na lista, nunca um bloco com zeros explícitos.
var porTipo = item.Tributos.ToDictionary(t => t.Tipo);

icms   = MapearIcms(porTipo[TipoTributo.Icms]);              // sempre presente (invariante de AdicionarItemResolvido)
icmsSt = porTipo.TryGetValue(TipoTributo.IcmsSt, out var st) ? MapearIcmsSt(st) : null;
ipi    = porTipo.TryGetValue(TipoTributo.Ipi, out var ipi) ? MapearIpi(ipi) : null;
pis    = MapearPis(porTipo.GetValueOrDefault(TipoTributo.Pis));     // ver nota "Simples não calcula" abaixo
cofins = MapearCofins(porTipo.GetValueOrDefault(TipoTributo.Cofins));
```

| Campo JSON (`imposto.icms`) | Fonte | Nota |
|---|---|---|
| `orig` | `ItemDocumentoFiscal.Origem` (enum `OrigemMercadoria`) → `((int)Origem).ToString()` | Nunca `Nacional` assumido — já obrigatório no domínio (§2.4 `arquitetura.md`) |
| `csosn` (regime Simples) OU `cst` (regime Normal) | `TributoResolvidoItem.SituacaoTributaria` — a MESMA propriedade serve os dois; a escolha de qual CHAVE JSON usar (`csosn` vs `cst`) vem de `RegimeTributarioExtensions.UsaCsosn(regime)`, nunca de inspecionar o valor da string | Isto é o ponto exato que fecha o defeito do `defaultIcmsCSOSN` do gestao-raiz — aqui não HÁ branch `crt === '3'` no mapper, só uma consulta ao mesmo booleano que o domínio já usou pra resolver |
| `modBC`, `valorBC`, `aliquota`, `valor` | `TributoResolvidoItem.Base.EmReais`, `.Aliquota.EmFracao * 100`, `.Valor.EmReais` | Presentes só quando `regime.UsaCsosn() == false` (schema do CST tributado exige; CSOSN não carrega esses campos quando não há ST) |
| `percentualReducaoBC` / MVA | `TributoResolvidoItem.ReducaoBaseCalculo`/`.Mva` | Omitir quando `null` |

| Campo JSON (`imposto.icmsSt`, quando `IcmsSt` presente) | Fonte |
|---|---|
| `baseCalculoST`, `aliquotaST`, `valorST` | `TributoResolvidoItem` do tipo `IcmsSt` — mesmos campos, chave diferente |

**DIFAL/FCP — extensão de contrato necessária, não só de mapeamento.** O payload observado no
`saas-erp` (`emit/route.ts`) **não tem** bloco de partilha interestadual (`ICMSUFDest` no layout
NF-e 4.00 — tags `vBCUFDest`, `pFCPUFDest`, `vFCPUFDest`, `pICMSUFDest`, `pICMSInter`,
`pICMSInterPart`, `vICMSUFDest`, `vICMSUFRemet`) porque o saas-erp nunca vendeu para consumidor
final não-contribuinte fora do estado com esse volume — o gateway `sefaz-api` pode ou não
aceitar esse bloco hoje. **Antes de emitir a primeira nota com `TributoResolvidoItem(IcmsDifal)`/
`(Fcp)` presentes, confirmar com quem mantém o gateway se o schema aceita `imposto.icmsUFDest`**
— se não aceitar, é bloqueio de infraestrutura externa, não deste mapeamento. Proposta de forma
JSON (a validar contra o gateway real):

```jsonc
"imposto": {
  "icms": { /* ICMS de origem, alíquota interestadual já aplicada — ver passo 7 do fluxo em arquitetura.md §3 */ },
  "icmsUFDest": {
    "vBCUFDest": 100.00,     // TributoResolvidoItem(IcmsDifal).Base.EmReais
    "pFCPUFDest": 2.0,       // TributoResolvidoItem(Fcp).Aliquota.EmFracao * 100 (quando presente)
    "vFCPUFDest": 2.00,      // TributoResolvidoItem(Fcp).Valor.EmReais
    "pICMSUFDest": 18.0,     // alíquota interna do UF de destino (RegraFiscalPorOperacao chaveada por UfDestino)
    "pICMSInter": 12.0,      // alíquota interestadual efetivamente usada no ICMS de origem
    "vICMSUFDest": 6.00,     // TributoResolvidoItem(IcmsDifal).Valor.EmReais
    "vICMSUFRemet": 0.00     // partilha do remetente — 0% desde o cronograma 2019 do Convênio 93/2015 (ver §2.2 arquitetura.md)
  }
}
```

| Campo JSON (`imposto.ipi`, opcional) | Fonte |
|---|---|
| `cst` | `TributoResolvidoItem(Ipi).SituacaoTributaria` |
| `baseCalculo`, `aliquota`, `valor` | `.Base`/`.Aliquota`/`.Valor` — presentes só quando `Aliquota.EmFracao > 0` (schema: CST tributável) |
| `cEnq` | **Gap** — código de enquadramento IPI não tem campo em `TributoResolvidoItem`/`PerfilFiscalNCM` hoje; saas-erp usa `product.fiscalTax.ipi.cEnq` com fallback `'999'` (enquadramento genérico) — adicionar `CodigoEnquadramentoIpi` a `PerfilFiscalNCM`/`TributacaoProduto` quando IPI por item virar prioridade (hoje nenhum tenant seedado usa IPI, ver §9 `arquitetura.md`) |

| Campo JSON (`imposto.pis`/`imposto.cofins`) | Fonte | Nota |
|---|---|---|
| `cst` | `TributoResolvidoItem(Pis\|Cofins).SituacaoTributaria` | Simples Nacional: CST 07/08/99 SEM valores (§2.1 `arquitetura.md` — "Simples não destaca PIS/COFINS por item, embutido no DAS") |
| `valorBC`, `aliquota`, `valor` | Presentes só quando `Aliquota.EmFracao > 0` | Mesmo padrão do saas-erp (`pisAliq > 0 ? ... : undefined`) — mas aqui a decisão de "tem alíquota ou não" já veio pronta do motor, o mapper só verifica `> 0`, nunca escolhe a alíquota |

### 4.5 Pagamento (`pagamento`)

**Gap #3 (§11) — bloqueante para NF-e/NFC-e.** `DocumentoFiscal` não tem `Pagamentos` — é
correto que não tenha (forma de pagamento é fato de Venda/PDV, não de tributação), mas o JSON
`pagamento.formas[]` é obrigatório em toda NF-e/NFC-e emitida com `indTot='1'`. Precisa entrar
como parâmetro adicional em `EmitirDocumentoFiscalUseCase.ExecutarAsync` (ao lado de
`IReadOnlyList<ItemParaEmitir> itens`, um novo `IReadOnlyList<FormaPagamentoParaEmitir>
pagamentos`) — carregado no agregado ou passado direto pro `TransmitirAsync` via um DTO paralelo
(mais simples: **não** persistir pagamento no agregado fiscal, só repassar como argumento de
transmissão, já que ele não faz parte do que a nota precisa "lembrar" depois de autorizada — o
`saleId`/`SourceRef` já aponta pra onde essa informação vive de verdade, em Vendas).

```csharp
public sealed record FormaPagamentoParaEmitir(string Metodo, Money Valor);
```

Tabela de código SEFAZ por método (`tPag`), reaproveitada 1:1 do `saas-erp`
(`lib/fiscal/number-sequence.ts:getPaymentCode`, já testada em produção):

| Método | `tPag` | Método | `tPag` |
|---|---|---|---|
| Dinheiro | `01` | Vale alimentação | `10` |
| Cheque | `02` | Vale refeição | `11` |
| Crédito | `03` | Vale presente / gift card | `12` |
| Débito | `04` | Vale combustível | `13` |
| Crédito loja (fiado) | `05` | Boleto | `15` |
| — | | Depósito | `16` |
| — | | PIX | `17` |
| — | | Transferência | `18` |
| — | | Pontos de fidelidade | `19` |
| Sem pagamento | `90` | Outros | `99` (exige `descricao`) |

`needsCardInfo` (`03`/`04`/`17` — crédito/débito/PIX) exige sub-bloco `cartao: {tipoIntegracao:
'2'}` (integração não-integrada com TEF, mesmo valor fixo do saas-erp — não há hoje modelagem de
TEF integrado no SistemaX). `indicadorPagamento` é sempre `'0'` (pagamento à vista) — não há
venda a prazo modelada com múltiplas datas de vencimento no fluxo fiscal hoje.

### 4.6 Transporte, referências, informações adicionais, CSC, certificado

| Campo JSON | Fonte | Nota |
|---|---|---|
| `transporte.modFrete` | Fixo `'9'` (sem frete) até o módulo de Vendas/Logística carregar modalidade de frete | Mesmo default do saas-erp quando o campo não é informado |
| `referencias` (NF-e, só quando `finalidade='4'`) | **Gap #5 (§11)** — `DocumentoFiscal`/`OperacaoFiscal` não carregam a chave de acesso (44 dígitos) da NF-e original sendo devolvida | Precisa de campo novo, ex. `SourceRef` alternativo ou parâmetro `RefNFe` em `ItemParaEmitir`/`EmitirDocumentoFiscalUseCase` — sem isso, `TipoOperacaoFiscal.DevolucaoDeVenda` resolve tributação e CFOP corretamente mas NUNCA consegue montar o payload de devolução de verdade (a SEFAZ rejeita finalidade=4 sem `NFref`, rejeição 235/236 — mesmo comentário do saas-erp linha 1210-1213) |
| `informacoesAdicionais.contribuinte` | Texto livre — não tem fonte de domínio hoje (seria um campo opcional passado no momento da emissão, análogo a `CfopDaEmissao` em `ItemParaEmitir`, mas em nível de documento não de item) | |
| `csc` (só NFC-e) | **Gap #2** — Código de Segurança do Contribuinte é config fiscal do tenant (id + token), não existe em `ConfiguracaoFiscalTenant` hoje | Adicionar `CscId`/`CscTokenCriptografado` a `ConfiguracaoFiscalTenant` OU um repositório-irmão dedicado (token é sensível — nunca gravado em texto puro, mesma disciplina do `cscTokenEncrypted` do saas-erp via `encryptToken`/`decryptToken`) |
| `certificado.{pfxBase64,password}` | **Gap #2** — não existe `ICertificadoDigitalRepository` em `Fiscal.Application/Ports/` hoje | Certificado A1 (.pfx) + senha, nunca persistido em texto puro; resolvido a cada transmissão a partir de um cofre (Storage criptografado, mesmo padrão do `certificate-manager.ts` do saas-erp) — nunca lido do `DocumentoFiscal` (o agregado não deveria SABER de certificado, é dado de infraestrutura pura) |

---

## 5. NFS-e — mapeamento (rascunho; ISS fora de escopo hoje, ver §9 de `arquitetura.md`)

`TipoDocumentoFiscal.NFSe` e `TipoTributo.Iss` já existem no enum (extensão aditiva reservada),
mas **nenhum** `PerfilFiscalNCM`/`RegraFiscalPorOperacao` cobre ISS por município hoje — cálculo
de ISS por LC 116/retenção/CNAE é modelo net-new. Este mapeamento é o rascunho de FORMA para
quando esse motor existir, não um contrato pronto para implementar já — segue a `NfsePayload`
observada em `sefaz-gateway.ts` (única fonte real, o gestao-raiz nunca implementou NFS-e —
`createNFSe` lá é stub que sempre lança, comentário do próprio arquivo).

| Campo JSON (`NfsePayload`) | Fonte proposta | Nota |
|---|---|---|
| `numeroDPS` | `DocumentoFiscal.Numero` | "DPS" = Declaração de Prestação de Serviço — sucessora do RPS no padrão nacional de NFS-e |
| `serie` | `DocumentoFiscal.Serie` | |
| `codigoMunicipioEmissao` | `ICadastroFiscalEmitenteRepository.CodigoMunicipio` (mesmo gap #4 de NF-e/NFC-e) | |
| `prestador.*` | Mesmo `CadastroFiscalEmitente` de §3, + `simplesNacional: '1'\|'2'` derivado de `RegimeTributarioExtensions.UsaCsosn(regime) ? '1' : '2'` (reaproveita o MESMO booleano que decide CSOSN vs CST — não é coincidência, `SimplesNacionalSublimite` deveria mapear pra `'2'` aqui também, mesma razão tributária de §2.1) | |
| `tomador.*` | Mesmo Gap #1 (destinatário) de NF-e/NFC-e — aqui chamado `tomador` | Nome, CPF/CNPJ, endereço |
| `servico.codigoTributacaoNacional` | **Gap novo** — não existe em `ItemDocumentoFiscal`/domínio hoje; seria um campo do futuro "cadastro de serviço" (Estoque hoje só modela `Produto`, não `Servico`) | Ver nota abaixo |
| `servico.discriminacao` | `ItemDocumentoFiscal.Descricao` (reaproveitável — já existe) | Único campo do bloco `servico` com fonte pronta hoje |
| `servico.localPrestacao.codigoMunicipio` | Default = código do emitente; diferente quando serviço presencial em outro município (campo hoje inexistente em `OperacaoFiscal` — precisaria de `UfDestino`/`CodigoMunicipioDestino` mais granular que hoje só tem UF) | |
| `servico.nbs`, `.cnae` | Sem fonte hoje | Exigidos por algumas prefeituras específicas (BH exige CNAE — mesma nota do saas-erp `municipalRequirements.ts`) |
| `valores.valorServicos` | `ItemDocumentoFiscal.Subtotal.EmReais` (somado, se múltiplos itens de serviço) | |
| `issqn.{baseCalculo,aliquota,valorISS}` | `TributoResolvidoItem(Iss)` quando o motor de ISS existir | Hoje: nenhuma linha de regra semeada — `MotorDeCalculoTributario` bloquearia (`Result.Falhar`) qualquer tentativa de resolver Iss, corretamente (nunca inventa alíquota) |

**Nota de escopo:** o principal bloqueio de NFS-e não é o mapeamento JSON (que é simples,
seção acima já cobre 80% dos campos) — é a ausência total de um motor de ISS (alíquota por
serviço/município, código de tributação nacional por LC 116, retenção). Isso é trabalho de
domínio novo (`Fiscal.Domain` + provavelmente um cadastro de "Serviço" em outro módulo), fora do
escopo deste documento de mapeamento.

---

## 6. Fluxo de contingência NFC-e — encaixe no local-first (ADR-0001)

Contingência NFC-e é o caso mais claro de **"local-first não é só sobre estoque/financeiro — é
sobre o próprio ato de emitir"** no ADR-0001: quando a rede cai no meio do fechamento de uma
venda no PDV, o terminal **não pode** esperar a SEFAZ responder para imprimir o cupom — ele
assina o XML localmente (`tpEmis=9`), imprime o DANFCE, e transmite depois. Isso é
estruturalmente o MESMO padrão de "reserva/autoridade local, reconciliação depois" que o
ADR-0001 já fixa para numeração fiscal (decisão #5) — contingência é esse mesmo princípio
aplicado ao momento da TRANSMISSÃO, não só da NUMERAÇÃO.

### 6.1 Dois métodos, não um — mesma separação do `sefaz-gateway.ts`

```csharp
public interface IGatewayEmissaoSefaz
{
    Task<Result<ResultadoTransmissaoSefaz>> TransmitirAsync(DocumentoFiscal documento, CancellationToken ct = default);

    // NOVOS — contingência NFC-e (extensão aditiva, mesma interface, sem quebrar o método acima)
    Task<Result<ContingenciaPreparada>> PrepararContingenciaAsync(
        DocumentoFiscal documento, DateTimeOffset dhCont, string justificativa, CancellationToken ct = default);

    Task<Result<ResultadoTransmissaoSefaz>> TransmitirContingenciaAsync(
        string xmlAssinado, string ufEmitente, CancellationToken ct = default);
}

public sealed record ContingenciaPreparada(string ChaveDeAcesso, string XmlAssinado);
```

`PrepararContingenciaAsync` chama `/nfe/nfce/contingencia/prepare` (payload = o MESMO payload de
`/nfe/nfce/emitir` + `contingencia: { dhCont, xJust }`) — **assina mas não envia**.
`TransmitirContingenciaAsync` chama `/nfe/nfce/contingencia/transmit` (payload =
`{ signedXml, ufEmitente, certificado, ambiente }`) quando a rede volta.

### 6.2 Gap #8 (§11) — `StatusDocumentoFiscal` não distingue contingência

O FSM de `saas-erp` (`FISCAL_DOCUMENT_STATUSES`, `lib/contracts/fsm/fiscalDocument.ts`) tem um
estado `'contingencia'` de primeira classe, com self-loop explícito (retry rejeitado
**preserva** `'contingencia'`, nunca cai pra terminal antes da janela de 24h expirar). O
`StatusDocumentoFiscal` do SistemaX (`Rascunho | BloqueadoPorConfiguracaoFiscal | NumeroAlocado |
Autorizado | Denegado | Rejeitado | Cancelado | Inutilizado`) **não tem estado equivalente** —
`NumeroAlocado` hoje representa só "número comprometido, aguardando transmissão normal", não
"XML já assinado localmente em `tpEmis=9`, DANFCE já impresso, aguardando rede para transmitir".

Confundir os dois é perigoso: um documento em contingência **já é um fato legal irreversível**
(o cliente já tem o cupom impresso, o item já não pode "voltar a ser rascunho") — mas hoje nada
no FSM impede alguém de tratar um `NumeroAlocado` de contingência como se fosse um
`NumeroAlocado` comum e desistir dele (`Desistir()` → `Inutilizado`) quando na verdade a nota já
foi entregue ao cliente. Proposta — estender o enum e a tabela de transições:

```csharp
public enum StatusDocumentoFiscal
{
    Rascunho,
    BloqueadoPorConfiguracaoFiscal,
    NumeroAlocado,
    EmContingencia,          // NOVO — XML já assinado localmente (tpEmis=9), aguardando rede
    Autorizado,
    Denegado,
    Rejeitado,
    Cancelado,
    Inutilizado
}
```

```
NumeroAlocado ──PrepararContingencia()──► EmContingencia
EmContingencia ──Transmitir() (rede voltou)──► Autorizado | Rejeitado | Denegado
EmContingencia ──Transmitir() rejeitado──► EmContingencia (self-loop — PRESERVA, mesmo racional do fiscalDocument.ts)
EmContingencia ──ExpirarJanela(motivo) (>24h sem transmitir — extemporaneidade)──► Rejeitado
```

A distinção importa porque **`Desistir()`/`Inutilizado` nunca deveria aceitar `EmContingencia`
como origem** — uma vez que o DANFCE foi impresso, o único destino possível é
autorizar/rejeitar/expirar, nunca "desistir" (o cliente já está com o comprovante físico em mão).
Isso é um novo item pra tabela `TransicoesPermitidas` de `DocumentoFiscal.cs`, e uma nova guarda
em `Desistir()` — mudança de domínio real, não só de mapeamento, então fica registrada aqui como
pré-requisito antes do adapter de contingência poder ser implementado.

### 6.3 Janela de 24h (extemporaneidade)

Mesma regra observada no comentário de `transmitirNFCeContingencia` (`sefaz-gateway.ts` linha
333-337): transmitir **em até 24h após `dhCont`** evita rejeição por extemporaneidade. Isso é
outro caso de "reconciliação autoritativa" do ADR-0001 — um job periódico (mesma família do job
de `DesistirDeNumeroUseCase` já desenhado em §5 de `arquitetura.md`) precisa:

1. Listar `DocumentoFiscal` em `EmContingencia` há mais de ~20h (margem de segurança antes das
   24h) que ainda não transmitiram.
2. Tentar `TransmitirContingenciaAsync` uma última vez.
3. Se ainda falhar (rede fora há mais de 24h — cenário raro mas possível), marcar `Rejeitado` com
   motivo `"contingência expirada — extemporaneidade"` — **nunca** deixar pendurado
   indefinidamente (mesmo princípio do invariante `[ ]` de §7 de `arquitetura.md`: todo
   `NumeroAlocado`/`EmContingencia` termina em terminal).

### 6.4 Numeração em contingência não muda (§5 de `arquitetura.md` já cobre)

Contingência NFC-e usa a MESMA série exclusiva por terminal já fixada em §5 de
`arquitetura.md` — não há numeração especial de contingência. `tpEmis=9` é um atributo do XML,
não da chave de numeração — o número já alocado antes de `PrepararContingenciaAsync` continua
sendo o número da nota, contingência ou não.

---

## 7. Tratamento de rejeição (422) e status

### 7.1 O que a SEFAZ (via gateway) pode responder, e pra onde cada resposta vai

| HTTP / `SefazResponse.status` | Significado | `ResultadoTransmissaoSefaz` | Transição de `DocumentoFiscal` |
|---|---|---|---|
| `2xx`, `status: 'autorizado'` | Nota aceita e autorizada | `.Autorizado(chaveAcesso, autorizadoEm)` | `NumeroAlocado → Autorizado` |
| `422`, `status: 'rejeitado'` | SEFAZ processou e **recusou** (erro de preenchimento/regra de negócio — corrigível, mesmo número pode ser reenviado) | `.Rejeitado(motivo)` — **`Result.Ok`**, nunca `Falhar` (contrato explícito do `IGatewayEmissaoSefaz.cs`: 422 é resposta de negócio, não falha de infra) | `NumeroAlocado → Rejeitado` (ou `Rejeitado → Rejeitado`, self-loop, se já era retry) |
| `status: 'denegado'` | SEFAZ recusa DEFINITIVA (ex.: CNPJ do emitente com inscrição suspensa) — número **não pode** ser reaproveitado, mas também não seria correto reenviar | `.Denegado(motivo)` | `NumeroAlocado → Denegado` (terminal) |
| `status: 'processando'` | SEFAZ recebeu mas ainda não decidiu (lote grande, contingência de autorização) | **Gap #9 (§11)** — `ResultadoTransmissaoSefaz` só tem 3 factories (`Autorizado`/`Rejeitado`/`Denegado`), nenhuma pra "ainda não sei" | Ver proposta abaixo |
| `401`/`403` | Auth errada (API key inválida, CNPJ do certificado não bate com emitente) | `Result.Falhar` (infra/config, nunca avança FSM) | Nenhuma — documento continua `NumeroAlocado`, log de erro de configuração |
| `400` | Payload malformado (DV inválido, cert expirado) | `Result.Falhar` | Nenhuma — é bug do mapper ou certificado vencido, precisa de intervenção manual antes de tentar de novo |
| `429` | Rate limit do gateway | `Result.Falhar` (retry mais tarde, não imediato — `Retry-After` do header, quando presente) | Nenhuma |
| `503` / `5xx` / timeout | Gateway/SEFAZ indisponível — TRANSIENTE | `Result.Falhar` após esgotar `MaxRetries` com backoff | Nenhuma — documento fica `NumeroAlocado` até o job de retry tentar de novo |

### 7.2 Gap #9 — resposta `'processando'` sem representação em `ResultadoTransmissaoSefaz`

Proposta: **não** modelar como uma 4ª transição de FSM. `'processando'` significa "SEFAZ ainda
não decidiu" — o documento continua legitimamente `NumeroAlocado` (nada mudou do ponto de vista
do agregado). A forma correta de tratar é:

```csharp
public sealed record ResultadoTransmissaoSefaz
{
    // ... Status/ChaveDeAcesso/AutorizadoEm/Motivo já existentes ...

    /// <summary>Quando true, a SEFAZ recebeu mas ainda não decidiu — o caller NÃO deve chamar
    /// nenhum Registrar*() do agregado (ele continua NumeroAlocado); deve agendar uma
    /// CONSULTA de status (não uma retransmissão) para mais tarde.</summary>
    public bool AindaProcessando { get; init; }

    public static ResultadoTransmissaoSefaz Processando() => new() { AindaProcessando = true, Status = StatusDocumentoFiscal.NumeroAlocado };
}
```

Um job de consulta periódica (`ConsultarStatusPendentesJob`, mesma família do
`consultaStatusRunner.ts` do saas-erp) chama um NOVO método do gateway,
`Task<Result<ResultadoTransmissaoSefaz>> ConsultarAsync(DocumentoFiscal)` → `/nfe/consultar`,
até obter um veredito definitivo (`Autorizado`/`Rejeitado`/`Denegado`) ou esgotar um prazo
máximo razoável (proposta: 48h, prazo de resposta assíncrona da SEFAZ é tipicamente minutos, não
dias — 48h já é folga generosa antes de considerar anômalo e alertar um operador humano).

### 7.3 Retransmissão de `Rejeitado` — caso de uso que ainda não existe

O próprio FSM já permite (`TransicoesPermitidas[Rejeitado] = [Autorizado, Denegado, Rejeitado,
Inutilizado]`, comentário no topo de `DocumentoFiscal.cs`: "Rejeitado ──Retransmitir() (mesmo
número)──►") mas **nenhum caso de uso hoje chama `TransmitirAsync` numa segunda vez** —
`EmitirDocumentoFiscalUseCase` só chega até `NumeroAlocado` e para (comentário explícito na
classe: "este caso de uso NUNCA chama RegistrarAutorizacao/RegistrarRejeicao/RegistrarDenegacao").
Isso é exatamente o invariante marcado `[ ]` (não confirmado) em §7 de `arquitetura.md`: "todo
número alocado termina em exatamente um de Autorizado/Denegado/Inutilizado".

**Este mapeamento propõe o caso de uso que fecha esse invariante — é o primeiro código a
escrever junto do adapter**, não depois dele:

```csharp
namespace SistemaX.Modules.Fiscal.Application.CasosDeUso;

/// <summary>Transmite (ou retransmite) um DocumentoFiscal em NumeroAlocado/Rejeitado —
/// a peça que fecha `docs/fiscal/arquitetura.md` §7 ("todo NumeroAlocado termina em terminal").
/// Idempotente por construção: FSM já rejeita a chamada se o documento já estiver em terminal
/// (Autorizado/Denegado/Cancelado/Inutilizado) — não precisa de guarda adicional aqui.</summary>
public sealed class TransmitirDocumentoFiscalUseCase(
    IDocumentoFiscalRepository documentos, IGatewayEmissaoSefaz gateway, IIntegrationEventBus bus)
{
    public async Task<Result<DocumentoFiscal>> ExecutarAsync(string documentoFiscalId, CancellationToken ct = default)
    {
        var documento = await documentos.ObterPorIdAsync(documentoFiscalId, ct);
        if (documento is null)
            return Result.Falhar<DocumentoFiscal>(new Error("fiscal.documento.nao_encontrado", $"'{documentoFiscalId}' não encontrado."));

        var transmissao = await gateway.TransmitirAsync(documento, ct);
        if (transmissao.Falha)
            return Result.Falhar<DocumentoFiscal>(transmissao.Erro); // infra — documento permanece como estava, caller decide retry

        var resultado = transmissao.Valor;
        if (resultado.AindaProcessando)
            return Result.Ok(documento); // nada muda — job de consulta assume a partir daqui (§7.2)

        var registro = resultado.Status switch
        {
            StatusDocumentoFiscal.Autorizado => documento.RegistrarAutorizacao(resultado.ChaveDeAcesso!, resultado.AutorizadoEm!.Value),
            StatusDocumentoFiscal.Rejeitado  => documento.RegistrarRejeicao(resultado.Motivo!),
            StatusDocumentoFiscal.Denegado   => documento.RegistrarDenegacao(resultado.Motivo!),
            _ => Result.Falhar(new Error("fiscal.transmissao.status_inesperado", $"Gateway devolveu status '{resultado.Status}' fora do esperado.")),
        };
        if (registro.Falha) return Result.Falhar<DocumentoFiscal>(registro.Erro);

        await documentos.SalvarAsync(documento, ct);

        foreach (var evento in documento.DomainEvents.OfType<DocumentoFiscalAutorizadoDomainEvent>())
            await bus.PublishAsync(evento.ParaEventoDeIntegracao(), ct);

        documento.ClearDomainEvents();
        return Result.Ok(documento);
    }
}
```

`EmitirDocumentoFiscalUseCase` continua responsável só por resolver+alocar (não muda); um
orquestrador (Application, ou o próprio handler de `VendaItensMovimentados` — ver §6 de
`arquitetura.md`) chama `EmitirDocumentoFiscalUseCase` e, se o resultado for `NumeroAlocado`,
encadeia `TransmitirDocumentoFiscalUseCase` na sequência — a MESMA transação local não precisa
cobrir os dois (numeração é a parte que exige atomicidade por lei, transmissão é I/O de rede que
pode falhar e ser retentada independentemente, ADR-0001 já separa esses dois mundos).

---

## 8. Idempotência ponta-a-ponta

Dois níveis distintos, cada um já coberto por um mecanismo diferente — nenhum novo mecanismo
precisa ser inventado:

1. **Criação do `DocumentoFiscal`** (evitar duas notas pra mesma venda) — já resolvido:
   `ObterPorOrigemAsync(tenantId, origem.Chave)` no topo de `EmitirDocumentoFiscalUseCase` +
   `UNIQUE(tenant_id, origem_modulo, origem_id)` no schema SQLite (§8/§11 de `arquitetura.md`).
   Nada muda aqui.
2. **Transmissão repetida do MESMO `DocumentoFiscal`** (`TransmitirDocumentoFiscalUseCase`
   chamado duas vezes — bug de job duplicado, dois workers, retry manual + automático
   simultâneos): a FSM já é a guarda suficiente — chamar `RegistrarAutorizacao` num documento
   que já está `Autorizado` falha (`TransicoesPermitidas[Autorizado] = [Cancelado]` — não inclui
   `Autorizado`), então uma segunda chamada concorrente que "vence a corrida" contra o SQLite
   simplesmente recebe `Result.Falhar` da FSM e não duplica nada. A própria SEFAZ é a segunda
   linha de defesa: reenviar o MESMO XML com a MESMA chave de acesso (numero+série+CNPJ+modelo,
   já todos fixos desde `AlocarNumero`) resulta em rejeição 539 "Duplicidade de NF-e" — comentário
   já presente em `number-sequence.ts` do saas-erp, mesmo comportamento esperado aqui.

Não é necessário desenhar uma chave de idempotência dedicada pro nível HTTP (`X-Idempotency-Key`
que o `saas-erp` usa em `/api/fiscal/emit`) — aquele mecanismo existe no saas-erp porque a rota
HTTP ali é chamada por um browser que pode duplo-clicar/dar refresh; no SistemaX, quem chama
`TransmitirDocumentoFiscalUseCase` é sempre código de Application (job, handler de evento),
nunca um cliente HTTP não confiável — o dedup por FSM + chave SEFAZ já é suficiente pra essa
topologia.

---

## 9. Cancelamento / Inutilização / Carta de Correção — extensão do port

`IGatewayEmissaoSefaz.cs` já declara explicitamente (comentário na própria interface): escopo é
só EMISSÃO; cancelamento e inutilização são "operações SEFAZ distintas — extensão aditiva deste
port (ou ports irmãos)". Proposta concreta (ports irmãos, não o mesmo `IGatewayEmissaoSefaz` —
mantém a interface de emissão pequena e focada):

```csharp
namespace SistemaX.Modules.Fiscal.Application.Ports;

public interface IGatewayCancelamentoSefaz
{
    /// <summary>Mapeia para /nfe/cancelar. Chamado por CancelarDocumentoFiscalUseCase DEPOIS
    /// que `DocumentoFiscal.Cancelar(justificativa)` já validou localmente (justificativa >= 15
    /// caracteres — mesma regra do layout SEFAZ, já no domínio). Payload: {chaveAcesso, protocolo,
    /// justificativa, ufEmitente, ambiente, certificado} — chaveAcesso/protocolo vêm de
    /// DocumentoFiscal.ChaveDeAcesso + o protocolo salvo na autorização original.</summary>
    Task<Result<ResultadoCancelamentoSefaz>> CancelarAsync(DocumentoFiscal documento, string justificativa, CancellationToken ct = default);
}

public interface IGatewayInutilizacaoSefaz
{
    /// <summary>Mapeia para /nfe/inutilizar. Chamado por um job periódico que agrega
    /// NumeroFiscalInutilizadoDomainEvent pendentes (§5 de arquitetura.md) — payload:
    /// {cnpj, serie, numeroInicial, numeroFinal, justificativa, modelo, ano, ufEmitente,
    /// ambiente, certificado}. numeroInicial==numeroFinal quando é 1 documento só; o schema
    /// aceita fechar um INTERVALO — útil se vários Desistir() da mesma série acumularem antes
    /// do job rodar (protocolar 1 inutilização de faixa em vez de N individuais).</summary>
    Task<Result> InutilizarAsync(string tenantId, string serie, long numeroInicial, long numeroFinal, string justificativa, CancellationToken ct = default);
}

public interface IGatewayCartaCorrecaoSefaz
{
    /// <summary>Mapeia para /nfe/carta-correcao. Erro leve pós-autorização (endereço, dado
    /// complementar) — NÃO existe caso de uso Application ainda porque DocumentoFiscal não tem
    /// conceito de "correção" no domínio hoje (Autorizado é imutável, §2.6 arquitetura.md,
    /// correta e intencionalmente). CCe é só texto, não muda nenhum campo do agregado — se
    /// vier a existir, é side-channel (log de correções) nunca uma transição de status.</summary>
    Task<Result> RegistrarCorrecaoAsync(string chaveAcesso, string correcao, string ufEmitente, int? sequencia, CancellationToken ct = default);
}
```

`InutilizarAsync` conecta diretamente ao gap de processo já nomeado em §5 de `arquitetura.md`
("`DesistirDeNumeroUseCase` já existe... falta o job periódico que agrega e protocola") — este
mapeamento fixa o formato exato do payload que esse job precisa montar quando for escrito.

---

## 10. Checklist de invariantes que este mapeamento deve preservar

- [ ] O mapper (`Fiscal.Infrastructure.Sefaz`) nunca lê `IPerfilFiscalNcmRepository`/
      `ITributacaoProdutoRepository`/`IRegraFiscalPorOperacaoRepository` — só projeta
      `DocumentoFiscal` já resolvido (§1).
- [ ] Toda chave de JSON cujo valor seria `null`/zero-não-aplicável é OMITIDA do objeto
      serializado, nunca enviada como `null`/`0` explícito (mesma disciplina do `stripEmpty` do
      saas-erp — esquemas SEFAZ rejeitam tag vazia em vários campos opcionais).
- [ ] `csosn` e `cst` NUNCA aparecem os dois no mesmo bloco `icms` — decidido por
      `RegimeTributarioExtensions.UsaCsosn(regime)`, nunca inspecionando o valor da string (§4.4).
- [ ] `Result.Falhar` do `IGatewayEmissaoSefaz` é reservado a falha de infraestrutura — 422
      (rejeição de negócio) é SEMPRE `Result.Ok(ResultadoTransmissaoSefaz.Rejeitado(...))` (§7.1,
      já fixado no comentário original da interface).
- [ ] Nenhuma transmissão bem-sucedida (`Autorizado`/`Rejeitado`/`Denegado`) é perdida por causa
      de uma falha ao PERSISTIR o resultado — mesmo padrão "grava antes/depois de forma que
      nunca fique órfã" já usado em `EmitirDocumentoFiscalUseCase` (transação em volta de
      `AlocarNumero`+`SalvarAsync`).
- [ ] `EmContingencia` (proposto em §6.2), se implementado, nunca aceita `Desistir()` como
      transição válida — o DANFCE já impresso é fato consumado.
- [ ] Retry/backoff do adapter HTTP replica exatamente os códigos e tempos do
      `sefaz-gateway.ts` (§2) — nunca uma segunda política divergente contra o mesmo gateway.

---

## 11. Gaps concretos — o que falta antes deste mapeamento rodar de ponta a ponta

| # | Gap | Onde aparece | Proposta (já detalhada acima) |
|---|---|---|---|
| 1 | ✅ `DocumentoFiscal` não tem destinatário/consumidor/tomador (cliente da nota) | Todo bloco `destinatario`/`consumidor`/`tomador`, §4.2, §5 | `DestinatarioDocumentoFiscal` (nullable) — novo parâmetro de emissão ou campo do agregado. **Persistência SQLite fechada** (`SqliteDestinatarioDocumentoFiscalRepository`); porta em memória seguia satisfazendo o contrato antes disso. |
| 2 | ✅ Certificado digital (.pfx+senha) e CSC (NFC-e) sem porta de acesso | `certificado` em todo payload; `csc` em NFC-e, §4.6 | `ICertificadoDigitalRepository` + extensão de `ConfiguracaoFiscalTenant` (CscId/CscToken). **Persistência SQLite fechada** (`SqliteCertificadoDigitalRepository`) — CSC (NFC-e) continua em aberto. |
| 3 | ✅ `DocumentoFiscal` não tem forma de pagamento | `pagamento.formas[]`, §4.5 | `FormaPagamentoParaEmitir` como parâmetro de emissão, nunca persistido no agregado. **Persistência SQLite fechada** (`SqliteFormaPagamentoDocumentoFiscalRepository`). |
| 4 | ✅ Cadastro do emitente (CNPJ/Razão Social/IE/IM/endereço) fora de `ConfiguracaoFiscalTenant` | `emitente`/`prestador`, §3, §5 | `ICadastroFiscalEmitenteRepository` — cópia local via evento, mesmo padrão de `NcmPorProduto`. **Persistência SQLite fechada** (`SqliteCadastroFiscalEmitenteRepository`) — cópia via evento de integração ainda não existe (seed manual continua). |
| 5 | ✅ Sem referência à NF-e original para devolução (`finalidade=4`) | `referencias`, §4.6 | `IReferenciaDevolucaoDocumentoFiscalRepository` (`ObterRefNFeAsync`/`VincularAsync`) consultado por `SefazApiGateway.ResolverInsumosAsync` e projetado em `NFePayload.Referencias` por `DocumentoFiscalPayloadMapper.MontarNFe`. **Persistência SQLite fechada** (`SqliteReferenciaDevolucaoDocumentoFiscalRepository`, tabela `fiscal_referencias_devolucao_documento` da V2). Coberto por `SefazApiGatewayTests.TransmitirAsync_ComReferenciaDevolucaoVinculada_IncluiRefNFeNoPayload` + contract tests (InMemory/SQLite) dedicados. |
| 6 | ✅ `ItemDocumentoFiscal` sem GTIN/unidade comercial | `cEAN`/`unidade`/`unidadeTrib`, §4.3 | `DadosFiscaisProdutoCache.Gtin`/`UnidadeComercial` (colunas da V2 em `fiscal_dados_produto_cache`), resolvido em `SefazApiGateway.ResolverInsumosAsync` e projetado por `DocumentoFiscalPayloadMapper.MontarItens` — fallback `"SEM GTIN"`/`"UN"` quando ausente, nunca inventado. Coberto por `SefazApiGatewayTests` (com/sem cache) + contract tests do repositório. |
| 7 | 🟡 Bloco JSON `icmsUFDest` (DIFAL/FCP) não confirmado no gateway externo | §4.4 | `MontarIcmsUfDest` já projeta `pICMSInter`/`pICMSUFDest`/`pFCPUFDest`/`vFCPUFDest` a partir do MESMO `TributoResolvidoItem` que resolveu ICMS/DIFAL/FCP do item — nunca hardcoded (coberto por `SefazApiGatewayTests.TransmitirAsync_ComDifalEFcp_IcmsUfDestUsaAliquotaRealDoItemNuncaHardcoded`, que prova 4% de item importado ≠ 12% fixo). O que resta é puramente externo: validar o schema do bloco com quem mantém `emissao.tensorroot.com` antes da primeira nota real com DIFAL — bloqueio de terceiro, não deste repo. |
| 8 | ✅ `StatusDocumentoFiscal` sem estado de contingência | FSM, §6.2 | `StatusDocumentoFiscal.EmContingencia` + `DocumentoFiscal.PrepararContingencia()` + transições dedicadas em `TransicoesPermitidas` (guarda: `Desistir()` nunca aceita `EmContingencia` como origem — DANFCE já impresso é fato legal irreversível). Ver §6.2/§6.3 deste documento. |
| 9 | 🟡 `ResultadoTransmissaoSefaz` sem representação de `'processando'` | §7.2 | Campo `AindaProcessando` já existe. **`RetransmitirDocumentosPendentesUseCase` fechado**: reavalia `NumeroAlocado` velhos e retransmite (`TransmitirDocumentoFiscalUseCase.ExecutarAsync`); `ConsultarAsync` só é alcançável quando o agregado passar a guardar um recibo provisório (hoje `Processando()` não carrega um) — host/cron que aciona o use case periodicamente ainda não está wireado (mesmo status do "cron ParcelaVencida" do Financeiro). |
| 10 | ✅ Nenhum caso de uso chama `TransmitirAsync` hoje — invariante `[ ]` de §7 `arquitetura.md` continua aberta | §7.3 | `TransmitirDocumentoFiscalUseCase` implementado (`CasosDeUso/TransmitirDocumentoFiscalUseCase.cs`) — chamado por `EmitirDocumentoFiscalUseCase` na primeira tentativa e por `RetransmitirDocumentosPendentesUseCase` no retry. Protocolo de autorização (`resultado.Protocolo`) agora também é persistido em `DocumentoFiscal.Protocolo`, fechando o gap irmão que fazia `SefazApiGateway.CancelarAsync` mandar `protocolo` vazio. |
| 11 | Cancelamento/Inutilização/CCe fora do escopo de `IGatewayEmissaoSefaz` (por design, já documentado na própria interface) | §9 | `IGatewayCancelamentoSefaz`/`IGatewayInutilizacaoSefaz`/`IGatewayCartaCorrecaoSefaz` — ports irmãos propostos |

Nenhum destes gaps invalida o modelo de domínio já implementado (`docs/fiscal/arquitetura.md`) —
são, sem exceção, coisas que o desenho atual deliberadamente deixou de fora do agregado fiscal
(cadastro de cliente, certificado, forma de pagamento) por não serem fato tributário, e que
precisam de um lar em outro lugar (parâmetro de caso de uso, port irmão, ou módulo adjacente)
antes da primeira emissão real rodar de ponta a ponta.

---

## 12. Documentos relacionados

| Arquivo | Conteúdo |
|---|---|
| `docs/fiscal/arquitetura.md` | Modelo de domínio completo (DocumentoFiscal/OperacaoFiscal/tributos/FSM/numeração) — pré-requisito de leitura deste documento |
| `docs/arquitetura/adr/0002-fiscal.md` | Decisão resumida (formato ADR) do desenho tributário |
| `docs/arquitetura/adr/0001-sincronizacao-local-first.md` | Fixa "numeração fiscal = alocação autoritativa" e o princípio de reconciliação autoritativa que §6 (contingência) deste documento aplica à transmissão |
| `saas-erp/lib/services/sefaz-gateway.ts` | Fonte de verdade do contrato HTTP do gateway de emissão (endpoints, `SefazResponse`, retry/backoff, `NfsePayload`) |
| `saas-erp/app/api/fiscal/emit/route.ts` | Referência funcional do payload NF-e/NFC-e/NFS-e campo-a-campo (o que este documento mapeia a partir de `DocumentoFiscal` em vez de a partir de request de UI) |
| `saas-erp/app/api/fiscal/retry/route.ts` | Referência funcional do fluxo de retry/contingência (idempotência, salvaguarda de numeração) |
| `saas-erp/lib/contracts/fsm/fiscalDocument.ts` | FSM de referência (TS) que inspira o `EmContingencia` proposto em §6.2 |
| `gestao-raiz/src/services/fiscal.service.ts` | Referência funcional (preflight-antes-do-gateway, idempotência por `orderId`) — **nunca** referência de resolução tributária (é o código auditado como defeituoso) |
| `Fiscal.Application/Ports/IGatewayEmissaoSefaz.cs` | Porta que este documento implementa (assinatura já fixada, não muda) |
