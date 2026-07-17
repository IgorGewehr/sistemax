import { Button } from '@/components/ui/Button';

import { duracaoTurno, horaAgora, totalSangriasCentavos } from './calc';
import { formatCentavosWhole } from './MoneyWhole';
import { StatTile } from './StatTile';
import type { SessaoCaixa } from './types';

interface SessaoDrillStatsProps {
  sessao: SessaoCaixa;
  onAbrirModalFechar: () => void;
}

/** Quem operou, quanto durou o turno e quanto saiu de sangria — com "Fechar caixa" à mão se essa
 * sessão (hoje) ainda estiver aberta. */
export function SessaoDrillStats({ sessao, onAbrirModalFechar }: SessaoDrillStatsProps) {
  const duracao =
    sessao.status === 'fechado'
      ? duracaoTurno(sessao.horaAbertura, sessao.horaFechamento)
      : `${duracaoTurno(sessao.horaAbertura, horaAgora())} até agora`;
  const operadorSub =
    sessao.status === 'fechado' ? `abriu ${sessao.horaAbertura} · fechou ${sessao.horaFechamento}` : `abriu ${sessao.horaAbertura} · ainda aberto`;

  const totalSangrias = totalSangriasCentavos(sessao);
  const ultimoDestino = sessao.sangrias.length ? sessao.sangrias[sessao.sangrias.length - 1].destino : null;
  const sangriaValor = totalSangrias > 0 ? `${formatCentavosWhole(totalSangrias)}${ultimoDestino ? ` → ${ultimoDestino}` : ''}` : 'Nenhuma';

  return (
    <div className="flex flex-col gap-2.5 px-[18px] pb-[18px] pt-3.5">
      <StatTile label="Operador" value={sessao.operador} mono={false} valueClassName="text-[19px]" sub={operadorSub} />
      <StatTile
        label="Duração do turno"
        value={duracao}
        valueClassName="text-[19px]"
        sub={sessao.status === 'fechado' ? 'turno completo' : 'em andamento'}
      />
      <StatTile label="Sangria do turno" value={sangriaValor} valueClassName="text-[19px]" sub="retirada da gaveta" />
      {sessao.status === 'aberto' && (
        <Button variant="primary" size="sm" className="self-start" onClick={onAbrirModalFechar}>
          Fechar caixa
        </Button>
      )}
    </div>
  );
}
