import { KpiCard } from '@/components/shared';
import { Button } from '@/components/ui/Button';
import { cn } from '@/lib/utils';

import type { EstatisticasMes, SangriasMes } from './calc';
import { descreverCaixaHojeFoot, valorNaGaveta } from './calc';
import { MoneyWhole } from './MoneyWhole';
import type { SessaoCaixa } from './types';

interface KpisSectionProps {
  /** `null` = nenhuma sessão aberta hoje ainda (nunca aberta) — mostra o convite pra abrir. */
  sessaoHoje: SessaoCaixa | null;
  estatisticasMes: EstatisticasMes;
  sangriasMes: SangriasMes;
  sangriasMaiorDestino: string | null;
  onAbrirModalFechar: () => void;
  onAbrirModalAbrirCaixa: () => void;
}

/** Badge de estado ao vivo do caixa — distinto do `StatusChip` de "aberto" (que sinaliza pendência
 * em amarelo): aqui verde-pulsante significa "em operação agora", não "precisa de atenção". */
function EstadoCaixaBadge({ aberto }: { aberto: boolean }) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-bold',
        aberto ? 'bg-pos-soft text-pos' : 'bg-surface-2 text-faint',
      )}
    >
      <span className={cn('h-1.5 w-1.5 rounded-full bg-current', aberto && 'animate-pulse')} />
      {aberto ? 'Aberto' : 'Fechado'}
    </span>
  );
}

/** As 4 KPIs de resumo — "leigo enxerga como consultor sênior" antes de entrar no detalhe. */
export function KpisSection({ sessaoHoje, estatisticasMes, sangriasMes, sangriasMaiorDestino, onAbrirModalFechar, onAbrirModalAbrirCaixa }: KpisSectionProps) {
  const aberto = sessaoHoje?.status === 'aberto';
  const gaveta = sessaoHoje ? valorNaGaveta(sessaoHoje) : null;

  return (
    <section className="mb-4 grid grid-cols-2 gap-3.5 min-[860px]:grid-cols-4">
      <KpiCard
        hero
        label="Na gaveta agora"
        value={
          gaveta ? (
            <>
              <MoneyWhole centavos={gaveta.centavos} /> <small className="text-sm font-semibold text-muted-foreground">{gaveta.sufixo}</small>
            </>
          ) : (
            <span className="text-lg font-semibold text-muted-foreground">—</span>
          )
        }
      >
        {!sessaoHoje ? (
          <Button variant="primary" size="sm" onClick={onAbrirModalAbrirCaixa}>
            Abrir caixa
          </Button>
        ) : aberto ? (
          <Button variant="primary" size="sm" onClick={onAbrirModalFechar}>
            Fechar caixa
          </Button>
        ) : (
          <span className="text-[12.5px] text-muted-foreground">Sessão encerrada</span>
        )}
      </KpiCard>

      <KpiCard
        label="Caixa de hoje"
        value={<EstadoCaixaBadge aberto={!!aberto} />}
        foot={sessaoHoje ? descreverCaixaHojeFoot(sessaoHoje) : 'nenhuma sessão aberta ainda'}
      />

      <KpiCard
        label="Diferença do mês"
        value={<MoneyWhole centavos={estatisticasMes.totalDiferencaCentavos} tone="auto" />}
        foot={`${estatisticasMes.quantidadeFaltas} falta${estatisticasMes.quantidadeFaltas !== 1 ? 's' : ''} · ${estatisticasMes.quantidadeSobras} sobra${estatisticasMes.quantidadeSobras !== 1 ? 's' : ''}`}
      />

      <KpiCard
        label="Sangrias do mês"
        value={
          <>
            <MoneyWhole centavos={sangriasMes.totalCentavos} /> <span className="num">({sangriasMes.quantidade})</span>
          </>
        }
        foot={sangriasMaiorDestino ? `maior parte → ${sangriasMaiorDestino}` : 'nenhuma sangria no mês'}
      />
    </section>
  );
}
