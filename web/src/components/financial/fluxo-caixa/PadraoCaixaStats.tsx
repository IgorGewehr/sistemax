import { MoneyValue } from '@/components/shared';

import { DIA_SEMANA_COMPLETO, type DiaCritico } from './calc';
import { StatTile } from './StatTile';

interface PadraoCaixaStatsProps {
  diaCritico: DiaCritico | null;
  mediaDiferencaCentavos: number;
  diasFechadosCount: number;
  vendasEspeciePercentual: number;
}

/** "O que os números escondem" — a leitura por trás do gráfico: média do dia, o dia crítico da
 * semana e quanto do faturamento é espécie (o resto vira conciliação no módulo Bancário). */
export function PadraoCaixaStats({ diaCritico, mediaDiferencaCentavos, diasFechadosCount, vendasEspeciePercentual }: PadraoCaixaStatsProps) {
  return (
    <div className="flex flex-col gap-2.5 px-[18px] pb-[18px] pt-3.5">
      <StatTile
        label="Diferença média por dia"
        value={<MoneyValue centavos={mediaDiferencaCentavos} tone="auto" />}
        sub={`considerando os ${diasFechadosCount} dias fechados`}
      />
      <StatTile
        label="Dia crítico"
        value={diaCritico ? DIA_SEMANA_COMPLETO[diaCritico.diaSemana] : '—'}
        mono={false}
        sub={
          diaCritico ? (
            <>
              média <MoneyValue centavos={diaCritico.mediaCentavos} tone="auto" className="text-xs" /> · sempre no fechamento sozinho
            </>
          ) : undefined
        }
      />
      <StatTile
        label="Vendas em espécie"
        value={
          <>
            {vendasEspeciePercentual}
            <small className="text-[13px] font-semibold text-muted-foreground">%</small>
          </>
        }
        sub="do total vendido — o resto é cartão/PIX, no Bancário"
      />
    </div>
  );
}
