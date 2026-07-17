/**
 * DTO (.NET, `Application.ReadModels` — centavos crus, sem `Money`) → fatia do view-model do
 * bloco Sobrevivência (`components/financial/visao-geral/sobrevivencia/types.ts`). Funções puras,
 * zero React — mesmo padrão de `adapters/financeiro/visaoGeral.ts`.
 */
import type {
  BreakevenCardData,
  FaixaInadimplenciaResumo,
  InadimplenciaCardData,
  RadarSimplesCardData,
  RunwayCardData,
} from '@/components/financial/visao-geral/sobrevivencia/types';
import type {
  FaixaDeAtrasoOrdinal,
  InadimplenciaDto,
  PontoDeEquilibrioDto,
  PrevisaoDeCaixaDto,
  RadarDoSimplesDto,
} from '@/lib/api/financeiro';
import { formatDateShort } from '@/lib/format';

export function deRunwayDto(dto: PrevisaoDeCaixaDto): RunwayCardData {
  return {
    diasRunwayRealista: dto.diasRunwayRealista,
    diasRunwayBruto: dto.diasRunwayBruto,
    probabilidadeSaldoNegativoEm30Dias: dto.probabilidadeSaldoNegativoEm30Dias,
    primeiroDiaP50NegativoLabel: dto.primeiroDiaP50Negativo ? formatDateShort(dto.primeiroDiaP50Negativo) : null,
  };
}

export function deBreakevenDto(dto: PontoDeEquilibrioDto): BreakevenCardData {
  const progresso =
    dto.receitaNecessariaMensalCentavos > 0
      ? Math.min(100, Math.round((dto.receitaAcumuladaNoMesCentavos / dto.receitaNecessariaMensalCentavos) * 100))
      : 0;

  return {
    receitaNecessariaMensalCentavos: dto.receitaNecessariaMensalCentavos,
    receitaNecessariaDiariaCentavos: dto.receitaNecessariaDiariaCentavos,
    receitaAcumuladaNoMesCentavos: dto.receitaAcumuladaNoMesCentavos,
    margemContribuicaoPercentual: dto.margemContribuicaoPercentual,
    diaDoEquilibrio: dto.diaDoEquilibrio,
    jaAtingiuNoMes: dto.jaAtingiuNoMes,
    progressoPercentual: progresso,
  };
}

/** Ordinal de `FaixaDeAtraso` (.NET) → rótulo em pt-BR — ver comentário do tipo no client. */
const FAIXA_LABELS: Record<FaixaDeAtrasoOrdinal, string> = {
  0: 'Em dia',
  1: 'Até 30 dias',
  2: '31–60 dias',
  3: '61–90 dias',
  4: '91–180 dias',
  5: 'Acima de 180 dias',
};

export function deInadimplenciaDto(dto: InadimplenciaDto): InadimplenciaCardData {
  const porFaixa: FaixaInadimplenciaResumo[] = dto.porFaixa
    .filter((f) => f.faixa !== 0) // "Em dia" não é inadimplência — não polui o resumo de risco.
    .map((f) => ({
      label: FAIXA_LABELS[f.faixa] ?? `Faixa ${f.faixa}`,
      valorCentavos: f.valorCentavos,
      quantidade: f.quantidade,
    }));

  return {
    valorTotalEmAbertoCentavos: dto.valorTotalEmAbertoCentavos,
    provisaoEsperadaCentavos: dto.provisaoEsperadaCentavos,
    valorLiquidoEsperadoCentavos: dto.valorLiquidoEsperadoCentavos,
    porFaixa,
  };
}

export function deRadarSimplesDto(dto: RadarDoSimplesDto): RadarSimplesCardData {
  return {
    rbt12Centavos: dto.rbt12Centavos,
    faixaAtual: dto.faixaAtual,
    aliquotaEfetiva: dto.aliquotaEfetiva,
    distanciaAoProximoDegrauCentavos: dto.distanciaAoProximoDegrauCentavos,
    mesesProjetadosAteOProximoDegrau: dto.mesesProjetadosAteOProximoDegrau,
  };
}
