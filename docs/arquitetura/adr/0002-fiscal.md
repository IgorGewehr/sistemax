# ADR-0002 — Fiscal adaptável: regime é dado plugável, CSOSN/CST resolvidos por regra configurável (nunca hardcode), NCM tributado por item com default+override, numeração por autoridade dedicada (não CRDT)

**Status:** Aceito · **Data:** 2026-07-17 (revisado 2026-07-17) · **Contexto do produto:** módulo
Fiscal do ERP de bancada — NF-e/NFC-e/NFS-e/MDF-e emitidas a partir do PDV (LAN) e do back-office
da loja, regimes tributários brasileiros (MEI, Simples Nacional, Simples Nacional-sublimite,
Lucro Presumido — preparado para Lucro Real), multi-tenant por loja (`TenantId`), construído
sobre a base local-first do ADR-0001.

## Pergunta que este ADR responde
> "Como desenhar o Fiscal para que regime tributário, CSOSN/CST e tributação por NCM sejam
> TROCÁVEIS/ESTENDÍVEIS sem refatorar a base — em vez de repetir o defeito estrutural encontrado
> na auditoria do `gestao-raiz` (CSOSN hardcoded no motor que emite de verdade, dois motores de
> cálculo tributário divergentes, config fiscal de produto gravada e nunca lida)?"

**Resposta curta:** **Regime é dado estável (enum fechado, muda raramente). Como o regime
tributa cada operação é DADO configurável (tabela, editável em runtime), nunca código.** O único
código é o motor de resolução — a função que sabe COMBINAR regime + operação + NCM até chegar
num CSOSN/CST; os valores em si (quais CSOSN, quais alíquotas) são linhas de tabela. Tributação
de NCM segue uma cascata de 2 camadas (perfil padrão por NCM/regime, override pontual por
produto) que nunca falha silenciosamente. Numeração fiscal é sempre alocação autoritativa de uma
única autoridade por chave — nunca merge/CRDT.

## Decisão

1. **`RegimeTributario` é um enum fechado de 5 valores** (MEI, Simples Nacional, Simples
   Nacional-sublimite, Lucro Presumido, Lucro Real) — é um fato legal do Brasil, não um conceito
   que o tenant inventa. Estender para um regime novo (ex.: quando a Reforma Tributária mudar a
   base) é adicionar um valor de enum + popular regra nova — nunca reescrever o motor.

2. **CSOSN/CST nunca são literais de código.** Vivem exclusivamente como valor dentro de
   `RegraFiscalPorOperacao` — uma tabela de decisão chaveada por
   `(RegimeTributario, TipoOperacaoFiscal, UF origem/destino, indicador ST)`, seedável e editável
   em runtime (por suporte/contador), sem deploy. Se a combinação não tem linha cadastrada, a
   resolução retorna `Result.Falhar` — nunca um default mudo (o defeito exato encontrado no
   gestao-raiz: `defaultIcmsCSOSN = crt !== '3' ? '400' : undefined`, aplicado a qualquer produto
   sem tributação explícita no payload). **Correção de uma revisão posterior a esta decisão:**
   `SimplesNacionalSublimite` (CRT=2, excesso de sublimite) usa CST, não CSOSN — o excesso de
   sublimite tira exatamente o ICMS/ISS do tratamento simplificado, mesmo a empresa continuando
   optante do Simples para os demais tributos. Só `Mei`/`SimplesNacional` (CRT=1) usam CSOSN. Ver
   `docs/fiscal/arquitetura.md` §2.1 para o detalhe e por que o agrupamento errado quebraria a
   emissão desse regime especificamente.

3. **Tributação de NCM em 2 camadas, cascata explícita:** `PerfilFiscalNCM` (padrão por
   `TenantId + Regime + NCM` — exige ST? CEST? IPI? origem da mercadoria? tratamento padrão de
   PIS/COFINS) e `TributacaoProduto` (override pontual por `productId`, campo a campo — incluindo
   o próprio CSOSN/CST de ICMS, não só ST/CEST/IPI/PIS/COFINS — com motivo obrigatório quando
   preenchido). A resolução por item é `override ?? perfil ?? Falha` — nunca
   `override ?? perfil ?? valor-padrão-inventado`. Isso fecha o gap mais grave da auditoria: no
   gestao-raiz, a configuração fiscal por produto (`product.impostos`) era gravada pela tela de
   cadastro e **nunca chegava** ao payload de emissão real. `Origem da Mercadoria` (0-8, campo
   obrigatório de todo item de ICMS, que força alíquota interestadual de 4% para importados —
   Resolução do Senado 13/2012) e o DIFAL/FCP de venda interestadual a consumidor final
   não-contribuinte também entram nesta cascata (`docs/fiscal/arquitetura.md` §2.2, §2.4, §2.6,
   §3) — ausentes de uma primeira versão deste design, adicionados numa revisão antes da
   implementação começar.

