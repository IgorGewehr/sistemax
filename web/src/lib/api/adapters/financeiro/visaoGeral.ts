/**
 * DTO (.NET, `Money`/camelCase) → view-model da Visão Geral v3 (SDD em
 * `components/financial/visao-geral/types.ts`). Funções puras, zero React — mesmo padrão de
 * `docs/wiring/financeiro-api-contract.md` §9.1. Reusa `calcularKpisAberto`/`deExtratoLinhas`
 * (`adapters/financeiro/entradasSaidas.ts`) para as tiles "A receber"/"A pagar" — mesmo dataset já
 * usado (e testado) pela tela Entradas & Saídas, números sempre consistentes entre as duas telas.
 */
import type {
  AbertoResumoViewModel,
  CorrenteChave,
  DreResumoViewModel,
  GaugeViewModel,
  InvestimentoViewModel,
  MixViewModel,
  SegmentoMix,
  SemanaPagar,
  SimplesViewModel,
  TileAPagarViewModel,
  TileAReceberViewModel,
  TileAssinaturasViewModel,
  TileResultadoViewModel,
  TimelinePonto,
  TimelineViewModel,
  ZonaFolego,
} from '@/components/financial/visao-geral/types';
import type { KpisAbertoReal } from '@/lib/api/adapters/financeiro/entradasSaidas';
import type {
  CorrenteDeReceitaOrdinal,
  DisponivelParaRetiradaDto,
  DreDto,
  FluxoDeCaixaDto,
  PrevisaoDeCaixaDto,
  RadarDoSimplesDto,
  ReceitaRecorrenteDto,
  RoiDoNegocioDto,
} from '@/lib/api/financeiro';
import type { LancamentoRow } from '@/components/financial/entradas-saidas/types';
import { addDays } from '@/lib/date';

// ─── ① Medidor de fôlego + chips (previsao-caixa + disponivel-retirada) ──────────────────────

function zonaFolego(dias: number): ZonaFolego {
  if (dias < 15) return 'crit';
  if (dias < 30) return 'warn';
  return 'pos';
}

const VERDICT_LABEL: Record<ZonaFolego, string> = { pos: 'Saudável', warn: 'Atenção', crit: 'Crítico' };

export function deGaugeDto(previsao: PrevisaoDeCaixaDto, disponivel: DisponivelParaRetiradaDto): GaugeViewModel {
  const dias = previsao.diasRunwayRealista ?? previsao.diasRunwayBruto;
  const zona = dias === null ? 'warn' : zonaFolego(dias);
  const probPct = Math.round(previsao.probabilidadeSaldoNegativoEm30Dias * 100);
  const diasLabel = dias === null ? 'sem estimativa' : `${dias} dias`;

  return {
    diasFolego: dias,
    zona,
    verdictoLabel: dias === null ? 'Sem dado' : VERDICT_LABEL[zona],
    probabilidadeFaltarPercent: probPct,
    tooltip:
      'Fôlego: quantos dias o caixa segura se nada novo entrar, já contando tudo que está agendado pra sair. ' +
      `Verde: mais de 30 dias · âmbar: 15 a 30 · vermelho: menos de 15. Hoje: ${diasLabel} e ${probPct}% de chance de faltar caixa no mês.`,
    emCaixaCentavos: disponivel.saldoEmCaixa.centavos,
    podeTirarCentavos: disponivel.podeTirar.centavos,
    drillDial: { rota: '/financeiro/fluxo-de-caixa', mensagem: '→ Fluxo de caixa — projeção dia a dia e fôlego' },
    drillEmCaixa: { rota: '/financeiro/bancario', mensagem: '→ Bancário — saldo por conta (banco + gaveta)' },
    drillPodeTirar: {
      rota: '/financeiro/entradas-saidas',
      mensagem: '→ Entradas & Saídas — o que já tem dono nos próximos dias',
    },
  };
}

// ─── ①b Projeção do caixa (fluxo) ────────────────────────────────────────────────────────────

export function deTimelineDto(dto: FluxoDeCaixaDto): TimelineViewModel {
  const pontos: TimelinePonto[] = dto.pontos.map((p, i) => ({
    index: i,
    dataIso: p.data.split('T')[0] ?? p.data,
    saldoCentavos: p.saldoAcumulado.centavos,
    projetado: p.projetado,
  }));

  let hojeIndex = 0;
  let menorIndex = 0;
  pontos.forEach((p, i) => {
    if (!p.projetado) hojeIndex = i;
    if (p.saldoCentavos < pontos[menorIndex].saldoCentavos) menorIndex = i;
  });

  return { pontos, hojeIndex, menorIndex };
}

