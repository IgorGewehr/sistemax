import { api, type Money } from './client';

export interface ItemDeVendaDto {
  id: string;
  produtoId: string;
  descricao: string;
  quantidade: number;
  precoUnitario: Money;
  desconto: Money;
  subtotal: Money;
}

export interface PagamentoDeVendaDto {
  id: string;
  metodo: string;
  valor: Money;
  valorRecebido: Money | null;
  troco: Money;
  registradoEm: string;
}

export type StatusVenda = 'Aberta' | 'Concluida' | 'Estornada';

export interface VendaDto {
  id: string;
  status: StatusVenda;
  itens: ItemDeVendaDto[];
  pagamentos: PagamentoDeVendaDto[];
  descontoVenda: Money;
  subtotalItens: Money;
  total: Money;
  totalPago: Money;
  restante: Money;
  formaPagamento: string | null;
}

export interface AdicionarItemRequest {
  produtoId: string;
  descricao: string;
  quantidade: number;
  precoUnitarioCentavos: number;
}

export const METODOS_PAGAMENTO = ['Dinheiro', 'Debito', 'Credito', 'Pix', 'Voucher', 'CreditoLoja', 'Outro'] as const;
export type MetodoPagamento = (typeof METODOS_PAGAMENTO)[number];

export interface RegistrarPagamentoRequest {
  metodo: MetodoPagamento;
  valorCentavos: number;
  valorRecebidoCentavos?: number | null;
}

export const vendasApi = {
  abrir: () => api.post<VendaDto>('/vendas'),
  obter: (id: string) => api.get<VendaDto>(`/vendas/${id}`),
  adicionarItem: (id: string, corpo: AdicionarItemRequest) => api.post<VendaDto>(`/vendas/${id}/itens`, corpo),
  registrarPagamento: (id: string, corpo: RegistrarPagamentoRequest) =>
    api.post<VendaDto>(`/vendas/${id}/pagamentos`, corpo),
  concluir: (id: string) => api.post<VendaDto>(`/vendas/${id}/concluir`),
};
