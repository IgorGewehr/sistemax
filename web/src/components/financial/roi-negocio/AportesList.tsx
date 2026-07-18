import { ArrowUp } from 'lucide-react';

import { SectionCard } from '@/components/shared';
import type { AporteDeCapitalDto } from '@/lib/api/financeiro';
import { formatDate } from '@/lib/format';
import { formatCentavosWhole } from '@/lib/money';


interface AportesListProps {
  aportes: AporteDeCapitalDto[];
}

/** `.aporte-list` do mockup — `GET /financeiro/aportes`, capital de giro/investimento inicial. */
export function AportesList({ aportes }: AportesListProps) {
  const total = aportes.reduce((acc, a) => acc + a.valorCentavos, 0);

  return (
    <SectionCard title="Aportes de capital de giro" hint="funding, não retorno">
      {aportes.length === 0 ? (
        <p className="px-[18px] pb-4 text-[13px] text-muted-foreground">Nenhum aporte registrado.</p>
      ) : (
        <div className="flex flex-col gap-2 px-[18px] pb-1">
          {aportes.map((aporte) => (
            <div key={aporte.id} className="flex items-center justify-between gap-3 rounded-xl bg-surface-2 px-3.5 py-3">
              <div className="flex min-w-0 items-center gap-2.5">
                <span className="grid h-8 w-8 shrink-0 place-items-center rounded-[9px] bg-pos/15 text-pos">
                  <ArrowUp className="h-4 w-4" />
                </span>
                <div className="min-w-0">
                  <div className="truncate text-[13px] font-semibold text-foreground">{aporte.descricao}</div>
                  <div className="text-[11.5px] text-muted-foreground">{formatDate(aporte.data)}</div>
                </div>
              </div>
              <div className="num shrink-0 text-[15px] font-bold text-pos">{formatCentavosWhole(aporte.valorCentavos)}</div>
            </div>
          ))}
        </div>
      )}

      <div className="flex items-center justify-between px-[18px] pb-1 pt-3 text-[13px]">
        <span className="font-semibold text-muted-foreground">Total aportado</span>
        <span className="num text-[17px] font-bold text-foreground">{formatCentavosWhole(total)}</span>
      </div>

      <p className="flex gap-2 px-[18px] pb-4 pt-3 text-xs leading-relaxed text-muted-foreground">
        <span>
          Aporte é <b className="text-foreground">funding</b>, não receita: entra no total investido e no denominador do ROI, nunca no fluxo que
          paga o negócio.
        </span>
      </p>
    </SectionCard>
  );
}
