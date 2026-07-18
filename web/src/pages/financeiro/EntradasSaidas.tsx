import { ChevronDown, Receipt } from 'lucide-react';

import { TODAY_ISO } from '@/components/financial/entradas-saidas/calc';
import { ConsultorFornecedores } from '@/components/financial/entradas-saidas/ConsultorFornecedores';
import { GastosPorCategoria } from '@/components/financial/entradas-saidas/GastosPorCategoria';
import { KpiRow } from '@/components/financial/entradas-saidas/KpiRow';
import { LinhaDoTempo } from '@/components/financial/entradas-saidas/LinhaDoTempo';
import { RaioXDoMes } from '@/components/financial/entradas-saidas/RaioXDoMes';
import { SegmentedFiltro } from '@/components/financial/entradas-saidas/SegmentedFiltro';
import { useEntradasSaidas } from '@/components/financial/entradas-saidas/useEntradasSaidas';
import { PageHeader } from '@/components/shared';
import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';

/**
 * Entradas & saídas — reprodução 1:1 de `docs/ui/mockups/entradas-saidas.html`. Página fina: só
 * compõe seções; todo estado/derivação vive em `useEntradasSaidas` (hook) e `calc.ts` (puro).
 * Linha do tempo, os 4 KPIs de topo (aberto/atrasado/resultado/projeção de caixa) e o Super
 * Consultor de Fornecedores são dado REAL (`GET /financeiro/extrato` + `relatorios/dre` + `fluxo`,
 * ver `useEntradasSaidas.ts`). "Para onde foi o dinheiro"/Raio-X do mês continuam ilustrativos
 * (`exemplos.ts`) — o domínio ainda não agrupa categoria por 6 meses.
 */
export function EntradasSaidas() {
  const vm = useEntradasSaidas();

  return (
    <div>
      <PageHeader
        subtitle="Tudo que entrou, saiu — e o que ainda vem. É aqui que você planeja."
        actions={
          <div className="flex items-center gap-2 rounded-[10px] border border-border bg-card px-3 py-2 text-[13px] font-semibold text-foreground">
            {vm.periodoLabel} <ChevronDown className="h-3.5 w-3.5" />
          </div>
        }
      />

      <SegmentedFiltro value={vm.segFiltro} onChange={vm.setSegFiltro} />

      {vm.kpis.carregando ? (
        <Surface padding="lg" className="mb-4 min-h-[140px]">
          <Skeleton className="h-24 w-full" />
        </Surface>
      ) : vm.kpis.erro || !vm.kpis.dado ? (
        <Surface padding="lg" className="mb-4">
          <EmptyState icon={<Receipt className="h-5 w-5" />} title="Não deu para carregar os KPIs" description={vm.kpis.erro ?? ''} className="border-none py-6" />
        </Surface>
      ) : (
        <KpiRow kpis={vm.kpis.dado} />
      )}

      {vm.consultorFornecedores.carregando ? (
        <Surface padding="lg" className="mb-4 min-h-[86px]">
          <Skeleton className="h-14 w-full" />
        </Surface>
      ) : vm.consultorFornecedores.erro || !vm.consultorFornecedores.dado ? (
        <Surface padding="lg" className="mb-4">
          <EmptyState
            icon={<Receipt className="h-5 w-5" />}
            title="Não deu para carregar o Consultor de Fornecedores"
            description={vm.consultorFornecedores.erro ?? ''}
            className="border-none py-6"
          />
        </Surface>
      ) : (
        <ConsultorFornecedores data={vm.consultorFornecedores.dado} onVerDetalhe={vm.onClickConsultorFornecedores} />
      )}

      <section ref={vm.analiseRef} className="mb-4 grid grid-cols-1 gap-4 lg:grid-cols-[1.15fr_1fr]">
        <GastosPorCategoria
          barras={vm.barras}
          categoriaSelecionada={vm.categoriaSelecionada}
          drillStats={vm.categoriaDrill}
          meses={vm.mesesHistorico}
          onSelecionar={vm.selecionarCategoria}
        />
        <RaioXDoMes
          categoriaSelecionada={vm.categoriaSelecionada}
          drillStats={vm.categoriaDrill}
          fixoPct={vm.fixoVariavel.fixoPct}
          varPct={vm.fixoVariavel.varPct}
          liderAlta={vm.liderAlta}
          atrasados30={vm.atrasados30}
          onClickAtrasados={vm.onClickAtrasadosTile}
        />
      </section>

      {vm.timelineCarregando ? (
        <Surface padding="lg" className="mb-4 min-h-[220px]">
          <Skeleton className="h-40 w-full" />
        </Surface>
      ) : vm.timelineErro ? (
        <Surface padding="lg" className="mb-4">
          <EmptyState icon={<Receipt className="h-5 w-5" />} title="Não deu para carregar a linha do tempo" description={vm.timelineErro} className="border-none py-6" />
        </Surface>
      ) : (
        <LinhaDoTempo
          entries={vm.timeline}
          hint={vm.hintLinhaDoTempo}
          filtroAtivo={vm.filtroAtivo}
          onLimparFiltro={vm.limparFiltro}
          cobradosIds={vm.cobradosIds}
          onDarBaixa={vm.abrirBaixa}
          onCobrar={vm.cobrar}
          onAbrirDetalhe={vm.abrirDetalhe}
          onVerExtratoCompleto={vm.verExtratoCompleto}
          resumoPdvMes={vm.resumoPdvMes}
          modalBaixa={vm.modalBaixa}
          onFecharBaixa={vm.fecharBaixa}
          onConfirmarBaixa={vm.confirmarBaixa}
          contasDisponiveis={vm.contasDisponiveis}
          modalLancarAberto={vm.modalLancarAberto}
          onAbrirLancar={vm.abrirLancar}
          onFecharLancar={vm.fecharLancar}
          onSalvarLancamento={vm.salvarLancamento}
          categoriasLancamentoRapido={vm.categoriasLancamentoRapido}
          vencimentoPadrao={TODAY_ISO}
          modalDetalhe={vm.modalDetalhe}
          onFecharDetalhe={vm.fecharDetalhe}
        />
      )}
    </div>
  );
}
