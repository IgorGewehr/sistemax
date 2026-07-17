import type { Centavos } from '@/lib/money';

/**
 * View-model de "Entradas & saídas" (SDD) — espelha 1:1 os dados que
 * `docs/ui/mockups/entradas-saidas.html` manipula em JS. A Linha do tempo e os 4 KPIs de topo são
 * dado REAL (`useEntradasSaidas.ts` → `GET /financeiro/extrato` + `relatorios/dre` + `fluxo`); os
 * blocos ainda sem read-model (categorias 6m, sparkline) usam `exemplos.ts`.
 */

/** Categoria de um lançamento (entrada ou saída) — usada na tabela e nos filtros. Os 7 slugs
 * abaixo alimentam o card "Para onde foi o dinheiro" (ilustrativo, ver `exemplos.ts`); a Linha do
 * tempo REAL (`GET /financeiro/extrato`) pode devolver qualquer `categoriaId` cadastrado no
 * domínio (ex. `cmv-fornecedor`, `despesa-com-pessoal`) — por isso `LancamentoRow.categoria` é
 * `string` livre, resolvido para rótulo por `categoriaLabel()` em `calc.ts` (com fallback pra
 * slugs desconhecidos), nunca um `Record` indexado à força. */
export type CategoriaId = 'folha' | 'fornecedores' | 'aluguel' | 'impostos' | 'software' | 'marketing' | 'servicos';

/** Categorias de despesa do card "Para onde foi o dinheiro" — exclui `servicos`, que é receita. */
export type CategoriaDespesaId = Exclude<CategoriaId, 'servicos'>;

export type TipoLancamento = 'entrada' | 'saida';
export type StatusLancamento = 'previsto' | 'pago' | 'atrasado';
export type SegFiltro = 'tudo' | 'receber' | 'pagar';

/**
 * Token de cor decorativa da categoria no gráfico (mapeado p/ CSS var em `calc.ts`). Nunca reusa
 * pos/crit/warn — esses são reservados p/ ESTADO (sobra/falta/atenção), não série de gráfico.
 */
export type CorCategoria = 'primary' | 'fg-62' | 'fg-48' | 'fg-36' | 'fg-26' | 'fg-18';

export type FiltroAtivo =
  // `value` é `string` livre (não `CategoriaDespesaId`) — o filtro "Ver detalhe" do Super
  // Consultor de Fornecedores compara contra `LancamentoRow.categoria` REAL (`cmv-fornecedor`),
  // que não pertence ao catálogo ilustrativo de `CategoriaDespesaId` (ver `calc.ts`/`exemplos.ts`).
  | { type: 'categoria'; value: string; label: string }
  | { type: 'status'; value: 'atrasado'; label: string };

/** Uma linha da Linha do tempo (ExtratoUnificado: MovimentoFinanceiro + Parcela do domínio). */
export interface LancamentoRow {
  id: string;
  /** ISO yyyy-mm-dd. */
  data: string;
  desc: string;
  sub: string | null;
  /** `categoriaId` livre do domínio real — ver comentário de `CategoriaId` acima. */
  categoria: string;
  tipo: TipoLancamento;
  status: StatusLancamento;
  valorCentavos: Centavos;
  /** Preenchido quando `status === 'pago'`. */
  conta?: string;
  /** Preenchido quando `status === 'pago'`. */
  origem?: string;
  /** Preenchido quando `status === 'atrasado'`. */
  diasAtraso?: number;
}

export interface MaiorLancamentoCategoria {
  desc: string;
  valorCentavos: Centavos;
}

/** Resumo de uma categoria de despesa nos últimos 6 meses (para as barras e o drill de colunas). */
export interface CategoriaDespesaResumo {
  id: CategoriaDespesaId;
  nome: string;
  cor: CorCategoria;
  /** Custo fixo (folha, aluguel, software) vs variável (fornecedores, impostos, marketing). */
  fixo: boolean;
  totalCentavos: Centavos;
  /** 6 meses, mais antigo primeiro — o último é o mês corrente. */
  historicoCentavos: Centavos[];
  maiorLancamento: MaiorLancamentoCategoria;
}

/** Barra derivada (largura relativa, % do total, badge de anomalia) — calculada em `calc.ts`. */
export interface CategoriaBarra {
  categoria: CategoriaDespesaResumo;
  widthPct: number;
  pctDoTotal: number;
  anomalia: boolean;
  variacaoPct: number;
}

/** Estatísticas do drill de uma categoria (card direito quando uma categoria está selecionada). */
export interface CategoriaDrillStats {
  avg5Centavos: Centavos;
  pctDoTotal: number;
  variacaoPct: number;
  isAnomalia: boolean;
}

export interface LiderAlta {
  categoria: CategoriaDespesaResumo;
  deltaPct: number;
}

export interface Atrasados30DiasResumo {
  totalCentavos: Centavos;
  qtdClientes: number;
}

export interface HeroSparkline {
  pathLinha: string;
  pathArea: string;
}

export interface EntradasSaidasKpis {
  aReceberAbertoCentavos: Centavos;
  aReceberAtrasadoCentavos: Centavos;
  aReceberParcelasAbertas: number;
  sparklineReceber: HeroSparkline;
  aPagarAbertoCentavos: Centavos;
  aPagarMaiorLabel: string;
  aPagarMaiorData: string;
  aPagarLancamentosAbertos: number;
  resultadoMesCentavos: Centavos;
  resultadoDeltaPct: number;
  resultadoComparadoMes: string;
  fechamentoCaixaCentavos: Centavos;
}

/** Tradução caixa × competência (D.4 do contrato) — a nota logo abaixo dos KPIs. */
export interface BridgeNoteData {
  resultadoCentavos: Centavos;
  caixaCentavos: Centavos;
  diferimentoCentavos: Centavos;
}

export interface ConsultorFornecedoresData {
  deltaPct: number;
  mediaHistoricaCentavos: Centavos;
  totalMesCentavos: Centavos;
  qtdPagamentos: number;
}

/** Resumo das vendas avulsas do PDV citado na linha de resumo da tabela. */
export interface ResumoPdvMes {
  qtdVendas: number;
  totalCentavos: Centavos;
}

export type TagConta = 'banco' | 'espécie';

export interface ContaDisponivel {
  nome: string;
  tag: TagConta;
}

/** Categorias oferecidas no formulário de "Lançamento rápido", por tipo. */
export interface CategoriasLancamentoRapido {
  entrada: string[];
  saida: string[];
}

/** Payload do formulário de "Lançamento rápido" ao salvar. */
export interface NovoLancamentoInput {
  tipo: TipoLancamento;
  descricao: string;
  categoriaLabel: string;
  /** Em reais (o input do form é decimal livre) — convertido p/ centavos ao persistir. */
  valorReais: number;
  vencimento: string;
  recorrente: boolean;
}

/** Entrada renderizável da Linha do tempo — linha real, divisor "Hoje", ou resumo do PDV. */
export type TimelineEntry = { kind: 'row'; row: LancamentoRow } | { kind: 'divider'; label: string } | { kind: 'summary' };

