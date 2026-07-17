import { Info, Plus, Wallet } from 'lucide-react';
import { useCallback, useRef, useState } from 'react';

import { Button } from '@/components/ui/Button';
import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';
import type { Centavos } from '@/lib/money';

import { AnaliseInterativa } from './AnaliseInterativa';
import type { DiaCritico, EstatisticasMes, SangriasMes } from './calc';
import { ConsultorSection } from './ConsultorSection';
import { KpisSection } from './KpisSection';
import { ModalAbrirCaixa } from './ModalAbrirCaixa';
import { ModalFecharCaixa } from './ModalFecharCaixa';
import { ModalNovaSangria } from './ModalNovaSangria';
import { ModalNovoSuprimento } from './ModalNovoSuprimento';
import { SessaoHojeFormula } from './SessaoHojeFormula';
import { SessoesTable } from './SessoesTable';
import type { ConsultorInsightMock, DiaSemanaAbrev, SessaoCaixa } from './types';
import type { Recurso } from './useFluxoCaixa';

interface FluxoCaixaBoardProps {
  board: Recurso<{ sessaoHoje: SessaoCaixa | null; sessoesFechadas: SessaoCaixa[] }>;
  todasAsSessoes: SessaoCaixa[];
  estatisticasMes: EstatisticasMes;
  sangriasMes: SangriasMes;
  sangriasMaiorDestino: string | null;
  diaCritico: DiaCritico | null;
  mediaDiferencaCentavos: number;
  consultorInsight: ConsultorInsightMock;
  vendasEspeciePercentual: number;
  destinosSangria: string[];
  enviandoAcao: boolean;
  onAbrirCaixa: (aberturaCentavos: Centavos, operadorNome: string) => Promise<void>;
  onRegistrarSangria: (sessaoId: string, valorCentavos: Centavos, destino: string) => Promise<void>;
  onRegistrarSuprimento: (sessaoId: string, valorCentavos: Centavos, origem: string) => Promise<void>;
  onFecharCaixa: (sessaoId: string, contadoCentavos: Centavos) => Promise<void>;
}

const PULSE_DURATION_MS = 1900;

/**
 * Corpo interativo do Fluxo de Caixa — dado REAL (`useFluxoCaixa`, `SessaoCaixa`/`caixa/*`, ver
 * docs/wiring/financeiro-telas-restantes.md §4). Estados loading/erro/vazio no mesmo padrão de
 * `BancarioBoard`: um bloco quebrado (aqui, a leitura combinada de hoje+histórico) não impede o
 * resto da página. `sessaoHoje === null` (nenhuma sessão aberta ainda hoje) é um estado de
 * primeira classe — mostra o convite "Abrir caixa" no lugar do formulário/KPIs de hoje.
 */
