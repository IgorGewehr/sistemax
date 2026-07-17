# PDV

Upgrade visual do PDV pro padrão dos mockups (`docs/ui/mockups/pdv.html`), preservando à risca
o wiring real que o `Pdv.tsx` anterior já tinha contra o Bridge: `vendasApi.abrir()` ao iniciar →
`estoqueApi.listarProdutos()` pro catálogo → `adicionarItem` a cada bipe/toque → `registrarPagamento`
por forma escolhida → `concluir` ao finalizar. As **mesmas 5 chamadas**, zero a mais, zero a menos.

> Diferente de Compras/Agenda/Vendas (mock tipado), o PDV é 100% dado real — sem `mocks/pdv.ts`.
> Tudo que se vê na tela vem do `ProdutoDto`/`VendaDto` que a API devolve.

## Arquitetura

- `types.ts` — os 2 tipos de UI que o módulo precisa (`PdvScreen`, `TerminalMode`) + o shape de
  um botão de método de pagamento (`MetodoOpcao`). Tipos de domínio (`ProdutoDto`, `VendaDto`,
  `MetodoPagamento`, ...) vêm direto de `lib/api/{estoque,vendas}.ts` — nunca redeclarados aqui.
- `usePdv.ts` — todo o estado e as 5 chamadas de API. A página e os componentes só consomem o
  `PdvVm` que ele devolve, mesmo contrato de `useCompras`/`useAgenda`. Owns: catálogo (busca
  typeahead + grade por categoria), navegação entre as 3 telas, campo de pagamento (valor/recebido/
  troco), atalhos de teclado reais (`useHotkeys`, já existente em `lib/`).
- `VendaScreen.tsx` — Tela 1 (montagem do carrinho): alterna Caixa (`ScanCard` + `UltimoItemCard`)
  ↔ Balcão (`BalcaoGrid`) na coluna esquerda; `CartPanel` sempre visível à direita.
- `PagamentoScreen.tsx` — Tela 2: `MetodoPagamentoRow` + `MetodoDetailCard` à esquerda,
  `CartPanel` (readonly) + `PagamentosList` + "Finalizar venda" à direita.
- `SucessoScreen.tsx` — Tela 3, derivada de `venda.status === 'Concluida'` (não é uma escolha do
  operador — por isso não tem botão "ir para": ela simplesmente aparece quando o `concluir()`
  responde).
- `CartPanel.tsx` — o carrinho, reaproveitado nas 2 primeiras telas via prop `readonly`.

## Divergências do mockup corrigidas (não reproduzidas de propósito)

- **Alternador Caixa/Balcão nunca some.** No mockup fonte, o botão "Balcão"/"Caixa" mora dentro
  do `.scan-card`, que o próprio JS esconde (`display:none`) quando o modo Balcão está ativo — um
  beco sem saída pra voltar ao Caixa (só dava pra sair editando o DOM à mão). Aqui o seletor é uma
  barra própria, sempre visível, fora dos 2 cards que ele alterna.
- **Busca sem acento** (`semAcento` do mockup, via `String.normalize('NFD')`) — "oleo" acha "Óleo
  de Soja". Sem isso a ergonomia de bipagem em pt-BR piora bastante (nomes de produto quase sempre
  têm acento).

## Fora de escopo (sem contrato de API por trás — não fingir que funciona)

O mockup mapeia F1–F12 inteiro e mais uma dúzia de recursos de demo. Só os que o `VendaDto`/
`AdicionarItemRequest`/`RegistrarPagamentoRequest` sustentam de verdade entraram:

| Recurso do mockup | Por que ficou de fora |
|---|---|
| Desconto por item / desconto na venda | Nenhum endpoint aceita desconto — só existe pra *exibir* (`venda.descontoVenda`), nunca pra editar. |
| Remover item / ajustar quantidade (−/+) | Sem `DELETE`/`PATCH` de item no contrato — só `adicionarItem`. |
| Cliente/CPF na nota | Sem campo em `AdicionarItemRequest`/`RegistrarPagamentoRequest`. |
| Parcelas (crédito) | `RegistrarPagamentoRequest` não tem campo de parcelas — mostrar um seletor que não persiste nada seria fingir uma capacidade que não existe. |
| QR Pix com timer + auto-confirmação | `PagamentoDeVendaDto` não tem `status` (sem noção de "aguardando") — todo pagamento já registra confirmado, síncrono. Pix aqui é só mais um método com campo de valor, igual Débito/Crédito. |
| Toggle "Emitir NFC-e" + chip fiscal | Não há integração fiscal em `lib/api/vendas.ts`. |
| Reimprimir cupom | Sem endpoint de impressão (ver `docs/impressao-termica.md` do saas-erp — outro produto). |
| Sangria/suprimento (F11), suspender venda (F9) | Sem endpoint de sessão de caixa. |
| Banner "offline simulado" + fila local | Simulação pura do mockup (`Math.random`), sem correspondente real. |
| "Vendas de hoje" (stats row + drill) | Sem endpoint de agregação — é o mesmo gap documentado em `components/vendas/README.md` (aquele módulo é mock, não integrado). |
| Multiplicador de quantidade (F3, tela de Venda) | Única exceção da tabela que **não** é falta de contrato — `adicionarItem` aceita `quantidade` livre, dava pra fazer sem endpoint novo. Ficou de fora porque o mockup arma via `window.prompt()` bloqueante, padrão que o resto do app não usa (zero `prompt()` em `web/src`), e a UI própria (campo/badge não-bloqueante) ainda não foi construída. Workaround atual: bipar o mesmo produto N vezes. |
| Overlay de ajuda (mapa de F-keys) | Só 9 atalhos têm ação real (ver abaixo); um mapa de 20 teclas prometeria 11 que não fazem nada. |

## Atalhos de teclado (só os que têm ação real)

`usePdv.ts` usa o `useHotkeys` de `lib/hotkeys.ts` (já existente, não escrito por este módulo):

- **F10** — ir para Pagamento (funciona com o cursor no campo de busca — é onde ele fica entre um
  bipe e outro, por isso `ignoreInInputs: false`).
- **F2 / F3 / F4 / F5 / F6** — na tela de Pagamento, seleciona o método (Dinheiro/Débito/Crédito/
  Pix/Outros) — a MESMA `selecionarMetodo` que o clique no `MetodoPagamentoRow` já chama, com o
  mesmo guard (`restanteCentavos > 0`) que desabilita os cards. Funciona com o cursor no campo de
  valor/recebido (`ignoreInInputs: false`), igual ao mockup.
- **F12** — finalizar venda, só quando o restante já zerou.
- **Esc** — voltar de Pagamento pra Venda.
- **Enter** — nova venda, só na tela de Sucesso (que não tem nenhum campo de texto — sem risco de
  roubar Enter de um input).

## Dinheiro

`MoneyValue` (`components/shared`) direto sobre os `Money` que a API devolve — nunca
`item.quantidade * item.precoUnitario` recalculado no cliente. Total, subtotal, desconto, troco:
todos vêm prontos do `VendaDto`/`PagamentoDeVendaDto`.
