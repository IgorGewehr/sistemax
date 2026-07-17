import { useCallback, useEffect, useMemo, useState } from 'react';

import { ApiError } from '@/lib/api/client';
import { estoqueApi, type ProdutoDto } from '@/lib/api/estoque';
import { vendasApi, type MetodoPagamento, type VendaDto } from '@/lib/api/vendas';
import { useHotkeys } from '@/lib/hotkeys';
import { useToast } from '@/lib/toast';

import type { PdvScreen, TerminalMode } from './types';

const VENDA_ID_KEY = 'sistemax:pdv:vendaId';
const MAX_RESULTADOS_BUSCA = 6;

function centavosParaInput(centavos: number): string {
  return (centavos / 100).toFixed(2).replace('.', ',');
}

/** Busca sem acento (`semAcento` do mockup) — "oleo" acha "Óleo de Soja", ergonomia de bipagem rápida em pt-BR. */
function semAcento(texto: string): string {
  return texto
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase();
}

/** Espelha o `parseMoneyInput` do mockup: pt-BR ("1.234,56") → centavos inteiros. */
function inputParaCentavos(texto: string): number {
  const limpo = texto.replace(/[^\d,.-]/g, '').replace(/\./g, '').replace(',', '.');
  const valor = parseFloat(limpo);
  return Number.isFinite(valor) ? Math.max(0, Math.round(valor * 100)) : 0;
}

/**
 * Todo o estado/lógica do PDV vive aqui — a página e os componentes só consomem o `PdvVm` que
 * este hook devolve, igual ao contrato de `useCompras`/`useAgenda`. As 5 chamadas reais contra
 * o Bridge (`estoqueApi.listarProdutos`, `vendasApi.abrir/obter/adicionarItem/registrarPagamento/
 * concluir`) são as MESMAS do `Pdv.tsx` anterior — só a superfície de UI ao redor mudou.
 *
 * A venda em montagem é crash-safe por natureza (o backend persiste a cada mutação — ver
 * `Venda` no domínio); o id fica em `sessionStorage` só pra sobreviver a um refresh da aba sem
 * abrir uma venda nova a cada F5.
 */
