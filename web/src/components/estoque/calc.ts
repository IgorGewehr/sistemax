import type { PosicaoDeItemDto, ProdutoDto } from '@/lib/api/estoque';

import type { CategoriaResumo, EstadoItem, EstoqueKpis, ProdutoComSaldo, ProdutosFiltro } from './types';

/**
 * Derivações puras de "Estoque" — tudo aqui parte só de `ProdutoDto[]`/`PosicaoDeItemDto[]` reais
 * (o que `estoqueApi.listarProdutos()`/`listarSaldos()` devolve). Nada de `useState`/JSX; testável
 * isolado da árvore de componentes, mesmo padrão de `components/compras/calc.ts`.
 */

const CATEGORIA_SEM_NOME = 'Sem categoria';

/** Estado do item — confia nos flags já computados pelo backend (`abaixoDoMinimo`/`zerado`) em vez
 * de reimplementar o corte de mínimo no cliente (R6: validação no boundary, confiança dentro). Sem
 * linha de saldo e controlando estoque = fisicamente zero ainda (produto novo, nenhum movimento). */
export function estadoDe(produto: ProdutoDto, saldo: PosicaoDeItemDto | null): EstadoItem {
  if (!produto.controlaEstoque) return { code: 'servico', label: 'Serviço' };
  if (!saldo || saldo.zerado) return { code: 'zerado', label: 'Zerado' };
  if (saldo.abaixoDoMinimo) return { code: 'baixo', label: 'Baixo' };
  return { code: 'ok', label: 'OK' };
}

export function joinProdutosComSaldo(produtos: ProdutoDto[], saldos: PosicaoDeItemDto[]): ProdutoComSaldo[] {
  const saldoPorProduto = new Map(saldos.map((s) => [s.produtoId, s]));
  return produtos.map((produto) => {
    const saldo = saldoPorProduto.get(produto.id) ?? null;
    return { produto, saldo, estado: estadoDe(produto, saldo) };
  });
}

export function categoriaNomeDe(item: ProdutoComSaldo): string {
  return item.saldo?.categoria ?? item.produto.categoria ?? CATEGORIA_SEM_NOME;
}

function controlaEstoque(item: ProdutoComSaldo): boolean {
  return item.produto.controlaEstoque;
}

export function kpisDe(itens: ProdutoComSaldo[]): EstoqueKpis {
  const controlados = itens.filter(controlaEstoque);
  const valorEmEstoqueCentavos = controlados.reduce((acc, i) => acc + (i.saldo?.valorTotal.centavos ?? 0), 0);
  const abaixoDoMinimo = controlados.filter((i) => i.estado.code === 'baixo').length;
  const zerados = controlados.filter((i) => i.estado.code === 'zerado').length;
  return {
    valorEmEstoqueCentavos,
    itensComSaldo: controlados.length,
    abaixoDoMinimo,
    zerados,
    produtosCadastrados: itens.length,
  };
}

/** "Valor por categoria" da Visão Geral — agrupa só itens controlados (mesmo corte de
 * `produtosComSaldo()` no mockup), soma `valorTotal` real, ordena desc. */
export function categoriasDe(itens: ProdutoComSaldo[]): CategoriaResumo[] {
  const controlados = itens.filter(controlaEstoque);
  const mapa = new Map<string, ProdutoComSaldo[]>();
  for (const item of controlados) {
    const nome = categoriaNomeDe(item);
    const lista = mapa.get(nome);
    if (lista) lista.push(item);
    else mapa.set(nome, [item]);
  }
  return [...mapa.entries()]
    .map(([nome, itensDaCategoria]) => ({
      nome,
      itens: itensDaCategoria,
      valorCentavos: itensDaCategoria.reduce((acc, i) => acc + (i.saldo?.valorTotal.centavos ?? 0), 0),
    }))
    .sort((a, b) => b.valorCentavos - a.valorCentavos);
}

/** Categorias do catálogo inteiro (inclui serviços/itens sem controle) — alimenta o `<select>` de
 * filtro da aba Produtos, que não se restringe a itens com saldo. */
export function categoriasNomesDe(itens: ProdutoComSaldo[]): string[] {
  return [...new Set(itens.map(categoriaNomeDe))].sort((a, b) => a.localeCompare(b, 'pt-BR'));
}

/** Quantos itens controlados já têm saldo mas custo médio zerado — sinal de qualidade de dado
 * (o valor em estoque desses itens fica subestimado), 100% derivado do real. */
export function semCustoMedioDe(itens: ProdutoComSaldo[]): number {
  return itens.filter((i) => controlaEstoque(i) && i.saldo !== null && i.saldo.custoMedio.centavos === 0).length;
}

export function filtrarProdutos(itens: ProdutoComSaldo[], filtro: ProdutosFiltro): ProdutoComSaldo[] {
  const termo = filtro.busca.trim().toLowerCase();
  return itens.filter((item) => {
    if (termo) {
      const alvo = `${item.produto.nome} ${item.produto.sku}`.toLowerCase();
      if (!alvo.includes(termo)) return false;
    }
    if (filtro.categoria !== 'todas' && categoriaNomeDe(item) !== filtro.categoria) return false;
    if (filtro.estado !== 'todos' && item.estado.code !== filtro.estado) return false;
    if (filtro.soProblema && item.estado.code !== 'baixo' && item.estado.code !== 'zerado') return false;
    return true;
  });
}

/** Milésimos-inteiros → texto legível ("2 un", "1,5 kg"). `UN` sempre inteiro (mesmo `fmtQty` do
 * mockup); demais unidades mostram até 3 casas. `null`/`undefined` = sem saldo carregado ainda. */
export function fmtQty(milesimos: number | null | undefined, unidade: string): string {
  if (milesimos === null || milesimos === undefined) return '—';
  const valor = milesimos / 1000;
  if (unidade === 'UN') return `${Math.round(valor).toLocaleString('pt-BR')} un`;
  return `${valor.toLocaleString('pt-BR', { maximumFractionDigits: 3 })} ${unidade.toLowerCase()}`;
}

export interface ConsultorResumo {
  zerados: ProdutoComSaldo[];
  baixos: ProdutoComSaldo[];
  /** Zerado mais valioso (por `valorTotal`) — o item que o Consultor destaca primeiro. */
  foco: ProdutoComSaldo | null;
}

/** Mensagem do Super Consultor da Visão Geral — só afirma o que dá pra provar com o saldo atual.
 * O mockup estima "perda de venda por semana" a partir de consumo histórico e OS reservadas; essa
 * API não existe aqui, então o Consultor real não fabrica esse número (Lei 2: observa, não inventa). */
export function consultorDe(itens: ProdutoComSaldo[]): ConsultorResumo {
  const controlados = itens.filter(controlaEstoque);
  const zerados = controlados.filter((i) => i.estado.code === 'zerado');
  const baixos = controlados.filter((i) => i.estado.code === 'baixo');
  const foco = [...zerados].sort((a, b) => (b.saldo?.valorTotal.centavos ?? 0) - (a.saldo?.valorTotal.centavos ?? 0))[0] ?? null;
  return { zerados, baixos, foco };
}
