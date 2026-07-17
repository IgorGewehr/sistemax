/**
 * Composição da lente Contas fixas — mockup: `#painelFixas` (`initFixas`). Deriva as
 * métricas a partir do histórico bruto do mock (nunca duplica número calculado).
 */
import { useMemo, useRef, useState } from 'react';

import {
  calcularProximaGrande,
  calcularRetratoFixo,
  calcularSerieMensalFixas,
  calcularTotaisFixas,
  derivarContaFixa,
} from './calc';
import { FixasAnalisePanel } from './FixasAnalisePanel';
import { FixasConsultor } from './FixasConsultor';
import { FixasKpis } from './FixasKpis';
import { FixasTabela } from './FixasTabela';
import type { ContasFixasViewModel } from './types';

interface PainelContasFixasProps {
  data: ContasFixasViewModel;
}

export function PainelContasFixas({ data }: PainelContasFixasProps) {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const leftCardRef = useRef<HTMLDivElement>(null);

  const itensDerivados = useMemo(() => data.itens.map(derivarContaFixa), [data.itens]);
  const totais = useMemo(
    () => calcularTotaisFixas(itensDerivados, data.receitaMediaReferencia, data.diasUteisMes),
    [itensDerivados, data.receitaMediaReferencia, data.diasUteisMes],
  );
  const retrato = useMemo(() => calcularRetratoFixo(itensDerivados), [itensDerivados]);
  const serieMensal = useMemo(() => calcularSerieMensalFixas(itensDerivados), [itensDerivados]);
  const maiorPendente = useMemo(() => calcularProximaGrande(itensDerivados), [itensDerivados]);
  const luz = itensDerivados.find((i) => i.id === 'luz') ?? null;
  const deltaAnualLuz = luz ? (luz.atual - luz.media6m) * 12 : 0;

  function handleVerConta() {
    setSelectedId('luz');
    requestAnimationFrame(() => leftCardRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' }));
  }

  return (
    <div>
      <FixasKpis
        totalAtual={totais.totalAtual}
        custoPorDia={totais.custoPorDia}
        diasUteisMes={data.diasUteisMes}
        deltaAbs={totais.deltaAbs}
        deltaPct={totais.deltaPct}
        notaVariacaoMensal={data.notaVariacaoMensal}
        pesoReceitaPct={totais.pesoReceitaPct}
        receitaMediaReferencia={data.receitaMediaReferencia}
        maiorPendente={maiorPendente}
        serieMensal={serieMensal}
      />
      {luz && (
        <FixasConsultor luz={luz} deltaAnualLuz={deltaAnualLuz} notaPrazoConsultor={data.notaPrazoConsultor} onVerConta={handleVerConta} />
      )}
      <FixasAnalisePanel
        itens={itensDerivados}
        totalAtual={totais.totalAtual}
        retrato={retrato}
        selectedId={selectedId}
        onSelect={setSelectedId}
        leftCardRef={leftCardRef}
      />
      <FixasTabela itens={itensDerivados} totalAtual={totais.totalAtual} />
    </div>
  );
}