// ─── ② Tiles "A receber"/"A pagar" (mesmo extrato de horizonte largo de Entradas & Saídas) ───

export function deTileAReceberDto(kpis: KpisAbertoReal): TileAReceberViewModel {
  const total = kpis.aReceberAbertoCentavos;
  const atrasado = kpis.aReceberAtrasadoCentavos;
  const pctAtrasado = total > 0 ? Math.round((atrasado / total) * 100) : 0;

  return {
    totalCentavos: total,
    atrasadoCentavos: atrasado,
    pctEmDia: 100 - pctAtrasado,
    pctAtrasado,
    drill: {
      rota: '/financeiro/entradas-saidas',
      mensagem: '→ Entradas & Saídas — recebíveis em aberto, atrasados primeiro',
    },
  };
}

const MS_DIA = 86_400_000;

/** "18–24/07" (mesmo mês) ou "29/07–04/08" (vira o mês) — janela de 7 dias a partir de `hojeIso`. */
function labelSemana(hojeIso: string, bucketIdx: number): string {
  const inicio = addDays(hojeIso, bucketIdx * 7);
  const fim = addDays(hojeIso, bucketIdx * 7 + 6);
  const [, mesInicio, diaInicio] = inicio.split('-');
  const [, mesFim, diaFim] = fim.split('-');
  return mesInicio === mesFim ? `${diaInicio}–${diaFim}/${mesFim}` : `${diaInicio}/${mesInicio}–${diaFim}/${mesFim}`;
}

/** 4 baldes semanais (28 dias) das saídas ainda não pagas a partir de hoje — o "quando o dinheiro
 * sai" do mockup. Atrasados (data < hoje) ficam fora do balde: já estão contados no total/maior,
 * mas a barra é sobre o que ainda vai vencer, não sobre o passado. */
function semanasPagar(linhas: LancamentoRow[], hojeIso: string): SemanaPagar[] {
  const hoje = new Date(`${hojeIso}T00:00:00`).getTime();
  const baldes = [0, 0, 0, 0];

  linhas.forEach((l) => {
    if (l.tipo !== 'saida' || l.status === 'pago') return;
    const diff = Math.floor((new Date(`${l.data}T00:00:00`).getTime() - hoje) / MS_DIA);
    if (diff < 0 || diff >= 28) return;
    const idx = Math.floor(diff / 7);
    baldes[idx] += l.valorCentavos;
  });

  const maiorBalde = Math.max(...baldes);
  return baldes.map((valor, i) => ({
    label: labelSemana(hojeIso, i),
    valorCentavos: valor,
    alturaPct: maiorBalde > 0 ? Math.round((valor / maiorBalde) * 100) : 0,
    destaque: maiorBalde > 0 && valor === maiorBalde,
  }));
}

export function deTileAPagarDto(kpis: KpisAbertoReal, linhas: LancamentoRow[], hojeIso: string): TileAPagarViewModel {
  return {
    totalCentavos: kpis.aPagarAbertoCentavos,
    semanas: semanasPagar(linhas, hojeIso),
    maiorLabel: kpis.aPagarMaiorLabel,
    maiorDataLabel: kpis.aPagarMaiorData,
    drill: { rota: '/financeiro/entradas-saidas', mensagem: '→ Entradas & Saídas — contas a pagar em aberto' },
  };
}

export function deAbertoResumoDto(kpis: KpisAbertoReal, linhas: LancamentoRow[], hojeIso: string): AbertoResumoViewModel {
  return { receber: deTileAReceberDto(kpis), pagar: deTileAPagarDto(kpis, linhas, hojeIso) };
}

// ─── ③ Resultado + mix das correntes (relatorios/dre) ────────────────────────────────────────

export function deTileResultadoDto(atual: DreDto, anteriorResultadoCentavos: number): TileResultadoViewModel {
  const resultado = atual.resultadoOperacional.centavos;
  const deltaAbs = resultado - anteriorResultadoCentavos;
  const deltaPct =
    anteriorResultadoCentavos !== 0 ? Math.round((deltaAbs / Math.abs(anteriorResultadoCentavos)) * 100) : 0;
  const margem = atual.receitaBruta.centavos > 0 ? Math.round((resultado / atual.receitaBruta.centavos) * 100) : 0;

  return {
    resultadoCentavos: resultado,
    deltaPercentual: Math.abs(deltaPct),
    deltaDirecao: deltaPct >= 0 ? 'up' : 'down',
    margemPercent: margem,
    drill: { rota: '/financeiro/relatorios', mensagem: '→ Relatórios — DRE do mês, aberto por corrente' },
  };
}

