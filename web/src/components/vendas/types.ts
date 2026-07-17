import type { MetodoPagamento, StatusVenda } from '@/lib/api/vendas';
import type { Centavos } from '@/lib/money';

/**
 * View-model de "Vendas" (SDD) — espec desta tela (não há mockup .html prévio; este arquivo É o
 * contrato). Dinheiro é SEMPRE `Centavos` (inteiro) — nunca float de reais (`lib/money`).
 *
 * Reaproveita os enums de domínio já declarados em `lib/api/vendas.ts` em vez de duplicá-los:
 * `StatusVenda` ('Aberta'|'Concluida'|'Estornada') e `MetodoPagamento`/`METODOS_PAGAMENTO`.
 * `VendaDto.total` etc. chegam como `Money` ({ centavos, moeda }) — o mapper da API extrai
 * `.centavos` (já é o mesmo valor; a UI descarta `moeda`, sistema é mono-moeda BRL).
 */
export type { StatusVenda, MetodoPagamento };

/** Terminal de origem da venda — "Caixa 01"/"Caixa 02"/"Balcão", ver `pdv.html` terminalLabel.
 * Este ERP de referência não tem e-commerce/delivery ainda; "canal" aqui é o terminal de PDV. */
export type Canal = string;

export interface ItemVendaRow {
  produtoId: string;
  nome: string;
  categoria: string;
  quantidade: number;
  /** `kg` para item por peso (porPeso do PDV), `un` para unidade. */
  unidade: 'un' | 'kg';
  precoUnitarioCentavos: Centavos;
  descontoCentavos: Centavos;
  subtotalCentavos: Centavos;
}

export interface PagamentoVendaRow {
  metodo: MetodoPagamento;
  valorCentavos: Centavos;
  valorRecebidoCentavos: Centavos | null;
  trocoCentavos: Centavos;
}

/** Linha da tabela — E o objeto que alimenta o modal de detalhe (sem fetch adicional ao clicar). */
export interface VendaRow {
  id: string;
  /** "V-01042" — número de exibição, distinto do id técnico. */
  numero: string;
  /** Pré-formatado, "16/07 14:32" (mesma convenção de datas do resto do app — nunca ISO cru na UI). */
  dataHoraLabel: string;
  canal: Canal;
  operador: string;
  /** `null` = consumidor final (venda sem cliente vinculado no PDV). */
  clienteNome: string | null;
  status: StatusVenda;
  itens: ItemVendaRow[];
  pagamentos: PagamentoVendaRow[];
  /** Pago em mais de um método (ex.: Pix + Dinheiro) — tabela mostra "Pix + Dinheiro"/tag "dividido". */
  formasPagamento: MetodoPagamento[];
  descontoCentavos: Centavos;
  subtotalCentavos: Centavos;
  totalCentavos: Centavos;
  /** Só quando `status === 'Estornada'`. */
  motivoEstorno?: string;
  estornadaEm?: string;
  estornadaPor?: string;
}

export interface VendasKpis {
  vendidoHojeCentavos: Centavos;
  vendidoHojeDeltaPct: number;
  vendidoMesCentavos: Centavos;
  vendidoMesDeltaPct: number;
  /** 2 casas — é uma média, precisão importa (ver `lib/money`). */
  ticketMedioCentavos: Centavos;
  ticketMedioDeltaPct: number;
  numeroDeVendas: number;
  numeroDeVendasEstornadas: number;
}

export interface VendasMock {
  periodoLabel: string;
  kpis: VendasKpis;
  /** 5 meses anteriores ao corrente — o sparkline do hero acrescenta `kpis.vendidoMesCentavos` como
   * 6º ponto (mesmo padrão do `compradoMesHistoricoCentavos` de Compras). */
  historicoVendidoMesCentavos: Centavos[];
  /** Opções do filtro Canal, na ordem em que aparecem no `<select>`. */
  canais: Canal[];
  /** Opções do filtro Operador. */
  operadores: string[];
  vendas: VendaRow[];
}

/** Estado dos filtros da tabela (tudo client-side sobre `vendas: VendaRow[]` — ver `calc.ts`). */
export interface FiltrosVendas {
  canal: 'todos' | Canal;
  operador: 'todos' | string;
  formaPagamento: 'todas' | MetodoPagamento;
  /** Ligado só pelo clique no rodapé do KPI "Nº de vendas" (`X estornadas`) — não tem `<select>`
   * próprio, mesmo padrão do `filtroStatusNota === 'conferir_kpi'` de Compras. */
  apenasEstornadas: boolean;
  busca: string;
}
