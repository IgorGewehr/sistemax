import { useCallback, useEffect, useState } from 'react';

import { deConciliacaoDto, deContasDto, deMovimentosDto, deSemanasDto, deTaxasPorFormaDto } from '@/lib/api/adapters/financeiro/bancario';
import { ApiError } from '@/lib/api/client';
import { financeiroApi, type SemanaMovimentoDto } from '@/lib/api/financeiro';
import { addDays, todayIso } from '@/lib/date';

import type { ConciliacaoBancaria, ConsultorBancarioInsight, ContaBancaria, KpiDeltaExemplo, MovimentoExtrato, SemanaMovimento } from './types';

export interface Recurso<T> {
  dado: T | null;
  erro: string | null;
  carregando: boolean;
}

function inicial<T>(): Recurso<T> {
  return { dado: null, erro: null, carregando: true };
}

function mensagemDeErro(e: unknown): string {
  return e instanceof ApiError ? e.message : 'Não foi possível carregar.';
}

export interface ExtratoViewModel {
  movimentos: MovimentoExtrato[];
  semanas: SemanaMovimento[];
}

const MESES_PT = [
  'Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho',
  'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro',
];

function periodoAtual(): { de: string; ate: string } {
  const hoje = todayIso();
  return { de: `${hoje.slice(0, 7)}-01`, ate: hoje };
}

function periodoAnterior(atual: { de: string }): { de: string; ate: string } {
  const fim = addDays(atual.de, -1);
  return { de: `${fim.slice(0, 7)}-01`, ate: fim };
}

function periodoLabel(deIso: string): string {
  const [ano, mes] = deIso.split('-');
  return `${MESES_PT[Number(mes) - 1]} ${ano}`;
}

function deltaPercentual(atual: number, anterior: number): number | null {
  if (anterior === 0) return null;
  return ((atual - anterior) / Math.abs(anterior)) * 100;
}

function formatDeltaLabel(pct: number | null, sufixo: string): KpiDeltaExemplo {
  if (pct === null) return { label: sufixo, direcao: 'up' };
  const sinal = pct >= 0 ? '+' : '';
  return { label: `${sinal}${pct.toFixed(1).replace('.', ',')}% ${sufixo}`, direcao: pct >= 0 ? 'up' : 'down' };
}

function somaEntradasSaidas(semanas: SemanaMovimentoDto[]): { entradas: number; saidas: number } {
  let entradas = 0;
  let saidas = 0;
  for (const s of semanas) {
    for (const d of s.dias) {
      entradas += d.entradas.centavos;
      saidas += d.saidas.centavos;
    }
  }
  return { entradas, saidas };
}

/**
 * Bancário — todo o dado REAL da tela (docs/wiring/financeiro-telas-restantes.md §3), mesmo padrão
 * `Recurso<T>` por bloco de `useVisaoGeral`: um bloco quebrado não derruba os outros. `conciliacao`
 * carrega sua própria função `recarregarConciliacao` porque confirmar/ignorar um item precisa
 * refletir de volta nos 3 baldes — mais simples e correto refazer a consulta (o SQLite local é
 * rápido) do que duplicar a lógica dos baldes no front.
 */
