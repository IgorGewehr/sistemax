/**
 * Formatação de reais inteiros (sem centavos) — fonte única em `@/lib/money`. Reexportado aqui só
 * porque os componentes desta tela importam deste caminho. A margem "R$ 0,18" continua usando o
 * `MoneyValue` COM decimais (é literal no mockup). Ver `docs/ui/financeiro-ui.md` §4/§5.
 */
export { formatCentavosWhole, formatSignedCentavosWhole } from '@/lib/money';
