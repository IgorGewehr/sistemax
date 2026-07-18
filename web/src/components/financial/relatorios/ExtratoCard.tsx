import { Landmark } from 'lucide-react';

import { MockBadge } from '@/components/shared';
import { Surface } from '@/components/ui/Surface';
import { cn } from '@/lib/utils';

import { DocActions } from './DocActions';
import type { AccountantContact, DocGenState, ExtratoViewModel } from './types';

interface ExtratoCardProps {
  extrato: ExtratoViewModel;
  selectedAccounts: string[];
  onToggleAccount: (id: string) => void;
  summaryLabel: string;
  contact: AccountantContact;
  pdfState: DocGenState;
  excelState: DocGenState;
  onGeneratePdf: () => void;
  onGenerateExcel: () => void;
  onSend: (channel: 'email' | 'whatsapp') => void;
  className?: string;
}

/** Card "Extrato por conta" — chips de conta (multi-seleção com "Todas" exclusiva) + período livre. */
export function ExtratoCard({
  extrato,
  selectedAccounts,
  onToggleAccount,
  summaryLabel,
  contact,
  pdfState,
  excelState,
  onGeneratePdf,
  onGenerateExcel,
  onSend,
  className,
}: ExtratoCardProps) {
  return (
    <Surface padding="none" className={cn('relative flex flex-col p-4 sm:p-[18px]', className)}>
      <MockBadge
        className="absolute right-3 top-3"
        titulo="As contas acima são reais (GET /financeiro/contas-bancarias) — geração de PDF/Excel ainda não tem backend."
      />
      <div className="mb-3 flex items-start gap-3">
        <span className="grid h-[38px] w-[38px] flex-none place-items-center rounded-xl bg-surface-2 text-muted-foreground">
          <Landmark className="h-[19px] w-[19px]" />
        </span>
        <div className="min-w-0">
          <h3 className="text-[14.5px] font-bold tracking-tight text-foreground">Extrato por conta</h3>
          <div className="mt-0.5 text-xs text-muted-foreground">Período livre, por conta bancária/caixa</div>
        </div>
      </div>

      <div className="flex-1">
        <div className="mb-2.5 flex flex-wrap gap-1.5">
          {extrato.accounts.map((acct) => {
            const active = selectedAccounts.includes(acct.id);
            return (
              <button
                key={acct.id}
                type="button"
                onClick={() => onToggleAccount(acct.id)}
                className={cn(
                  'rounded-full border px-2.5 py-1.5 text-[11.5px] font-semibold transition-colors',
                  active
                    ? 'border-primary-600/40 bg-primary-soft text-primary-600'
                    : 'border-border bg-card text-muted-foreground hover:bg-surface-2',
                )}
              >
                {acct.label}
              </button>
            );
          })}
        </div>
        <div className="mb-3 flex items-center gap-1.5 text-xs">
          <input
            type="date"
            defaultValue={extrato.defaultFrom}
            className="w-[118px] rounded-lg border border-border bg-card px-1.5 py-1 text-foreground"
          />
          <span className="text-faint">até</span>
          <input
            type="date"
            defaultValue={extrato.defaultTo}
            className="w-[118px] rounded-lg border border-border bg-card px-1.5 py-1 text-foreground"
          />
        </div>
        <div className="mb-2.5 text-[11.5px] text-faint">{summaryLabel}</div>
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