export function useBancario() {
  const [contas, setContas] = useState<Recurso<ContaBancaria[]>>(inicial);
  const [extrato, setExtrato] = useState<Recurso<ExtratoViewModel>>(inicial);
  const [conciliacao, setConciliacao] = useState<Recurso<ConciliacaoBancaria>>(inicial);
  const [consultor, setConsultor] = useState<Recurso<ConsultorBancarioInsight>>(inicial);
  const [kpiEntrouDelta, setKpiEntrouDelta] = useState<KpiDeltaExemplo>({ label: 'vs período anterior', direcao: 'up' });
  const [kpiSaiuDelta, setKpiSaiuDelta] = useState<KpiDeltaExemplo>({ label: 'vs período anterior', direcao: 'up' });
  const [kpiSaldoDelta, setKpiSaldoDelta] = useState<KpiDeltaExemplo>({ label: 'no período', direcao: 'up' });

  const atual = periodoAtual();
  const anterior = periodoAnterior(atual);

  const recarregarConciliacao = useCallback(() => {
    setConciliacao(inicial());
    financeiroApi
      .conciliacao(atual.de, atual.ate)
      .then((dto) => setConciliacao({ dado: deConciliacaoDto(dto), erro: null, carregando: false }))
      .catch((e) => setConciliacao({ dado: null, erro: mensagemDeErro(e), carregando: false }));
     
  }, [atual.de, atual.ate]);

  const carregar = useCallback(() => {
    setContas(inicial());
    setExtrato(inicial());
    setConsultor(inicial());

    financeiroApi
      .contasBancarias()
      .then((dtos) => setContas({ dado: deContasDto(dtos), erro: null, carregando: false }))
      .catch((e) => setContas({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    Promise.all([
      financeiroApi.movimentos(atual.de, atual.ate),
      financeiroApi.movimentosSemana(atual.de, atual.ate),
    ])
      .then(([movimentosDto, semanasDto]) => {
        setExtrato({
          dado: { movimentos: deMovimentosDto(movimentosDto, semanasDto), semanas: deSemanasDto(semanasDto) },
          erro: null,
          carregando: false,
        });

        const totaisAtual = somaEntradasSaidas(semanasDto);
        financeiroApi
          .movimentosSemana(anterior.de, anterior.ate)
          .then((semanasAnterioresDto) => {
            const totaisAnterior = somaEntradasSaidas(semanasAnterioresDto);
            setKpiEntrouDelta(formatDeltaLabel(deltaPercentual(totaisAtual.entradas, totaisAnterior.entradas), 'vs período anterior'));
            setKpiSaiuDelta(formatDeltaLabel(deltaPercentual(totaisAtual.saidas, totaisAnterior.saidas), 'vs período anterior'));
          })
          .catch(() => {
            // Sem período anterior pra comparar não é erro do bloco principal — os KPIs de delta
            // ficam com o rótulo neutro default (formatDeltaLabel já cobre `anterior === 0`).
          });
      })
      .catch((e) => setExtrato({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .taxasPorForma(atual.de, atual.ate)
      .then((dto) => setConsultor({ dado: deTaxasPorFormaDto(dto), erro: null, carregando: false }))
      .catch((e) => setConsultor({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    recarregarConciliacao();
     
  }, [atual.de, atual.ate, anterior.de, anterior.ate, recarregarConciliacao]);

  useEffect(() => {
    carregar();
  }, [carregar]);

  useEffect(() => {
    if (!contas.dado || !extrato.dado) return;
    const saldoAtual = contas.dado.reduce((acc, c) => acc + c.saldoCentavos, 0);
    const totaisAtual = extrato.dado.semanas.reduce(
      (acc, s) => ({
        entradas: acc.entradas + s.entrouPorDiaCentavos.reduce((a, b) => a + b, 0),
        saidas: acc.saidas + s.saiuPorDiaCentavos.reduce((a, b) => a + b, 0),
      }),
      { entradas: 0, saidas: 0 },
    );
    const deltaCentavos = totaisAtual.entradas - totaisAtual.saidas;
    const saldoInicioPeriodo = saldoAtual - deltaCentavos;
    setKpiSaldoDelta(formatDeltaLabel(deltaPercentual(saldoAtual, saldoInicioPeriodo), 'no período'));
  }, [contas.dado, extrato.dado]);

  async function confirmarItem(movimentoFinanceiroId: string, extratoBancarioItemId: string) {
    await financeiroApi.confirmarConciliacao(movimentoFinanceiroId, extratoBancarioItemId);
    recarregarConciliacao();
  }

  async function ignorarItem(movimentoFinanceiroId: string, extratoBancarioItemId: string) {
    await financeiroApi.ignorarConciliacao(movimentoFinanceiroId, extratoBancarioItemId);
    recarregarConciliacao();
  }

  return {
    periodoLabel: periodoLabel(atual.de),
    contas,
    extrato,
    conciliacao,
    consultor,
    kpiSaldoDelta,
    kpiEntrouDelta,
    kpiSaiuDelta,
    confirmarItem,
    ignorarItem,
    recarregar: carregar,
  };
}

export type BancarioVm = ReturnType<typeof useBancario>;
