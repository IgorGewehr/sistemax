/**
 * View-model do módulo Clientes (SDD — este arquivo é o spec).
 * Mesmo rigor de `components/financial/visao-geral/types.ts` e `components/compras/types.ts`.
 *
 * Dinheiro é SEMPRE `Centavos` (inteiro) — nunca float de reais (`lib/money`).
 * Datas chegam PRÉ-FORMATADAS em pt-BR ("16/07/2026"), como nos mocks de Compras — nunca ISO,
 * nunca passam por `new Date(string)`/`formatDate` (evita `RangeError` e parsing que o domínio
 * não pede). `aniversario` é só "DD/MM" (sem ano) — não guardamos idade, e não precisamos do ano
 * pra decidir "é aniversário este mês".
 */
import type { Centavos } from '@/lib/money';

/** Status cadastral (soft-delete/LGPD). NÃO é o mesmo que o segmento "sem comprar há 90d+" —
 *  aquele é sempre derivado em `calc.ts`, nunca persistido, pra não divergir do "hoje". */
export type ClienteStatus = 'ativo' | 'inativo';

export type TipoHistorico = 'venda' | 'os';

/** Uma linha do histórico de compras/OS de um cliente (Ficha, bloco 3). */
export interface HistoricoItem {
  id: string;
  /** "16/07/2026" — pré-formatada. */
  data: string;
  tipo: TipoHistorico;
  /** "3 itens · Ração + Antipulgas" ou "OS #482 · Troca de tela". */
  descricao: string;
  valorCentavos: Centavos;
  /** Só quando `tipo === 'os'` — "Concluída" / "Em andamento" / "Orçamento". */
  statusLabel?: string;
}

export interface Cliente {
  id: string;
  nome: string;
  telefone: string | null;
  email: string | null;
  /** "DD/MM" — `null` quando o cliente não informou. */
  aniversario: string | null;
  enderecoResumo: string | null;
  observacoes: string | null;
  /** Rótulos livres do operador (ex.: "vip", "atacado") — NUNCA guarda estado derivado
   *  ("aniversariante", "sumiu"): isso é recalculado sempre contra o "hoje" em `calc.ts`. */
  tags: string[];
  status: ClienteStatus;
  /** "14/07/2026" — data de cadastro. */
  criadoEm: string;
  /** `null` = nunca comprou (cliente novo, cadastrado mas sem 1ª compra ainda). */
  ultimaVisita: string | null;
  comprasCount: number;
  /** 0 quando `comprasCount === 0`. */
  ticketMedioCentavos: Centavos;
  totalGasto12mCentavos: Centavos;
  totalGastoVidaCentavos: Centavos;
}

/** Formulário de criar/editar — subconjunto editável de `Cliente` (o resto é derivado/servidor). */
export interface ClienteFormValues {
  nome: string;
  telefone: string;
  email: string;
  /** Máscara livre "DD/MM" no input — validada em `calc.ts` (`parseAniversario`), não aqui. */
  aniversario: string;
  enderecoResumo: string;
  observacoes: string;
  tags: string[];
}

export type FiltroClientes = 'todos' | 'aniversariantes' | 'semComprar90d';

/** Dados de exemplo — hoje mock (`mocks/clientes.ts`), amanhã API, sem mudar a tela. */
export interface ClientesMock {
  /** "16/07/2026" — o "hoje" fixo do cenário (mesma convenção de `DATA_HOJE` em `useCompras.ts`). */
  hojeLabel: string;
  clientes: Cliente[];
  historicoPorCliente: Record<string, HistoricoItem[]>;
  /** 5 meses anteriores ao corrente — o 6º ponto do sparkline do KPI hero é `clientes.length`
   *  (calculado a partir do array, nunca hardcoded ao lado). */
  totalClientesHistoricoMensal: number[];
}
