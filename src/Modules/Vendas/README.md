# Módulo Vendas — PDV

Agregado `Venda` (frente de caixa) + casos de uso de montagem/conclusão + adapter in-memory.
Exemplo EMISSOR canônico de "tudo alimenta o financeiro" (ver `docs/arquitetura/ARCHITECTURE.md` §5)
e módulo de referência do guia `docs/arquitetura/COMO-CRIAR-UM-MODULO.md`.

## O que existe

```
SistemaX.Modules.Vendas.Domain/
  Venda.cs                 agregado raiz — FSM Aberta → Concluida → Estornada (inalterada)
  ItemDeVenda.cs            VO com Id (endereçamento estável), desconto por linha
  PagamentoDeVenda.cs        VO — split payment, troco (só dinheiro)
  MetodoPagamento.cs         enum
  StatusVenda.cs             FSM
  VendaDomainEvents.cs       evento de domínio → ParaEventoDeIntegracao()

SistemaX.Modules.Vendas.Application/
  Ports/IVendaRepository.cs
  CasosDeUso/VendaUseCases.cs   Iniciar · Montar (item/desconto/pagamento) · Concluir · Estornar
  VendasModule.cs                IModule (Codigo="vendas")

SistemaX.Modules.Vendas.Infrastructure/
  InMemory/InMemoryVendaRepository.cs
  VendasInfrastructureModule.cs  IModule (Codigo="vendas.infra", DependeDe=["vendas"])
```

## Regra central: montagem vs pagamento sem 4º status

A FSM do agregado continua com só 3 estados. A distinção entre "carrinho em montagem" e "recebendo
pagamento" é uma **invariante**, não um estado novo: assim que o primeiro `PagamentoDeVenda` é
registrado, itens/descontos ficam travados (`GarantirEmMontagem()` — código `venda.pagamento_ja_iniciado`).
Isso preserva o contrato de status com o Financeiro e ainda assim impede editar o carrinho depois
que dinheiro já mudou de mão.

## Pagamento: `Valor` nunca inclui troco

`PagamentoDeVenda.Valor` é sempre a parcela do TOTAL que aquele pagamento cobre — nunca excede o
`Restante` no instante do registro (`venda.pagamento_excede_restante`), em qualquer método. Em
dinheiro, `ValorRecebido` pode ser maior que `Valor` (cédula redonda); a diferença é `Troco`,
sempre **calculado**, nunca armazenado. Em qualquer outro método, `ValorRecebido` diferente de
`Valor` é rejeitado (`venda.pagamento.troco_apenas_dinheiro`) — não existe "troco de PIX".

## Evento de integração — o que NÃO mudou

`Concluir()` continua emitindo `VendaConcluidaDomainEvent → VendaConcluida` (Modules.Abstractions)
com a MESMA forma de hoje: `TotalCentavos` + `FormaPagamento` (string). Com split payment,
`FormaPagamento` passa a ser o **método de maior valor** entre os registrados — ver
`Venda.FormaPagamento`. Uma evolução aditiva do contrato (`Itens[]`/`Pagamentos[]`/dimensões de
loja) está documentada, **não implementada**, no topo de `VendaDomainEvents.cs` — mexer em
`Modules.Abstractions/IntegrationEvents.cs` é decisão de quem também mantém o Financeiro.

## O que é stub / não está aqui

- **Persistência real**: `InMemoryVendaRepository` — trocar por SQLite local é só um novo adapter
  atrás do mesmo `IVendaRepository` (nenhuma mudança em Domain/Application).
- **`SessaoDeCaixa`** (abertura/sangria/suprimento/fechamento cego), fiscal (NFC-e), hardware
  (balança/impressora/TEF/PIX dinâmico) e o wiring no `Host.Desktop`/`SistemaXHost` — fora do
  escopo desta fatia; ver `scratchpad/design/pdv-plano.md` (roadmap V1–V5) para o desenho completo.
- Registro em `SistemaXHost.Bootstrap` (`.Adicionar(new VendasModule()).Adicionar(new VendasInfrastructureModule())`)
  fica para quem for religar o composition root do Host.Desktop.

## Testes

`tests/SistemaX.Modules.Vendas.Tests` — invariantes de total/desconto, FSM (transições válidas e
inválidas), pagamento (split/troco/excedente) e evento (domínio → integração, chave de
idempotência, ordem commit-depois-publica nos casos de uso).
