import { cn } from '@/lib/utils';

import { deltaBadge, type DeltaBadgeKind } from './calc';

const KIND_CLASSES: Record<DeltaBadgeKind, string> = {
  novo: 'text-[hsl(217_80%_52%)] dark:text-[hsl(213_90%_68%)] font-semibold',
  flat: 'text-faint font-bold',
  'up-bad': 'text-warn font-bold',
  'up-crit': 'text-crit font-bold',
  'down-good': 'text-pos font-bold',
};

interface DeltaBadgeViewProps {
  pct: number | null | undefined;
  className?: string;
}

/** Badge "▲ +14,0% vs últ." / "1ª compra" / "▬ estável" (`deltaBadge()` do mockup). */
export function DeltaBadgeView({ pct, className }: DeltaBadgeViewProps) {
  const badge = deltaBadge(pct);
  return <span className={cn('whitespace-nowrap text-[12px]', KIND_CLASSES[badge.kind], className)}>{badge.label}</span>;
}