export function usePdv() {
  const { toast } = useToast();

  const [produtos, setProdutos] = useState<ProdutoDto[] | null>(null);
  const [venda, setVenda] = useState<VendaDto | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [tela, setTela] = useState<'venda' | 'pagamento'>('venda');
  const [terminalMode, setTerminalMode] = useState<TerminalMode>('caixa');
  const [balcaoCategoria, setBalcaoCategoria] = useState('Todos');

  const [busca, setBusca] = useState('');
  const [buscaSelecionada, setBuscaSelecionada] = useState(0);
  const [adicionando, setAdicionando] = useState(false);

  const [metodoSelecionado, setMetodoSelecionado] = useState<MetodoPagamento | null>(null);
  const [valorCampoInput, setValorCampoInput] = useState('');
  const [recebidoCampoInput, setRecebidoCampoInput] = useState('');
  const [processandoPagamento, setProcessandoPagamento] = useState(false);
  const [finalizando, setFinalizando] = useState(false);

  const iniciarOuRecuperarVenda = useCallback(async () => {
    const idSalvo = sessionStorage.getItem(VENDA_ID_KEY);
    if (idSalvo) {
      try {
        const existente = await vendasApi.obter(idSalvo);
        if (existente.status === 'Aberta') {
          setVenda(existente);
          return;
        }
      } catch {
        // venda não encontrada/de outra sessão — cai para abrir uma nova abaixo.
      }
    }
    const nova = await vendasApi.abrir();
    sessionStorage.setItem(VENDA_ID_KEY, nova.id);
    setVenda(nova);
  }, []);

  useEffect(() => {
    let ativo = true;
    async function carregarTudo() {
      setCarregando(true);
      setErro(null);
      try {
        const [listaProdutos] = await Promise.all([estoqueApi.listarProdutos(), iniciarOuRecuperarVenda()]);
        if (ativo) setProdutos(listaProdutos);
      } catch (e) {
        if (ativo) setErro(e instanceof ApiError ? e.message : 'Não foi possível abrir o PDV.');
      } finally {
        if (ativo) setCarregando(false);
      }
    }
    void carregarTudo();
    return () => {
      ativo = false;
    };
  }, [iniciarOuRecuperarVenda]);

  // ───────────────────────── Catálogo: categorias, busca (Caixa), grade (Balcão) ─────────────────────────

  const produtosAtivos = useMemo(() => (produtos ?? []).filter((p) => p.ativo), [produtos]);

  const categorias = useMemo(() => {
    const vistas = new Set<string>();
    produtosAtivos.forEach((p) => {
      if (p.categoria) vistas.add(p.categoria);
    });
    return ['Todos', ...Array.from(vistas).sort((a, b) => a.localeCompare(b, 'pt-BR'))];
  }, [produtosAtivos]);

  const buscaNormalizada = semAcento(busca.trim());
  const resultadosBusca = useMemo(() => {
    if (!buscaNormalizada) return [];
    return produtosAtivos
      .filter((p) => semAcento(p.nome).includes(buscaNormalizada) || semAcento(p.sku).includes(buscaNormalizada))
      .slice(0, MAX_RESULTADOS_BUSCA);
  }, [produtosAtivos, buscaNormalizada]);

  const produtosBalcao = useMemo(
    () => (balcaoCategoria === 'Todos' ? produtosAtivos : produtosAtivos.filter((p) => p.categoria === balcaoCategoria)),
    [produtosAtivos, balcaoCategoria],
  );

  /** Quantas unidades de cada produto já estão no carrinho — pro badge da grade de Balcão. */
  const qtdNoCarrinhoPorProduto = useMemo(() => {
    const mapa = new Map<string, number>();
    venda?.itens.forEach((it) => mapa.set(it.produtoId, (mapa.get(it.produtoId) ?? 0) + it.quantidade));
    return mapa;
  }, [venda]);

  const ultimoItem = venda && venda.itens.length > 0 ? venda.itens[venda.itens.length - 1] : null;

  // ───────────────────────── Carrinho: adicionar item (o VendaDto que volta é a fonte da verdade) ─────────────────────────

  const adicionarProduto = useCallback(
    async (produto: ProdutoDto) => {
      if (!venda || adicionando) return;
      setAdicionando(true);
      try {
        const atualizada = await vendasApi.adicionarItem(venda.id, {
          produtoId: produto.id,
          descricao: produto.nome,
          quantidade: 1,
          precoUnitarioCentavos: produto.precoVenda.centavos,
        });
        setVenda(atualizada);
        setBusca('');
        setBuscaSelecionada(0);
      } catch (e) {
        toast(e instanceof ApiError ? e.message : 'Não foi possível adicionar o item.', 'warning');
      } finally {
        setAdicionando(false);
      }
    },
    [venda, adicionando, toast],
  );

  function confirmarBusca() {
    const produto = resultadosBusca[buscaSelecionada] ?? resultadosBusca[0];
    if (produto) void adicionarProduto(produto);
  }

  function moverSelecaoBusca(delta: number) {
    setBuscaSelecionada((i) => Math.min(Math.max(i + delta, 0), Math.max(resultadosBusca.length - 1, 0)));
  }

  function onChangeBusca(valor: string) {
    setBusca(valor);
    setBuscaSelecionada(0);
  }

  // ───────────────────────── Navegação entre telas ─────────────────────────

  function irParaPagamento() {
    if (!venda || venda.itens.length === 0) return;
    setTela('pagamento');
  }
  function voltarParaVenda() {
    setTela('venda');
  }

  // ───────────────────────── Pagamento ─────────────────────────

  const restanteCentavos = venda?.restante.centavos ?? 0;

  function selecionarMetodo(metodo: MetodoPagamento) {
    if (restanteCentavos <= 0) return;
    setMetodoSelecionado(metodo);
    setValorCampoInput(centavosParaInput(restanteCentavos));
    setRecebidoCampoInput(centavosParaInput(restanteCentavos));
  }

  function aplicarSugestaoRecebido(centavos: number) {
    setRecebidoCampoInput(centavosParaInput(centavos));
  }

  const valorCampoCentavos = Math.min(inputParaCentavos(valorCampoInput), restanteCentavos);
  const recebidoCampoCentavos = inputParaCentavos(recebidoCampoInput);
  const trocoPreview = Math.max(0, recebidoCampoCentavos - valorCampoCentavos);

  async function confirmarPagamento() {
    if (!venda || !metodoSelecionado) return;
    if (valorCampoCentavos <= 0) {
      toast('Informe um valor maior que zero.', 'warning');
      return;
    }
    if (metodoSelecionado === 'Dinheiro' && recebidoCampoCentavos < valorCampoCentavos) {
      toast('Valor recebido não pode ser menor que o valor do pagamento.', 'warning');
      return;
    }
    setProcessandoPagamento(true);
    try {
      const atualizada = await vendasApi.registrarPagamento(venda.id, {
        metodo: metodoSelecionado,
        valorCentavos: valorCampoCentavos,
        valorRecebidoCentavos: metodoSelecionado === 'Dinheiro' ? recebidoCampoCentavos : null,
      });
      setVenda(atualizada);
      setMetodoSelecionado(null);
      setValorCampoInput('');
      setRecebidoCampoInput('');
    } catch (e) {
      toast(e instanceof ApiError ? e.message : 'Não foi possível registrar o pagamento.', 'warning');
    } finally {
      setProcessandoPagamento(false);
    }
  }

  // ───────────────────────── Finalização ─────────────────────────

  async function finalizarVenda() {
    if (!venda) return;
    setFinalizando(true);
    try {
      const concluida = await vendasApi.concluir(venda.id);
      setVenda(concluida);
      toast('Venda concluída — já refletida no financeiro.', 'success');
    } catch (e) {
      toast(e instanceof ApiError ? e.message : 'Não foi possível concluir a venda.', 'warning');
    } finally {
      setFinalizando(false);
    }
  }

  async function novaVenda() {
    sessionStorage.removeItem(VENDA_ID_KEY);
    setTela('venda');
    setTerminalMode('caixa');
    setMetodoSelecionado(null);
    setValorCampoInput('');
    setRecebidoCampoInput('');
    setBusca('');
    setCarregando(true);
    try {
      await iniciarOuRecuperarVenda();
    } finally {
      setCarregando(false);
    }
  }

  const screen: PdvScreen = !venda ? 'venda' : venda.status === 'Concluida' ? 'sucesso' : tela;

  /**
   * Só os 9 atalhos que têm ação real por trás (o mockup mapeia F1-F12 inteiro, mas metade não
   * tem endpoint — F4 desconto de item, F5 desconto de venda, F6 cliente/CPF na tela de Venda, F9
   * suspender, F11 sangria não existem no contrato; ver README, "Fora de escopo"). F10 precisa
   * funcionar com o cursor no campo de busca (é onde ele fica entre um bipe e outro), por isso
   * `ignoreInInputs: false`; `Escape` já ignora esse flag por padrão do hook; `Enter` só age na
   * tela de sucesso, que não tem nenhum campo de texto — sem risco de roubar Enter de um input.
   * Na tela de Pagamento, F2-F6 chamam a MESMA `selecionarMetodo` que os cards de método já
   * chamam no clique (mockup: `handleFKey`, `mode==='pagamento'`) — mesmo guard de
   * `restanteCentavos > 0` que desabilita os cards.
   */
  useHotkeys([
    { key: 'F10', ignoreInInputs: false, handler: (e) => { if (screen === 'venda') { e.preventDefault(); irParaPagamento(); } } },
    {
      key: 'F12',
      ignoreInInputs: false,
      handler: (e) => {
        if (screen === 'pagamento' && restanteCentavos <= 0 && venda !== null && venda.itens.length > 0) {
          e.preventDefault();
          void finalizarVenda();
        }
      },
    },
    {
      key: 'F2',
      ignoreInInputs: false,
      handler: (e) => { if (screen === 'pagamento' && restanteCentavos > 0) { e.preventDefault(); selecionarMetodo('Dinheiro'); } },
    },
    {
      key: 'F3',
      ignoreInInputs: false,
      handler: (e) => { if (screen === 'pagamento' && restanteCentavos > 0) { e.preventDefault(); selecionarMetodo('Debito'); } },
    },
    {
      key: 'F4',
      ignoreInInputs: false,
      handler: (e) => { if (screen === 'pagamento' && restanteCentavos > 0) { e.preventDefault(); selecionarMetodo('Credito'); } },
    },
    {
      key: 'F5',
      ignoreInInputs: false,
      handler: (e) => { if (screen === 'pagamento' && restanteCentavos > 0) { e.preventDefault(); selecionarMetodo('Pix'); } },
    },
    {
      key: 'F6',
      ignoreInInputs: false,
      handler: (e) => { if (screen === 'pagamento' && restanteCentavos > 0) { e.preventDefault(); selecionarMetodo('Outro'); } },
    },
    { key: 'Escape', handler: () => { if (screen === 'pagamento') voltarParaVenda(); } },
    { key: 'Enter', handler: () => { if (screen === 'sucesso') void novaVenda(); } },
  ]);

  return {
    produtos,
    venda,
    carregando,
    erro,
    screen,

    terminalMode,
    setTerminalMode,
    categorias,
    balcaoCategoria,
    setBalcaoCategoria,
    produtosBalcao,
    qtdNoCarrinhoPorProduto,

    busca,
    onChangeBusca,
    buscaSelecionada,
    moverSelecaoBusca,
    resultadosBusca,
    confirmarBusca,
    ultimoItem,
    adicionando,
    adicionarProduto,

    irParaPagamento,
    voltarParaVenda,

    restanteCentavos,
    metodoSelecionado,
    selecionarMetodo,
    valorCampoInput,
    setValorCampoInput,
    recebidoCampoInput,
    setRecebidoCampoInput,
    valorCampoCentavos,
    recebidoCampoCentavos,
    trocoPreview,
    aplicarSugestaoRecebido,
    confirmarPagamento,
    processandoPagamento,

    finalizarVenda,
    finalizando,
    novaVenda,
  };
}

export type PdvVm = ReturnType<typeof usePdv>;
