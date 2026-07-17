/**
 * DTO (.NET, `Money`/camelCase) → fatias do `BancarioViewModel` (SDD em
 * `components/financial/bancario/types.ts`). Funções puras, zero React — mesmo padrão de
 * `adapters/financeiro/visaoGeral.ts`. Onde o domínio real não tem um dado que o mockup tinha
 * (ex.: copy contextual do botão de ação, "crédito parcelado" como forma distinta), usamos texto
 * genérico honesto em vez de inventar — mesma diretriz de `deTimelineDto` (não fabricar origem).
 */
import type { ContaBancaria, ConciliacaoBancaria, ConsultorBancarioInsight, MovimentoExtrato, SemanaMovimento } from '@/components/financial/bancario/types';
import type {
  ConciliacaoBancariaDto,
  ContaBancariaDto,
  MovimentoBancarioDto,
  SemanaMovimentoDto,
  TaxasPorFormaDto,
} from '@/lib/api/financeiro';

/** Paleta rotativa dos "pontinhos" de conta na tabela do extrato — a mesma escala fg/0.6, /0.4,
 * /0.25 do mockup, agora por índice em vez de por id fixo (contas reais têm ULID, não 'itau'). */
const DOT_CLASSNAMES = ['bg-foreground/60', 'bg-foreground/40', 'bg-foreground/25'];

export function deContasDto(dtos: ContaBancariaDto[]): ContaBancaria[] {
  return dtos.map((dto, i) => ({
    id: dto.id,
    label: dto.nome,
    saldoCentavos: dto.saldo.centavos,
    dotClassName: DOT_CLASSNAMES[i % DOT_CLASSNAMES.length],
  }));
}

/** "2026-07-16T00:00:00+00:00" ou "2026-07-16" → "16/07". Nunca faz `new Date(iso)` (R do
 * projeto: formatador não confia em Date sem validar) — extração textual, determinística. */
function ddMmDeIso(iso: string): string {
  const [dataParte] = iso.split('T');
  const [, mes, dia] = dataParte.split('-');
  return `${dia}/${mes}`;
}

const MESES_PT: Record<string, string> = {
  '01': 'jan', '02': 'fev', '03': 'mar', '04': 'abr', '05': 'mai', '06': 'jun',
  '07': 'jul', '08': 'ago', '09': 'set', '10': 'out', '11': 'nov', '12': 'dez',
};

/** "01" + "07" (dd, mm) → "jul" — mês por extenso curto, igual ao rótulo de semana do mockup. */
function mesAbreviado(iso: string): string {
  const [, mes] = iso.split('-');
  return MESES_PT[mes] ?? mes;
}

function diaDeIso(iso: string): string {
  const [, , dia] = iso.split('-');
  return dia;
}

export function deSemanasDto(dtos: SemanaMovimentoDto[]): SemanaMovimento[] {
  return dtos.map((s) => ({
    id: s.numero,
    label: `${diaDeIso(s.inicio)}–${diaDeIso(s.fim)} ${mesAbreviado(s.fim)}${s.parcial ? '*' : ''}`,
    parcial: s.parcial,
    diasLabel: s.dias.map((d) => diaDeIso(d.dia)),
    entrouPorDiaCentavos: s.dias.map((d) => d.entradas.centavos),
    saiuPorDiaCentavos: s.dias.map((d) => d.saidas.centavos),
  }));
}

/** Em qual semana (bloco de `semanas`) cai a data (ISO) do movimento — 0 se nenhuma bater
 * (não deveria acontecer: o período do extrato e o das semanas vêm da mesma chamada do hook). */
function semanaIdDe(dataIso: string, semanas: SemanaMovimentoDto[]): number {
  const [dataParte] = dataIso.split('T');
  const semana = semanas.find((s) => dataParte >= s.inicio && dataParte <= s.fim);
  return semana?.numero ?? 0;
}

export function deMovimentosDto(dtos: MovimentoBancarioDto[], semanas: SemanaMovimentoDto[]): MovimentoExtrato[] {
  return dtos.map((m) => ({
    id: m.id,
    data: ddMmDeIso(m.data),
    descricao: m.descricao,
    forma: m.forma,
    contaId: m.contaBancariaCaixaId,
    valorCentavos: m.valor.centavos,
    status: m.conciliado ? 'conciliado' : 'pendente',
    semanaId: semanaIdDe(m.data, semanas),
  }));
}

export function deConciliacaoDto(dto: ConciliacaoBancariaDto): ConciliacaoBancaria {
  return {
    bateuCertinhoTotal: dto.bateuCertinhoTotal,
    bateuCertinhoAmostra: dto.bateuCertinhoAmostra.map((a) => ({ data: ddMmDeIso(a.data), descricao: a.descricao })),
    sobrouNoBanco: dto.sobrouNoBanco.map((item) => ({
      id: item.id,
      data: ddMmDeIso(item.data),
      descricao: item.descricao,
      valorCentavos: item.valor.centavos,
      sugestao: item.sugestao ?? 'Sem sugestão automática — confira manualmente.',
      rotuloAcaoPrimaria: 'Confirmar',
      rotuloAcaoSecundaria: 'Ignorar',
      idSugerido: item.idSugerido,
    })),
    sobrouNoSistema: dto.sobrouNoSistema.map((item) => ({
      id: item.id,
      data: ddMmDeIso(item.data),
      descricao: item.descricao,
      valorCentavos: item.valor.centavos,
      sugestao: item.sugestao ?? 'Sem sugestão automática — confira manualmente.',
      rotuloAcaoPrimaria: 'Confirmar',
      rotuloAcaoSecundaria: 'Ignorar',
      idSugerido: item.idSugerido,
    })),
  };
}

/** "0.0349" (fração) → "3,5" (percentual pt-BR, 1 casa) — o mesmo formato que
 * `SuperConsultorBancario.formatPercentPtBr` já espera receber pronto no `taxaLabel`. */
function formatPercentualDeFracao(fracao: number): string {
  return (fracao * 100).toFixed(1).replace('.', ',');
}

export function deTaxasPorFormaDto(dto: TaxasPorFormaDto): ConsultorBancarioInsight {
  const maiorTaxa = dto.porForma.reduce((max, p) => Math.max(max, p.taxaPercentual), 0);

  return {
    taxaTotalCentavos: dto.taxaTotal.centavos,
    percentualVolume: dto.percentualVolume,
    // Sem distinção "crédito parcelado" vs "à vista" no domínio hoje (TipoFormaPagamento não
    // diferencia) — a maior taxa cadastrada é o número real mais próximo do que o mockup mostrava.
    taxaCreditoParceladoPct: maiorTaxa * 100,
    porForma: dto.porForma.map((p) => ({
      forma: p.forma,
      valorCentavos: p.volume.centavos,
      taxaLabel: `${formatPercentualDeFracao(p.taxaPercentual)}%`,
      destaque: maiorTaxa > 0 && p.taxaPercentual === maiorTaxa,
    })),
  };
}
