import { ChevronDown, Plus } from 'lucide-react';

import { PageHeader } from '@/components/shared';
import { Button } from '@/components/ui/Button';

import { ConsultorSection } from './ConsultorSection';
import { FilaSection } from './FilaSection';
import { FunilOperacaoSection } from './FunilOperacaoSection';
import { KpiRow } from './KpiRow';
import type { UseOrdemServico } from './useOrdemServico';

interface OrdemServicoListaProps {
  vm: UseOrdemServico;
}

/**
 * Tela de lista — "A bancada, o que espera o cliente, e o que isso vale." Página fina: só
 * compõe as seções na mesma ordem do mockup (KPIs → Super Consultor → funil/operação → fila).
 */
export function OrdemServicoLista({ vm }: OrdemServicoListaProps) {
  return (
    <div>
      <PageHeader
        subtitle="A bancada, o que espera o cliente, e o que isso vale."
        actions={
          <>
            <div className="flex items-center gap-2 rounded-[10px] border border-border bg-card px-3 py-2 text-[13px] font-semibold text-foreground">
              Julho 2026 <ChevronDown className="h-3.5 w-3.5" />
            </div>
            <Button variant="primary" onClick={vm.abrirNovaOs} icon={<Plus className="h-[15px] w-[15px]" strokeWidth={2.4} />}>
              Nova OS
            </Button>
          </>
        }
      />

      <KpiRow kpis={vm.kpis} />

      <ConsultorSection data={vm.consultor} />

      <FunilOperacaoSection
        buckets={vm.buckets}
        onSelecionarEtapa={vm.selecionarEtapa}
        bucketSelecionado={vm.bucketSelecionado}
        bucketSelecionadoStats={vm.bucketSelecionadoStats}
        operacao={vm.operacao}
        onIrParaDetalhe={vm.irParaDetalhe}
      />

      <FilaSection
        filtroFila={vm.filtroFila}
        onFiltroChange={vm.setFiltroFila}
        buscaFila={vm.buscaFila}
        onBuscaChange={vm.setBuscaFila}
        itens={vm.filaItens}
        totalCount={vm.totalCount}
        ativasCount={vm.ativasCount}
        encerradasCount={vm.encerradasCount}
        acaoPrimaria={vm.acaoPrimaria}
        onRowClick={vm.irParaDetalhe}
      />
    </div>
  );
}
