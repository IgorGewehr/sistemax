/**
 * "Todas as recorrências" — template REAL (`GET /financeiro/recorrentes/fixas`), mockup
 * `docs/ui/mockups/recorrentes.html` (`#tabelaFixas`). Só o TEMPLATE que o read-model devolve —
 * valor previsto/dia fixo/próxima ocorrência/frequência — sem "Variação vs média"/status
 * `emAlerta` do painel ilustrativo abaixo (`FixasTabela`/`PainelContasFixas`): esse cruzamento com
 * 12 meses de histórico ainda não existe no backend (ver XML doc de `ContasFixasService`).
 */
import type { ReactNode } from 'react';

import { SectionCard } from '@/components/shared';
import type { ContaFixaResumoReal } from '@/lib/api/adapters/financeiro/recorrentes';
import { formatCentavosWhole } from '@/lib/money';
import { cn } from '@/lib/utils';

interface FixasTabelaRealProps {
  itens: ContaFixaResumoReal[];
}

function Th({ children, align }: { children: ReactNode; align?: 'right' }) {
  return (
    <th
      className={cn(
        'border-b border-border px-4 py-3 text-[11px] font-semibold uppercase tracking-[0.06em] text-muted-foreground',
        align === 'right' ? 'text-right' : 'text-left',
      )}
    >
      {children}
    </th>
  );
}

function Td({ children, align, className }: { children: ReactNode; align?: 'right'; className?: string }) {
  return <td className={cn('px-4 py-[13px] text-[13.5px]', align === 'right' && 'text-right', className)}>{children}</td>;
}

export function FixasTabelaReal({ itens }: FixasTabelaRealProps) {
  const totalAtual = itens.reduce((acc, it) => acc + it.valorPrevistoCentavos, 0);

  return (
    <SectionCard
      title="Todas as recorrências"
      hint={`${itens.length} ativas · previsto total ${formatCentavosWhole(totalAtual)}/mês`}
      bodyClassName="overflow-x-auto pb-1"
      className="mb-4"
    >
      {itens.length === 0 ? (
        <p className="px-4 py-6 text-sm text-muted-foreground">Nenhuma recorrência ativa no momento.</p>
      ) : (
        <table className="w-full min-w-[680px] border-collapse">
          <thead>
            <tr>
              <Th>Descrição</Th>
              <Th>Categoria</Th>
              <Th align="right">Valor previsto</Th>
              <Th>Dia venc.</Th>
              <Th>Próxima</Th>
              <Th>Frequência</Th>
              <Th>Tipo</Th>
            </tr>
          </thead>
          <tbody>
            {itens.map((it) => (
              <tr key={it.id} className="border-b border-border/60 last:border-0 hover:bg-surface-2/60">
                <Td className="font-semibold text-foreground">{it.descricao}</Td>
                <Td className="text-[12.5px] text-muted-foreground">{it.categoriaId}</Td>
                <Td align="right" className="num font-semibold">
                  {formatCentavosWhole(it.valorPrevistoCentavos)}
                </Td>
                <Td className="num">{it.diaFixo ? `dia ${it.diaFixo}` : '—'}</Td>
                <Td className="num">{it.proximaOcorrenciaLabel}</Td>
                <Td className="text-muted-foreground">{it.frequencia}</Td>
                <Td className="text-muted-foreground">{it.tipo}</Td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </SectionCard>
  );
}
