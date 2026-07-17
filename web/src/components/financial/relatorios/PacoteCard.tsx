import { AnimatePresence, motion } from 'framer-motion';
import { Check, Package } from 'lucide-react';

import { Button } from '@/components/ui/Button';
import { Surface } from '@/components/ui/Surface';
import { cn } from '@/lib/utils';

import { RichText } from './RichText';
import { SendMenu } from './SendMenu';
import type { AccountantContact, DocGenState, PacoteViewModel, Regime } from './types';

interface PacoteCardProps {
  pacote: PacoteViewModel;
  regime: Regime;
  contact: AccountantContact;
  state: DocGenState;
  revealed: boolean;
  onGerar: () => void;
  onBaixar: () => void;
  onSend: (channel: 'email' | 'whatsapp') => void;
  className?: string;
}

const BUTTON_LABEL: Record<DocGenState, string> = {
  idle: 'Gerar pacote (.zip)',
  generating: 'Gerando pacote…',
  done: '✓ Pacote pronto',
};

/** Card-estrela da tela ("O pacote completo pro contador") — o único com CTA primário próprio. */
export function PacoteCard({ pacote, regime, contact, state, revealed, onGerar, onBaixar, onSend, className }: PacoteCardProps) {
  return (
    <Surface padding="none" className={cn('flex flex-col p-4 sm:p-[18px]', className)}>
      <div className="mb-3 flex items-start gap-3">
        <span className="grid h-[38px] w-[38px] flex-none place-items-center rounded-xl bg-primary-soft text-primary-600">
          <Package className="h-[19px] w-[19px]" />
        </span>
        <div className="min-w-0">
          <h3 className="text-[14.5px] font-bold tracking-tight text-foreground">Fechamento mensal</h3>
          <div className="mt-0.5 text-xs text-muted-foreground">O pacote completo pro contador</div>
        </div>
      </div>

      <div className="flex-1">
        <div className="mb-2.5 flex flex-col gap-[7px]">
          {pacote.checklist.map((item) => (
            <div key={item.label} className="flex items-start gap-2 text-xs">
              <Check className="mt-0.5 h-3.5 w-3.5 flex-none text-pos" strokeWidth={3} />
              <span>
                {item.label}
                {item.count && <span className="ml-1 text-muted-foreground">{item.count}</span>}
              </span>
            </div>
          ))}
        </div>
        <div className="mb-3 text-[11.5px] text-faint">
          <RichText parts={pacote.resultLineByRegime[regime]} />
        </div>
      </div>

      <Button variant="primary" size="md" onClick={onGerar} disabled={state !== 'idle'} className="w-full justify-center">
        {BUTTON_LABEL[state]}
      </Button>

      <AnimatePresence initial={false}>
        {revealed && (
          <motion.div
            initial={{ opacity: 0, height: 0, marginTop: 0 }}
            animate={{ opacity: 1, height: 'auto', marginTop: 10 }}
            transition={{ duration: 0.25, ease: [0.22, 1, 0.36, 1] }}
            className="flex gap-2 overflow-hidden"
          >
            <button
              type="button"
              onClick={onBaixar}
              className="flex-1 rounded-lg border border-border bg-card px-3 py-2 text-center text-[13px] font-medium text-foreground transition-colors hover:bg-surface-2 active:brightness-95"
            >
              Baixar .zip
            </button>
            <SendMenu contact={contact} onSend={onSend} triggerLabel="Enviar pacote ▾" triggerClassName="w-full" />
          </motion.div>
        )}
      </AnimatePresence>
    </Surface>
  );
}
