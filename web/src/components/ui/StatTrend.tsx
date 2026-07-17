import { ArrowDownRight, ArrowUpRight, Minus } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { Trend } from '@/types/financeiro';

interface StatTrendProps {
  trend: Trend;
  deltaPct: number | null;
  /** Se a alta é boa para o negócio (nem toda subida é positiva, ex. despesa). */
  isGood: boolean;
  className?: string;
}

/** Seta de tendência + cor semântica — nunca cor sozinha (ícone + número sempre juntos). */
export function StatTrend({ trend, deltaPct, isGood, className }: StatTrendProps) {
  const color = trend === 'flat' ? 'text-muted-foreground' : isGood ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400';
  const Icon = trend === 'up' ? ArrowUpRight : trend === 'down' ? ArrowDownRight : Minus;

  return (
    <span className={cn('inline-flex items-center gap-0.5 text-xs font-semibold num', color, className)}>
      <Icon className="h-3.5 w-3.5" strokeWidth={2.5} />
      {deltaPct !== null ? `${Math.abs(deltaPct)}%` : null}
    </span>
  );
}
