import { ArrowUpRight } from 'lucide-react';

import { KpiCard, MoneyValue } from '@/components/shared';
import type { Centavos } from '@/lib/money';

import type { HomeKpis } from './calc';
import { KpiClickable } from './KpiClickable';
import { Sparkline } from './Sparkline';

interface KpisRowProps {
  kpis: HomeKpis;
  /** 5 meses anteriores — o mês corrente (`kpis.compradoMesCentavos`) é acrescentado como 6º ponto. */
  historicoAnteriorCentavos: Centavos[];
  notasConferirAtivo: boolean;
  variacaoAberta: boolean;
  onToggleConferirKpi: () => void;
  onToggleVariacao: () => void;
}

/** As 4 KPIs do topo da Home (`.kpis` do mockup). */
export function KpisRow({ kpis, historicoAnteriorCentavos, notasConferirAtivo, variacaoAberta, onToggleConferirKpi, onToggleVariacao }: KpisRowProps) {
  const pedidosCount = kpis.pedidosAbertos.length;

  return (
    <section className="mb-4 grid grid-cols-2 gap-3.5 lg:grid-cols-4">
      <KpiCard hero label="Comprado no mês" value={<MoneyValue centavos={kpis.compradoMesCentavos} />}>
        <div className="flex items-center gap-1 text-[12.5px] font-semibold text-pos">
          <ArrowUpRight className="h-3.5 w-3.5" strokeWidth={2.5} /> +8% vs jun
        </div>
        <Sparkline valoresCentavos={[...historicoAnteriorCentavos, kpis.compradoMesCentavos]} />
      </KpiCard>

      <KpiCard label="Pedidos em aberto" value={<MoneyValue centavos={kpis.pedidosAbertoTotalCentavos} />}>
        <div className="text-[12.5px] font-semibold text-foreground">
          {pedidosCount} pedido{pedidosCount === 1 ? '' : 's'}
        </div>
        <div className="mt-[7px] text-xs text-muted-foreground">previsão em até 5 dias</div>
      </KpiCard>

      <KpiClickable label="Notas a conferir" value={kpis.notasConferir.length} active={notasConferirAtivo} onClick={onToggleConferirKpi}>
        <div className="mt-[7px] text-[12.5px] font-semibold text-warn">{kpis.notasComDivergencia} com divergência</div>
        <div className="mt-[3px] text-xs text-muted-foreground">→ conferir</div>
      </KpiClickable>

      <KpiClickable label="Variação de custo" value="▲ +2,4%" active={variacaoAberta} onClick={onToggleVariacao}>
        <div className="mt-[7px] text-[12.5px] font-semibold text-foreground">
          {kpis.subiram} itens subiram, {kpis.cairam} caiu
        </div>
        <div className="mt-[3px] text-xs text-muted-foreground">itens recomprados · → ver itens</div>
      </KpiClickable>
    </section>
  );
}
