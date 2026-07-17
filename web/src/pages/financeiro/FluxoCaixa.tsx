import { ChevronDown } from 'lucide-react';

import { FluxoCaixaBoard } from '@/components/financial/fluxo-caixa/FluxoCaixaBoard';
import { useFluxoCaixa } from '@/components/financial/fluxo-caixa/useFluxoCaixa';
import { PageHeader } from '@/components/shared';

/**
 * Fluxo de Caixa — o ritual do caixa em espécie (abertura, fechamento cego, sangria, quebra).
 * Página fina (SDD): `PageHeader` aqui (padrão das outras telas do Financeiro), todo estado real
 * vive em `useFluxoCaixa` (`SessaoCaixa`/`GET,POST /financeiro/caixa/*` — ver
 * docs/wiring/financeiro-telas-restantes.md §4) e a composição/estados loading-erro-vazio em
 * `FluxoCaixaBoard`.
 */
export function FluxoCaixa() {
  const vm = useFluxoCaixa();

  return (
    <div>
      <PageHeader
        subtitle="Dinheiro em espécie — abertura, suprimento, sangria e fechamento."
        actions={
          <div className="flex items-center gap-2 rounded-xl border border-border bg-card px-3 py-2 text-[13px] font-semibold text-foreground">
            {vm.periodoLabel} <ChevronDown className="h-3.5 w-3.5" />
          </div>
        }
      />

      <FluxoCaixaBoard
        board={vm.board}
        todasAsSessoes={vm.todasAsSessoes}
        estatisticasMes={vm.estatisticasMes}
        sangriasMes={vm.sangriasMes}
        sangriasMaiorDestino={vm.sangriasMaiorDestino}
        diaCritico={vm.diaCritico}
        mediaDiferencaCentavos={vm.mediaDiferencaCentavos}
        consultorInsight={vm.consultorInsight}
        vendasEspeciePercentual={vm.vendasEspeciePercentual}
        destinosSangria={vm.destinosSangria}
        enviandoAcao={vm.enviandoAcao}
        onAbrirCaixa={vm.abrirCaixa}
        onRegistrarSangria={vm.registrarSangria}
        onRegistrarSuprimento={vm.registrarSuprimento}
        onFecharCaixa={vm.fecharCaixa}
      />
    </div>
  );
}