/** Ordem/rótulo/cor de exibição do mix — igual ao mockup (Serviços · Assinaturas · Loja), que não
 * é a ordem dos valores pinados de `CorrenteDeReceitaOrdinal` (0 Recorrente, 1 Servico, 2 Comercio). */
const MIX_DISPLAY: { corrente: CorrenteDeReceitaOrdinal; label: string; chave: CorrenteChave }[] = [
  { corrente: 1, label: 'Serviços', chave: 'serv' },
  { corrente: 0, label: 'Assinaturas', chave: 'rec' },
  { corrente: 2, label: 'Loja', chave: 'com' },
];

export function deMixDto(dto: DreDto): MixViewModel | null {
  const porCorrente = dto.porCorrente ?? [];
  const total = porCorrente.reduce((acc, p) => acc + Math.max(0, p.receitaBruta.centavos), 0);
  if (total <= 0) return null;

  const segmentos: SegmentoMix[] = MIX_DISPLAY.map(({ corrente, label, chave }) => {
    const linha = porCorrente.find((p) => p.corrente === corrente);
    const valor = Math.max(0, linha?.receitaBruta.centavos ?? 0);
    return { label, chave, percent: Math.round((valor / total) * 100) };
  }).filter((s) => s.percent > 0);

  return {
    totalCentavos: total,
    segmentos,
    drill: { rota: '/financeiro/relatorios', mensagem: '→ Relatórios — DRE por corrente (Serviços, Assinaturas, Loja)' },
  };
}

export function deDreResumoDto(atual: DreDto, anteriorResultadoCentavos: number): DreResumoViewModel {
  return { resultado: deTileResultadoDto(atual, anteriorResultadoCentavos), mix: deMixDto(atual) };
}

// ─── ④ Assinaturas/MRR (receita-recorrente) ──────────────────────────────────────────────────

export function deTileAssinaturasDto(dto: ReceitaRecorrenteDto): TileAssinaturasViewModel {
  return {
    mrrCentavos: dto.mrr.centavos,
    assinaturasAtivas: dto.assinaturasAtivas,
    drill: { rota: '/financeiro/recorrentes', mensagem: '→ Recorrentes — MRR por serviço e assinaturas ativas' },
  };
}

// ─── ROI (opt-in) e Radar do Simples ──────────────────────────────────────────────────────────

export function deInvestimentoDto(dto: RoiDoNegocioDto): InvestimentoViewModel {
  return {
    percentRecuperado: Math.round(dto.recuperacao.percentRecuperado),
    recuperadoCentavos: dto.recuperacao.recuperadoCentavos,
    totalCentavos: dto.investimento.totalCentavos,
    drill: { rota: '/financeiro/roi-negocio', mensagem: '→ Investimento & ROI — curva investido × recuperado' },
  };
}

function distanciaLabel(mesesProjetados: number | null): string {
  if (mesesProjetados === null) return 'sem previsão de mudança de faixa';
  const unidade = mesesProjetados === 1 ? 'mês' : 'meses';
  return mesesProjetados <= 2 ? `degrau perto (~${mesesProjetados} ${unidade})` : `degrau longe (~${mesesProjetados} ${unidade})`;
}

export function deSimplesDto(dto: RadarDoSimplesDto): SimplesViewModel {
  const teto = dto.rbt12Centavos + dto.distanciaAoProximoDegrauCentavos;
  const fillPercent = teto > 0 ? Math.min(100, Math.round((dto.rbt12Centavos / teto) * 100)) : 0;

  return {
    aliquotaPercent: dto.aliquotaEfetiva * 100,
    faixaAtual: dto.faixaAtual,
    fillPercent,
    distanciaLabel: `faixa ${dto.faixaAtual} · ${distanciaLabel(dto.mesesProjetadosAteOProximoDegrau)}`,
    drill: { rota: '/financeiro/relatorios', mensagem: '→ Relatórios — Radar do Simples (RBT12, faixa e degraus)' },
  };
}
