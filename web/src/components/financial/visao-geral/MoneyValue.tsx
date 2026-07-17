import type { Centavos } from '@/lib/money';
import { cn } from '@/lib/utils';

import { formatCentavosWhole, formatSignedCentavosWhole } from './money';

type MoneyTone = 'auto' | 'pos' | 'crit' | 'none';

interface MoneyValueProps {
  /** Valor em CENTAVOS inteiros (espelha o Money do domínio). */
  centavos: Centavos | null | undefined;
  /** Mostra sinal explícito (+/−) — usado em deltas/diferenças. */
  signed?: boolean;
  /** `auto` = verde se positivo, vermelho se negativo. Padrão `none` (herda a cor do texto). */
  tone?: MoneyTone;
  className?: string;
}

/**
 * Mesma API/aparência do `MoneyValue` compartilhado, mas sem casas decimais — o mockup da Visão
 * Geral nunca mostra ',00' (ver `./money.ts`). Fica local a esta tela porque o `MoneyValue` de
 * `components/financial/shared` é usado por outras telas do financeiro que precisam exibir
 * centavos, e não deve ser alterado fora de sua própria tela.
 *
 * Exceção: a margem ("R$ 0,18") em `LucroDoMesCard` importa o `MoneyValue` compartilhado (com
 * decimais) diretamente, porque é a única linha do mockup que nasce em centavos de propósito.
 */
export function MoneyValue({ centavos, signed = false, tone = 'none', className }: MoneyValueProps) {
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
