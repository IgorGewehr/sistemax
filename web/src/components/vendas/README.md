# Vendas

Relatório de vendas — KPIs, filtros e drill pro comprovante de cada venda. Distinta do PDV
(que abre/edita a venda em andamento): aqui só se ANALISA o que já foi vendido. Sem mockup
.html prévio — `types.ts` é o contrato desta tela.

> **Status: código pronto, não integrado.** O Sidebar já lista `/vendas` (sem `live: true`,
> badge "em breve") e não há `<Route path="vendas">` em `App.tsx` — rota e link ficam a
> cargo do orquestrador quando o módulo for publicado (fora do escopo deste diretório).
> Até lá, este README não deve ser lido como "entregável fechado e navegável".

- Dinheiro sempre `Centavos`, 2 casas (`MoneyValue` sem `whole`) — mesma escolha de Compras,
  porque aqui os valores são de transação exata, não big-picture arredondado.
- "Canal" = terminal de origem (Caixa 01/02, Balcão) — não existe e-commerce/delivery neste
  produto; é o conceito real de `docs/arquitetura/ARCHITECTURE.md` (múltiplos PDVs por loja).
- Reaproveita `StatusVenda`/`MetodoPagamento` de `lib/api/vendas.ts` em vez de duplicar enum.
- Estornar/editar venda é FORA DE ESCOPO — ação do PDV/Financeiro. O modal de detalhe é
  só leitura mesmo para vendas estornadas (mostra motivo/quem/quando, não oferece reverter).
- Filtros (canal/operador/forma de pagamento/busca) são client-side sobre o array já
  carregado — `calc.ts` puro, sem chamada de rede por filtro.
- O drill do Super Consultor ("Ver sábados →") rola até a tabela em vez de aplicar um filtro
  fantasma: `FiltrosVendas` (o contrato) não tem dimensão dia-da-semana/faixa-horária, então
  fingir esse recorte quebraria o SDD — ver JSDoc de `useVendas.aplicarFiltroSabados`.
- Super Consultor é read-only (Lei 2): observa e aconselha, nunca "aplica"/"automatiza".

## Arquivos

```
types.ts                 view-model (SDD — o spec)
calc.ts                  filtros puros + buildSparkline + tons de status
useVendas.ts             estado (filtros, drill do modal) — a página fica fina
KpisRow.tsx              4 KPIs (Vendido hoje · Vendido no mês · Ticket médio · Nº de vendas)
Sparkline.tsx            cópia local do desenho de `components/compras/Sparkline.tsx`
KpiClickable.tsx         cópia local do padrão de `components/compras/KpiClickable.tsx`
VendasConsultor.tsx      Super Consultor (ConsultorInsight, read-only)
FiltrosVendasBar.tsx     canal · operador · forma de pagamento · busca
VendasTable.tsx          tabela única (sem segmentação em abas, diferente de Compras)
TableCells.tsx           Th/Td (cópia local)
chips.tsx                Chip de status da venda (tones pos/warn/crit, não reusa StatusChip)
VendasTableSection.tsx   SectionCard: filtros no header + tabela
VendaDetalheModal.tsx    comprovante completo — só leitura, sem botão de ação
```
