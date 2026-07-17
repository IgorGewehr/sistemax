# Estoque

Upgrade visual 1:1 com `docs/ui/mockups/estoque.html` (5 abas), preservando o wiring real ao
Bridge que a página anterior já tinha. Diferente de Compras/Vendas (mockups portados sem backend
próprio ainda), Estoque **já tinha** uma API real (`lib/api/estoque.ts`) antes deste upgrade — a
regra dura aqui foi: nenhuma chamada real sai, nenhuma tela vira mock disfarçado de real.

## O que é real

- **Dado**: `estoqueApi.listarProdutos()` (`ProdutoDto[]`) + `estoqueApi.listarSaldos()`
  (`PosicaoDeItemDto[]`), carregados juntos em `useEstoque` — mesma chamada dupla da página
  anterior. Todo número exibido (valor em estoque, abaixo do mínimo, zerados, disponível,
  reservado, custo médio, valor total) vem de `calc.ts`, puro, derivado só desses dois arrays.
- **Ação**: `+ Novo produto` → `estoqueApi.criarProduto()` (`NovoProdutoModal.tsx`), idêntico à
  página anterior.
- **Relatório R1 · Posição valorizada**: única aba de Relatórios que roda 100% sobre dado real —
  agrupa `listarSaldos()` por categoria, sem depender de histórico nenhum.

## O que o mockup tem e o Bridge não expõe (ainda)

O mockup fonte é uma demo com dados mock ricos (consumo semanal, OS reservadas, razão de
movimentações, inventários, curva ABC). Nenhuma dessas séries tem contrato de API hoje — em vez de
fabricar números que pareceriam reais, cada lugar afetado mostra um estado vazio explicando
exatamente o que falta (mesmo princípio do `EmBreveSection` de Fiscal/Integrações):

- **Aba Movimentações** (`MovimentacoesView`) e **Aba Inventários** (`InventariosView`): telas
  vazias — não há API de razão de movimentações nem de contagens físicas.
- **Ficha do produto** (`ProdutoFichaView`): os 4 stat-cards são reais; o "Consumo × entradas" e o
  Kardex do mockup viram uma única `EmptyState` (precisam de histórico de movimentação).
- **Relatórios R2–R7** (Curva ABC, Giro & cobertura, Ruptura, Kardex, Inventário, Sugestão de
  compra): cartões da galeria continuam visíveis (fiel ao mockup), mas cada um abre um preview
  explicando a API que falta em vez de uma tabela com números inventados.
- **Consultor da Visão Geral**: o mockup estima "perda de venda por semana" a partir de consumo
  histórico + OS reservadas. Aqui ele só afirma o que dá pra provar com o saldo atual (zerado/
  abaixo do mínimo) — Lei 2 (a IA observa, nunca inventa).
- **Grid2 da Visão Geral**: sem o gráfico "entradas × saídas, 6 semanas" do mockup; o card direito
  mostra totais reais (valor imobilizado, itens, abaixo do mínimo) quando uma categoria é
  selecionada, e um resumo do catálogo (itens controlados, itens sem custo médio informado) por
  padrão.

Quando o Bridge ganhar essas APIs, o lugar certo pra plugar é `calc.ts` (nova derivação) +
substituir a `EmptyState` correspondente — a estrutura de abas/componentes já está pronta pra
receber.

## Arquivos

```
types.ts                    view-model (SDD — o spec)
calc.ts                     derivações puras (join produto×saldo, KPIs, categorias, filtros)
useEstoque.ts                estado (abas, drills, filtros, modal) — a página fica fina
chips.tsx                    EstadoChip (ok/baixo/zerado/serviço)
EstoqueTabs.tsx               barra de abas client-side (mesma rota /estoque)

VisaoGeralView.tsx            KPIs + Consultor + Valor por categoria
KpisRow.tsx
ConsultorCard.tsx
CategoriaSection.tsx          grid2: bars por categoria × resumo/drill

ProdutosView.tsx               filtros + tabela, ou a ficha em drill
ProdutosFiltrosBar.tsx
ProdutosTable.tsx
ProdutoFichaView.tsx
NovoProdutoModal.tsx           cadastro — wired a estoqueApi.criarProduto()

MovimentacoesView.tsx          estado vazio honesto (sem API de razão)
InventariosView.tsx            estado vazio honesto (sem API de contagem)

RelatoriosView.tsx             galeria de 7 relatórios (só R1 real)
PosicaoValorizadaReport.tsx    R1 · Posição valorizada (real)
```
