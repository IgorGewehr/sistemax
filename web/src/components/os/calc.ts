import type { Centavos } from '@/lib/money';

import type {
  AcaoPrimaria,
  Bucket,
  BucketDrillStats,
  BucketKey,
  ConsultorInsightData,
  KpisLista,
  MaiorAguardando,
  OperacaoStats,
  OrdemServico,
  OsStatus,
} from './types';

/**
 * "Hoje" fixo do cenário de exemplo — mesma data do mockup aprovado (linha `HOJE = new
 * Date(2026, 6, 18, 10, 0)`). Os dados de exemplo (prazos, dias de espera) são todos relativos
 * a este dia; não deriva de `new Date()` porque isso desalinharia o cenário a cada virada de dia.
 */
export const HOJE = new Date(2026, 6, 18, 10, 0);
const DIA_MS = 86_400_000;

/** Açúcar de autoria do mock: `hoje(-3)` → 3 dias antes de `HOJE`, à mesma hora. Espelha o `hoje()` do mockup. */
export function hoje(offsetDias: number): Date {
  const d = new Date(HOJE);
  d.setDate(d.getDate() + offsetDias);
  return d;
}

export function addDias(data: Date, dias: number): Date {
  const d = new Date(data);
  d.setDate(d.getDate() + dias);
  return d;
}

export function diasEntre(de: Date, ate: Date): number {
  return Math.floor((ate.getTime() - de.getTime()) / DIA_MS);
}

export function diasDesde(d: Date): number {
  return Math.max(0, Math.floor((HOJE.getTime() - d.getTime()) / DIA_MS));
}

/** Nome completo do dia da semana, capitalizado — igual ao `diaSemana()` do mockup. */
export function diaSemana(d: Date): string {
  const s = d.toLocaleDateString('pt-BR', { weekday: 'long' });
  return s.charAt(0).toUpperCase() + s.slice(1);
}

export function ehTerminal(status: OsStatus): boolean {
  return status === 'Entregue' || status === 'Cancelada' || status === 'DevolvidaSemReparo';
}

export const STATUS_LABEL: Record<OsStatus, string> = {
  Aberta: 'Aberta',
  EmDiagnostico: 'Diagnóstico',
  AguardandoAprovacao: 'Aguard. aprovação',
  Aprovada: 'Aprovada',
  EmExecucao: 'Em execução',
  Pronta: 'Pronta',
  Entregue: 'Entregue',
  Reprovada: 'Reprovada',
  DevolvidaSemReparo: 'Devolvida',
  Cancelada: 'Cancelada',
};

/** Título de cada etapa na linha do tempo (`TITULOS` do mockup) — mais formal que o chip da fila. */
export const TITULOS: Record<OsStatus, string> = {
  Aberta: 'Entrada',
  EmDiagnostico: 'Diagnóstico',
  AguardandoAprovacao: 'Orçamento',
  Aprovada: 'Aprovado',
  EmExecucao: 'Execução',
  Pronta: 'Pronta',
  Entregue: 'Entrega',
  Reprovada: 'Reprovado',
  DevolvidaSemReparo: 'Devolução sem reparo',
  Cancelada: 'Cancelada',
};

export const ORDEM_PRINCIPAL: readonly OsStatus[] = [
  'Aberta',
  'EmDiagnostico',
  'AguardandoAprovacao',
  'Aprovada',
  'EmExecucao',
  'Pronta',
  'Entregue',
];

// ── Cálculos monetários (mesmos que o agregado C# expõe: TotalGeral, EstaAtrasada…) ─────────

export function totalOrcamento(orc: OrdemServico['orcamento']): Centavos {
  if (!orc) return 0;
  return orc.maoDeObra + orc.pecas.reduce((s, p) => s + p.preco * p.qtd, 0);
}

export function totalExecucaoAtual(os: OrdemServico): Centavos {
  const mao = os.maoDeObraFinal ?? (os.orcamento ? os.orcamento.maoDeObra : 0);
  const pecas = (os.pecasExecucao ?? []).reduce((s, p) => s + p.preco * p.qtd, 0);
  return mao + pecas;
}

/** Valor "em jogo" da OS no status atual — o que a fila e os KPIs somam. */
export function valorAtual(os: OrdemServico): Centavos {
  switch (os.status) {
    case 'Entregue':
      return (os.valorServico ?? 0) + (os.valorPecas ?? 0);
    case 'DevolvidaSemReparo':
      return os.taxaDiagnostico ?? 0;
    case 'Cancelada':
      return 0;
    case 'EmExecucao':
    case 'Pronta':
      return totalExecucaoAtual(os);
    case 'AguardandoAprovacao':
    case 'Aprovada':
    case 'Reprovada':
      return totalOrcamento(os.orcamento);
    default:
      return 0;
  }
}

