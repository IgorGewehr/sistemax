import { Check } from 'lucide-react';

import { MoneyValue } from '@/components/shared';
import { Surface } from '@/components/ui/Surface';

import type { NotaEntrada } from './types';

interface CustoEntradaCardProps {
  nota: NotaEntrada;
  readonly: boolean;
}

/** "Custo de entrada": breakdown do vNF (produtos + frete/IPI/ST − desconto) + nota de rateio. */
export function CustoEntradaCard({ nota, readonly }: CustoEntradaCardProps) {
  return (
    <Surface padding="none" className="overflow-hidden">
      <h2 className="px-[18px] pt-[15px] text-[13px] font-bold tracking-tight">Custo de entrada</h2>
      <div className="flex flex-col gap-2 px-[18px] pb-1.5 pt-3.5 text-[13px]">
        <div className="flex justify-between">
          <span>Produtos</span>
          <MoneyValue centavos={nota.vProdCentavos} className="text-[13px]" />
        </div>
        {nota.vFreteCentavos > 0 && (
          <div className="flex justify-between text-muted-foreground">
            <span>+ Frete</span>
            <MoneyValue centavos={nota.vFreteCentavos} className="text-[13px]" />
          </div>
        )}
        {nota.vIpiCentavos > 0 && (
          <div className="flex justify-between text-muted-foreground">
            <span>+ IPI</span>
            <MoneyValue centavos={nota.vIpiCentavos} className="text-[13px]" />
          </div>
        )}
        {nota.vStCentavos > 0 && (
          <div className="flex justify-between text-muted-foreground">
            <span>+ ICMS-ST</span>
            <MoneyValue centavos={nota.vStCentavos} className="text-[13px]" />
          </div>
        )}
        {nota.vDescontoCentavos > 0 && (
          <div className="flex justify-between text-muted-foreground">
            <span>− Desconto</span>
            <span className="num text-[13px]">
              −<MoneyValue centavos={nota.vDescontoCentavos} />
            </span>
          </div>
        )}
        <div className="mt-0.5 flex justify-between border-t border-border pt-2.5 text-[14.5px] font-bold">
          <span>TOTAL (vNF)</span>
          <MoneyValue centavos={nota.totalCentavos} className="text-[14.5px] font-bold" />
        </div>
      </div>
      <div className="flex items-center gap-1.5 px-[18px] py-2.5 text-xs text-pos">
        <Check className="h-3.5 w-3.5" strokeWidth={2.6} /> Σ itens confere com vNF
      </div>
      <div className="flex items-center justify-between border-t border-border/70 px-[18px] py-2.5 text-xs text-muted-foreground">
        <span>rateio: por valor (vProd)</span>
        {!readonly && (
          <button type="button" disabled className="cursor-not-allowed rounded-lg border border-border bg-card px-2.5 py-1 text-xs font-semibold opacity-50">
            alterar
          </button>
        )}
      </div>
    </Surface>
  );
}
