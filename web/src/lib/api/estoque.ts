import { api, type Money } from './client';

export interface ProdutoDto {
  id: string;
  sku: string;
  nome: string;
  categoria: string | null;
  unidade: string;
  precoVenda: Money;
  controlaEstoque: boolean;
  ativo: boolean;
}

export interface CriarProdutoRequest {
  nome: string;
  unidade: string;
  sku?: string | null;
  precoVendaCentavos?: number;
  categoria?: string | null;
  controlaEstoque?: boolean;
  estoqueMinimoMilesimos?: number | null;
}

export interface Quantidade {
  milesimos: number;
}

export interface PosicaoDeItemDto {
  produtoId: string;
  produtoNome: string;
  categoria: string | null;
  sku: string;
  fisico: Quantidade;
  reservado: Quantidade;
  disponivel: Quantidade;
  custoMedio: Money;
  valorTotal: Money;
  abaixoDoMinimo: boolean;
  zerado: boolean;
}

export const UNIDADES = ['UN', 'KG', 'L', 'M', 'M2', 'M3', 'CX', 'PCT'] as const;

export const estoqueApi = {
  listarProdutos: () => api.get<ProdutoDto[]>('/estoque/produtos'),
  criarProduto: (corpo: CriarProdutoRequest) => api.post<ProdutoDto>('/estoque/produtos', corpo),
  listarSaldos: () => api.get<PosicaoDeItemDto[]>('/estoque/saldos'),
};
