import { Sparkles } from 'lucide-react';
import type { ReactNode } from 'react';

import { cn } from '@/lib/utils';

interface ConsultorInsightProps {
  /** Título — padrão "Super Consultor". */
  title?: string;
  /** O texto do insight (pode conter <b>, valores coloridos). */
  children: ReactNode;
  /**
   * Link de aprofundamento — SÓ navegação/drill (ex.: "Ver as quintas →"). READ-ONLY: nunca uma
   * ação que a IA executa no sistema (Lei 2 do contrato). A IA observa e aconselha; quem age é o
   * usuário.
   */
  action?: { label: string; onClick: () => void };
  className?: string;
}

/**
 * Card do Super Consultor (`.consultor` do mockup) — borda em gradiente de marca, ícone, e um
 * parágrafo de análise. É a IA falando: observa, explica, aconselha. **Não age.**
 */
export function ConsultorInsight({ title = 'Super Consultor', children, action, className }: ConsultorInsightProps) {
  return (
    <div
      className={cn(
        'rounded-2xl bg-gradient-to-br from-primary-600/40 to-border/20 p-px',
        className,
      )}
    >
      <div className="flex items-start gap-3.5 rounded-2xl bg-card p-4 sm:p-[18px]">
        <span className="grid h-[38px] w-[38px] flex-none place-items-center rounded-xl bg-primary-soft text-primary-600">
          <Sparkles className="h-5 w-5" />
        </span>
        <div className="min-w-0">
          <h3 className="mb-1.5 text-[13px] font-bold tracking-tight text-foreground">{title}</h3>
          <p className="text-[13.5px] leading-relaxed text-foreground">
            {children}
            {action && (
              <button
                type="button"
                onClick={action.onClick}
                className="ml-1.5 font-bold text-primary-600 hover:underline"
              >
                {action.label}
              </button>
            )}
          </p>
        </div>
      </div>
    </div>
  );
}
