import type { Centavos } from '@/lib/money';

/**
 * View-model do Fluxo de Caixa (`docs/ui/mockups/fluxo-de-caixa.html` — fonte da verdade).
 * O ritual do caixa em espécie: abertura → vendas em espécie → sangria(s) → troco → fechamento
 * cego. Hoje é mock local (mutável via `useState` no board); amanhã troca por API sem mudar a tela.
 */

export type DiaSemanaAbrev = 'Dom' | 'Seg' | 'Ter' | 'Qua' | 'Qui' | 'Sex' | 'Sáb';

/** Uma retirada de dinheiro da gaveta durante o turno. */
export interface SangriaEvento {
  hora: string; // "HH:MM"
  valorCentavos: Centavos;
  destino: string;
}

/** Um reforço de dinheiro colocado na gaveta durante o turno (contrapartida da sangria). */
export interface SuprimentoEvento {
  hora: string; // "HH:MM"
  valorCentavos: Centavos;
  origem: string;
}

interface SessaoCaixaBase {
  /** Id real da sessão (`SessaoCaixaDto.id`) — necessário pras ações (sangria/suprimento/fechar)
   * apontarem pro recurso certo no backend. Vazio (`''`) só no estado local anterior a qualquer
   * fetch. */
  id: string;
  /** Dia do mês (1-31) — o mês é sempre o `periodoLabel` corrente, não repetido por sessão. */
  dia: number;
  diaSemana: DiaSemanaAbrev;
  operador: string;
  horaAbertura: string; // "HH:MM"
  aberturaCentavos: Centavos;
  vendasEspecieCentavos: Centavos;
  /** Pode ter 0, 1 ou várias retiradas — a sessão de hoje ganha uma nova a cada "Nova sangria". */
  sangrias: SangriaEvento[];
  /** Pode ter 0, 1 ou vários reforços — a sessão de hoje ganha um novo a cada "Novo suprimento". */
  suprimentos: SuprimentoEvento[];
  trocoCentavos: Centavos;
}

/** Sessão ainda em curso — sem contagem, sem hora de fechamento. Só existe para "hoje". */
export interface SessaoCaixaAberta extends SessaoCaixaBase {
  status: 'aberto';
  horaFechamento: null;
  contadoCentavos: null;
}

/** Sessão encerrada — contagem cega feita, diferença apurável. */
export interface SessaoCaixaFechada extends SessaoCaixaBase {
  status: 'fechado';
  horaFechamento: string; // "HH:MM"
  contadoCentavos: Centavos;
}

/**
 * União discriminada por `status` — em vez de `horaFechamento`/`contadoCentavos` opcionais soltos.
 * Isso empurra o TypeScript a provar, em cada leitura, que a sessão já foi fechada antes de
 * acessar o valor contado (zero `!`/asserts espalhados pela tela).
 */
export type SessaoCaixa = SessaoCaixaAberta | SessaoCaixaFechada;

/** O insight fixo do Super Consultor desta tela — a IA só observa/explica (Lei 2), nunca age. */
export interface ConsultorInsightMock {
  faltasMesCentavos: Centavos;
  sobrasMesCentavos: Centavos;
  /** Nome do padrão apontado, ex.: "quintas à tarde". */
  diaCriticoLabel: string;
  diaCriticoMediaCentavos: Centavos;
  operadorCritico: string;
  /** Copy do link de drill (ex.: "Ver as quintas →") — navegação, não ação da IA. */
  acaoLabel: string;
}

/** Formato histórico (`FluxoCaixaData`) — não usado mais diretamente pela página (que hoje monta
 * o board a partir de `SessaoCaixa[]` reais via `useFluxoCaixa`), mantido como referência do
 * shape completo do mockup (período + insight + destinos de sangria). */
export interface FluxoCaixaData {
  /** Rótulo do seletor de período no cabeçalho (ex.: "Julho 2026") — apenas exibição, sem
   * dropdown funcional no mockup. */
  periodoLabel: string;
  /** Sessões já fechadas do mês, em ordem crescente de dia. */
  sessoesFechadas: SessaoCaixaFechada[];
  /** Estado inicial da sessão de hoje — pode ser mutado localmente (fechar caixa, nova sangria). */
  sessaoHojeInicial: SessaoCaixa;
  consultorInsight: ConsultorInsightMock;
  /** % do total vendido no mês que foi em espécie — o resto é cartão/PIX (módulo Bancário). */
  vendasEspeciePercentual: number;
  /** Opções do select "Foi para onde" no modal de sangria. */
  destinosSangria: string[];
}
