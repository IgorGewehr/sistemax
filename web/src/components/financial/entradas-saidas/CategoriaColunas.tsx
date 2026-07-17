import { buildColunasGeometry, categoriaCorCss, COLUNAS_BASELINE, COLUNAS_LABEL_Y, COLUNAS_X0, COLUNAS_X1 } from './calc';
import { formatCentavosWhole } from './money';
import type { CategoriaDespesaResumo } from './types';

interface CategoriaColunasProps {
  categoria: CategoriaDespesaResumo;
  mediaCentavos: number;
  anomalia: boolean;
  meses: string[];
}

/** Colunas dos últimos 6 meses da categoria + linha tracejada da média — mesma matemática do `svgColunas` do mockup. */
export function CategoriaColunas({ categoria, mediaCentavos, anomalia, meses }: CategoriaColunasProps) {
  const { bars, avgY } = buildColunasGeometry(categoria.historicoCentavos, mediaCentavos);
  const slot = (COLUNAS_X1 - COLUNAS_X0) / bars.length;
  const corNormal = categoriaCorCss(categoria.cor);

  return (
    <svg viewBox="0 0 340 132" role="img" aria-label={`${categoria.nome} nos últimos 6 meses`} className="block w-full">
      <line x1={COLUNAS_X0} y1={COLUNAS_BASELINE} x2={COLUNAS_X1} y2={COLUNAS_BASELINE} stroke="hsl(var(--border))" strokeWidth={1} />
      <line
        x1={COLUNAS_X0}
        y1={avgY}
        x2={COLUNAS_X1}
        y2={avgY}
        stroke="hsl(var(--muted-foreground))"
        strokeWidth={1.3}
        strokeDasharray="4 3"
      />
      {bars.map((bar, i) => {
        const isLast = i === bars.length - 1;
        const fill = isLast && anomalia ? 'hsl(var(--warn))' : corNormal;
        return (
          <rect key={meses[i] ?? i} x={bar.x} y={bar.y} width={bar.width} height={bar.height} rx={4} fill={fill} className="transition-opacity hover:opacity-[.74]">
            <title>
              {meses[i]} · {formatCentavosWhole(categoria.historicoCentavos[i])}
            </title>
          </rect>
        );
      })}
      {meses.map((mes, i) => (
        <text key={mes} x={COLUNAS_X0 + slot * i + slot / 2} y={COLUNAS_LABEL_Y} textAnchor="middle" className="fill-muted-foreground text-[9px]">
          {mes}
        </text>
      ))}
    </svg>
  );
}
