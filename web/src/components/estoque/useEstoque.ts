import { useCallback, useEffect, useMemo, useState } from 'react';

import { ApiError } from '@/lib/api/client';
import { estoqueApi, type PosicaoDeItemDto, type ProdutoDto } from '@/lib/api/estoque';
import { useToast } from '@/lib/toast';

import { categoriasDe, consultorDe, filtrarProdutos, joinProdutosComSaldo, kpisDe } from './calc';
import type { EstoqueTab, ProdutosFiltro } from './types';

const FILTRO_INICIAL: ProdutosFiltro = { busca: '', categoria: 'todas', estado: 'todos', soProblema: false };

/**
 * Todo o estado de "Estoque" vive aqui — a página (`pages/Estoque.tsx`) e as views por aba ficam
 * finas, só compondo a partir do que este hook devolve. Dado real: `estoqueApi.listarProdutos()` +
 * `listarSaldos()` (F1c), carregados juntos como na página anterior. Ação real: `criarProduto()`.
 */
export function useEstoque() {
  const { toast } = useToast();
  const [produtos, setProdutos] = useState<ProdutoDto[] | null>(null);
  const [saldos, setSaldos] = useState<PosicaoDeItemDto[] | null>(null);
  const [erroCarregamento, setErroCarregamento] = useState<string | null>(null);

  const [tabAtiva, setTabAtiva] = useState<EstoqueTab>('geral');
  const [categoriaDrill, setCategoriaDrill] = useState<string | null>(null);
  const [produtoDrillId, setProdutoDrillId] = useState<string | null>(null);
  const [filtro, setFiltro] = useState<ProdutosFiltro>(FILTRO_INICIAL);
  const [modalAberto, setModalAberto] = useState(false);

  const carregar = useCallback(async () => {
    setErroCarregamento(null);
    try {
      const [listaProdutos, listaSaldos] = await Promise.all([estoqueApi.listarProdutos(), estoqueApi.listarSaldos()]);
      setProdutos(listaProdutos);
      setSaldos(listaSaldos);
    } catch (e) {
      setErroCarregamento(e instanceof ApiError ? e.message : 'Não foi possível carregar o estoque.');
    }
  }, []);

  useEffect(() => {
    void carregar();
  }, [carregar]);

  const itens = useMemo(() => joinProdutosComSaldo(produtos ?? [], saldos ?? []), [produtos, saldos]);
  const kpis = useMemo(() => kpisDe(itens), [itens]);
  const categorias = useMemo(() => categoriasDe(itens), [itens]);
  const categoriaAtiva = useMemo(() => categorias.find((c) => c.nome === categoriaDrill) ?? null, [categorias, categoriaDrill]);
  const consultor = useMemo(() => consultorDe(itens), [itens]);
  const produtosFiltrados = useMemo(() => filtrarProdutos(itens, filtro), [itens, filtro]);
  const produtoFicha = useMemo(() => itens.find((i) => i.produto.id === produtoDrillId) ?? null, [itens, produtoDrillId]);

  function irParaTab(tab: EstoqueTab) {
    setTabAtiva(tab);
  }

  function abrirProduto(id: string) {
    setProdutoDrillId(id);
  }
  function fecharProduto() {
    setProdutoDrillId(null);
  }

  /** Clique de novo na mesma categoria fecha o drill — mesmo toggle do `bar-click` do mockup. */
  function selecionarCategoria(nome: string) {
    setCategoriaDrill((atual) => (atual === nome ? null : nome));
  }
  function voltarCategorias() {
    setCategoriaDrill(null);
  }

  /** Drill do Super Consultor ("Ver produtos com problema →") — filtro real (`soProblema`), não
   * um recorte fantasma que o filtro não suporta (mesma cautela do `useVendas.aplicarFiltroSabados`). */
  function irParaProdutosComProblema() {
    setFiltro((f) => ({ ...f, soProblema: true }));
    setTabAtiva('produtos');
  }

  async function onCriado(produto: ProdutoDto) {
    setProdutos((atual) => (atual ? [...atual, produto] : [produto]));
    setModalAberto(false);
    toast(`Produto "${produto.nome}" cadastrado.`, 'success');
    await carregar();
  }

  return {
    carregando: produtos === null && !erroCarregamento,
    erroCarregamento,

    tabAtiva,
    irParaTab,

    itens,
    kpis,
    categorias,
    categoriaAtiva,
    selecionarCategoria,
    voltarCategorias,
    consultor,
    irParaProdutosComProblema,

    filtro,
    onChangeFiltro: setFiltro,
    produtosFiltrados,
    totalProdutos: itens.length,

    produtoFicha,
    abrirProduto,
    fecharProduto,

    modalAberto,
    abrirModal: () => setModalAberto(true),
    fecharModal: () => setModalAberto(false),
    onCriado,
  };
}

export type EstoqueVm = ReturnType<typeof useEstoque>;
