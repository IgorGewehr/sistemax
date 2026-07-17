import { SendMenu } from './SendMenu';
import type { AccountantContact, DocGenState } from './types';

interface DocActionsProps {
  pdfState: DocGenState;
  excelState: DocGenState;
  onGeneratePdf: () => void;
  onGenerateExcel: () => void;
  onSend: (channel: 'email' | 'whatsapp') => void;
  contact: AccountantContact;
}

const LABEL: Record<DocGenState, (format: string) => string> = {
  idle: (format) => format,
  generating: () => 'Gerando…',
  done: () => '✓ Gerado',
};

function GenerateButton({ format, state, onClick }: { format: string; state: DocGenState; onClick: () => void }) {
  return (
    <button
      type="button"
      disabled={state !== 'idle'}
      onClick={onClick}
      className="flex-1 rounded-lg border border-border bg-card px-3 py-2 text-[13px] font-medium text-foreground transition-colors hover:bg-surface-2 disabled:cursor-default disabled:opacity-70"
    >
      {LABEL[state](format)}
    </button>
  );
}

/** Linha de ações comum aos 4 cards de documento "simples" (DRE, Extrato, Aberto, MRR): PDF, Excel, Enviar. */
export function DocActions({ pdfState, excelState, onGeneratePdf, onGenerateExcel, onSend, contact }: DocActionsProps) {
  return (
    <div className="mt-3.5 flex gap-2">
      <GenerateButton format="PDF" state={pdfState} onClick={onGeneratePdf} />
      <GenerateButton format="Excel" state={excelState} onClick={onGenerateExcel} />
      <SendMenu contact={contact} onSend={onSend} />
    </div>
  );
}
