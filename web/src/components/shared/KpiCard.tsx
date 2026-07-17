import type { ReactNode } from 'react';

import { Surface } from '@/components/ui/Surface';
import { cn } from '@/lib/utils';

import { Eyebrow } from './Eyebrow';

interface KpiCardProps {
  label: ReactNode;
  /** Valor grande (normalmente `.num`). Pode ser string, MoneyValue, ou markup. */
  value: ReactNode;
  /** Linha de rodapé (contexto: "3 faltas · 1 sobra"). */
  foot?: ReactNode;
  /** Card em destaque (o `.kpi.hero` do mockup — glow radial de marca no canto). */
  hero?: boolean;
  valueClassName?: string;
  /** Slot abaixo do valor (ex.: botão "Fechar caixa" no KPI hero). */
  children?: ReactNode;
  className?: string;
}

/** Card de KPI do padrão dos mockups (`.kpi`): rótulo, valor grande, rodapé/CTA opcional. */
export function KpiCard({ label, value, foot, hero, valueClassName, children, className }: KpiCardProps) {
  return (
    <Surface
      padding="none"
      className={cn(
        'relative overflow-hidden p-4 sm:p-[18px]',
        hero &&
          'before:pointer-events-none before:absolute before:inset-0 before:bg-[radial-gradient(120%_90%_at_100%_0,hsl(var(--primary)/0.10),transparent_60%)]',
        className,
      )}
    >
      <Eyebrow>{label}</Eyebrow>
      <div className={cn('relative z-[1] mt-2.5 text-2xl font-bold tracking-tight', valueClassName)}>{value}</div>
      {foot && <div className="relative z-[1] mt-1 text-xs text-muted-foreground">{foot}</div>}
      {children && <div className="relative z-[1] mt-2.5">{children}</div>}
    </Surface>
  );
}
