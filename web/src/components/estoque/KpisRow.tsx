import { KpiCard, MoneyValue } from '@/components/shared';

import type { EstoqueKpis } from './types';

interface KpisRowProps {
  kpis: EstoqueKpis;
}

/** As 4 KPIs da Visão Geral (`.kpis` do mockup) — 100% derivados de `listarSaldos()` real. Sem
 * sparkline no hero: o mockup anima um histórico de 6 pontos que aqui seria inventado (não há
 * snapshot histórico de valor em estoque nesta API). */
export function KpisRow({ kpis }: KpisRowProps) {
  return (
    <section className="mb-4 grid grid-cols-2 gap-3.5 lg:grid-cols-4">
      <KpiCard
        hero
        label="Valor em estoque"
        value={<MoneyValue centavos={kpis.valorEmEstoqueCentavos} />}
        foot={`físico × custo médio · ${kpis.itensComSaldo} ${kpis.itensComSaldo === 1 ? 'item' : 'itens'}`}
      />
      <KpiCard
        label="Abaixo do mínimo"
        value={`${kpis.abaixoDoMinimo} ${kpis.abaixoDoMinimo === 1 ? 'item' : 'itens'}`}
        valueClassName={kpis.abaixoDoMinimo > 0 ? 'text-warn' : undefined}
        foot={`${kpis.zerados} já ${kpis.zerados === 1 ? 'zerado' : 'zerados'}`}
      />
      <KpiCard
        label="Zerados (ruptura)"
        value={`${kpis.zerados} ${kpis.zerados === 1 ? 'item' : 'itens'}`}
        valueClassName={kpis.zerados > 0 ? 'text-crit' : undefined}
        foot="perdendo venda agora"
      />
      <KpiCard label="Produtos cadastrados" value={kpis.produtosCadastrados} foot="no catálogo" />
    </section>
  );
}