4. **Um único motor de cálculo, nunca dois.** `MotorDeCalculoTributario` (função pura em
   `Fiscal.Domain`) é chamado tanto pela emissão real quanto por qualquer preview de UI. O
   gestao-raiz tinha `tax-calculation.service.ts` (regras corretas, órfão) e uma segunda lógica
   inline em `fiscal.service.ts` (pobre, é a que roda) — as duas nunca foram reconciliadas.
   Aqui não há como divergir porque não há um segundo caminho.

5. **Numeração fiscal é sempre alocação autoritativa de UMA autoridade por chave — nunca
   contador CRDT, nunca "detectar colisão e renumerar" pós-fato.** Dois mecanismos concretos, sem
   coordenação distribuída em nenhum dos dois: (a) NFC-e — série dedicada e exclusiva por
   terminal de PDV, cada linha da `SequenciaFiscal` só é escrita por um processo; (b) NF-e/MDF-e
   — série única da loja, autoridade única no `Store.Server`. `Cloud.Api` nunca aloca número. A
   alocação roda na MESMA transação SQLite local que persiste o documento em `NumeroAlocado`
   (nunca dois passos separados) — mesma disciplina de "unidade de crash-safety é a transação do
   banco, não a lógica da app" já registrada em `docs/robustez/robustez-hardware-licoes.md` §1.

6. **Todo tributo calculado carrega a situação tributária junto do valor, para sempre.**
   `TributoResolvidoItem.SituacaoTributaria` (CST/CSOSN) é gravado como parte imutável do item no
   momento da emissão — nunca precisa ser reinferido depois. O gestao-raiz descartava esse campo
   no parse pós-autorização (`convertToFirestoreTaxes`), obrigando o gerador de SPED a
   "adivinhar" (`inferCstIcms`) — risco real de divergência SPED×NF-e numa auditoria.

## Por que NÃO um enum/switch por regime cobrindo CSOSN direto

A tentação óbvia é `switch (regime) { case Simples: return "102"; ... }` — é exatamente o que o
gestao-raiz fez, e quebra por dois motivos: (a) o CSOSN certo depende também da **operação**
(venda normal ≠ devolução ≠ transferência) e do **NCM** (tem ST? tem redução de base?) — um
switch só em regime nunca cobre isso sem crescer para um emaranhado de `if` aninhado; (b) toda
correção de alíquota/CSOSN por convênio novo, ato COTEPE, ou erro de cadastro **exige deploy de
código**. Separar "o que é regime" (código, estável) de "como o regime tributa" (dado, mutável)
é o que permite a um contador corrigir uma alíquota de UF sem esperar uma release — e é também o
que torna a transição da Reforma Tributária (IBS/CBS, 2026–2033) uma questão de popular linhas
novas, não reescrever o motor.

## Por que NÃO CRDT/renumeração para número fiscal

`docs/robustez/robustez-hardware-licoes.md` §3 documenta que o Supermarket-OS usa
"detectar colisão de `sale_number` e renumerar no servidor" — decisão correta **para um
identificador interno de venda**, porque nada de fora do sistema depende daquele número
especificamente. Um número de NF-e é diferente: no instante em que aparece assinado num XML
autorizado pela SEFAZ, ele é um fato jurídico externo — não existe "renumerar uma nota já
autorizada". CRDT (merge convergente) garante que dois nós cheguem ao mesmo estado, não que esse
estado seja **legal**; dois PDVs "convergindo" para o mesmo número de NF-e é uma nota duplicada
perante o Fisco, não um conflito resolvido. Por isso aqui a exigência é mais forte que em
qualquer outro contador do sistema: a colisão não pode ser corrigida depois, tem que ser
estruturalmente impossível (autoridade única por chave, nunca dois escritores na mesma linha).

## Consequências

- **(+)** Corrigir uma alíquota de UF, cadastrar um NCM novo, ou reagir a um convênio ICMS
  novo é edição de dado (`RegraFiscalPorOperacao`/`PerfilFiscalNCM`), não deploy.
- **(+)** Lucro Real (e, no limite, IBS/CBS da Reforma Tributária) entram como valor de enum +
  linhas de regra novas — o modelo já foi desenhado para isso, sem refatoração.
- **(+)** Impossível a nota sair com um CSOSN "chutado" — falta de configuração bloqueia a
  emissão (`DocumentoFiscal.Bloquear`) e nomeia o motivo, nunca inventa um valor.
- **(+)** Zero risco de divergência SPED×NF-e por reinferência de CST — o valor real fica gravado
  desde a resolução.
- **(−)** Mais peças que um `if/switch` direto: a tabela de regras, a cascata de resolução, o
  motor único — mais disciplina de modelagem inicial do que "resolver rápido com um switch". É o
  mesmo trade-off já aceito em ADR-0001 (reconciliação autoritativa é mais engenharia que merge
  cego, mas é o que torna o sistema correto, não só rápido de escrever).
