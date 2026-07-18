import { FileText, Info, TrendingDown, TrendingUp } from 'lucide-react';

import { MockBadge } from '@/components/shared';
import { Surface } from '@/components/ui/Surface';
import { cn } from '@/lib/utils';

import { DocActions } from './DocActions';
import { MoneyWhole } from './MoneyWhole';
import { RichText } from './RichText';
import type { AccountantContact, DocGenState, DreViewModel, Regime } from './types';

interface DreCardProps {
  dre: DreViewModel;
  regime: Regime;
  contact: AccountantContact;
  pdfState: DocGenState;
  excelState: DocGenState;
  onGeneratePdf: () => void;
  onGenerateExcel: () => void;
  onSend: (channel: 'email' | 'whatsapp') => void;
  className?: string;
}

/** Card "DRE do mês" — único card cujo corpo alterna 100% conforme o regime (competência/caixa). */
export function DreCard({
  dre,
  regime,
  contact,
  pdfState,
  excelState,
  onGeneratePdf,
  onGenerateExcel,
  onSend,
  className,
}: DreCardProps) {
  const block = dre.byRegime[regime];
  const DeltaIcon = block.delta.direction === 'up' ? TrendingUp : TrendingDown;

  return (
    <Surface padding="none" className={cn('relative flex flex-col p-4 sm:p-[18px]', className)}>
      {regime === 'caixa' && (
        <MockBadge
          className="absolute right-3 top-3"
          titulo="Regime de caixa ainda não tem serviço no backend (só competência é real) — números ilustrativos."
        />
      )}
      <div className="mb-3 flex items-start gap-3">
        <span className="grid h-[38px] w-[38px] flex-none place-items-center rounded-xl bg-surface-2 text-muted-foreground">
          <FileText className="h-[19px] w-[19px]" />
        </span>
        <div className="min-w-0">
          <h3 className="text-[14.5px] font-bold tracking-tight text-foreground">DRE do mês</h3>
          <div className="mt-0.5 text-xs text-muted-foreground">
            {dre.periodLabel} · prévia · {block.regimeLabel}
          </div>
        </div>
      </div>

      <div className="flex-1">
        <div className="flex items-center justify-between gap-2.5 border-b border-dashed border-border py-[7px] text-[13px]">
          <span className="text-muted-foreground">{block.topLine.label}</span>
          <MoneyWhole centavos={block.topLine.valueCentavos} className="font-semibold" />
        </div>
        {block.deductionLines.map((line) => (
          <div
            key={line.label}
            className="flex items-center justify-between gap-2.5 border-b border-dashed border-border py-[7px] text-[13px]"
          >
            <span className="text-muted-foreground">{line.label}</span>
            <MoneyWhole centavos={line.valueCentavos} className="font-medium text-muted-foreground" />
          </div>
        ))}
        <div className="mt-0.5 flex items-center justify-between gap-2.5 border-t border-border pt-2.5 text-[13px] font-bold">
          <span>{block.totalLine.label}</span>
          <MoneyWhole centavos={block.totalLine.valueCentavos} className="text-base" />
        </div>
        <div
          className={cn(
            'mt-2 inline-flex items-center gap-1 text-xs font-semibold',
            block.delta.direction === 'up' ? 'text-pos' : 'text-crit',
          )}
        >
          <DeltaIcon className="h-3 w-3" />
          {block.delta.label}
        </div>

        <div className="mt-3 flex gap-2 rounded-[11px] bg-surface-2 p-2.5 text-xs leading-relaxed text-muted-foreground">
          <Info className="mt-0.5 h-3.5 w-3.5 flex-none text-primary-600" />
          <p>
            <RichText parts={block.bridgeNote} />
          </p>
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
