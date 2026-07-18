interface DecorativeSparklineProps {
  tone: 'pos' | 'crit';
}

/**
 * Mini-gráfico decorativo das tiles "Resultado"/"Assinaturas" (mesmo papel do `Sparkline` de
 * `components/financial/entradas-saidas/Sparkline.tsx`) — asset visual fixo do mockup, SEM série
 * numérica própria por trás: nem `relatorios/dre` nem `receita-recorrente` expõem histórico mensal
 * ainda, então desenhar uma curva "real" aqui seria inventar dado. O número-herói ao lado É real;
 * só o traço é ilustrativo, como no HTML original (`docs/ui/mockups/visao-geral-v3.html`).
 */
export function DecorativeSparkline({ tone }: DecorativeSparklineProps) {
  return (
    <svg viewBox="0 0 122 30" aria-hidden="true" className="block h-[30px] w-full max-w-[128px]">
      <polyline
        points="2,27 20,24 38,19 56,20 74,13 92,10 108,7 118,5"
        fill="none"
        className="stroke-faint"
        strokeWidth={2}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <circle cx={118} cy={5} r={3.5} className={tone === 'pos' ? 'fill-pos' : 'fill-crit'} stroke="hsl(var(--card))" strokeWidth={2} />
    </svg>
  );
}