- **(−)** Exige seed inicial de `PerfilFiscalNCM`/`RegraFiscalPorOperacao` por regime antes do
  primeiro tenant daquele regime poder emitir — não há "funciona de graça" no primeiro uso; é o
  preço de não hardcodar valores de exemplo como o gestao-raiz fazia.

## Decisão adicional (fechada nesta revisão) — CFOP configurável, com override por produto e por emissão

Pergunta em aberto quando este ADR foi escrito pela primeira vez: como resolver `5101`/`6101`
(produção própria) vs `5102`/`6102` (revenda de terceiros) — os dois CFOPs mais comuns do varejo
brasileiro — já que ambos compartilham `TipoOperacaoFiscal`/`EhInterestadual`/
`DestinatarioContribuinteIcms`? **Decisão de Igor:** CFOP PADRÃO é configurável (Settings→Fiscal,
mesmo padrão de dado seedável/editável das demais tributações), com **override por produto**
(cadastro do Estoque) **e override na emissão**. Ordem de resolução, sempre no domínio, nunca
hardcode: **emissão > produto > padrão-config** (o padrão, por sua vez, é resolvido por
NCM/regime/operação/UF via a mesma disciplina de `RegraFiscalPorOperacao`, chaveado também pela
natureza da operação do produto). Implementado:

- `NaturezaOperacaoProduto` (`ProducaoPropria | RevendaDeTerceiros | ImportacaoPropria`) +
  `CfopOverride` viraram campos de `DadosFiscaisProduto` no módulo **Estoque** — é o cadastro do
  produto, não do Fiscal, que responde "este item eu produzo ou revendo?" (mesma fronteira de
  NCM/CEST). `Produto.AtualizarDadosFiscais(...)` (gap documentado abaixo) materializa a mutação.
- `RegraCfop(TenantId?, TipoOperacaoFiscal, EhInterestadual, DestinatarioContribuinteIcms,
  NaturezaOperacaoProduto) → Cfop` é a camada "padrão-config" no Fiscal — dado seedável, nunca
  `const`/`switch`.
- `IResolvedorDeCfop` (Application) resolve a cadeia dos 3 níveis; falha nomeada
  (`fiscal.cfop.nao_encontrado`) se nenhum resolve — nunca um CFOP "chutado", mesma régua da
  decisão #2 (CSOSN) deste ADR.

Ver `docs/fiscal/arquitetura.md` §2.3 para o detalhe completo (inclusive por que a natureza da
operação vive no Estoque e não aqui) e §4 para a ponte de evento+cache que propaga o dado sem
Fiscal acoplar em `Estoque.Domain`.

## Estado atual no repo

`src/Modules/Fiscal/{Domain,Application,Infrastructure}` implementado e compilando (`dotnet build`
verde, testes de invariante de `Money`/FSM/motor de cálculo passando) — registrado em
`SistemaXHost` (Host.Desktop). `docs/fiscal/arquitetura.md` continua sendo o detalhamento completo
do modelo de domínio, fluxo de resolução por item, e tabela de rastreamento defeito→correção contra
a auditoria do `gestao-raiz`. Escopo desta primeira implementação é o CORE tributário — a
integração SEFAZ/certificado (`IGatewayEmissaoSefaz`) continua fora de escopo, como fixado acima.

Pré-requisito de outro módulo que este design assume (mas não implementa): `Vendas.Application`
publicando `VendaItensMovimentados` — hoje `Vendas` só tem `.Domain`/`.Application` sem essa
publicação, gap documentado no próprio catálogo de eventos (`Modules.Abstractions/IntegrationEvents.cs`).
O handler do lado do Fiscal (`VendaItensMovimentadosHandler`) já existe e está testado — falta só a
torneira do lado de quem mantiver `Vendas`. `Produto.AtualizarDadosFiscais(...)` (Estoque), citado
como gap na versão anterior deste ADR, foi implementado junto desta decisão (ver acima).

**Correções de uma revisão crítica posterior a esta decisão (antes de qualquer código), aplicadas
diretamente em `docs/fiscal/arquitetura.md` §2.6/§6/§11.1:** o esboço de `FiscalModule` foi
partido em `FiscalModule` (Application) + `FiscalInfrastructureModule` (Infrastructure,
`DependeDe: ["fiscal"]`, com adapters InMemory/Sqlite) — a versão anterior misturava as duas
camadas num só `IModule`, o que não compila no grafo `Infrastructure → Application → Domain` que
`Financeiro`/`Vendas`/`Estoque` já seguem; e `DependeDe` não lista mais `"estoque"`/`"vendas"`,
porque assinar um evento de integração de outro módulo não exige esse módulo fisicamente presente
(mesmo padrão que `EstoqueModule` já demonstra). `DocumentoFiscal` também passou a usar
`Fsm<StatusDocumentoFiscal>.ValidarTransicao` contra um mapa `TransicoesPermitidas` explícito, em
vez dos `if (Status is not (...))` soltos da primeira versão — a decisão #5 (numeração) e o
restante deste ADR não mudam.