export function FluxoCaixaBoard({
  board,
  todasAsSessoes,
  estatisticasMes,
  sangriasMes,
  sangriasMaiorDestino,
  diaCritico,
  mediaDiferencaCentavos,
  consultorInsight,
  vendasEspeciePercentual,
  destinosSangria,
  enviandoAcao,
  onAbrirCaixa,
  onRegistrarSangria,
  onRegistrarSuprimento,
  onFecharCaixa,
}: FluxoCaixaBoardProps) {
  const [selectedDay, setSelectedDay] = useState<number | null>(null);
  const [modalAbrirAberto, setModalAbrirAberto] = useState(false);
  const [modalFecharAberto, setModalFecharAberto] = useState(false);
  const [modalSangriaAberto, setModalSangriaAberto] = useState(false);
  const [modalSuprimentoAberto, setModalSuprimentoAberto] = useState(false);
  const [pulsingWeekday, setPulsingWeekday] = useState<DiaSemanaAbrev | null>(null);
  const pulseTimeoutRef = useRef<number | undefined>(undefined);
  const gridRef = useRef<HTMLDivElement>(null);

  const sessaoHoje = board.dado?.sessaoHoje ?? null;

  const scrollToGrid = useCallback(() => {
    gridRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }, []);

  const handleSelectDay = useCallback((dia: number) => setSelectedDay(dia), []);
  const handleSelectDayAndScroll = useCallback(
    (dia: number) => {
      setSelectedDay(dia);
      scrollToGrid();
    },
    [scrollToGrid],
  );
  const handleVoltarOverview = useCallback(() => setSelectedDay(null), []);

  const handleVerDiaCritico = useCallback(() => {
    if (!diaCritico) return;
    setSelectedDay(null);
    scrollToGrid();
    window.clearTimeout(pulseTimeoutRef.current);
    setPulsingWeekday(null);
    // Um pequeno atraso garante que o "voltar pro overview" já renderizou antes do pulso começar.
    window.setTimeout(() => {
      setPulsingWeekday(diaCritico.diaSemana);
      pulseTimeoutRef.current = window.setTimeout(() => setPulsingWeekday(null), PULSE_DURATION_MS);
    }, 260);
  }, [diaCritico, scrollToGrid]);

  async function handleConfirmarAbertura(aberturaCentavos: Centavos, operadorNome: string) {
    await onAbrirCaixa(aberturaCentavos, operadorNome);
    setModalAbrirAberto(false);
  }

  async function handleConfirmarFechamento(contadoCentavos: Centavos) {
    if (!sessaoHoje) return;
    await onFecharCaixa(sessaoHoje.id, contadoCentavos);
    setModalFecharAberto(false);
  }

  async function handleConfirmarSangria(valorCentavos: Centavos, destino: string) {
    if (!sessaoHoje) return;
    await onRegistrarSangria(sessaoHoje.id, valorCentavos, destino);
    setModalSangriaAberto(false);
  }

  async function handleConfirmarSuprimento(valorCentavos: Centavos, origem: string) {
    if (!sessaoHoje) return;
    await onRegistrarSuprimento(sessaoHoje.id, valorCentavos, origem);
    setModalSuprimentoAberto(false);
  }

  if (board.carregando) {
    return (
      <Surface padding="lg" className="mb-4 min-h-[280px]">
        <Skeleton className="h-56 w-full" />
      </Surface>
    );
  }

  if (board.erro || !board.dado) {
    return (
      <Surface padding="lg" className="mb-4">
        <EmptyState icon={<Wallet className="h-5 w-5" />} title="Não deu para carregar o caixa" description={board.erro ?? ''} className="border-none py-6" />
      </Surface>
    );
  }

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2.5">
        <span className="inline-flex items-center gap-1.5 text-xs text-muted-foreground">
          <Info className="h-3.5 w-3.5 text-primary-600" />
          Visível porque este negócio opera caixa em espécie
        </span>
        {sessaoHoje?.status === 'aberto' && (
          <div className="flex flex-wrap gap-2">
            <Button
              variant="outline"
              size="sm"
              icon={<Plus className="h-[15px] w-[15px]" strokeWidth={2.4} />}
              onClick={() => setModalSuprimentoAberto(true)}
            >
              Novo suprimento
            </Button>
            <Button variant="primary" size="sm" icon={<Plus className="h-[15px] w-[15px]" strokeWidth={2.4} />} onClick={() => setModalSangriaAberto(true)}>
              Nova sangria
            </Button>
          </div>
        )}
      </div>

      <KpisSection
        sessaoHoje={sessaoHoje}
        estatisticasMes={estatisticasMes}
        sangriasMes={sangriasMes}
        sangriasMaiorDestino={sangriasMaiorDestino}
        onAbrirModalFechar={() => setModalFecharAberto(true)}
        onAbrirModalAbrirCaixa={() => setModalAbrirAberto(true)}
      />

      {sessaoHoje && <SessaoHojeFormula sessaoHoje={sessaoHoje} />}

      <ConsultorSection insight={consultorInsight} onVerQuintas={handleVerDiaCritico} />

      <AnaliseInterativa
        todasAsSessoes={todasAsSessoes}
        diaCritico={diaCritico}
        mediaDiferencaCentavos={mediaDiferencaCentavos}
        vendasEspeciePercentual={vendasEspeciePercentual}
        selectedDay={selectedDay}
        pulsingWeekday={pulsingWeekday}
        onSelectDay={handleSelectDay}
        onVoltarOverview={handleVoltarOverview}
        onAbrirModalFechar={() => setModalFecharAberto(true)}
        containerRef={gridRef}
      />

      <SessoesTable todasAsSessoes={todasAsSessoes} onSelectDay={handleSelectDayAndScroll} />

      <ModalAbrirCaixa open={modalAbrirAberto} onClose={() => setModalAbrirAberto(false)} onConfirmar={handleConfirmarAbertura} enviando={enviandoAcao} />
      {sessaoHoje && (
        <>
          <ModalFecharCaixa
            open={modalFecharAberto}
            sessaoHoje={sessaoHoje}
            onClose={() => setModalFecharAberto(false)}
            onConfirmar={handleConfirmarFechamento}
          />
          <ModalNovaSangria
            open={modalSangriaAberto}
            destinos={destinosSangria}
            onClose={() => setModalSangriaAberto(false)}
            onConfirmar={handleConfirmarSangria}
          />
          <ModalNovoSuprimento
            open={modalSuprimentoAberto}
            origens={destinosSangria}
            onClose={() => setModalSuprimentoAberto(false)}
            onConfirmar={handleConfirmarSuprimento}
          />
        </>
      )}
    </>
  );
}
