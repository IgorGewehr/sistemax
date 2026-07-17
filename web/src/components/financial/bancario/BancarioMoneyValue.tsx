import type { Centavos } from '@/lib/money';
import { cn } from '@/lib/utils';

import { formatCentavosWhole, formatSignedCentavosWhole } from './money';

type MoneyTone = 'auto' | 'pos' | 'crit' | 'none';

interface BancarioMoneyValueProps {
  /** Valor em CENTAVOS inteiros (espelha o Money do domínio). */
  centavos: Centavos | null | undefined;
  /** Mostra sinal explícito (+/−) — usado em deltas/diferenças. */
  signed?: boolean;
  /** `auto` = verde se positivo, vermelho se negativo. Padrão `none` (herda a cor do texto). */
  tone?: MoneyTone;
  className?: string;
}

/**
 * Mesma API/aparência do `MoneyValue` compartilhado, mas sem casas decimais — o mockup do
 * Bancário nunca mostra ',00' (ver `./money.ts`). Fica local a esta tela porque o `MoneyValue`
 * de `components/financial/shared` é usado por outras telas do financeiro que precisam exibir
 * centavos, e não deve ser alterado fora de sua própria tela.
 */
export function BancarioMoneyValue({ centavos, signed = false, tone = 'none', className }: BancarioMoneyValueProps) {
  const toneClass =
    tone === 'pos'
      ? 'text-pos'
      : tone === 'crit'
        ? 'text-crit'
        : tone === 'auto' && typeof centavos === 'number'
          ? centavos > 0
            ? 'text-pos'
            : centavos < 0
              ? 'text-crit'
              : ''
          : '';
  return (
    <span className={cn('num', toneClass, className)}>
      {signed ? formatSignedCentavosWhole(centavos) : formatCentavosWhole(centavos)}
    </span>
  );
}
