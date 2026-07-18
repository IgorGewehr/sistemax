import { cn } from '@/lib/utils';

interface InfoTipProps {
  /** Texto do tooltip nativo (`title`) — o `.info-ic[data-tip]` do mockup. */
  text: string;
  className?: string;
}

/** Selinho "ⓘ" ao lado de rótulos técnicos (payback, LTV, TIR...) — mesmo papel do `.info-ic`
 * CSS-only dos mockups. Usa `title` nativo (sem JS de hover custom) — simples, acessível
 * (`tabIndex`), sem depender de biblioteca de tooltip. */
export function InfoTip({ text, className }: InfoTipProps) {
  return (
    <span
      title={text}
      tabIndex={0}
      className={cn(
        'ml-1 inline-flex h-[15px] w-[15px] shrink-0 cursor-help items-center justify-center rounded-full bg-surface-2 align-middle text-[10px] font-semibold text-muted-foreground',
        className,
      )}
    >
      i
    </span>
  );
}
