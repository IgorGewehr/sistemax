# Módulo Compras

O **terceiro emissor** de eventos de integração do sistema (ao lado de Vendas): fecha o ciclo
"mercadoria entra" que o Estoque e o Financeiro já sabiam assinar antes deste módulo existir.
Nunca é chamado por ninguém — só publica `CompraRecebida`/`CompraItensRecebidos`/`CompraEstornada`
depois do commit local. Plano completo em `scratchpad/design/compras-plano.md`.

## O que existe (Fase 1 — MVP entrada, plano §12)

```
SistemaX.Modules.Compras.Domain/
  Comum/            Quantidade (milésimos), ChaveDeAcesso (VO, 44 dígitos), SourceRef, IdGenerator
  Fornecedores/      Fornecedor (AR) + StatusFornecedor (FSM Ativo⇄Inativo, Ativo→Bloqueado→Ativo)
  Vinculos/          VinculoProdutoFornecedor (AR) — o de-para aprendido fornecedor+cProd→produto
  Notas/             NotaDeCompra (AR) + ItemDeNotaDeCompra + TotaisDaNota + CustoDeEntrada (rateio)
                      + MatchState + OrigemNota + StatusNotaDeCompra (FSM) + NotaDeCompraDomainEvents

SistemaX.Modules.Compras.Application/
  Ports/              INotaDeCompraRepository, IFornecedorRepository, IVinculoProdutoFornecedorRepository
  CasosDeUso/          RegistrarEntradaDeNota (pipeline §4) · ResolverMatchDeItem · IgnorarItemDaNota ·
                       ConfirmarRecebimento · EstornarRecebimento · DescartarNota · Fornecedor (CRUD+FSM)
  ComprasModule.cs     IModule (Codigo="compras")

SistemaX.Modules.Compras.Infrastructure/
  InMemory/            3 adapters in-memory
  ComprasInfrastructureModule.cs  IModule (Codigo="compras.infra", DependeDe=["compras"])
```

## O momento do módulo: `ConfirmarRecebimento`

`NotaDeCompra.ConfirmarRecebimento` (FSM `EmConferencia → Recebida`) é onde tudo acontece: valida
que todo item não-ignorado tem match resolvido e fator de conversão aplicado, roda o rateio de
custo de entrada (`CustoDeEntrada.Ratear` — landed cost: `vProd + frete/seguro/outras residuais
rateadas por participação no vProd + IPI + ICMS-ST − desconto`, com o **último item absorvendo o
resíduo de arredondamento** para que `Σ landed == vNF` seja uma igualdade exata em centavos),
congela o resultado em cada item, e levanta `NotaDeCompraRecebidaDomainEvent`. A Application
(`ConfirmarRecebimentoUseCase`) persiste esse fato **antes** de publicar qualquer coisa no
barramento — commit local primeiro, publicação depois, nunca o contrário (regra dura R3).

Um fato, dois eventos de integração lado a lado: `CompraRecebida` (Financeiro — já existia,
`CompraRecebidaHandler` cria a ContaAPagar) e `CompraItensRecebidos` (Estoque — já existia,
`CompraItensRecebidosHandler` credita quantidade e recalcula custo médio). **Este módulo não
alterou nenhuma assinatura de evento existente** — `Modules.Abstractions/IntegrationEvents.cs` já
trazia os dois eventos e os respectivos handlers prontos (o Estoque rodou antes e já wireou o lado
dele); Compras só passou a ser quem efetivamente os publica.

## De-para aprendido: a peça central da superioridade sobre as referências estudadas

`VinculoProdutoFornecedor` (fornecedor + código do produto no fornecedor → produto do catálogo +
fator de conversão de unidade) é o que nenhuma das duas referências de mercado estudadas persiste.
`RegistrarEntradaDeNotaUseCase` roda uma cascata de match: (1) vínculo aprendido → `Auto`, zero
interação; (2) produto já informado por quem chamou (nota manual/UI) → `Manual`; (3) senão →
`SemMatch`, bloqueia até resolução humana em `ResolverMatchDeItemUseCase`, que grava/atualiza o
vínculo — a próxima nota do mesmo fornecedor com o mesmo `cProd` cai direto na estratégia 1.

## Invariantes de negócio (validadas em `ConfirmarRecebimento`, plano §3.3)

