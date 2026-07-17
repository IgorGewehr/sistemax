import { SectionCard, StatusChip } from '@/components/shared';

import type { AccountChipId } from './AccountFilterBar';
import { BancarioMoneyValue } from './BancarioMoneyValue';
import { ordenarMovimentosDesc } from './derive';
import type { ContaBancaria, MovimentoExtrato } from './types';

interface ExtratoTableProps {
  movimentos: MovimentoExtrato[];
  contas: ContaBancaria[];
  selectedAccountId: AccountChipId;
  hint: string;
}

/** Tabela "Extrato" — sempre o último bloco da tela; filtra pela conta selecionada nos chips. */
export function ExtratoTable({ movimentos, contas, selectedAccountId, hint }: ExtratoTableProps) {
  const contaMap = new Map(contas.map((c) => [c.id, c]));
  const filtrados = selectedAccountId === 'todas' ? movimentos : movimentos.filter((m) => m.contaId === selectedAccountId);
  const linhas = ordenarMovimentosDesc(filtrados);

  return (
    <SectionCard title="Extrato" hint={hint}>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[660px] border-collapse">
          <thead>
            <tr>
              <th className="border-b border-border px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
                Data
              </th>
              <th className="border-b border-border px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
                Descrição
              </th>
              <th className="border-b border-border px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
                Forma
              </th>
              <th className="border-b border-border px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
                Conta
              </th>
              <th className="border-b border-border px-4 py-3 text-right text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
                Valor
              </th>
              <th className="border-b border-border px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
                Conciliação
              </th>
            </tr>
          </thead>
          <tbody>
            {linhas.map((m) => {
              const conta = contaMap.get(m.contaId);
              return (
                <tr key={m.id} className="border-b border-border/60 last:border-b-0 hover:bg-surface-2/60">
                  <td className="num px-4 py-[13px] text-[13.5px]">{m.data}</td>
                  <td className="px-4 py-[13px] text-[13.5px]">{m.descricao}</td>
                  <td className="px-4 py-[13px] text-[13.5px]">{m.forma}</td>
                  <td className="px-4 py-[13px] text-[13.5px]">
                    <span className="inline-flex items-center gap-2 font-semibold">
                      <span className={`h-[9px] w-[9px] flex-none rounded-[3px] ${conta?.dotClassName ?? 'bg-foreground/40'}`} />
                      {conta?.label ?? m.contaId}
                    </span>
                  </td>
                  <td className="px-4 py-[13px] text-right text-[13.5px]">
                    <BancarioMoneyValue centavos={m.valorCentavos} signed tone="auto" className="font-semibold" />
                  </td>
                  <td className="px-4 py-[13px] text-[13.5px]">
                    {m.status === 'conciliado' ? (
                      <StatusChip tone="sobra" dot={false}>
                        ✔ conciliado auto
                      </StatusChip>
                    ) : (
                      <StatusChip tone="aberto" dot={false}>
                        ⚠ sem lançamento
                      </StatusChip>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </SectionCard>
  );
}
