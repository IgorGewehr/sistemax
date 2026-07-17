import { ChevronDown } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

import { PageHeader } from '@/components/shared';
import { Button } from '@/components/ui/Button';
import { KpisRow } from '@/components/vendas/KpisRow';
import { useVendas } from '@/components/vendas/useVendas';
import { VendaDetalheModal } from '@/components/vendas/VendaDetalheModal';
import { VendasConsultor } from '@/components/vendas/VendasConsultor';
import { VendasTableSection } from '@/components/vendas/VendasTableSection';
import { useToast } from '@/lib/toast';

/**
 * Vendas — relatório READ-ONLY do que já foi vendido: KPIs, filtros e drill pro comprovante de
 * cada venda. Distinta do PDV (que abre/edita a venda em andamento) e não oferece estorno/edição
 * — isso é ação do PDV/Financeiro, fora do escopo desta tela. Sem mockup .html prévio:
 * `components/vendas/types.ts` é o contrato. Página fina: só compõe seções a partir de `useVendas`.
 */
export function Vendas() {
  const vm = useVendas();
  const { toast } = useToast();
  const navigate = useNavigate();

  return (
    <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:py-8">
      <PageHeader
        subtitle="O que já foi vendido — e como."
        actions={
          <>
            <button
              type="button"
              onClick={() => toast('Trocar o período recarregaria os KPIs e a tabela com a nova janela.', 'info')}
              className="inline-flex items-center gap-2 rounded-xl border border-border bg-card px-3 py-2 text-sm font-semibold text-foreground transition-colors hover:bg-surface-2 active:brightness-95"
            >
              {vm.periodoLabel}
              <ChevronDown className="h-3.5 w-3.5" />
            </button>
            <Button variant="outline" size="sm" onClick={() => navigate('/pdv')}>
              Abrir PDV
            </Button>
          </>
        }
      />

      <KpisRow
        kpis={vm.kpis}
        historicoVendidoMesCentavos={vm.historicoVendidoMesCentavos}
        apenasEstornadas={vm.filtros.apenasEstornadas}
        onToggleEstornadas={vm.onToggleEstornadas}
      />

      <VendasConsultor onVerSabados={vm.aplicarFiltroSabados} />

      <VendasTableSection
        vendas={vm.vendasFiltradas}
        canais={vm.canais}
        operadores={vm.operadores}
        filtros={vm.filtros}
        onChangeCanal={vm.onChangeCanal}
        onChangeOperador={vm.onChangeOperador}
        onChangeFormaPagamento={vm.onChangeFormaPagamento}
        onChangeBusca={vm.onChangeBusca}
        onAbrirVenda={vm.abrirDetalhe}
      />

      {vm.vendaSelecionada && <VendaDetalheModal venda={vm.vendaSelecionada} onClose={vm.fecharDetalhe} />}
    </div>
  );
}
