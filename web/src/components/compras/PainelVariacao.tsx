import { MoneyValue, SectionCard } from '@/components/shared';

import { custoUnitCentavosOf, type ItemComVariacao } from './calc';
import { DeltaBadgeView } from './DeltaBadgeView';

interface PainelVariacaoProps {
  itens: ItemComVariacao[];
}

/** Painel do KPI "Variação de custo" (`renderPainelVariacao()` do mockup) — top 6 itens por |Δ|. */
export function PainelVariacao({ itens }: PainelVariacaoProps) {
  const top6 = itens.slice(0, 6);

  return (
    <div className="mb-4">
      <SectionCard title="Itens com variação de custo" hint="últimas notas · Δ vs compra anterior do mesmo item">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[520px] border-collapse">
            <thead>
              <tr>
                <th className="border-b border-border px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Item</th>
                <th className="border-b border-border px-4 py-3 text-right text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Custo entrada</th>
                <th className="border-b border-border px-4 py-3 text-right text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Variação</th>
              </tr>
            </thead>
            <tbody>
              {top6.map(({ nota, item, fornecedorNome }) => (
                <tr key={`${nota.id}-${item.nItem}`}>
                  <td className="border-b border-border/60 px-4 py-3 align-middle text-[13.5px]">
                    <div className="font-semibold">{item.nome}</div>
                    <div className="text-xs text-muted-foreground">
                      {fornecedorNome} · NF {nota.numero}
                    </div>
                  </td>
                  <td className="num border-b border-border/60 px-4 py-3 text-right text-[13.5px]">
                    <MoneyValue centavos={custoUnitCentavosOf(item)} />
                  </td>
                  <td className="border-b border-border/60 px-4 py-3 text-right text-[13.5px]">
                    <DeltaBadgeView pct={item.deltaPct} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </SectionCard>
    </div>
  );
}
