import { MoneyValue } from '@/components/shared';
import { Surface } from '@/components/ui/Surface';

import { atrasoDias } from './calc';
import type { Fornecedor } from './types';

interface FornecedorStatsProps {
  fornecedor: Fornecedor;
}

/** 3 stat cards do drill de fornecedor (`.forn-stats` do mockup). */
export function FornecedorStats({ fornecedor }: FornecedorStatsProps) {
  const atraso = atrasoDias(fornecedor);

  return (
    <section className="mb-4 grid grid-cols-1 gap-3.5 sm:grid-cols-3">
      <Surface className="p-4">
        <div className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground">Comprado · 12 meses</div>
        <div className="num mt-2 text-xl font-bold">
          <MoneyValue centavos={fornecedor.comprado12mCentavos} />
        </div>
        <div className="mt-1 text-xs text-faint">
          <MoneyValue centavos={fornecedor.comprado90dCentavos} /> nos últimos 90 dias
        </div>
      </Surface>

      <Surface className="p-4">
        <div className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground">Lead time real × prometido</div>
        <div className={`num mt-2 text-xl font-bold ${atraso > 0 ? 'text-warn' : ''}`}>
          {fornecedor.leadTimeRealDias.toFixed(1).replace('.', ',')}d <small className="text-sm font-semibold text-muted-foreground">vs {fornecedor.leadTimePrometidoDias}d</small>
        </div>
        <div className="mt-1 text-xs text-faint">{atraso > 0 ? `▲ ${atraso.toFixed(1).replace('.', ',')} dias de atraso médio` : 'dentro do combinado'}</div>
      </Surface>

      <Surface className="p-4">
        <div className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground">Taxa de divergência</div>
        <div className={`num mt-2 text-xl font-bold ${fornecedor.divergNotas > 0 ? 'text-warn' : ''}`}>
          {fornecedor.divergNotas} <small className="text-sm font-semibold text-muted-foreground">de {fornecedor.divergTotal} notas</small>
        </div>
        <div className="mt-1 text-xs text-faint">últimas notas recebidas</div>
      </Surface>
    </section>
  );
}
