/**
 * Reexport — o componente canônico `MoneyWhole` vive em `components/financial/shared`, e os
 * formatadores em `@/lib/money`. Mantido aqui só porque os componentes desta tela importam deste
 * caminho. Ver `docs/ui/financeiro-ui.md` §5.
 */
export { MoneyWhole } from '@/components/shared';
export { formatCentavosWhole, formatSignedCentavosWhole } from '@/lib/money';
