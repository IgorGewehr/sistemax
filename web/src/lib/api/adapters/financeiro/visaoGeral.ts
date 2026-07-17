/**
 * DTO (.NET, `Money`/camelCase) → fatia do `VisaoGeralViewModel` (SDD em
 * `components/financial/visao-geral/types.ts`). Função pura, zero React — ver o padrão em
 * `docs/wiring/financeiro-api-contract.md` §9.1. Só os 2 blocos com read-model real hoje:
 * `disponivel` (`QuantoSobrouDeVerdadeService`) e `timeline` (`FluxoDeCaixaService`).
 */
import type { DisponivelViewModel, TimelineViewModel } from '@/components/financial/visao-geral/types';
import type { DisponivelParaRetiradaDto, FluxoDeCaixaDto } from '@/lib/api/financeiro';

/** "Quanto sobrou de verdade" — hoje é 30 dias fixo de contas a pagar, sem imposto (o próprio
 * XML doc do serviço .NET admite: "fórmula do MVP, REDUZIDA"). O sublabel do mockup original
 * prometia "(15 dias + imposto)" — trocado aqui pra não afirmar o que o dado não confere (ver
 * docs/wiring/financeiro-api-contract.md §3, nota de rodapé). */
export function deDisponivelDto(dto: DisponivelParaRetiradaDto): DisponivelViewModel {
  return {
    livreDeVerdadeCentavos: dto.podeTirar.centavos,
    noBancoEGaveta: {
      label: 'No banco e na gaveta hoje',
      valorCentavos: dto.saldoEmCaixa.centavos,
      tone: 'pos',
      arrowLabel: 'Bancário →',
      drill: { rota: '/financeiro/bancario', mensagem: '→ Bancário — saldo por conta (banco + gaveta)' },
    },
    jaTemDono: {
      label: 'Já tem dono',
      sublabel: '(30 dias)',
      valorCentavos: -dto.jaTemDono.centavos,
      tone: 'crit',
      arrowLabel: 'E&S →',
      drill: {
        rota: '/financeiro/entradas-saidas',
        mensagem: '→ Entradas & Saídas — contas a pagar nos próximos 30 dias',
      },
    },
  };
}

/** Índice do último ponto REALIZADO (não projetado) — é "hoje" na timeline. */
function hojeIndexDe(pontos: FluxoDeCaixaDto['pontos']): number {
  let idx = 0;
  for (let i = 0; i < pontos.length; i++) {
    if (!pontos[i].projetado) idx = i;
  }
  return idx;
}

/**
 * `eventosPorDia` fica vazio de propósito: `PontoFluxoCaixa` (.NET) não carrega descrição de
 * origem por dia (precisaria juntar com `ContaAPagar.Descricao`/`ContaAReceber.Descricao` por
 * vencimento — read-model que ainda não existe, ver contrato §3). Preencher com os textos do mock
 * ("Aluguel", "Folha de pagamento"...) por cima de datas REAIS seria inventar dado — o tooltip cai
 * pro texto genérico "Sem vencimento grande" do componente em vez de mentir uma origem.
 */
export function deTimelineDto(dto: FluxoDeCaixaDto): TimelineViewModel {
  const valoresDiarios = dto.pontos.map((p) => p.saldoAcumulado.centavos);
  const datasISO = dto.pontos.map((p) => p.data);
  const hojeIndex = hojeIndexDe(dto.pontos);
  const mesLabel = (datasISO[hojeIndex] ?? datasISO[0] ?? '').split('-')[1] ?? '';

  return { valoresDiarios, hojeIndex, eventosPorDia: {}, mesLabel, datasISO };
}
