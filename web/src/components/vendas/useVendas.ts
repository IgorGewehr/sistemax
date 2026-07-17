import { useMemo, useState } from 'react';

import { VENDAS_MOCK } from '@/mocks/vendas';

import { filtrarVendasTabela } from './calc';
import type { FiltrosVendas, VendaRow } from './types';

const FILTROS_INICIAIS: FiltrosVendas = {
  canal: 'todos',
  operador: 'todos',
  formaPagamento: 'todas',
  apenasEstornadas: false,
  busca: '',
};

/**
 * Todo o estado/lógica de "Vendas" vive aqui — a página (`Vendas.tsx`) fica fina, só compondo
 * seções a partir do que este hook devolve. Filtro derivado vem de `calc.ts`, puro e testável.
 */
export function useVendas() {
  const mock = VENDAS_MOCK;
  const [filtros, setFiltros] = useState<FiltrosVendas>(FILTROS_INICIAIS);
  const [vendaSelecionadaId, setVendaSelecionadaId] = useState<string | null>(null);

  const vendasFiltradas = useMemo(() => filtrarVendasTabela(mock.vendas, filtros), [mock.vendas, filtros]);

  const vendaSelecionada: VendaRow | null = useMemo(
    () => mock.vendas.find((v) => v.id === vendaSelecionadaId) ?? null,
    [mock.vendas, vendaSelecionadaId],
  );

  function onToggleEstornadas() {
    setFiltros((prev) => ({ ...prev, apenasEstornadas: !prev.apenasEstornadas }));
  }
  function onChangeCanal(canal: FiltrosVendas['canal']) {
    setFiltros((prev) => ({ ...prev, canal }));
  }
  function onChangeOperador(operador: FiltrosVendas['operador']) {
    setFiltros((prev) => ({ ...prev, operador }));
  }
  function onChangeFormaPagamento(formaPagamento: FiltrosVendas['formaPagamento']) {
    setFiltros((prev) => ({ ...prev, formaPagamento }));
  }
  function onChangeBusca(busca: string) {
    setFiltros((prev) => ({ ...prev, busca }));
  }
  function abrirDetalhe(vendaId: string) {
    setVendaSelecionadaId(vendaId);
  }
  function fecharDetalhe() {
    setVendaSelecionadaId(null);
  }

  /**
   * Drill do Super Consultor ("Ver sábados →"). O view-model (`types.ts`, o contrato desta tela)
   * não declara uma dimensão dia-da-semana/faixa-de-horário em `FiltrosVendas` — inventar um
   * filtro que os dados não sustentam quebraria o SDD (R2/R6 do repo: contrato antes de
   * implementação, validar no boundary). Em vez de fingir um recorte, rola até a seção da tabela
   * — navegação honesta, ainda assim um "drill" de fato (Lei 2: só navegação/filtro, nunca ação).
   */
  function aplicarFiltroSabados() {
    document.getElementById('vendas-tabela')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  return {
    periodoLabel: mock.periodoLabel,
    kpis: mock.kpis,
    historicoVendidoMesCentavos: mock.historicoVendidoMesCentavos,
    canais: mock.canais,
    operadores: mock.operadores,

    filtros,
    vendasFiltradas,
    onChangeCanal,
    onChangeOperador,
    onChangeFormaPagamento,
    onChangeBusca,
    onToggleEstornadas,

    vendaSelecionada,
    abrirDetalhe,
    fecharDetalhe,
    aplicarFiltroSabados,
  };
}

export type VendasVm = ReturnType<typeof useVendas>;
