import { ClipboardList } from 'lucide-react';

import { Surface } from '@/components/ui/Surface';
import { cn } from '@/lib/utils';

import { DocActions } from './DocActions';
import { agingWidths } from './helpers';
import { MoneyWhole } from './MoneyWhole';
import type { AbertoViewModel, AccountantContact, DocGenState } from './types';

interface AbertoCardProps {
  aberto: AbertoViewModel;
  contact: AccountantContact;
  pdfState: DocGenState;
  excelState: DocGenState;
  onGeneratePdf: () => void;
  onGenerateExcel: () => void;
  onSend: (channel: 'email' | 'whatsapp') => void;
  className?: string;
}

/** Card "Contas em aberto" — a pagar/receber + barra de aging (larguras derivadas, não hardcoded). */
export function AbertoCard({
  aberto,
  contact,
  pdfState,
  excelState,
  onGeneratePdf,
  onGenerateExcel,
  onSend,
  className,
}: AbertoCardProps) {
  const widths = agingWidths(aberto.agingBuckets);

  return (
    <Surface padding="none" className={cn('flex flex-col p-4 sm:p-[18px]', className)}>
      <div className="mb-3 flex items-start gap-3">
        <span className="grid h-[38px] w-[38px] flex-none place-items-center rounded-xl bg-surface-2 text-muted-foreground">
          <ClipboardList className="h-[19px] w-[19px]" />
        </span>
        <div className="min-w-0">
          <h3 className="text-[14.5px] font-bold tracking-tight text-foreground">Contas em aberto</h3>
          <div className="mt-0.5 text-xs text-muted-foreground">A pagar e a receber, com prazo</div>
        </div>
      </div>

      <div className="flex-1">
        <div className="flex items-baseline justify-between py-[6px] text-[13px]">
          <span className="text-muted-foreground">A receber em aberto</span>
          <span>
            <MoneyWhole centavos={aberto.receberEmAberto} className="text-[15px] font-bold" />
            <span className="ml-1.5 whitespace-nowrap text-[11.5px] font-semibold text-crit">
              <MoneyWhole centavos={aberto.receberAtrasado} /> atrasado
            </span>
          </span>
        </div>
        <div className="flex items-baseline justify-between py-[6px] text-[13px]">
          <span className="text-muted-foreground">A pagar em aberto</span>
          <MoneyWhole centavos={aberto.pagarEmAberto} className="text-[15px] font-bold" />
        </div>

        <div className="my-2.5 flex h-2 overflow-hidden rounded-md bg-surface-2">
          {aberto.agingBuckets.map((bucket, i) => (
            <span key={bucket.id} style={{ width: `${widths[i]}%`, backgroundColor: bucket.colorVar }} className="h-full" />
          ))}
        </div>
        <div className="mb-1 flex flex-wrap gap-3 text-[10.5px] text-muted-foreground">
          {aberto.agingBuckets.map((bucket) => (
            <span key={bucket.id} className="inline-flex items-center gap-1">
              <i className="inline-block h-2 w-2 rounded-sm" style={{ backgroundColor: bucket.colorVar }} />
              {bucket.label} <MoneyWhole centavos={bucket.amountCentavos} />
            </span>
          ))}
        </div>
      </div>

      <DocActions
        pdfState={pdfState}
        excelState={excelState}
        onGeneratePdf={onGeneratePdf}
        onGenerateExcel={onGenerateExcel}
        onSend={onSend}
        contact={contact}
      />
    </Surface>
  );
}