1. `Σ CustoTotalEntrada` dos itens == `vNF` (centavos exatos).
2. Todo item não-ignorado tem `MatchState ∈ {Auto, Manual}` — nunca `Sugerido`/`SemMatch` numa nota
   `Recebida` (recebimento parcial é honesto: item ignorado fica **fora** do evento, nunca um buraco
   silencioso).
3. Item com unidade da NF diferente da unidade de estoque exige fator de conversão `> 0` — sem
   fator, `Result.Falhar` (nunca corrompe quantidade).
4. Dedupe: 1 nota por `ChaveDeAcesso` por tenant — reimportar o mesmo XML abre a nota já existente
   (`RegistrarEntradaDeNotaUseCase`), nunca duplica, nunca erra pro usuário.
5. Fornecedor sem documento (produtor rural/informal) nunca deduplica por documento vazio — lição
   real corrigida do estudo de mercado (dois fornecedores distintos sem CNPJ eram fundidos no
   primeiro doc `""`). O agregado `Fornecedor` não decide isso; `CadastrarFornecedorUseCase` só
   busca por documento quando ele não é vazio.
6. `Estornar` exige `Status == Recebida`; `ConfirmarRecebimento` exige `Status == EmConferencia`
   (`Fsm<StatusNotaDeCompra>.ValidarTransicao`) — dois operadores confirmando ao mesmo tempo: o
   segundo perde limpo, sem duplicar.

## O que é stub / fora do escopo desta entrega

- **Persistência real**: 3 adapters in-memory — trocar por SQLite é um novo adapter atrás de cada
  port, zero mudança em Domain/Application (mesmo padrão do Estoque/Financeiro).
- **Parser de XML NF-e**: os casos de uso já recebem o fato estruturado (`EntradaDeNotaInput`/
  `ItemDeEntradaInput`) — o parser regex namespace-agnóstico sobre `<det>`/`<ICMSTot>`/`<cobr><dup>`
  é um adapter de Infrastructure (I/O de formato externo) que fica para quem plugar leitura de
  arquivo real; o pipeline de classificação/dedupe/match já está pronto para recebê-lo.
- **Pedido de Compra + three-way match + divergências** (plano §7, fase 2): o mockup interativo já
  desenha a tela (`scratchpad/mockups/compras.html`, conferência de `NF-e 8790/1` contra
  `PC-0042`), mas o agregado `PedidoDeCompra`/`DivergenciaDeRecebimento` não foi implementado nesta
  fase — fora do pedido explícito (Domain: Fornecedor/NotaDeCompra/itens/entrada; Application: caso
  de uso de entrada; Infrastructure: InMemory).
- **Frete avulso/CT-e** (`FreteDeCompraRecebido`/`CustoDeEntradaAjustado`, plano §6.2/§12 fase 2) e
  **devolução ao fornecedor** (`CompraDevolvida`, fase 3) — eventos ainda não declarados em
  `Modules.Abstractions`, roadmap.
- **Sync SEFAZ DFe + manifestação** (fase 3) — depende de certificado A1 na Cloud, fora do escopo.
- **Read-models de análise** (histórico de preço, scorecard de fornecedor, painel de compras) —
  demonstrados no mockup como dados derivados client-side; a versão real lê do razão de eventos do
  próprio módulo (mesmo racional dos read-models do Estoque), fica para quando a UI ligar em dados
  reais.

## Testes

`tests/SistemaX.Modules.Compras.Tests` (48 testes) — VO `ChaveDeAcesso`/`Quantidade`, invariantes
de `Fornecedor` (FSM + dedupe por documento), `NotaDeCompra` (FSM completo, match pendente bloqueia
recebimento, fator de conversão ausente falha explícito, ignorar item exclui do evento), rateio de
`CustoDeEntrada` (reconciliação `Σ == vNF` com arredondamentos hostis), `VinculoProdutoFornecedor`
(aprendizado e reaprendizado preservando Id), e os casos de uso ponta a ponta — `RegistrarEntradaDe
Nota` (dedupe por chave, cascata de match), `ConfirmarRecebimento`/`EstornarRecebimento` (publicação
dos dois eventos de integração na ordem certa, via `FakeIntegrationEventBus`), `IgnorarItem`/
`DescartarNota`.
