import type { Centavos } from '@/lib/money';

/**
 * View-model de "Compras" (SDD) — espelha 1:1 os dados que
 * `docs/ui/mockups/compras.html` manipula em JS. Isto é o spec: `mocks/compras.ts` implementa
 * estes tipos hoje (mock); a API vai implementá-los amanhã sem que as telas precisem mudar.
 *
 * Datas já vêm PRÉ-FORMATADAS em pt-BR (`"12/07/2026"`), exatamente como o mockup as embute —
 * não são ISO, então nunca passam por `formatDate`/`new Date()` (evita `RangeError` e não
 * reintroduz um parsing que o mockup nunca teve).
 */

export type NotaStatus = 'conferir' | 'divergente' | 'recebida' | 'estornada';

export type PedidoStatus = 'rascunho' | 'enviado' | 'parcial' | 'recebido' | 'cancelado';

export type FornecedorStatus = 'ativo' | 'bloqueado' | 'inativo';

/** Qualidade do vínculo linha-da-nota × produto do catálogo — cascata de matching do pipeline de importação de XML. */
export type MatchKind = 'auto' | 'sugerido' | 'semmatch' | 'ignorado';

export interface Fornecedor {
  id: string;
  nome: string;
  cnpj: string | null;
  status: FornecedorStatus;
  /** Comprado nos últimos 90 dias — base do ranking "Compras por fornecedor". */
  comprado90dCentavos: Centavos;
  leadTimeRealDias: number;
  leadTimePrometidoDias: number;
  /** Notas com divergência, de `divergTotal` notas recebidas — base da "Taxa de divergência". */
  divergNotas: number;
  divergTotal: number;
  comprado12mCentavos: Centavos;
}

/** De-para aprendido (VinculoProdutoFornecedor): código do fornecedor na NF → produto do catálogo + fator de conversão. */
export interface Vinculo {
  fornecedorId: string;
  cprod: string;
  produto: string;
  unidadeNf: string;
  fator: string;
  notaOrigem: string;
  data: string;
}

/** Item de nota em conferência padrão (sem pedido de compra vinculado) — Tela 9.3. */
export interface ItemNotaPadrao {
  nItem: number;
  nome: string;
  cprod: string;
  match: MatchKind;
  /** Descrição bruta da linha do XML — ex.: "25 CX × R$ 96,00 → 300,000 kg". */
  nf: string;
  custoUnitCentavos: Centavos | null;
  unidade: string;
  /** % vs a compra anterior do mesmo produto×fornecedor. `null` = 1ª compra (sem histórico ainda). */
  deltaPct: number | null;
  /** Só quando `match === 'sugerido'`. */
  sugestao?: string;
  fatorSugerido?: string;
}

export type DivergenciaTipo = 'PrecoMaior' | 'QtdMenor' | 'Avaria' | 'ItemFaltante';
export type DivergenciaSeveridade = 'w' | 'c';

export interface DivergenciaOpcao {
  /** Chave da resolução (ex.: `"aceitar"`, `"devolver"`) — persistida como escolha do operador. */
  chave: string;
  label: string;
}

export interface Divergencia {
  tipo: DivergenciaTipo;
  severidade: DivergenciaSeveridade;
  msg: string;
  opcoes: DivergenciaOpcao[];
}

/** Item conferido contra pedido de compra (three-way match: Pedido × Nota × Físico) — Tela 9.4. */
export interface ItemNotaPedido {
  nItem: number;
  nome: string;
  cprod: string;
  unidade: string;
  pedidoQtd: number;
  pedidoPrecoCentavos: Centavos;
  notaQtd: number | null;
  notaPrecoCentavos: Centavos | null;
  fisicoQtd: number | null;
  deltaPct: number | null;
  divergencia?: Divergencia;
  /** Resolução escolhida pelo operador (chave de `divergencia.opcoes`) — `null` = pendente. */
  divergenciaResolucao?: string | null;
}

export type ItemNota = ItemNotaPadrao | ItemNotaPedido;

export interface Parcela {
  n: number;
  venc: string;
  valorCentavos: Centavos;
}

export interface PedidoRefNota {
  numero: string;
  enviado: string;
  previsto: string;
}

interface NotaEntradaBase {
  id: string;
  numero: string;
  fornecedorId: string;
  emissao: string;
  status: NotaStatus;
  totalCentavos: Centavos;
  chave?: string;
  /** Contagem de itens exibida na tabela — quando a nota já foi recebida/estornada, `itens` pode vir resumido. */
  itensCount?: number;
  recebidaEm?: string;
  recebidaPor?: string;
  jaPago?: boolean;
  estornadaEm?: string;
  estornadaPor?: string;
  motivoEstorno?: string;
  vProdCentavos: Centavos;
  vFreteCentavos: Centavos;
  vSeguroCentavos: Centavos;
  vOutroCentavos: Centavos;
  vDescontoCentavos: Centavos;
  vStCentavos: Centavos;
  vIpiCentavos: Centavos;
  parcelas: Parcela[];
}

/** Nota de entrada em conferência padrão (sem pedido de compra vinculado) — Tela 9.3. */
export interface NotaEntradaPadrao extends NotaEntradaBase {
  pedidoId: null;
  pedido?: undefined;
  itens: ItemNotaPadrao[];
}

/** Nota de entrada conferida contra um pedido de compra (three-way match) — Tela 9.4. */
export interface NotaEntradaPedido extends NotaEntradaBase {
  pedidoId: string;
  pedido: PedidoRefNota;
  itens: ItemNotaPedido[];
}

export type NotaEntrada = NotaEntradaPadrao | NotaEntradaPedido;

export interface Pedido {
  id: string;
  numero: string;
  fornecedorId: string;
  status: PedidoStatus;
  enviado: string | null;
  previsto: string | null;
  totalCentavos: Centavos;
  itensQtd: number;
  notaId: string | null;
}

export interface CategoriaCusto {
  nome: string;
  /** Token de cor decorativa — nunca reusa pos/crit/warn (reservados p/ estado, não série de gráfico). */
  cor: 'primary' | 'fg-50' | 'fg-30';
  /** Um valor em centavos por mês, na mesma ordem de `CustoPorCategoria.meses`. */
  valoresCentavos: Centavos[];
}

export interface CustoPorCategoria {
  meses: string[];
  categorias: CategoriaCusto[];
}

/** Ponto de série do "Histórico de custo" no drill de fornecedor (Tela 9.5). */
export interface HistoricoCustoPonto {
  label: string;
  valorCentavos: Centavos;
}

export interface HistoricoCustoSerie {
  nome: string;
  cor: 'primary' | 'fg-50';
  pontos: HistoricoCustoPonto[];
}

export interface ComprasMock {
  periodoLabel: string;
  fornecedores: Fornecedor[];
  vinculos: Vinculo[];
  notas: NotaEntrada[];
  pedidos: Pedido[];
  custoPorCategoria: CustoPorCategoria;
  /** 5 meses anteriores ao corrente — o 6º ponto do sparkline do KPI hero é o mês corrente (calculado). */
  compradoMesHistoricoCentavos: Centavos[];
  /**
   * Histórico de custo unitário exibido no drill de fornecedor (Tela 9.5). No mockup fonte esse
   * gráfico é fixo — os mesmos 2 itens aparecem não importa qual fornecedor esteja aberto (ainda
   * não segmenta por fornecedor). Reproduzido 1:1; quando a API existir, passa a ser por fornecedor.
   */
  historicoCustoDemo: HistoricoCustoSerie[];
}
