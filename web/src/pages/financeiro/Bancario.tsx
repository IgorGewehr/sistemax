import { ChevronDown, Plus } from 'lucide-react';

import { BancarioBoard } from '@/components/financial/bancario/BancarioBoard';
import { useBancario } from '@/components/financial/bancario/useBancario';
import { PageHeader } from '@/components/shared';

/** Página fina — só compõe o cabeçalho e delega o corpo interativo ao `BancarioBoard`, que
 * consome dado REAL (`useBancario` → GET /financeiro/{contas-bancarias,formas-pagamento,
 * movimentos,movimentos-semana,conciliacao,taxas-por-forma} — ver
 * docs/wiring/financeiro-telas-restantes.md §3). */
export function Bancario() {
  const vm = useBancario();

  return (
    <div>
      <PageHeader
        subtitle="O que de fato entrou e saiu das suas contas — e se bate com o que você lançou."
        actions={
          <div className="inline-flex items-center gap-2 rounded-[10px] border border-border bg-card px-3 py-2 text-sm font-semibold">
            {vm.periodoLabel}
            <ChevronDown className="h-3.5 w-3.5" />
          </div>
        }
      />

      <BancarioBoard
        contas={vm.contas}
        extrato={vm.extrato}
        conciliacao={vm.conciliacao}
        consultor={vm.consultor}
        kpiSaldoDelta={vm.kpiSaldoDelta}
        kpiEntrouDelta={vm.kpiEntrouDelta}
        kpiSaiuDelta={vm.kpiSaiuDelta}
        onConfirmar={vm.confirmarItem}
        onIgnorar={vm.ignorarItem}
      />

      <button
        type="button"
        title="Lançar"
        aria-label="Lançar"
        className="fixed bottom-6 right-6 z-10 grid h-[54px] w-[54px] place-items-center rounded-full bg-primary-600 text-white shadow-red-lg transition-transform hover:brightness-105 hover:-translate-y-0.5 active:brightness-95"
      >
        <Plus className="h-6 w-6" strokeWidth={2.4} />
      </button>
    </div>
  );
}
