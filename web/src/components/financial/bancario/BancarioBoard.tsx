import { Landmark } from 'lucide-react';
import { useState } from 'react';

import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';

import { AccountFilterBar, TODAS_AS_CONTAS, type AccountChipId } from './AccountFilterBar';
import { BancarioKpiRow } from './BancarioKpiRow';
import { ConciliacaoCard } from './ConciliacaoCard';
import { ExtratoTable } from './ExtratoTable';
import { SuperConsultorBancario } from './SuperConsultorBancario';
import type { ConciliacaoBancaria, ConsultorBancarioInsight, ContaBancaria, KpiDeltaExemplo } from './types';
import type { ExtratoViewModel, Recurso } from './useBancario';
import { WeeksAnalysisCard } from './WeeksAnalysisCard';

interface BancarioBoardProps {
  contas: Recurso<ContaBancaria[]>;
  extrato: Recurso<ExtratoViewModel>;
  conciliacao: Recurso<ConciliacaoBancaria>;
  consultor: Recurso<ConsultorBancarioInsight>;
  kpiSaldoDelta: KpiDeltaExemplo;
  kpiEntrouDelta: KpiDeltaExemplo;
  kpiSaiuDelta: KpiDeltaExemplo;
  onConfirmar: (movimentoFinanceiroId: string, extratoBancarioItemId: string) => void;
  onIgnorar: (movimentoFinanceiroId: string, extratoBancarioItemId: string) => void;
}

/**
 * Corpo interativo do Bancário — orquestra o filtro de conta (afeta o Extrato) e traduz a ação de
 * 1 clique do `ConciliacaoCard` ("sim"/"não" num item de um balde) no par
 * (movimentoFinanceiroId, extratoBancarioItemId) que a API real espera: em "sobrou no banco" o
 * item É o extrato e `idSugerido` é o movimento candidato; em "sobrou no sistema" é o inverso.
 */
export function BancarioBoard({
  contas,
  extrato,
  conciliacao,
  consultor,
  kpiSaldoDelta,
  kpiEntrouDelta,
  kpiSaiuDelta,
  onConfirmar,
  onIgnorar,
}: BancarioBoardProps) {
  const [contaSelecionada, setContaSelecionada] = useState<AccountChipId>(TODAS_AS_CONTAS);

  function resolverItem(kind: 'banco' | 'sistema', itemId: string, action: 'sim' | 'nao') {
    const balde = kind === 'banco' ? conciliacao.dado?.sobrouNoBanco : conciliacao.dado?.sobrouNoSistema;
    const item = balde?.find((i) => i.id === itemId);
    if (!item || item.idSugerido === null || item.idSugerido === undefined) return;

    const [movimentoFinanceiroId, extratoBancarioItemId] = kind === 'banco' ? [item.idSugerido, item.id] : [item.id, item.idSugerido];

    if (action === 'sim') onConfirmar(movimentoFinanceiroId, extratoBancarioItemId);
    else onIgnorar(movimentoFinanceiroId, extratoBancarioItemId);
  }

  const conciliarCount = conciliacao.dado ? conciliacao.dado.sobrouNoBanco.length + conciliacao.dado.sobrouNoSistema.length : 0;
  const conciliarTotalCentavos = conciliacao.dado
    ? [...conciliacao.dado.sobrouNoBanco, ...conciliacao.dado.sobrouNoSistema].reduce((acc, i) => acc + Math.abs(i.valorCentavos), 0)
    : 0;

  return (
    <>
      {contas.dado && <AccountFilterBar contas={contas.dado} selected={contaSelecionada} onSelect={setContaSelecionada} />}

      {contas.carregando || extrato.carregando ? (
        <Surface padding="lg" className="mb-4 min-h-[140px]">
          <Skeleton className="h-20 w-full" />
        </Surface>
      ) : contas.erro || extrato.erro ? (
        <Surface padding="lg" className="mb-4">
          <EmptyState icon={<Landmark className="h-5 w-5" />} title="Não deu para carregar" description={contas.erro ?? extrato.erro ?? ''} className="border-none py-6" />
        </Surface>
      ) : (
        contas.dado &&
        extrato.dado && (
          <BancarioKpiRow
            contas={contas.dado}
            semanas={extrato.dado.semanas}
            kpiSaldoDelta={kpiSaldoDelta}
            kpiEntrouDelta={kpiEntrouDelta}
            kpiEntrouFoot={`${extrato.dado.movimentos.filter((m) => m.valorCentavos > 0).length} movimentos`}
            kpiSaiuDelta={kpiSaiuDelta}
            kpiSaiuFoot={`${extrato.dado.movimentos.filter((m) => m.valorCentavos < 0).length} movimentos`}
            conciliarCount={conciliarCount}
            conciliarTotalCentavos={conciliarTotalCentavos}
          />
        )
      )}

      {consultor.dado && <SuperConsultorBancario insight={consultor.dado} />}

      <section className="mb-4 grid grid-cols-1 gap-4 lg:grid-cols-[1.15fr_1fr]">
        {extrato.carregando ? (
          <Surface padding="lg" className="min-h-[300px]">
            <Skeleton className="h-full w-full" />
          </Surface>
        ) : extrato.erro ? (
          <Surface padding="lg" className="min-h-[300px]">
            <EmptyState icon={<Landmark className="h-5 w-5" />} title="Não deu para carregar" description={extrato.erro} className="border-none py-6" />
          </Surface>
        ) : (
          extrato.dado && <WeeksAnalysisCard semanas={extrato.dado.semanas} movimentos={extrato.dado.movimentos} />
        )}

        {conciliacao.carregando ? (
          <Surface padding="lg" className="min-h-[300px]">
            <Skeleton className="h-full w-full" />
          </Surface>
        ) : conciliacao.erro ? (
          <Surface padding="lg" className="min-h-[300px]">
            <EmptyState icon={<Landmark className="h-5 w-5" />} title="Não deu para carregar" description={conciliacao.erro} className="border-none py-6" />
          </Surface>
        ) : (
          conciliacao.dado && (
            <ConciliacaoCard
              bateuCertinhoTotal={conciliacao.dado.bateuCertinhoTotal}
              bateuCertinhoAmostra={conciliacao.dado.bateuCertinhoAmostra}
              sobrouNoBanco={conciliacao.dado.sobrouNoBanco}
              sobrouNoSistema={conciliacao.dado.sobrouNoSistema}
              onResolve={resolverItem}
            />
          )
        )}
      </section>

      {extrato.dado && contas.dado && (
        <ExtratoTable
          movimentos={extrato.dado.movimentos}
          contas={contas.dado}
          selectedAccountId={contaSelecionada}
          hint={`${extrato.dado.movimentos.length} movimentos no período`}
        />
      )}
    </>
  );
}
