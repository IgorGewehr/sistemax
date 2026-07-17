import { ChevronDown } from 'lucide-react';

import { useToast } from '@/lib/toast';

interface PeriodoTriggerProps {
  label: string;
}

/** Seletor de período do cabeçalho (`.period` do mockup) — trocar o mês ainda não existe nesta fatia. */
export function PeriodoTrigger({ label }: PeriodoTriggerProps) {
  const { toast } = useToast();

  return (
    <button
      type="button"
      onClick={() => toast('Trocar o mês recarregaria os 5 blocos com o novo período.', 'info')}
      className="inline-flex items-center gap-2 rounded-xl border border-border bg-card px-3 py-2 text-sm font-semibold text-foreground transition-colors hover:bg-surface-2 active:brightness-95"
    >
      {label}
      <ChevronDown className="h-3.5 w-3.5" />
    </button>
  );
}
