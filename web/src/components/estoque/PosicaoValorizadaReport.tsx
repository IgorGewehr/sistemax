import { Fragment } from 'react';

import { MoneyValue } from '@/components/shared';

import { fmtQty } from './calc';
import type { CategoriaResumo } from './types';

interface PosicaoValorizadaReportProps {
  categorias: CategoriaResumo[];
  totalCentavos: number;
  totalItens: number;
  dataLabel: string;
}

/** Relatório "R1 · Posição valorizada" (`previewPosicao()` do mockup) — o único que roda 100% em
 * cima de dado real: agrupa `listarSaldos()` por categoria, sem depender de histórico algum. */
export function PosicaoValorizadaReport({ categorias, totalCentavos, totalItens, dataLabel }: PosicaoValorizadaReportProps) {
  return (
    <div>
      <div className="mx-[18px] mt-3 rounded-xl bg-primary-soft px-3.5 py-3 text-[13px] leading-relaxed text-foreground">
        Posição em <b className="text-primary-600">{dataLabel}</b> — base para o registro de inventário. Total:{' '}
        <b className="text-primary-600">
          <MoneyValue centavos={totalCentavos} />
        </b>{' '}
        em {totalItens} {totalItens === 1 ? 'item' : 'itens'}.
      </div>

      {categorias.length === 0 ? (
        <p className="px-[18px] pb-6 pt-3 text-sm text-muted-foreground">Nenhum produto com controle de estoque ainda.</p>
      ) : (
        <div className="overflow-x-auto px-[18px] pb-5 pt-3">
          <table className="w-full min-w-[560px] border-collapse text-left text-sm">
            <thead>
              <tr className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
                <th className="border-b border-border px-3 py-2.5">Categoria / produto</th>
                <th className="border-b border-border px-3 py-2.5 text-right">Físico</th>
                <th className="border-b border-border px-3 py-2.5 text-right">Custo médio</th>
                <th className="border-b border-border px-3 py-2.5 text-right">Valor total</th>
              </tr>
            </thead>
            <tbody>
              {categorias.map((cat) => (
                <Fragment key={cat.nome}>
                  <tr className="bg-surface-2/50">
                    <td className="px-3 py-2.5 font-bold text-foreground">{cat.nome}</td>
                    <td />
                    <td />
                    <td className="num px-3 py-2.5 text-right font-bold text-foreground">
                      <MoneyValue centavos={cat.valorCentavos} />
                    </td>
                  </tr>
                  {cat.itens.map((item) => (
                    <tr key={item.produto.id} className="border-b border-border/50 last:border-0">
                      <td className="py-2 pl-7 pr-3 text-foreground">{item.produto.nome}</td>
                      <td className="num px-3 py-2 text-right">{fmtQty(item.saldo?.fisico.milesimos, item.produto.unidade)}</td>
                      <td className="num px-3 py-2 text-right">
                        {item.saldo ? <MoneyValue centavos={item.saldo.custoMedio.centavos} /> : '—'}
                      </td>
                      <td className="num px-3 py-2 text-right">
                        {item.saldo ? <MoneyValue centavos={item.saldo.valorTotal.centavos} /> : '—'}
                      </td>
                    </tr>
                  ))}
                </Fragment>
              ))}
              <tr>
                <td className="px-3 py-2.5 font-bold text-foreground">Total geral</td>
                <td />
                <td />
                <td className="num px-3 py-2.5 text-right font-bold text-foreground">
                  <MoneyValue centavos={totalCentavos} />
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
