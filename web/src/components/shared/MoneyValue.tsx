import {
  type Centavos,
  formatCentavos,
  formatCentavosWhole,
  formatSignedCentavos,
  formatSignedCentavosWhole,
} from '@/lib/money';
import { cn } from '@/lib/utils';

type MoneyTone = 'auto' | 'pos' | 'crit' | 'none';

interface MoneyValueProps {
  /** Valor em CENTAVOS inteiros (espelha o Money do domínio). */
  centavos: Centavos | null | undefined;
  /** Mostra sinal explícito (+/−) — usado em deltas/diferenças. */
  signed?: boolean;
  /** Reais inteiros, sem casas decimais (como a maioria dos mockups do Financeiro exibe). */
  whole?: boolean;
  /** `auto` = verde se positivo, vermelho se negativo. Padrão `none` (herda a cor do texto). */
  tone?: MoneyTone;
  className?: string;
}

/** Valor monetário formatado (tabular/mono via `.num`), com cor de sinal opcional. */
export function MoneyValue({ centavos, signed = false, whole = false, tone = 'none', className }: MoneyValueProps) {
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
  const texto = whole
    ? signed
      ? formatSignedCentavosWhole(centavos)
      : formatCentavosWhole(centavos)
    : signed
      ? formatSignedCentavos(centavos)
      : formatCentavos(centavos);
  return <span className={cn('num', toneClass, className)}>{texto}</span>;
}
