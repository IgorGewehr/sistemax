import type { Centavos } from '@/lib/money';

/**
 * View-model de "Ordem de Serviço" (SDD) — espelha 1:1 os dados que
 * `docs/ui/mockups/ordem-servico.html` manipula em JS (o agregado `OrdemDeServico` do domínio
 * C#, sua FSM e as derivações que a tela expõe). Isto é o spec: `mocks/os.ts` implementa estes
 * tipos hoje; a API vai implementá-los amanhã sem que as telas precisem mudar.
 */

/**
 * Status da FSM de uma Ordem de Serviço. `Reprovada` é um estado de repouso real (o cliente
 * recusou o orçamento) — a transição para `DevolvidaSemReparo` não tem UI própria no mockup
 * aprovado (ver `StepAtual`), então não a inventamos aqui.
 */
export type OsStatus =
  | 'Aberta'
  | 'EmDiagnostico'
  | 'AguardandoAprovacao'
  | 'Aprovada'
  | 'EmExecucao'
  | 'Pronta'
  | 'Entregue'
  | 'Reprovada'
  | 'DevolvidaSemReparo'
  | 'Cancelada';

/** Status que encerram a OS — não recebem mais transições (`ehTerminal` do mockup). */
export const STATUS_TERMINAIS: readonly OsStatus[] = ['Entregue', 'Cancelada', 'DevolvidaSemReparo'];

export type CanalResposta = 'Presencial' | 'WhatsApp' | 'Telefone';
export type FormaPagamento = 'Dinheiro' | 'Pix' | 'CartaoDebito' | 'CartaoCredito';
export type OrigemPeca = 'orcada' | 'extra';

/** Peça prevista no orçamento enviado ao cliente. */
export interface PecaOrcada {
  desc: string;
  produtoId: string;
  qtd: number;
  preco: Centavos;
}

/** Peça de fato baixada na execução — pode vir do orçamento (`orcada`) ou ser adicionada depois (`extra`). */
export interface PecaExecucao extends PecaOrcada {
  linhaId: string;
  origem: OrigemPeca;
  aplicada: boolean;
}

export interface Orcamento {
  pecas: PecaOrcada[];
  maoDeObra: Centavos;
  validadeDias: number;
  enviadoEm: Date;
}

export interface Aprovacao {
  decisao: 'Aprovada' | 'Reprovada';
  canal: CanalResposta;
  em: Date;
}

/** Uma entrada da FSM — usada para reconstruir a linha do tempo e "há quanto tempo está aqui". */
export interface HistoricoEntry {
  para: OsStatus;
  em: Date;
}

/** Agregado `OrdemDeServico` — o equipamento, o cliente, o diagnóstico e tudo que a FSM acumulou. */
export interface OrdemServico {
  numero: string;
  cliente: string;
  telefone: string;
  equipamento: string;
  marca: string;
  modelo: string;
  serie: string;
  senha: string | null;
  acessorios: string;
  estadoEntrada: string;
  defeito: string;
  tecnico: string | null;
  abertaEm: Date;
  prazo: Date | null;
  status: OsStatus;
  diagnostico: string | null;
  orcamento: Orcamento | null;
  aprovacao: Aprovacao | null;
  historico: HistoricoEntry[];

  // Preenchidos ao entrar em EmExecucao.
  maoDeObraFinal?: Centavos;
  pecasExecucao?: PecaExecucao[];

  // Preenchidos conforme o ramo percorrido.
  motivoReprovacao?: string | null;
  motivoCancelamento?: string;
  taxaDiagnostico?: Centavos;
  dataEntrega?: Date;
  valorServico?: Centavos;
  valorPecas?: Centavos;
  formaPagamento?: FormaPagamento;
  desconto?: Centavos;
  garantiaDias?: number;
}

// ── Navegação da tela ──────────────────────────────────────────────────────

export type TelaOs = 'lista' | 'detalhe';
export type FiltroFila = 'ativas' | 'todas' | 'encerradas';
export type BucketKey = 'aguardando' | 'execucao' | 'prontas' | 'diagnostico' | 'abertas';

/** Um balde do funil "Onde as OS travam" — grupo de status + as OS que caem nele. */
export interface Bucket {
  key: BucketKey;
  label: string;
  itens: OrdemServico[];
  count: number;
  valor: Centavos;
}

// ── View-models derivados (calculados em `calc.ts`) ─────────────────────────

export interface KpiNaBancada {
  count: number;
  valorCentavos: Centavos;
}

export interface KpiEsperandoCliente {
  valorCentavos: Centavos;
  count: number;
  diasMedio: number;
}

export interface KpiProntas {
  valorCentavos: Centavos;
  count: number;
  maisAntigaDias: number | null;
}

export interface KpiFaturadoMes {
  valorCentavos: Centavos;
  deltaCentavos: number;
  deltaPct: number;
  servicoPct: number;
  pecasPct: number;
}

export interface KpisLista {
  naBancada: KpiNaBancada;
  esperando: KpiEsperandoCliente;
  prontas: KpiProntas;
  faturado: KpiFaturadoMes;
}

/** Maior orçamento parado há 5+ dias — o exemplo que o Super Consultor cita nominalmente. */
export interface MaiorAguardando {
  numero: string;
  clientePrimeiroNome: string;
  equipamentoLower: string;
  valorCentavos: Centavos;
  venceDiaSemanaLower: string;
}

export interface ConsultorInsightData {
  qtdEsperaLonga: number;
  valorParadoCentavos: Centavos;
  maiorAguardando: MaiorAguardando | null;
  prontasCount: number;
  prontasValorCentavos: Centavos;
}

export interface OperacaoStats {
  portaAPortaDias: number;
  taxaAprovacaoPct: number;
  aprovadasCount: number;
  decididasCount: number;
  ticketMedioCentavos: Centavos;
}

export interface BucketDrillStats {
  tempoMedioDias: number;
  maisAntigaDias: number;
  valorCentavos: Centavos;
}

/** Ação primária de uma linha da fila (`acaoPrimaria` do mockup) — ou navega, ou age direto. */
export interface AcaoPrimaria {
  label: string;
  /** Quando `true`, o label é só um texto informativo (ex.: "2/3 peças aplicadas") — o clicável é "Ver peças →". */
  nota?: boolean;
  onClick: () => void;
}
