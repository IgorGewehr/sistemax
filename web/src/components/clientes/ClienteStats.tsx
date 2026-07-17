import { MoneyValue, MoneyWhole } from '@/components/shared';
import { Surface } from '@/components/ui/Surface';

import { frequenciaMediaDias, ticketMedioExibicaoCentavos } from './calc';
import type { Cliente } from './types';

interface ClienteStatsProps {
  cliente: Cliente;
  hojeLabel: string;
}

/**
 * 3 stat cards da Ficha do cliente. Casas decimais seguem o critério do contrato: ticket médio usa
 * `MoneyValue` (2 casas — precisão importa num valor pequeno recorrente); totais usam `MoneyWhole`
 * (reais inteiros, como a maioria dos totais do Financeiro).
 */
export function ClienteStats({ cliente, hojeLabel }: ClienteStatsProps) {
  const frequencia = frequenciaMediaDias(cliente, hojeLabel);

  return (
    <section className="mb-4 grid grid-cols-1 gap-3.5 sm:grid-cols-3">
      <Surface className="p-4">
        <div className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground">Ticket médio</div>
        <div className="num mt-2 text-xl font-bold">
          <MoneyValue centavos={ticketMedioExibicaoCentavos(cliente)} />
        </div>
        <div className="mt-1 text-xs text-faint">{cliente.comprasCount} compra{cliente.comprasCount === 1 ? '' : 's'} no total</div>
      </Surface>

      <Surface className="p-4">
        <div className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground">Total gasto · 12 meses</div>
        <div className="num mt-2 text-xl font-bold">
          <MoneyWhole centavos={cliente.totalGasto12mCentavos} />
        </div>
        <div className="mt-1 text-xs text-faint">
          <MoneyWhole centavos={cliente.totalGastoVidaCentavos} className="text-faint" /> vitalício
        </div>
      </Surface>

      <Surface className="p-4">
        <div className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground">Última visita</div>
        <div className="num mt-2 text-xl font-bold">{cliente.ultimaVisita ?? '—'}</div>
        <div className="mt-1 text-xs text-faint">{frequencia !== null ? `compra a cada ~${frequencia} dias, em média` : 'ainda não comprou'}</div>
      </Surface>
    </section>
  );
}