export function entrouEm(os: OrdemServico, status: OsStatus): Date | null {
  return os.historico.find((h) => h.para === status)?.em ?? null;
}

export function atrasada(os: OrdemServico): boolean {
  if (ehTerminal(os.status) || !os.prazo) return false;
  const h = new Date(HOJE);
  h.setHours(0, 0, 0, 0);
  const p = new Date(os.prazo);
  p.setHours(0, 0, 0, 0);
  return h.getTime() > p.getTime();
}

export function orcamentoVencido(os: OrdemServico): boolean {
  if (os.status !== 'AguardandoAprovacao' || !os.orcamento) return false;
  return HOJE.getTime() > addDias(os.orcamento.enviadoEm, os.orcamento.validadeDias).getTime();
}

function mesmoMes(d: Date | null | undefined): d is Date {
  return !!d && d.getFullYear() === HOJE.getFullYear() && d.getMonth() === HOJE.getMonth();
}

/** Faturamento do mês anterior (exemplo fixo do mockup) — base do delta "▲ x% vs jun". */
const FATURADO_MES_ANTERIOR_CENTS: Centavos = 132_500;

export function buildKpis(lista: OrdemServico[]): KpisLista {
  const ativas = lista.filter((o) => !ehTerminal(o.status));
  const aguardando = lista.filter((o) => o.status === 'AguardandoAprovacao');
  const prontas = lista.filter((o) => o.status === 'Pronta');
  const faturadasMes = lista.filter(
    (o) => (o.status === 'Entregue' || o.status === 'DevolvidaSemReparo') && mesmoMes(o.dataEntrega),
  );

  const naBancadaValor = ativas.reduce((s, o) => s + valorAtual(o), 0);
  const esperandoValor = aguardando.reduce((s, o) => s + valorAtual(o), 0);
  const esperandoDiasMedio = aguardando.length
    ? aguardando.reduce((s, o) => s + diasDesde(o.orcamento!.enviadoEm), 0) / aguardando.length
    : 0;
  const prontasValor = prontas.reduce((s, o) => s + valorAtual(o), 0);
  const prontasMaisAntigaDias = prontas.length
    ? Math.max(...prontas.map((o) => diasDesde(entrouEm(o, 'Pronta') ?? o.abertaEm)))
    : null;
  const faturadoValor = faturadasMes.reduce((s, o) => s + valorAtual(o), 0);
  const faturadoServico = faturadasMes.reduce((s, o) => s + (o.valorServico ?? o.taxaDiagnostico ?? 0), 0);
  const servPct = faturadoValor ? Math.round((faturadoServico / faturadoValor) * 100) : 0;
  // `pecPct` é o complemento (mesma conta do mockup) — não deriva de um "faturadoPecas" separado.
  const pecPct = 100 - servPct;
  const deltaMes = faturadoValor - FATURADO_MES_ANTERIOR_CENTS;
  const deltaPct = FATURADO_MES_ANTERIOR_CENTS ? Math.round((deltaMes / FATURADO_MES_ANTERIOR_CENTS) * 100) : 0;

  return {
    naBancada: { count: ativas.length, valorCentavos: naBancadaValor },
    esperando: { valorCentavos: esperandoValor, count: aguardando.length, diasMedio: esperandoDiasMedio },
    prontas: { valorCentavos: prontasValor, count: prontas.length, maisAntigaDias: prontasMaisAntigaDias },
    faturado: { valorCentavos: faturadoValor, deltaCentavos: deltaMes, deltaPct, servicoPct: servPct, pecasPct: pecPct },
  };
}

