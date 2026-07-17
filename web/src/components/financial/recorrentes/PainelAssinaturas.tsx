/**
 * Composição do resumo da lente Assinaturas — mockup: `#painelAssinaturas`. Deriva
 * MRR/churn/novos a partir dos serviços brutos do mock (nunca duplica número calculado).
 */
import { useMemo, useRef, useState } from 'react';

import { AssinAnalisePanel } from './AssinAnalisePanel';
import { AssinConsultor } from './AssinConsultor';
import { AssinKpis } from './AssinKpis';
import { AssinNotaReferencia } from './AssinNotaReferencia';
import {
  calcularArrEstimado,
  calcularChurnClientesMesTotal,
  calcularChurnMesTotal,
  calcularChurnPctBase,
  calcularDeltaPct,
  calcularMrrTotal,
  calcularNovosClientesMesTotal,
  calcularNovosMaisExpansaoMes,
  calcularPctMrr,
  calcularTicketMedio,
} from './calc';
import type { AssinaturasResumoViewModel } from './types';

interface PainelAssinaturasProps {
  data: AssinaturasResumoViewModel;
}

export function PainelAssinaturas({ data }: PainelAssinaturasProps) {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const leftCardRef = useRef<HTMLDivElement>(null);

  const mrrTotal = useMemo(() => calcularMrrTotal(data.servicos), [data.servicos]);
  const churnMesTotal = useMemo(() => calcularChurnMesTotal(data.servicos), [data.servicos]);
  const churnClientesMesTotal = useMemo(() => calcularChurnClientesMesTotal(data.servicos), [data.servicos]);
  const novosMaisExpansaoMes = useMemo(() => calcularNovosMaisExpansaoMes(data.servicos), [data.servicos]);
  const novosClientesMesTotal = useMemo(() => calcularNovosClientesMesTotal(data.servicos), [data.servicos]);
  const churnPctBase = calcularChurnPctBase(churnMesTotal, mrrTotal);
  const arrEstimado = calcularArrEstimado(mrrTotal);
  const ticketMedio = calcularTicketMedio(mrrTotal, data.assinaturasAtivasCount);
  const mrrDeltaAbs = mrrTotal - data.mrrMesAnterior;
  const mrrDeltaPct = calcularDeltaPct(mrrTotal, data.mrrMesAnterior);

  const concentracaoServico = data.servicos.find((s) => s.id === data.concentracaoServicoId) ?? null;
  const concentracaoPct = concentracaoServico ? calcularPctMrr(concentracaoServico, mrrTotal) : 0;

  function handleVerConcentracao() {
    setSelectedId(data.concentracaoServicoId);
    requestAnimationFrame(() => leftCardRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' }));
  }

  return (
    <div>
      <AssinKpis
        mrrAtual={mrrTotal}
        mrrDeltaAbs={mrrDeltaAbs}
        mrrDeltaPct={mrrDeltaPct}
        sparklineMrr6m={data.sparklineMrr6m}
        churnMesTotal={churnMesTotal}
        churnClientesMesTotal={churnClientesMesTotal}
        churnPctBase={churnPctBase}
        churnClienteNomes={data.churnClienteNomes}
        novosMaisExpansaoMes={novosMaisExpansaoMes}
        novosClientesMesTotal={novosClientesMesTotal}
        novoClienteNomes={data.novoClienteNomes}
        arrEstimado={arrEstimado}
        assinaturasAtivasCount={data.assinaturasAtivasCount}
        ticketMedio={ticketMedio}
      />
      <AssinConsultor
        concentracaoClienteNome={data.concentracaoClienteNome}
        concentracaoPct={concentracaoPct}
        churnMesTotal={churnMesTotal}
        churnClientesMesTotal={churnClientesMesTotal}
        onVerConcentracao={handleVerConcentracao}
      />
      <AssinAnalisePanel
        servicos={data.servicos}
        mrrTotal={mrrTotal}
        carteira={data.carteira}
        selectedId={selectedId}
        onSelect={setSelectedId}
        leftCardRef={leftCardRef}
      />
      <AssinNotaReferencia />
    </div>
  );
}
