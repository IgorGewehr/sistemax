import type { Centavos } from '@/lib/money';

import { MoneyValue } from './MoneyValue';

interface MoneyWholeProps {
  centavos: Centavos | null | undefined;
  signed?: boolean;
  tone?: 'auto' | 'pos' | 'crit' | 'none';
  className?: string;
}

/**
 * Atalho para `<MoneyValue whole />` — reais inteiros, sem casas decimais. É como a maioria dos
 * mockups do Financeiro exibe dinheiro (o `money()`/`brl()` deles arredonda e não mostra centavos).
 */
export function MoneyWhole({ centavos, signed = false, tone = 'none', className }: MoneyWholeProps) {
  return <MoneyValue centavos={centavos} whole signed={signed} tone={tone} className={className} />;
}
