import { useMemo, useState } from 'react';

import { COMPRAS_MOCK } from '@/mocks/compras';

import {
  buildFornecedorRanking,
  buildHomeKpis,
  filtrarFornecedoresTabela,
  filtrarNotasTabela,
  filtrarPedidosTabela,
  fornById,
  itensComVariacao,
  notaById,
  type FiltroStatusNota,
} from './calc';
import type { Fornecedor, MatchKind, NotaEntrada } from './types';

export type SegmentoTabela = 'notas' | 'pedidos' | 'fornecedores';

type Rota = { kind: 'home' } | { kind: 'conferencia'; notaId: string } | { kind: 'fornecedor'; fornecedorId: string };

export type ComprasView =
  | { kind: 'home' }
  | { kind: 'conferencia'; nota: NotaEntrada; fornecedor: Fornecedor }
  | { kind: 'fornecedor'; fornecedor: Fornecedor };

/**
 * "Hoje" e operador fixos do cenário de exemplo (mesmos valores hardcoded no `bindConferenciaAcoes`
 * do mockup) — não há sessão/auth neste mock, então confirmar/descartar sempre grava os mesmos
 * carimbos que o mockup grava.
 */
const DATA_HOJE = '16/07/2026';
const OPERADOR_ATUAL = 'Igor';

/**
 * Todo o estado/lógica de "Compras" vive aqui — a página (`Compras.tsx`) e as 3 telas
 * (Home/Conferência/Fornecedor) permanecem finas, só compondo seções a partir do que este hook
 * devolve. Cálculo derivado (KPIs, ranking, filtros) vem de `calc.ts`, puro e testável.
 */