/** Insight do Super Consultor — orçamentos parados há 5+ dias, ou (se nenhum) a prateleira de prontas. */
export function buildConsultorInsight(lista: OrdemServico[]): ConsultorInsightData {
  const aguardando = lista.filter((o) => o.status === 'AguardandoAprovacao');
  const prontas = lista.filter((o) => o.status === 'Pronta');
  const diasEsperaLongos = aguardando.filter((o) => diasDesde(o.orcamento!.enviadoEm) >= 5);
  const maior = diasEsperaLongos.slice().sort((a, b) => valorAtual(b) - valorAtual(a))[0] ?? null;

  return {
    qtdEsperaLonga: diasEsperaLongos.length,
    valorParadoCentavos: diasEsperaLongos.reduce((s, o) => s + valorAtual(o), 0),
    maiorAguardando: maior
      ? ({
          numero: maior.numero,
          clientePrimeiroNome: maior.cliente.split(' ')[0],
          equipamentoLower: maior.equipamento.toLowerCase(),
          valorCentavos: valorAtual(maior),
          venceDiaSemanaLower: diaSemana(addDias(maior.orcamento!.enviadoEm, maior.orcamento!.validadeDias)).toLowerCase(),
        } satisfies MaiorAguardando)
      : null,
    prontasCount: prontas.length,
    prontasValorCentavos: prontas.reduce((s, o) => s + valorAtual(o), 0),
  };
}

// ── Funil "Onde as OS travam" ────────────────────────────────────────────────

const BUCKETS_META: { key: BucketKey; label: string; statuses: OsStatus[] }[] = [
  { key: 'aguardando', label: 'Aguard. aprovação', statuses: ['AguardandoAprovacao'] },
  { key: 'execucao', label: 'Em execução', statuses: ['EmExecucao', 'Aprovada'] },
  { key: 'prontas', label: 'Prontas', statuses: ['Pronta'] },
  { key: 'diagnostico', label: 'Diagnóstico', statuses: ['EmDiagnostico'] },
  { key: 'abertas', label: 'Abertas', statuses: ['Aberta'] },
];

export function bucketDados(lista: OrdemServico[]): Bucket[] {
  return BUCKETS_META.map((b) => {
    const itens = lista.filter((o) => b.statuses.includes(o.status));
    return { key: b.key, label: b.label, itens, count: itens.length, valor: itens.reduce((s, o) => s + valorAtual(o), 0) };
  });
}

export function bucketPorChave(lista: OrdemServico[], key: BucketKey): Bucket {
  const b = bucketDados(lista).find((x) => x.key === key);
  if (!b) throw new Error(`Bucket desconhecido: ${key}`);
  return b;
}

/** OS de um balde, mais antiga na etapa primeiro — mesma ordenação do drill do mockup. */
export function bucketItensOrdenados(bucket: Bucket): OrdemServico[] {
  return bucket.itens.slice().sort((x, y) => {
    const dx = (entrouEm(x, x.status) ?? x.abertaEm).getTime();
    const dy = (entrouEm(y, y.status) ?? y.abertaEm).getTime();
    return dx - dy;
  });
}

export function bucketDrillStats(bucket: Bucket): BucketDrillStats {
  const dias = bucket.itens.map((o) => diasDesde(entrouEm(o, o.status) ?? o.abertaEm));
  const tempoMedio = dias.length ? dias.reduce((s, d) => s + d, 0) / dias.length : 0;
  const maisAntiga = dias.length ? Math.max(...dias) : 0;
  return { tempoMedioDias: tempoMedio, maisAntigaDias: maisAntiga, valorCentavos: bucket.valor };
}

export function operacaoStats(lista: OrdemServico[]): OperacaoStats {
  const entregues = lista.filter((o) => o.status === 'Entregue');
  const portaAPorta = entregues.length
    ? entregues.reduce((s, o) => s + diasEntre(o.abertaEm, o.dataEntrega!), 0) / entregues.length
    : 0;
  const decididas = lista.filter((o) => o.aprovacao);
  const aprovadas = decididas.filter((o) => o.aprovacao!.decisao === 'Aprovada');
  const taxaAprov = decididas.length ? Math.round((aprovadas.length / decididas.length) * 100) : 0;
  const comValor = lista.filter((o) => valorAtual(o) > 0);
  const ticketMedio = comValor.length ? comValor.reduce((s, o) => s + valorAtual(o), 0) / comValor.length : 0;
  return {
    portaAPortaDias: portaAPorta,
    taxaAprovacaoPct: taxaAprov,
    aprovadasCount: aprovadas.length,
    decididasCount: decididas.length,
    ticketMedioCentavos: Math.round(ticketMedio),
  };
}

// ── Fila (tabela) ────────────────────────────────────────────────────────────

/** Id curto pra linhas novas (peça extra) — mesmo formato do `uid()` do mockup. */
export function uid(): string {
  return 'l' + Math.random().toString(36).slice(2, 9);
}

