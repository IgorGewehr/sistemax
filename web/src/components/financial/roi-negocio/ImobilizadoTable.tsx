import { SectionCard } from '@/components/shared';
import type { AtivoDeCapitalDto } from '@/lib/api/financeiro';
import { formatDate } from '@/lib/format';
import { formatCentavosWhole } from '@/lib/money';
import { cn } from '@/lib/utils';

interface ImobilizadoTableProps {
  bens: AtivoDeCapitalDto[];
}

const DOT_CLASSES = ['bg-primary-600', 'bg-[hsl(215_60%_55%)]', 'bg-warn', 'bg-pos', 'bg-[hsl(268_50%_58%)]'];

const CATEGORIA_LABEL: Record<string, string> = {
  Reforma: 'Reforma',
  Equipamento: 'Equipamento',
  Moveis: 'Móveis',
  ComunicacaoVisual: 'Comunicação visual',
  Computador: 'Computador',
  Veiculo: 'Veículo',
  LicencaSoftware: 'Licença de software',
  Intangivel: 'Intangível',
};

/** `.tbl-wrap` do mockup — registro de imobilizado, `GET /financeiro/imobilizado`. */
export function ImobilizadoTable({ bens }: ImobilizadoTableProps) {
  const totalCusto = bens.reduce((acc, b) => acc + b.custoAquisicaoCentavos, 0);
  const totalDepr = bens.reduce((acc, b) => acc + b.amortizacaoMensalCentavos, 0);
  const totalContabil = bens.reduce((acc, b) => acc + b.valorContabilAtualCentavos, 0);

  return (
    <SectionCard title="Registro de imobilizado" hint={`${bens.length} be${bens.length === 1 ? 'm' : 'ns'} · depreciação linear`} bodyClassName="">
      <div className="overflow-x-auto px-1">
        <table className="w-full min-w-[720px] border-collapse">
          <thead>
            <tr>
              {['Bem', 'Custo', 'Aquisição', 'Vida útil', 'Depr./mês', 'Valor contábil'].map((h, i) => (
                <th
                  key={h}
                  className={cn(
                    'border-b border-border px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-muted-foreground',
                    i > 0 && 'text-right',
                  )}
                >
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {bens.map((bem, i) => (
              <tr key={bem.id} className="hover:bg-surface-2/60">
                <td className="border-b border-border/60 px-4 py-3.5 text-[13.5px]">
                  <span className="inline-flex items-center gap-2 font-semibold text-foreground">
                    <span className={cn('h-[9px] w-[9px] shrink-0 rounded-[3px]', DOT_CLASSES[i % DOT_CLASSES.length])} />
                    {bem.nome}
                  </span>
                  <div className="text-[12.5px] text-muted-foreground">{CATEGORIA_LABEL[bem.categoria] ?? bem.categoria}</div>
                </td>
                <td className="num border-b border-border/60 px-4 py-3.5 text-right text-[13.5px]">{formatCentavosWhole(bem.custoAquisicaoCentavos)}</td>
                <td className="num border-b border-border/60 px-4 py-3.5 text-[13.5px]">{formatDate(bem.dataAquisicao)}</td>
                <td className="num border-b border-border/60 px-4 py-3.5 text-right text-[13.5px]">{bem.vidaUtilMeses} m</td>
                <td className="num border-b border-border/60 px-4 py-3.5 text-right text-[13.5px]">{formatCentavosWhole(bem.amortizacaoMensalCentavos)}</td>
                <td className="num border-b border-border/60 px-4 py-3.5 text-right text-[13.5px]">{formatCentavosWhole(bem.valorContabilAtualCentavos)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td className="px-4 py-3.5 text-[13.5px] font-bold">Total · {bens.length} bens</td>
              <td className="num px-4 py-3.5 text-right text-[13.5px] font-bold">{formatCentavosWhole(totalCusto)}</td>
              <td />
              <td />
              <td className="num px-4 py-3.5 text-right text-[13.5px] font-bold">{formatCentavosWhole(totalDepr)}</td>
              <td className="num px-4 py-3.5 text-right text-[13.5px] font-bold">{formatCentavosWhole(totalContabil)}</td>
            </tr>
          </tfoot>
        </table>
      </div>
      <p className="flex gap-2 px-[18px] pb-4 pt-2 text-xs leading-relaxed text-muted-foreground">
        <span>
          A compra não vira despesa de uma vez: entra no balanço e o DRE sente{' '}
          <b className="text-foreground">{formatCentavosWhole(totalDepr)}/mês</b> de depreciação. Valor contábil = custo − depreciação acumulada.
        </span>
      </p>
    </SectionCard>
  );
}