export function useCompras() {
  const mock = COMPRAS_MOCK;
  const [notas, setNotas] = useState<NotaEntrada[]>(mock.notas);
  const [rota, setRota] = useState<Rota>({ kind: 'home' });
  const [segmentoAtivo, setSegmentoAtivo] = useState<SegmentoTabela>('notas');
  const [filtroStatusNota, setFiltroStatusNota] = useState<FiltroStatusNota>('todas');
  const [buscaTexto, setBuscaTexto] = useState('');
  const [fornecedorDrillBarId, setFornecedorDrillBarId] = useState<string | null>(null);
  const [variacaoAberta, setVariacaoAberta] = useState(false);
  const [importando, setImportando] = useState(false);

  const fornecedores = mock.fornecedores;
  const pedidos = mock.pedidos;

  // ───────────────────────── Navegação (3 "telas" da mesma rota, como no mockup) ─────────────────────────

  function irParaHome() {
    setRota({ kind: 'home' });
  }
  function irParaConferencia(notaId: string) {
    setRota({ kind: 'conferencia', notaId });
  }
  function irParaFornecedor(fornecedorId: string) {
    setRota({ kind: 'fornecedor', fornecedorId });
  }

  const view: ComprasView = useMemo(() => {
    if (rota.kind === 'conferencia') {
      const nota = notaById(notas, rota.notaId);
      const fornecedor = nota ? fornById(fornecedores, nota.fornecedorId) : undefined;
      if (nota && fornecedor) return { kind: 'conferencia', nota, fornecedor };
    }
    if (rota.kind === 'fornecedor') {
      const fornecedor = fornById(fornecedores, rota.fornecedorId);
      if (fornecedor) return { kind: 'fornecedor', fornecedor };
    }
    return { kind: 'home' };
  }, [rota, notas, fornecedores]);

  // ───────────────────────── Home: KPIs, ranking, painel de variação, tabela segmentada ─────────────────────────

  const homeKpis = useMemo(() => buildHomeKpis(notas, pedidos, fornecedores), [notas, pedidos, fornecedores]);
  const variacaoLista = useMemo(() => itensComVariacao(notas, fornecedores), [notas, fornecedores]);
  const fornecedorRanking = useMemo(() => buildFornecedorRanking(fornecedores), [fornecedores]);
  const fornecedorScorecard = fornecedorDrillBarId ? (fornById(fornecedores, fornecedorDrillBarId) ?? null) : null;

  const buscaNormalizada = buscaTexto.trim().toLowerCase();
  const notasFiltradas = useMemo(
    () => filtrarNotasTabela(notas, fornecedores, buscaNormalizada, filtroStatusNota),
    [notas, fornecedores, buscaNormalizada, filtroStatusNota],
  );
  const pedidosFiltrados = useMemo(() => filtrarPedidosTabela(pedidos, fornecedores, buscaNormalizada), [pedidos, fornecedores, buscaNormalizada]);
  const fornecedoresFiltrados = useMemo(() => filtrarFornecedoresTabela(fornecedores, buscaNormalizada), [fornecedores, buscaNormalizada]);

  function onToggleConferirKpi() {
    setFiltroStatusNota((prev) => (prev === 'conferir_kpi' ? 'todas' : 'conferir_kpi'));
    setSegmentoAtivo('notas');
  }
  function onToggleVariacao() {
    setVariacaoAberta((v) => !v);
  }
  function onChangeSegmento(seg: SegmentoTabela) {
    setSegmentoAtivo(seg);
    setBuscaTexto('');
  }
  function onImportarXml() {
    if (importando) return;
    setImportando(true);
    // Pipeline real: parse → classifica → dedupe → upsert fornecedor → match em cascata → Importada → EmConferencia.
    // Aqui a nota já existe no mock (n1) representando o resultado desse pipeline — mesma simulação do mockup.
    window.setTimeout(() => {
      setImportando(false);
      irParaConferencia('n1');
    }, 650);
  }
  function onNovoPedido() {
    setSegmentoAtivo('pedidos');
  }
  function onNovoPedidoForn() {
    setSegmentoAtivo('pedidos');
    irParaHome();
  }
  function onVerTodosFornecedores() {
    setSegmentoAtivo('fornecedores');
  }

  // ───────────────────────── Conferência: ações que mutam a nota em foco ─────────────────────────

  function updateNota(notaId: string, updater: (nota: NotaEntrada) => NotaEntrada) {
    setNotas((prev) => prev.map((n) => (n.id === notaId ? updater(n) : n)));
  }

  /** Confirmar/outro produto/criar produto resolvem o item como "auto"; ignorar tira o item da entrada. */
  function onAcaoItemPadrao(notaId: string, nItem: number, acao: 'confirmar' | 'outro' | 'criar' | 'vincular' | 'ignorar') {
    updateNota(notaId, (nota) => {
      if (nota.pedidoId !== null) return nota;
      const novoMatch: MatchKind = acao === 'ignorar' ? 'ignorado' : 'auto';
      return { ...nota, itens: nota.itens.map((it) => (it.nItem === nItem ? { ...it, match: novoMatch } : it)) };
    });
  }

  function onDivergenciaChange(notaId: string, nItem: number, chave: string) {
    updateNota(notaId, (nota) => {
      if (nota.pedidoId === null) return nota;
      return { ...nota, itens: nota.itens.map((it) => (it.nItem === nItem ? { ...it, divergenciaResolucao: chave } : it)) };
    });
  }

  function onFisicoChange(notaId: string, nItem: number, valor: number | null) {
    updateNota(notaId, (nota) => {
      if (nota.pedidoId === null) return nota;
      return { ...nota, itens: nota.itens.map((it) => (it.nItem === nItem ? { ...it, fisicoQtd: valor } : it)) };
    });
  }

  function onConfirmarRecebimento(notaId: string, jaPago: boolean) {
    updateNota(notaId, (nota) => ({ ...nota, status: 'recebida', recebidaEm: DATA_HOJE, recebidaPor: OPERADOR_ATUAL, jaPago }));
    irParaHome();
  }

  function onDescartarNota(notaId: string) {
    updateNota(notaId, (nota) => ({
      ...nota,
      status: 'estornada',
      estornadaEm: DATA_HOJE,
      estornadaPor: OPERADOR_ATUAL,
      motivoEstorno: 'Descartada pelo operador na conferência.',
    }));
    irParaHome();
  }

  return {
    periodoLabel: mock.periodoLabel,
    vinculos: mock.vinculos,
    custoPorCategoria: mock.custoPorCategoria,
    historicoCustoDemo: mock.historicoCustoDemo,
    compradoMesHistoricoCentavos: mock.compradoMesHistoricoCentavos,
    fornecedores,
    notas,

    view,
    irParaHome,
    irParaConferencia,
    irParaFornecedor,

    homeKpis,
    variacaoLista,
    variacaoAberta,
    onToggleVariacao,
    filtroStatusNota,
    onToggleConferirKpi,

    fornecedorRanking,
    fornecedorScorecard,
    onSelecionarFornecedorBarra: setFornecedorDrillBarId,
    onVoltarScorecard: () => setFornecedorDrillBarId(null),
    onVerTodosFornecedores,

    segmentoAtivo,
    onChangeSegmento,
    buscaTexto,
    onChangeBusca: setBuscaTexto,
    onChangeFiltroStatus: setFiltroStatusNota,
    notasFiltradas,
    pedidosFiltrados,
    fornecedoresFiltrados,

    importando,
    onImportarXml,
    onNovoPedido,
    onNovoPedidoForn,

    onAcaoItemPadrao,
    onDivergenciaChange,
    onFisicoChange,
    onConfirmarRecebimento,
    onDescartarNota,
  };
}

export type ComprasVm = ReturnType<typeof useCompras>;
