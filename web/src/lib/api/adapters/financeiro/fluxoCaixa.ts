/**
 * DTO (.NET, `SessaoCaixaDto`) → `SessaoCaixa` (view-model existente de
 * `components/financial/fluxo-caixa/types.ts` — mantido 1:1 pra reaproveitar toda a UI já
 * construída: `calc.ts`/`KpisSection`/`SessaoHojeFormula`/`SessoesTable`/`AnaliseInterativa`/etc.
 * seguem inalterados). Ver docs/wiring/financeiro-telas-restantes.md §4 (task #33).
 *
 * `suprimentos` mapeia 1:1 os movimentos `tipo: 'suprimento'` do domínio (contrapartida exata da
 * `sangrias`, que já mapeava `tipo: 'sangria'`) — ação "Novo suprimento" wired na UI via
 * `financeiroApi.caixaSuprimento` (ver `useFluxoCaixa.ts`). `trocoCentavos` continua em 0: é um
 * conceito só do mock antigo ("troco fornecido ao cliente"), sem equivalente no domínio real
 * (`SessaoCaixa.SaldoEsperado = abertura + entradas − saídas`, entradas = suprimento +
 * vendaEmEspecie), e nenhuma ação de UI o popula.
 */
import type { DiaSemanaAbrev, SangriaEvento, SessaoCaixa, SuprimentoEvento } from '@/components/financial/fluxo-caixa/types';
import type { MovimentoSessaoCaixaDto, SessaoCaixaDto } from '@/lib/api/financeiro';
import { safeDate } from '@/lib/format';

const DIAS_SEMANA: DiaSemanaAbrev[] = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];

function pad2(n: number): string {
  return String(n).padStart(2, '0');
}

function horaHHMM(iso: string | null): string {
  const d = safeDate(iso);
  return d ? `${pad2(d.getHours())}:${pad2(d.getMinutes())}` : '--:--';
}

function diaDoMes(iso: string): number {
  return safeDate(iso)?.getDate() ?? 0;
}

function diaSemanaAbrev(iso: string): DiaSemanaAbrev {
  const d = safeDate(iso);
  return d ? DIAS_SEMANA[d.getDay()] : 'Dom';
}

function somaPorTipo(movimentos: MovimentoSessaoCaixaDto[], tipo: MovimentoSessaoCaixaDto['tipo']): number {
  return movimentos.filter((m) => m.tipo === tipo).reduce((acc, m) => acc + m.valorCentavos, 0);
}

export function deSessaoCaixaDto(dto: SessaoCaixaDto): SessaoCaixa {
  const sangrias: SangriaEvento[] = dto.movimentos
    .filter((m) => m.tipo === 'sangria')
    .map((m) => ({ hora: horaHHMM(m.registradoEm), valorCentavos: m.valorCentavos, destino: m.motivo ?? '—' }));

  const suprimentos: SuprimentoEvento[] = dto.movimentos
    .filter((m) => m.tipo === 'suprimento')
    .map((m) => ({ hora: horaHHMM(m.registradoEm), valorCentavos: m.valorCentavos, origem: m.motivo ?? '—' }));

  const base = {
    id: dto.id,
    dia: diaDoMes(dto.abertaEm),
    diaSemana: diaSemanaAbrev(dto.abertaEm),
    operador: dto.operadorNome,
    horaAbertura: horaHHMM(dto.abertaEm),
    aberturaCentavos: dto.saldoAberturaCentavos,
    vendasEspecieCentavos: somaPorTipo(dto.movimentos, 'vendaEmEspecie'),
    sangrias,
    suprimentos,
    trocoCentavos: 0,
  };

  if (dto.status === 'Fechada') {
    return {
      ...base,
      status: 'fechado',
      horaFechamento: horaHHMM(dto.fechadaEm),
      contadoCentavos: dto.saldoInformadoCentavos ?? 0,
    };
  }

  return { ...base, status: 'aberto', horaFechamento: null, contadoCentavos: null };
}

/** Destino informado (submetido como `Motivo`, ver `useFluxoCaixa.ts`) com a maior soma retirada
 * no mês — alimenta o rodapé do KPI "Sangrias do mês" ("maior parte → X"), sempre derivado das
 * sangrias reais, nunca hardcoded. */
export function maiorDestinoSangria(todasAsSessoes: SessaoCaixa[]): string | null {
  const totais = new Map<string, number>();
  todasAsSessoes.forEach((s) => s.sangrias.forEach((sg) => totais.set(sg.destino, (totais.get(sg.destino) ?? 0) + sg.valorCentavos)));
  let maior: string | null = null;
  let maiorValor = 0;
  totais.forEach((valor, destino) => {
    if (valor > maiorValor) {
      maior = destino;
      maiorValor = valor;
    }
  });
  return maior;
}