/**
 * `brlOuTraco` do mockup: valor zero (ou negativo) é "nada a mostrar" — não "R$ 0,00". Devolve
 * `null` pra alimentar direto o `<MoneyValue>` (que já sabe render '—' pra `null`).
 */
export function centavosOuTraco(centavos: Centavos): Centavos | null {
  return centavos > 0 ? centavos : null;
}

interface AcaoPrimariaHandlers {
  irParaDetalhe: (numero: string) => void;
  iniciarExecucao: (numero: string) => void;
  concluirExecucao: (numero: string) => void;
}

/** Ação primária de cada linha da fila (`acaoPrimaria` do mockup) — por status. */
export function acaoPrimaria(os: OrdemServico, handlers: AcaoPrimariaHandlers): AcaoPrimaria | null {
  switch (os.status) {
    case 'Aberta':
      return { label: 'Registrar diagnóstico', onClick: () => handlers.irParaDetalhe(os.numero) };
    case 'EmDiagnostico':
      return { label: 'Enviar orçamento', onClick: () => handlers.irParaDetalhe(os.numero) };
    case 'AguardandoAprovacao':
      return { label: 'Cliente respondeu?', onClick: () => handlers.irParaDetalhe(os.numero) };
    case 'Aprovada':
      return { label: 'Iniciar execução', onClick: () => handlers.iniciarExecucao(os.numero) };
    case 'EmExecucao': {
      const pecas = os.pecasExecucao ?? [];
      const pendentes = pecas.filter((p) => !p.aplicada).length;
      if (pendentes > 0) {
        return { label: `${pecas.length - pendentes}/${pecas.length} peças aplicadas`, nota: true, onClick: () => handlers.irParaDetalhe(os.numero) };
      }
      return { label: 'Concluir execução', onClick: () => handlers.concluirExecucao(os.numero) };
    }
    case 'Pronta':
      return { label: 'Receber e entregar', onClick: () => handlers.irParaDetalhe(os.numero) };
    case 'Reprovada':
      return { label: 'Devolver sem reparo', onClick: () => handlers.irParaDetalhe(os.numero) };
    default:
      return null;
  }
}

export function filtrarFila(lista: OrdemServico[], filtro: 'ativas' | 'todas' | 'encerradas', busca: string): OrdemServico[] {
  let itens =
    filtro === 'ativas'
      ? lista.filter((o) => !ehTerminal(o.status))
      : filtro === 'encerradas'
        ? lista.filter((o) => ehTerminal(o.status))
        : lista.slice();

  const q = busca.trim().toLowerCase();
  if (q) {
    itens = itens.filter(
      (o) => o.numero.toLowerCase().includes(q) || o.cliente.toLowerCase().includes(q) || o.equipamento.toLowerCase().includes(q),
    );
  }

  return itens.slice().sort((a, b) => {
    const atrasoA = atrasada(a) ? 1 : 0;
    const atrasoB = atrasada(b) ? 1 : 0;
    if (atrasoA !== atrasoB) return atrasoB - atrasoA;
    const da = (entrouEm(a, a.status) ?? a.abertaEm).getTime();
    const db = (entrouEm(b, b.status) ?? b.abertaEm).getTime();
    return da - db;
  });
}

// ── Linha do tempo (detalhe) ─────────────────────────────────────────────────

/**
 * Passos exibidos na linha do tempo. O ramo Reprovada/DevolvidaSemReparo substitui
 * Aprovada→Entregue depois de AguardandoAprovacao; Cancelada nunca chega por este ramo (a FSM
 * não permite `Cancelar()` a partir de Reprovada).
 */
export function passosDaLinhaDoTempo(os: OrdemServico): readonly OsStatus[] {
  const ramoReprovado = os.status === 'Reprovada' || os.status === 'DevolvidaSemReparo';
  return ramoReprovado ? ['Aberta', 'EmDiagnostico', 'AguardandoAprovacao', 'Reprovada', 'DevolvidaSemReparo'] : ORDEM_PRINCIPAL;
}

export function indiceAtualDaLinha(os: OrdemServico, passos: readonly OsStatus[]): number {
  let indice = passos.indexOf(os.status);
  if (os.status === 'Cancelada') {
    // Acha o último status antes de cancelar, pra saber onde a linha do tempo parou.
    const ultimoAntes = os.historico[os.historico.length - 2]?.para ?? 'Aberta';
    indice = passos.indexOf(ultimoAntes);
    if (indice < 0) indice = 0;
  }
  return indice;
}
