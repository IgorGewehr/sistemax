import { Eyebrow, MoneyValue } from '@/components/shared';
import type { ItemDeVendaDto } from '@/lib/api/vendas';

interface UltimoItemCardProps {
  item: ItemDeVendaDto | null;
}

/** `.ultimo-card` do mockup — reforço visual do último item bipado, direto do `VendaDto`. */
export function UltimoItemCard({ item }: UltimoItemCardProps) {
  return (
    <div className="surface flex-none rounded-2xl p-3.5">
      <Eyebrow>Último item</Eyebrow>
      {!item ? (
        <p className="mt-1.5 text-[13px] text-faint">Bipe ou digite um produto para começar a venda.</p>
      ) : (
        <div className="mt-2 flex items-baseline justify-between gap-3">
          <div className="min-w-0">
            <div className="truncate text-[17px] font-bold text-foreground">{item.descricao}</div>
            <div className="num mt-0.5 text-[12.5px] text-muted-foreground">
              {item.quantidade} × <MoneyValue centavos={item.precoUnitario.centavos} />
            </div>
          </div>
          <MoneyValue centavos={item.subtotal.centavos} className="shrink-0 text-xl font-bold text-foreground" />
        </div>
      )}
    </div>
  );
}
