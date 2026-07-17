/**
 * DTO (.NET, `Abstractions.Consultor` — `frase` narrada + `facts` string→string + `drill`) → VM do
 * bloco ⑤ "Super Consultor" (`components/financial/visao-geral/types.ts`). Função pura, zero React —
 * mesmo padrão de `adapters/financeiro/visaoGeral.ts` e `.../sobrevivencia.ts`.
 *
 * O backend já narra E rankeia (ver `ConsultorService`/`ConsultorRanking`): a lista chega na ordem
 * de prioridade, então aqui NÃO reordenamos — só mapeamos 1:1, preservando a ordem.
 */
import type {
  ConsultorViewModel,
  FatoConsultor,
  FinanceiroRoute,
  InsightConsultor,
} from '@/components/financial/visao-geral/types';
import type { ConsultorInsightDto } from '@/lib/api/financeiro';

/**
 * Slot de tela do backend (`ConsultorFato.Tela`/`DrillTarget.Tela`) → rota real do Financeiro +
 * rótulo pt-BR para a mensagem de contexto do drill. `visao-geral` é a própria tela (sem drill), e
 * qualquer slot desconhecido cai em "sem drill" — nunca navega pra uma rota inventada.
 */
const TELA_PARA_ROTA: Record<string, { rota: FinanceiroRoute; label: string }> = {
  'fluxo-caixa': { rota: '/financeiro/fluxo-de-caixa', label: 'Fluxo de caixa' },
  recorrentes: { rota: '/financeiro/recorrentes', label: 'Recorrentes' },
  'entradas-saidas': { rota: '/financeiro/entradas-saidas', label: 'Entradas & Saídas' },
  relatorios: { rota: '/financeiro/relatorios', label: 'Relatórios' },
  bancario: { rota: '/financeiro/bancario', label: 'Bancário' },
};

/**
 * Chaves de `facts` (identificadores camelCase que o `FinanceiroConsultorFactProvider` emite) →
 * rótulo humano pt-BR. Chave desconhecida cai no próprio identificador — um `facts` de uma regra
 * futura ainda renderiza (menos bonito), nunca some nem quebra.
 */
const ROTULO_FATO: Record<string, string> = {
  runway: 'Situação do caixa',
  runwayDias: 'Dias de fôlego',
  runwayOrigem: 'Base do cálculo',
  probabilidadeSaldoNegativo30d: 'Chance de caixa negativo em 30 dias',
  primeiroDiaNegativoProvavel: 'Primeiro dia negativo provável',
  margemContribuicao: 'Margem de contribuição',
  diaDoEquilibrio: 'Dia do ponto de equilíbrio',
  custosFixosMensais: 'Custos fixos mensais',
  receitaNecessariaDiaria: 'Receita necessária por dia',
  valorEmAberto: 'Valor em aberto',
  provisaoEsperada: 'Perda esperada por atraso',
  valorLiquidoEsperado: 'Valor líquido esperado',
  aliquotaEfetiva: 'Alíquota efetiva',
  faixaAtual: 'Faixa atual',
  mesesAteProximaFaixa: 'Meses até a próxima faixa',
  contaAPagarDescricao: 'Conta a pagar',
  contaAPagarValor: 'Valor a pagar',
  contaAPagarVencimento: 'Vence em',
  saldoAtual: 'Saldo atual',
  contaAReceberDescricao: 'Recebimento previsto',
  contaAReceberValor: 'Valor a receber',
  contaAReceberVencimento: 'Recebimento em',
};

function fatosDe(facts: Record<string, string>): FatoConsultor[] {
  return Object.entries(facts).map(([chave, valor]) => ({
    label: ROTULO_FATO[chave] ?? chave,
    valor,
  }));
}

function drillDe(dto: ConsultorInsightDto): InsightConsultor['drill'] {
  const slot = dto.drill?.tela ?? dto.tela;
  const destino = TELA_PARA_ROTA[slot];
  if (!destino) return null; // `visao-geral` (já estamos aqui) ou slot desconhecido.
  return { rota: destino.rota, mensagem: `→ ${destino.label} — ver de onde vem esse número` };
}

export function deConsultorDtos(dtos: ConsultorInsightDto[]): ConsultorViewModel {
  const insights: InsightConsultor[] = dtos.map((dto) => ({
    id: `${dto.modulo}:${dto.ruleId}`,
    frase: dto.frase,
    fatos: fatosDe(dto.facts),
    drill: drillDe(dto),
  }));
  return { insights };
}
