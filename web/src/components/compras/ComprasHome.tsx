import { ChevronDown, ClipboardList, Upload } from 'lucide-react';

import { PageHeader } from '@/components/shared';
import { Button } from '@/components/ui/Button';

import { ComprasConsultor } from './ComprasConsultor';
import { FornecedorCategoriaDrill } from './FornecedorCategoriaDrill';
import { KpisRow } from './KpisRow';
import { PainelVariacao } from './PainelVariacao';
import { TabelaComprasSection } from './TabelaComprasSection';
import type { ComprasVm } from './useCompras';

interface ComprasHomeProps {
  vm: ComprasVm;
}

/** Home de Compras — 1:1 com `view-home` de `docs/ui/mockups/compras.html`. Página fina: só compõe seções. */
export function ComprasHome({ vm }: ComprasHomeProps) {
  return (
    <div>
      <PageHeader
        subtitle="O que você compra, de quem, e o que isso faz com o seu custo."
        actions={
          <>
            <div className="inline-flex items-center gap-2 rounded-[10px] border border-border bg-card px-3 py-2 text-[13px] font-semibold text-foreground">
              {vm.periodoLabel} <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
            </div>
            <Button variant="outline" size="sm" icon={<ClipboardList className="h-[15px] w-[15px]" strokeWidth={2.2} />} onClick={vm.onNovoPedido}>
              Pedido de compra
            </Button>
            <Button size="sm" icon={<Upload className="h-[15px] w-[15px]" strokeWidth={2.4} />} onClick={vm.onImportarXml} disabled={vm.importando}>
              {vm.importando ? 'Processando XML…' : 'Importar XML'}
            </Button>
          </>
        }
      />

      <KpisRow
        kpis={vm.homeKpis}
        historicoAnteriorCentavos={vm.compradoMesHistoricoCentavos}
        notasConferirAtivo={vm.filtroStatusNota === 'conferir_kpi'}
        variacaoAberta={vm.variacaoAberta}
        onToggleConferirKpi={vm.onToggleConferirKpi}
        onToggleVariacao={vm.onToggleVariacao}
      />

      {vm.variacaoAberta && <PainelVariacao itens={vm.variacaoLista} />}

      <ComprasConsultor />

      <FornecedorCategoriaDrill
        ranking={vm.fornecedorRanking}
        custoPorCategoria={vm.custoPorCategoria}
        scorecardFornecedor={vm.fornecedorScorecard}
        onSelecionarBarra={vm.onSelecionarFornecedorBarra}
        onVoltarScorecard={vm.onVoltarScorecard}
        onVerTodosFornecedores={vm.onVerTodosFornecedores}
        onVerPerfil={vm.irParaFornecedor}
      />

      <TabelaComprasSection
        segmentoAtivo={vm.segmentoAtivo}
        onChangeSegmento={vm.onChangeSegmento}
        buscaTexto={vm.buscaTexto}
        onChangeBusca={vm.onChangeBusca}
        filtroStatusNota={vm.filtroStatusNota}
        onChangeFiltroStatus={vm.onChangeFiltroStatus}
        notasFiltradas={vm.notasFiltradas}
        pedidosFiltrados={vm.pedidosFiltrados}
        fornecedoresFiltrados={vm.fornecedoresFiltrados}
        fornecedores={vm.fornecedores}
        onAbrirNota={vm.irParaConferencia}
        onAbrirFornecedor={vm.irParaFornecedor}
      />
    </div>
  );
}
