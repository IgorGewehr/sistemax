/**
 * DTO (.NET, `Money`) → resumo agregado REAL da lente Assinaturas (`GET
 * /financeiro/receita-recorrente`). Função pura, zero React — mesmo padrão de
 * `adapters/financeiro/visaoGeral.ts`/`sobrevivencia.ts`.
 *
 * Só mapeia os agregados que o endpoint já devolve HOJE (mrr/arr/assinaturas ativas/ticket
 * médio/churn do mês/maior concentração). NÃO tenta reconstruir `AssinaturaServico[]` completo
 * (`RecorrentesViewModel.assinaturas.servicos`) — esse detalhamento por serviço (churn/novos por
 * mês, LTV, retenção) não existe como read-model ainda, e os ids de serviço do mock
 * (`mocks/financeiro/recorrentes.ts`, ex. "tensorroot") não correspondem aos ids reais do
 * `DemoSeeder` (ex. "servicepro", "gestao-raiz", "brain") — misturar um `maiorConcentracao` real
 * com uma busca por id no array mock quebraria o drill (`servicos.find(s => s.id === ...)`
 * voltando `undefined`). Ver docs/wiring/financeiro-api-contract.md §5.
 */
import type { ContaFixaResumoDto, RecorrenteDetalheDto, ReceitaRecorrenteDto } from '@/lib/api/financeiro';
import type { Centavos } from '@/lib/money';

export interface ResumoRealAssinaturas {
  mrrCentavos: Centavos;
  arrCentavos: Centavos;
  assinaturasAtivasCount: number;
  ticketMedioCentavos: Centavos;
  churnMesCentavos: Centavos;
  clientesChurnNoMes: number;
  churnPercent: number;
  maiorConcentracaoNome: string | null;
  maiorConcentracaoPercentual: number | null;
}

export function deReceitaRecorrenteDto(dto: ReceitaRecorrenteDto): ResumoRealAssinaturas {
  return {
    mrrCentavos: dto.mrr.centavos,
    arrCentavos: dto.arr.centavos,
    assinaturasAtivasCount: dto.assinaturasAtivas,
    ticketMedioCentavos: dto.ticketMedio.centavos,
    churnMesCentavos: dto.mrrChurnNoMes.centavos,
    clientesChurnNoMes: dto.clientesChurnNoMes,
    churnPercent: dto.churnPercent,
    maiorConcentracaoNome: dto.maiorConcentracao?.servicoNome ?? null,
    maiorConcentracaoPercentual: dto.maiorConcentracao?.percentual ?? null,
  };
}

/** "2026-08-20T00:00:00-03:00" ou "2026-08-20" → "20/08". Nunca `new Date(iso)` — extração
 * textual (mesma diretriz de `adapters/financeiro/bancario.ts`). */
function ddMmDeIso(iso: string): string {
  const [dataParte] = iso.split('T');
  const [, mes, dia] = dataParte.split('-');
  return `${dia}/${mes}`;
}

/** Linha nominal REAL de "Todas as assinaturas" — `GET /financeiro/recorrentes/detalhe`
 * (`AssinaturaDetalheService`, docs/wiring/financeiro-telas-restantes.md §2/§C). Só assinaturas
 * ATIVAS (o read-model não lista canceladas); `status` é o enum `.ToString()` do domínio, sem
 * distinção rica "atrasada Nd"/"risco de churn" do mockup (não modelada no backend hoje). */
export interface AssinaturaDetalheReal {
  id: string;
  servicoNome: string;
  clienteNome: string;
  valorPorCicloCentavos: Centavos;
  ciclo: string;
  proximaCobrancaLabel: string;
  status: string;
}

export function deRecorrentesDetalheDto(dtos: RecorrenteDetalheDto[]): AssinaturaDetalheReal[] {
  return dtos.map((a) => ({
    id: a.id,
    servicoNome: a.servicoNome,
    clienteNome: a.clienteNome,
    valorPorCicloCentavos: a.valorPorCiclo.centavos,
    ciclo: a.ciclo,
    proximaCobrancaLabel: ddMmDeIso(a.proximaCobranca),
    status: a.status,
  }));
}

/** Template REAL de recorrência ativa — `GET /financeiro/recorrentes/fixas`
 * (`ContasFixasService`). SÓ o template (valor previsto/dia fixo/próxima) — sem histórico de 12
 * meses/variação/`emAlerta` (fora de escopo do read-model, ver XML doc do serviço .NET). */
export interface ContaFixaResumoReal {
  id: string;
  descricao: string;
  categoriaId: string;
  valorPrevistoCentavos: Centavos;
  diaFixo: number | null;
  frequencia: string;
  tipo: string;
  proximaOcorrenciaLabel: string;
}

export function deRecorrentesFixasDto(dtos: ContaFixaResumoDto[]): ContaFixaResumoReal[] {
  return dtos.map((c) => ({
    id: c.id,
    descricao: c.descricao,
    categoriaId: c.categoriaId,
    valorPrevistoCentavos: c.valorPrevisto.centavos,
    diaFixo: c.diaFixo,
    frequencia: c.frequencia,
    tipo: c.tipo,
    proximaOcorrenciaLabel: c.proximaOcorrencia ? ddMmDeIso(c.proximaOcorrencia) : '—',
  }));
}
