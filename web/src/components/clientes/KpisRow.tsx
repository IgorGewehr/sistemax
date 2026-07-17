import { KpiCard } from '@/components/shared';

import type { ClientesKpis } from './calc';
import { KpiClickable } from './KpiClickable';
import { Sparkline } from './Sparkline';

interface KpisRowProps {
  kpis: ClientesKpis;
  /** 5 meses anteriores — o mês corrente (`kpis.clientesAtivos`) é acrescentado como 6º ponto. */
  historicoAnteriorMensal: number[];
  aniversariantesAtivo: boolean;
  semComprar90dAtivo: boolean;
  onToggleAniversariantes: () => void;
  onToggleSemComprar90d: () => void;
}

/** As 4 KPIs do topo da Home. */
export function KpisRow({
  kpis,
  historicoAnteriorMensal,
  aniversariantesAtivo,
  semComprar90dAtivo,
  onToggleAniversariantes,
  onToggleSemComprar90d,
}: KpisRowProps) {
  return (
    <section className="mb-4 grid grid-cols-2 gap-3.5 lg:grid-cols-4">
      <KpiCard hero label="Clientes cadastrados" value={kpis.clientesAtivos}>
        <Sparkline valores={[...historicoAnteriorMensal, kpis.clientesAtivos]} />
      </KpiCard>

      <KpiCard label="Novos no mês" value={kpis.novosNoMes}>
        <div className="mt-[7px] text-xs text-muted-foreground">
          {kpis.novosNaSemana} nesta semana
        </div>
      </KpiCard>

      <KpiClickable label="Aniversariantes no mês" value={kpis.aniversariantesNoMes.length} active={aniversariantesAtivo} onClick={onToggleAniversariantes}>
        <div className="mt-[7px] text-[12.5px] font-semibold text-foreground">{kpis.aniversariantesNaSemana} na semana</div>
        <div className="mt-[3px] text-xs text-muted-foreground">→ ver aniversariantes</div>
      </KpiClickable>

      <KpiClickable label="Sem comprar há 90d+" value={kpis.semComprar90d.length} active={semComprar90dAtivo} onClick={onToggleSemComprar90d}>
        <div className="mt-[7px] text-[12.5px] font-semibold text-warn">clientes ativos parados</div>
        <div className="mt-[3px] text-xs text-muted-foreground">→ ver quem sumiu</div>
      </KpiClickable>
    </section>
  );
}
