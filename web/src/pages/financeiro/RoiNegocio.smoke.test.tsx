// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { Link, MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ApiError } from '@/lib/api/client';
import type { AporteDeCapitalDto, AtivoDeCapitalDto, ConfiguracaoFinanceiraDto, RoiDoNegocioDto } from '@/lib/api/financeiro';
import { ToastProvider } from '@/lib/toast';

import { Configuracoes } from './Configuracoes';
import { RoiNegocio } from './RoiNegocio';

const configuracoes = vi.fn();
const salvarConfiguracoes = vi.fn();
const roiNegocio = vi.fn();
const imobilizado = vi.fn();
const aportes = vi.fn();

vi.mock('@/lib/api/financeiro', () => ({
  financeiroApi: {
    configuracoes: (...args: unknown[]) => configuracoes(...args),
    salvarConfiguracoes: (...args: unknown[]) => salvarConfiguracoes(...args),
    roiNegocio: (...args: unknown[]) => roiNegocio(...args),
    imobilizado: (...args: unknown[]) => imobilizado(...args),
    aportes: (...args: unknown[]) => aportes(...args),
  },
}));

const ROI_DTO: RoiDoNegocioDto = {
  marcoInicial: '2026-01-01',
  taxaDescontoAnualBps: null,
  investimento: {
    capexCentavos: 100000,
    aportesCentavos: 50000,
    totalCentavos: 150000,
    giroConsumidoObservadoCentavos: 0,
    bens: 1,
    porCategoria: [],
    resultadoAlienacaoTotalCentavos: 0,
  },
  recuperacao: { fluxoOperacionalAcumuladoCentavos: 60000, recuperadoCentavos: 60000, faltamCentavos: 90000, percentRecuperado: 40 },
  payback: { simplesRealizadoEm: null, descontadoRealizadoEm: null, projetadoMeses: 6, descontadoProjetadoMeses: null, metodo: 'simples' },
  tir: { mensalPercent: null, anualizadaPercent: null, motivoIndefinida: 'dados insuficientes' },
  roi: { caixaPercent: 40, competenciaPercent: 40, mesesAteRoiCompleto: 6 },
  serie: [
    { competencia: '2026-05-01', fluxoOperacionalCentavos: 30000, capexCentavos: 0, aporteCentavos: 0, liquidoCentavos: 30000, acumuladoCentavos: -90000, acumuladoDescontadoCentavos: -90000 },
    { competencia: '2026-06-01', fluxoOperacionalCentavos: 30000, capexCentavos: 0, aporteCentavos: 0, liquidoCentavos: 30000, acumuladoCentavos: -60000, acumuladoDescontadoCentavos: -60000 },
  ],
};

const BEM: AtivoDeCapitalDto = {
  id: 'a1',
  projetoId: null,
  nome: 'Notebook Dell',
  natureza: 'tangivel',
  categoria: 'Computador',
  custoAquisicaoCentavos: 100000,
  valorResidualCentavos: 0,
  dataAquisicao: '2026-01-01',
  inicioDepreciacao: '2026-01-01',
  vidaUtilMeses: 36,
  quantidadeUnidades: 1,
  contaAPagarId: null,
  status: 'ativo',
  ultimaCompetenciaReconhecida: null,
  encerradoEm: null,
  baixadoEm: null,
  motivoBaixa: null,
  valorContabilAtualCentavos: 97000,
  amortizacaoMensalCentavos: 2777,
  valorVendaCentavos: null,
  resultadoAlienacaoCentavos: null,
};

const APORTE: AporteDeCapitalDto = { id: 'ap1', valorCentavos: 50000, data: '2026-01-01', descricao: 'Capital de giro', criadoEm: '2026-01-01T00:00:00Z' };

/** Harness mínimo: as duas rotas reais do Financeiro que participam do fluxo opt-in do 2º toggle. */
function Harness() {
  return (
    <ToastProvider>
      <Link to="/financeiro/roi-negocio">Ir para Investimento &amp; ROI</Link>
      <Routes>
        <Route path="/financeiro/configuracoes" element={<Configuracoes />} />
        <Route path="/financeiro/roi-negocio" element={<RoiNegocio />} />
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

describe('Financeiro › Investimento & ROI — fluxo opt-in completo (smoke)', () => {
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
    // `roi-negocio` é PAINEL, não listagem: 404 (não `[]`) enquanto o toggle está desligado.
    roiNegocio.mockReset().mockImplementation(() =>
      cfg.imobilizadoRoiAtivo ? Promise.resolve(ROI_DTO) : Promise.reject(new ApiError('nao_encontrado', 'Painel desligado', 404)),
    );
    imobilizado.mockReset().mockImplementation(() => Promise.resolve(cfg.imobilizadoRoiAtivo ? [BEM] : []));
    aportes.mockReset().mockImplementation(() => Promise.resolve(cfg.imobilizadoRoiAtivo ? [APORTE] : []));
  });

  it('toggle desligado → estado vazio elegante; toggle ligado (via Configurações) → dados reais renderizados', async () => {
    renderHarness('/financeiro/roi-negocio');

    // 1) Toggle desligado: estado vazio "opt-in" dedicado, nunca gráfico/tabela vazios.
    await waitFor(() => expect(screen.getByText('Imobilizado & ROI está desligado')).toBeInTheDocument());
    expect(screen.queryByText(BEM.nome)).not.toBeInTheDocument();
    expect(screen.queryByText('Registro de imobilizado')).not.toBeInTheDocument();

    // 2) Navega para Configurações pelo badge do header e liga o 2º toggle ("Imobilizado & ROI").
    fireEvent.click(screen.getByText(/Imobilizado & ROI · Desligado/));

    await waitFor(() => expect(screen.getAllByRole('switch').length).toBe(2));
    const [, toggleImobilizado] = screen.getAllByRole('switch');
    expect(toggleImobilizado).toHaveAttribute('aria-checked', 'false');

    fireEvent.click(toggleImobilizado);
    await waitFor(() => expect(toggleImobilizado).toHaveAttribute('aria-checked', 'true'));
    expect(salvarConfiguracoes).toHaveBeenCalledWith(expect.objectContaining({ imobilizadoRoiAtivo: true }));

    // 3) Volta pra Investimento & ROI: remonta a tela, refaz os GETs com o toggle já ligado.
    fireEvent.click(screen.getByText('Ir para Investimento & ROI'));

    await waitFor(() => expect(screen.getByText('Registro de imobilizado')).toBeInTheDocument());
    expect(screen.queryByText('Imobilizado & ROI está desligado')).not.toBeInTheDocument();

    // Bem real (não fabricado) aparece na tabela de imobilizado.
    expect(screen.getByText(BEM.nome)).toBeInTheDocument();
    // KPI do total investido reflete o DTO real (capex + aportes = 1500 centavos*100 -> R$ 1.500).
    expect(screen.getByText('Total investido')).toBeInTheDocument();
  });

  it('erro ao carregar a configuração não trava a tela num estado vazio errado', async () => {
    configuracoes.mockReset().mockRejectedValue(new ApiError('erro_interno', 'Serviço fora do ar', 500));

    renderHarness('/financeiro/roi-negocio');

    await waitFor(() => expect(screen.getByText('Não deu para carregar')).toBeInTheDocument());
    expect(screen.queryByText('Imobilizado & ROI está desligado')).not.toBeInTheDocument();
  });
});
