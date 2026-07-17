import { MoneyValue } from '@/components/shared';

import { fmtQty } from './calc';
import { EstadoChip } from './chips';
import type { ProdutoComSaldo } from './types';

interface ProdutosTableProps {
  itens: ProdutoComSaldo[];
  total: number;
  onAbrir: (id: string) => void;
}

/** Tabela da aba Produtos (`#tbodyProdutos` do mockup) — cada célula vem do join real
 * `ProdutoDto` × `PosicaoDeItemDto`. Linha clicável → ficha do produto. */
export function ProdutosTable({ itens, total, onAbrir }: ProdutosTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[720px] border-collapse text-left text-sm">
        <thead>
          <tr className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
            <th className="whitespace-nowrap border-b border-border px-4 py-3">Produto</th>
            <th className="whitespace-nowrap border-b border-border px-4 py-3">SKU</th>
            <th className="whitespace-nowrap border-b border-border px-4 py-3 text-right">Disponível</th>
            <th className="whitespace-nowrap border-b border-border px-4 py-3 text-right">Reservado</th>
            <th className="whitespace-nowrap border-b border-border px-4 py-3 text-right">Custo médio</th>
            <th className="whitespace-nowrap border-b border-border px-4 py-3 text-right">Valor</th>
            <th className="whitespace-nowrap border-b border-border px-4 py-3">Estado</th>
          </tr>
        </thead>
        <tbody>
          {itens.length === 0 && (
            <tr>
              <td colSpan={7} className="px-4 py-10 text-center text-sm text-muted-foreground">
                Nenhum produto encontrado com esses filtros.
              </td>
            </tr>
          )}
          {itens.map((item) => (
            <tr
              key={item.produto.id}
              onClick={() => onAbrir(item.produto.id)}
              className="cursor-pointer border-b border-border/60 transition-colors last:border-0 hover:bg-surface-2/60"
            >
              <td className="px-4 py-3 font-semibold text-foreground">{item.produto.nome}</td>
              <td className="num px-4 py-3 text-muted-foreground">{item.produto.sku}</td>
              <td className="num px-4 py-3 text-right text-foreground">
                {item.produto.controlaEstoque ? fmtQty(item.saldo?.disponivel.milesimos, item.produto.unidade) : '—'}
              </td>
              <td className="num px-4 py-3 text-right text-muted-foreground">
                {item.produto.controlaEstoque && item.saldo && item.saldo.reservado.milesimos > 0
                  ? fmtQty(item.saldo.reservado.milesimos, item.produto.unidade)
                  : '—'}
              </td>
              <td className="num px-4 py-3 text-right text-muted-foreground">
                {item.produto.controlaEstoque && item.saldo ? <MoneyValue centavos={item.saldo.custoMedio.centavos} /> : '—'}
              </td>
              <td className="num px-4 py-3 text-right font-semibold text-foreground">
                {item.produto.controlaEstoque && item.saldo ? <MoneyValue centavos={item.saldo.valorTotal.centavos} /> : '—'}
              </td>
              <td className="px-4 py-3">
                <EstadoChip code={item.estado.code} label={item.estado.label} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {itens.length > 0 && (
        <div className="border-t border-border/70 px-4 py-2.5 text-right text-xs text-muted-foreground">
          mostrando {itens.length} de {total}
        </div>
      )}
    </div>
  );
}
