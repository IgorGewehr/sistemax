import { ArrowLeftRight } from 'lucide-react';

import { PageHeader, SectionCard } from '@/components/shared';
import { EmptyState } from '@/components/ui/EmptyState';

/**
 * Aba "Movimentações" — o mockup mostra um razão append-only (entradas, saídas, reservas,
 * ajustes) com resumo por tipo. O Bridge não expõe essa API ainda (só produtos + saldo atual),
 * então em vez de inventar lançamentos que pareceriam reais, a aba explica exatamente o que falta
 * — mesmo padrão do `EmBreveSection` já usado em Fiscal/Integrações.
 */
export function MovimentacoesView() {
  return (
    <div>
      <PageHeader subtitle="O razão, navegável — nada aqui se edita. Corrigiu errado? Novo movimento de estorno." />
      <SectionCard title="Razão de movimentações">
        <div className="px-[18px] pb-6 pt-1">
          <EmptyState
            icon={<ArrowLeftRight className="h-5 w-5" />}
            title="Ainda não disponível"
            description="O Bridge ainda não expõe uma API de movimentações de estoque (entradas, saídas, reservas, ajustes) — hoje o módulo só mostra o saldo atual (aba Produtos) e o valor por categoria (aba Visão geral). Quando o razão existir no backend, esta aba lista cada lançamento, filtrável por produto e tipo."
            className="border-none py-10"
          />
        </div>
      </SectionCard>
    </div>
  );
}
