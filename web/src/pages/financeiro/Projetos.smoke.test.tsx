// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { Link, MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { ConfiguracaoFinanceiraDto, PainelDoProjetoDto, ProjetoDto } from '@/lib/api/financeiro';
import { ToastProvider } from '@/lib/toast';

import { Configuracoes } from './Configuracoes';
import { Projetos } from './Projetos';

const configuracoes = vi.fn();
const salvarConfiguracoes = vi.fn();
const projetos = vi.fn();
const projetoPainel = vi.fn();

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: {
    configuracoes: (...args: unknown[]) => configuracoes(...args),
    salvarConfiguracoes: (...args: unknown[]) => salvarConfiguracoes(...args),
    projetos: (...args: unknown[]) => projetos(...args),
    projetoPainel: (...args: unknown[]) => projetoPainel(...args),
  },
}));

function money(centavos: number) {
  return { centavos, moeda: 'BRL' };
}

const PROJETO: ProjetoDto = {
  id: 'p1',
  nome: 'Consultoria Fiscal',
  descricao: 'Linha de assessoria recorrente',
  status: 'ativo',
  criadoEm: '2026-01-01T00:00:00Z',
  arquivadoEm: null,
};

const PAINEL: PainelDoProjetoDto = {
  projeto: PROJETO,
  receita: { mrr: money(500000), arr: money(6000000), assinaturasAtivas: 3, ticketMedio: money(166666) },
  churn: { cancelamentos12m: 0, exposicaoAssinaturaMeses12m: 12, churnMensalPercent: 0, vidaEsperadaMeses: null },
  ltv: { ltv: null, limiteInferior: money(0), metodo: 'observado', observacao: null },
  margem: {
    competencia: '2026-07-01',
    receita: money(500000),
    custoDireto: money(100000),
    mc1: money(400000),
    mc1Percent: 80,
    amortizacaoMes: money(0),
    mc2: money(400000),
    mc2Percent: 80,
    custoTempoMes: null,
    mc3: null,
    mc3Percent: null,
  },
  capacidade: { unidadesTotais: 0, unidadesUtilizadas: 0, utilizacaoPercent: 0, custoOciosidadeMesCentavos: 0 },
  payback: { investimentoTotalCentavos: 0, fluxoCaixaAcumuladoCentavos: 0, paybackRealizadoEm: null, paybackProjetadoMeses: null, metodo: 'simples' },
  roi: { realizadoPercent: null, roiSobreInvestimentoPercent: null, runRateAnualizadoPercent: null },
  tempo: { minutosJanela: 0, custoJanelaCentavos: null, porCliente: [] },
};

/** Harness mínimo: as duas rotas reais do Financeiro que participam do fluxo opt-in, com um link
 * de navegação (a própria `Projetos.tsx` já navega para `/financeiro/configuracoes` via botão do
 * header — aqui só precisamos do caminho de volta). */
function Harness() {
  return (
    <ToastProvider>
      <Link to="/financeiro/projetos">Ir para Projetos</Link>
      <Routes>
        <Route path="/financeiro/configuracoes" element={<Configuracoes />} />
        <Route path="/financeiro/projetos" element={<Projetos />} />
      </Routes>
    </ToastProvider>
  );
}

function renderHarness(initialEntry: string) {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Harness />
    </MemoryRouter>,
  );
}

describe('Financeiro › Projetos — fluxo opt-in completo (smoke)', () => {
  let cfg: ConfiguracaoFinanceiraDto;

  beforeEach(() => {
    cfg = {
      analisePorProjetoAtiva: false,
      custoHoraPadraoCentavos: null,
      tempoEntraNoDre: false,
      imobilizadoRoiAtivo: false,
      taxaDescontoAnualBps: null,
      inicioOperacao: null,
    };

    configuracoes.mockReset().mockImplementation(() => Promise.resolve(cfg));
    salvarConfiguracoes.mockReset().mockImplementation((payload: ConfiguracaoFinanceiraDto) => {
      cfg = payload;
      return Promise.resolve(cfg);
    });
    projetos.mockReset().mockImplementation(() => Promise.resolve(cfg.analisePorProjetoAtiva ? [PROJETO] : []));
    projetoPainel.mockReset().mockImplementation(() => Promise.resolve(PAINEL));
  });

  it('toggle desligado → estado vazio elegante; toggle ligado (via Configurações) → dados reais renderizados', async () => {
    renderHarness('/financeiro/projetos');

    // 1) Toggle desligado: nunca tabela/gráfico vazios — o estado vazio "opt-in" dedicado.
    await waitFor(() => expect(screen.getByText('Análise por Projeto está desligada')).toBeInTheDocument());
    expect(screen.queryByText(PROJETO.nome)).not.toBeInTheDocument();
    expect(projetos).toHaveBeenCalled();

    // 2) Navega para Configurações pelo próprio badge do header (rota real usada pela tela) e
    // liga o toggle "Análise por Projeto" (primeiro switch da tela).
    fireEvent.click(screen.getByText(/Análise por Projeto · Desligada/));

    await waitFor(() => expect(screen.getAllByRole('switch').length).toBeGreaterThan(0));
    const [toggleProjeto] = screen.getAllByRole('switch');
    expect(toggleProjeto).toHaveAttribute('aria-checked', 'false');

    fireEvent.click(toggleProjeto);
    await waitFor(() => expect(toggleProjeto).toHaveAttribute('aria-checked', 'true'));
    expect(salvarConfiguracoes).toHaveBeenCalledWith(expect.objectContaining({ analisePorProjetoAtiva: true }));

    // 3) Volta pra Projetos (Link do harness): remonta a tela, refaz o GET com o toggle já ligado.
    fireEvent.click(screen.getByText('Ir para Projetos'));

    await waitFor(() => expect(screen.getAllByText(PROJETO.nome).length).toBeGreaterThan(0));
    expect(screen.queryByText('Análise por Projeto está desligada')).not.toBeInTheDocument();

    // Card do seletor mostra o MRR real vindo do painel (nunca um valor de exemplo hardcoded).
    // (o nome do projeto também aparece narrado no Consultor — pega especificamente o card-botão.)
    const nomeNoCard = screen.getAllByText(PROJETO.nome).find((el) => el.closest('button'));
    const card = nomeNoCard?.closest('button') as HTMLElement;
    expect(card).toBeTruthy();
    await waitFor(() => expect(card.querySelector('.num')?.textContent ?? '').toMatch(/R\$\s?5\.000/));
  });

  it('toggle ligado sem nenhum projeto cadastrado: estado vazio de "nenhum projeto", não o de opt-in', async () => {
    cfg.analisePorProjetoAtiva = true;
    projetos.mockReset().mockResolvedValue([]);

    renderHarness('/financeiro/projetos');

    await waitFor(() => expect(screen.getByText('Nenhum projeto cadastrado ainda')).toBeInTheDocument());
    expect(screen.queryByText('Análise por Projeto está desligada')).not.toBeInTheDocument();
  });
});
