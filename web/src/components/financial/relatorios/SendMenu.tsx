import { ChevronDown, Mail, MessageCircle } from 'lucide-react';

import { cn } from '@/lib/utils';

import type { AccountantContact } from './types';
import { useDisclosure } from './useDisclosure';

interface SendMenuProps {
  contact: AccountantContact;
  onSend: (channel: 'email' | 'whatsapp') => void;
  /** Cards normais usam "Enviar" + ícone; o Pacote usa o texto literal "Enviar pacote ▾" do mockup. */
  triggerLabel?: string;
  className?: string;
  triggerClassName?: string;
}

/** Dropdown "Enviar" (`.send-menu` do mockup) — 2 opções fixas: e-mail e WhatsApp do contador. */
export function SendMenu({ contact, onSend, triggerLabel = 'Enviar', className, triggerClassName }: SendMenuProps) {
  const { open, ref, toggle, close } = useDisclosure<HTMLDivElement>();

  const handleSend = (channel: 'email' | 'whatsapp') => {
    close();
    onSend(channel);
  };

  return (
    <div ref={ref} className={cn('relative flex-1', className)}>
      <button
        type="button"
        onClick={toggle}
        className={cn(
          'inline-flex w-full items-center justify-center gap-1.5 whitespace-nowrap rounded-lg border border-border bg-card px-3 py-2 text-[13px] font-medium text-foreground transition-colors hover:bg-surface-2 active:brightness-95',
          triggerClassName,
        )}
      >
        {triggerLabel}
        {triggerLabel === 'Enviar' && <ChevronDown className="h-3 w-3" />}
      </button>
      {open && (
        <div className="absolute right-0 top-[calc(100%+6px)] z-30 min-w-[232px] rounded-xl border border-border bg-card p-1.5 shadow-lg">
          <button
            type="button"
            onClick={() => handleSend('email')}
            className="flex w-full items-start gap-2.5 rounded-lg p-2 text-left transition-colors hover:bg-surface-2"
          >
            <span className="grid h-[30px] w-[30px] flex-none place-items-center rounded-lg bg-surface-2 text-muted-foreground">
              <Mail className="h-[15px] w-[15px]" />
            </span>
            <span className="min-w-0">
              <span className="block text-[12.5px] font-semibold text-foreground">{contact.emailLabel}</span>
              <span className="block text-[11px] text-muted-foreground">{contact.email}</span>
            </span>
          </button>
          <button
            type="button"
            onClick={() => handleSend('whatsapp')}
            className="flex w-full items-start gap-2.5 rounded-lg p-2 text-left transition-colors hover:bg-surface-2"
          >
            <span className="grid h-[30px] w-[30px] flex-none place-items-center rounded-lg bg-surface-2 text-muted-foreground">
              <MessageCircle className="h-[15px] w-[15px]" />
            </span>
            <span className="min-w-0">
              <span className="block text-[12.5px] font-semibold text-foreground">{contact.whatsappLabel}</span>
              <span className="block text-[11px] text-muted-foreground">{contact.whatsapp}</span>
            </span>
          </button>
        </div>
      )}
    </div>
  );
}
