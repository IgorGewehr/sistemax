import { MoneyValue } from '@/components/shared';
import { Surface } from '@/components/ui/Surface';

import type { Parcela } from './types';

interface ParcelasCardProps {
  parcelas: Parcela[];
  readonly: boolean;
  jaPago: boolean;
  onChangeJaPago: (checked: boolean) => void;
}

/** "Financeiro" — parcelas do XML `<cobr><dup>` + checkbox "paguei no ato". Só existe quando `parcelas.length > 0`. */
export function ParcelasCard({ parcelas, readonly, jaPago, onChangeJaPago }: ParcelasCardProps) {
  return (
    <Surface padding="none" className="overflow-hidden">
      <h2 className="flex items-center gap-2 px-[18px] pt-[15px] text-[13px] font-bold tracking-tight">
        Financeiro <span className="text-xs font-medium text-muted-foreground">do XML &lt;cobr&gt;&lt;dup&gt;</span>
      </h2>
      <div className="mt-2">
        {parcelas.map((p) => (
          <div key={p.n} className="flex items-center justify-between border-b border-border/60 px-[18px] py-2 text-[13px] last:border-b-0">
            <span className="text-muted-foreground">{p.n}ª parcela</span>
            <span>{p.venc}</span>
            <MoneyValue centavos={p.valorCentavos} className="font-semibold" />
          </div>
        ))}
      </div>
      <div className="px-[18px] pb-3 pt-2.5 text-xs text-muted-foreground">Σ parcelas confere com vNF ✓</div>
      {!readonly && (
        <label className="flex cursor-pointer items-center gap-2 border-t border-border/70 px-[18px] py-3 text-[12.5px] text-muted-foreground">
          <input type="checkbox" checked={jaPago} onChange={(e) => onChangeJaPago(e.target.checked)} className="accent-primary-600" />
          paguei no ato (pix/dinheiro)
        </label>
      )}
    </Surface>
  );
}
