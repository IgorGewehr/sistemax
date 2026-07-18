// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';

import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { FluxoCaixaBoard } from './FluxoCaixaBoard';
import type { ConsultorInsightMock, SessaoCaixa, SessaoCaixaFechada } from './types';
import type { Recurso } from './useFluxoCaixa';

const CONSULTOR_VAZIO: ConsultorInsightMock = {
  faltasMesCentavos: 0,
  sobrasMesCentavos: 0,
  diaCriticoLabel: 'sem padrão ainda',
  diaCriticoMediaCentavos: 0,
  operadorCritico: '—',
  acaoLabel: 'Ver o mês →',
};

const SESSAO_FECHADA: SessaoCaixaFechada = {
  id: 's1',
  dia: 10,
  diaSemana: 'Qui',
  operador: 'Maria',
  horaAbertura: '08:00',
  aberturaCentavos: 10000,
  vendasEspecieCentavos: 5000,
  sangrias: [],
  suprimentos: [],
  trocoCentavos: 0,
  status: 'fechado',
  horaFechamento: '18:00',
  contadoCentavos: 15000,
};

const NOOP_HANDLERS = {
  onAbrirCaixa: vi.fn(async () => {}),
  onRegistrarSangria: vi.fn(async () => {}),
  onRegistrarSuprimento: vi.fn(async () => {}),
  onFecharCaixa: vi.fn(async () => {}),
};

interface BoardReal {
  sessaoHoje: SessaoCaixa | null;
  sessoesFechadas: SessaoCaixaFechada[];
}

function renderBoard(board: Recurso<BoardReal>, todasAsSessoes: SessaoCaixa[] = []) {
  return render(
    <FluxoCaixaBoard
      board={board}
      todasAsSessoes={todasAsSessoes}
      estatisticasMes={{ totalDiferencaCentavos: 0, quantidadeFaltas: 0, quantidadeSobras: 0, diasFechados: todasAsSessoes.length }}
      sangriasMes={{ totalCentavos: 0, quantidade: 0 }}
      sangriasMaiorDestino={null}
      diaCritico={null}
      mediaDiferencaCentavos={0}
      consultorInsight={CONSULTOR_VAZIO}
      vendasEspeciePercentual={0}
      destinosSangria={['Cofre da loja']}
      enviandoAcao={false}
      {...NOOP_HANDLERS}
    />,
  );
}

describe('FluxoCaixaBoard — estados loading/erro/vazio/completo', () => {
  afterEach(cleanup);

  it('loading: mostra skeleton, nunca o board nem o vazio', () => {
    renderBoard({ dado: null, erro: null, carregando: true });

    expect(screen.queryByText('Nenhum caixa aberto ainda')).not.toBeInTheDocument();
    expect(screen.queryByText('Não deu para carregar o caixa')).not.toBeInTheDocument();
    expect(screen.queryByText('Abrir caixa')).not.toBeInTheDocument();
  });

  it('erro: mostra um estado de erro claro, não tela branca/travada', () => {
    renderBoard({ dado: null, erro: 'Serviço fora do ar', carregando: false });

    expect(screen.getByText('Não deu para carregar o caixa')).toBeInTheDocument();
    expect(screen.getByText('Serviço fora do ar')).toBeInTheDocument();
  });

  it('vazio (sessaoHoje null + sem sessões fechadas) não crasha e mostra CTA "Abrir caixa" — reprodução exata do estado do seed', () => {
    renderBoard({ dado: { sessaoHoje: null, sessoesFechadas: [] }, erro: null, carregando: false });

    expect(screen.getByText('Nenhum caixa aberto ainda')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Abrir caixa' })).toBeInTheDocument();

    // O board completo (KPIs, Super Consultor, tabela de sessões) não deve renderizar nesse estado.
    expect(screen.queryByText('Sessões')).not.toBeInTheDocument();
    expect(screen.queryByText('Prioridade da semana:')).not.toBeInTheDocument();
  });

  it('completo (histórico com sessão fechada) renderiza KPIs, Super Consultor e a tabela de sessões', () => {
    renderBoard({ dado: { sessaoHoje: null, sessoesFechadas: [SESSAO_FECHADA] }, erro: null, carregando: false }, [SESSAO_FECHADA]);

    expect(screen.queryByText('Nenhum caixa aberto ainda')).not.toBeInTheDocument();
    expect(screen.getByText('Sessões')).toBeInTheDocument();
    expect(screen.getByText('Prioridade da semana:', { exact: false })).toBeInTheDocument();
  });
});
