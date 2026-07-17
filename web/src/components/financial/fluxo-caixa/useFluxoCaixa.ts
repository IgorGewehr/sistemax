import { useCallback, useEffect, useMemo, useState } from 'react';

import { deSessaoCaixaDto, maiorDestinoSangria } from '@/lib/api/adapters/financeiro/fluxoCaixa';
import { ApiError } from '@/lib/api/client';
import { financeiroApi } from '@/lib/api/financeiro';
import { addDays, todayIso } from '@/lib/date';
import { useToast } from '@/lib/toast';

import {
  calcularDiaCritico,
  calcularEstatisticasMes,
  calcularFaltasSobrasMes,
  calcularMediaDiferencaDia,
  calcularSangriasMes,
  diaSemanaPlural,
  operadorMaisFrequenteNoDia,
} from './calc';
import type { ConsultorInsightMock, DiaSemanaAbrev, SessaoCaixa, SessaoCaixaFechada } from './types';

export interface Recurso<T> {
  dado: T | null;
  erro: string | null;
  carregando: boolean;
}

function inicial<T>(): Recurso<T> {
  return { dado: null, erro: null, carregando: true };
}

function mensagemDeErro(e: unknown): string {
  return e instanceof ApiError ? e.message : 'Não foi possível carregar.';
}

const MESES_PT_MIN = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro'];

function periodoMesAtual(): { de: string; ate: string } {
  const hoje = todayIso();
  const [ano, mes] = hoje.split('-');
  const primeiroDoProximoMes = new Date(Number(ano), Number(mes), 1);
  const ultimoDoMes = addDays(`${primeiroDoProximoMes.getFullYear()}-${String(primeiroDoProximoMes.getMonth() + 1).padStart(2, '0')}-01`, -1);
  return { de: `${hoje.slice(0, 7)}-01`, ate: ultimoDoMes };
}

function periodoLabelDeIso(iso: string): string {
  const nome = MESES_PT_MIN[Number(iso.slice(5, 7)) - 1] ?? iso.slice(5, 7);
  return `${nome.charAt(0).toUpperCase()}${nome.slice(1)} ${iso.slice(0, 4)}`;
}

/** Deriva um id estável a partir do nome — o Bridge local não expõe identidade de usuário na
 * sessão (token opaco, ver `lib/auth.tsx`/`lib/api/client.ts`), então `OperadorId` do backend
 * (campo livre, não validado contra `Usuario` — ver `AbrirSessaoCaixaUseCase`) usa esse slug em vez
 * de um UUID real. Trocar por `usuarioId` de verdade assim que existir um endpoint "quem sou eu". */
function operadorIdDeNome(nome: string): string {
  const slug = nome
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/(^-|-$)/g, '');
  return slug || 'operador';
}

export interface BoardReal {
  sessaoHoje: SessaoCaixa | null;
  sessoesFechadas: SessaoCaixaFechada[];
}

/**
 * Fluxo de Caixa — o ritual do caixa físico REAL (`SessaoCaixa`, docs/wiring/
 * financeiro-telas-restantes.md §4, task #33). `board` junta `GET /financeiro/caixa/atual` (a
 * sessão aberta agora, se houver) + `GET /financeiro/caixa/historico` (mês corrente) num único
 * `Recurso<T>` porque as duas leituras compõem o mesmo conceito de tela (sessão de hoje × sessões
 * já fechadas) — mutuamente inconsistentes se carregadas com granularidade menor.
 */
export function useFluxoCaixa() {
  const { toast } = useToast();
  const [board, setBoard] = useState<Recurso<BoardReal>>(inicial);
  const [destinosSangria, setDestinosSangria] = useState<string[]>(['Cofre da loja']);
  const [enviandoAcao, setEnviandoAcao] = useState(false);

  const atual = periodoMesAtual();

  const carregar = useCallback(() => {
    setBoard(inicial());
    const hojeIso = todayIso();
    const hojeDia = Number(hojeIso.slice(8, 10));

    Promise.all([financeiroApi.caixaAtual(), financeiroApi.caixaHistorico(undefined, atual.de, atual.ate)])
      .then(([atualDto, historicoDtos]) => {
        const historicoMapeado = historicoDtos.map(deSessaoCaixaDto);
        const sessaoHojeFechada = historicoMapeado.find((s) => s.dia === hojeDia && s.status === 'fechado') ?? null;
        const sessaoHoje = atualDto ? deSessaoCaixaDto(atualDto) : sessaoHojeFechada;
        const sessoesFechadas = historicoMapeado.filter(
          (s): s is SessaoCaixaFechada => s.status === 'fechado' && s.dia !== hojeDia,
        );
        setBoard({ dado: { sessaoHoje, sessoesFechadas }, erro: null, carregando: false });
      })
      .catch((e: unknown) => setBoard({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .contasBancarias()
      .then((contas) => setDestinosSangria([...contas.map((c) => c.nome), 'Cofre da loja']))
      .catch(() => {
        // Sem contas bancárias pra sugerir não é erro do bloco principal — o select de sangria
        // continua com a opção fixa "Cofre da loja".
      });
  }, [atual.de, atual.ate]);

  useEffect(() => {
    carregar();
  }, [carregar]);

  const todasAsSessoes = useMemo<SessaoCaixa[]>(() => {
    if (!board.dado) return [];
    return board.dado.sessaoHoje ? [...board.dado.sessoesFechadas, board.dado.sessaoHoje] : board.dado.sessoesFechadas;
  }, [board.dado]);

  const estatisticasMes = useMemo(() => calcularEstatisticasMes(todasAsSessoes), [todasAsSessoes]);
  const sangriasMes = useMemo(() => calcularSangriasMes(todasAsSessoes), [todasAsSessoes]);
  const diaCritico = useMemo(() => calcularDiaCritico(todasAsSessoes), [todasAsSessoes]);
  const mediaDiferencaCentavos = useMemo(() => calcularMediaDiferencaDia(todasAsSessoes), [todasAsSessoes]);
  const maiorDestino = useMemo(() => maiorDestinoSangria(todasAsSessoes), [todasAsSessoes]);

  const consultorInsight = useMemo<ConsultorInsightMock>(() => {
    const { faltasCentavos, sobrasCentavos } = calcularFaltasSobrasMes(todasAsSessoes);
    const diaSemanaCritico = diaCritico?.diaSemana ?? null;
    const operadorCritico = diaSemanaCritico ? (operadorMaisFrequenteNoDia(todasAsSessoes, diaSemanaCritico) ?? '—') : '—';
    return {
      faltasMesCentavos: faltasCentavos,
      sobrasMesCentavos: sobrasCentavos,
      diaCriticoLabel: diaSemanaCritico ? diaSemanaPlural(diaSemanaCritico) : 'sem padrão ainda',
      diaCriticoMediaCentavos: diaCritico?.mediaCentavos ?? 0,
      operadorCritico,
      acaoLabel: diaSemanaCritico ? `Ver as ${diaSemanaPlural(diaSemanaCritico)} →` : 'Ver o mês →',
    };
  }, [todasAsSessoes, diaCritico]);

  // Sem endpoint de "total vendido" (Vendas/PDV) nesta tela — 0% é o valor REAL enquanto nenhuma
  // venda em espécie for registrada na sessão (nenhuma ação de "venda em espécie" existe na UI
  // ainda), não um placeholder inventado.
  const vendasEspeciePercentual = 0;

  async function abrirCaixa(aberturaCentavos: number, operadorNome: string) {
    setEnviandoAcao(true);
    try {
      await financeiroApi.abrirCaixa({ saldoAberturaCentavos: aberturaCentavos, operadorId: operadorIdDeNome(operadorNome), operadorNome });
      toast(`Caixa aberto por ${operadorNome}.`, 'success');
      carregar();
    } catch (e) {
      toast(mensagemDeErro(e), 'warning');
    } finally {
      setEnviandoAcao(false);
    }
  }

  async function registrarSuprimento(sessaoId: string, valorCentavos: number, origem: string) {
    const operador = board.dado?.sessaoHoje;
    if (!operador) return;
    setEnviandoAcao(true);
    try {
      await financeiroApi.caixaSuprimento({
        sessaoId,
        valorCentavos,
        motivo: origem,
        operadorId: operadorIdDeNome(operador.operador),
        operadorNome: operador.operador,
      });
      toast(`Suprimento registrado ← ${origem}.`, 'success');
      carregar();
    } catch (e) {
      toast(mensagemDeErro(e), 'warning');
    } finally {
      setEnviandoAcao(false);
    }
  }

  async function registrarSangria(sessaoId: string, valorCentavos: number, destino: string) {
    const operador = board.dado?.sessaoHoje;
    if (!operador) return;
    setEnviandoAcao(true);
    try {
      await financeiroApi.caixaSangria({
        sessaoId,
        valorCentavos,
        motivo: destino,
        operadorId: operadorIdDeNome(operador.operador),
        operadorNome: operador.operador,
      });
      toast(`Sangria registrada → ${destino}.`, 'success');
      carregar();
    } catch (e) {
      toast(mensagemDeErro(e), 'warning');
    } finally {
      setEnviandoAcao(false);
    }
  }

  async function fecharCaixa(sessaoId: string, contadoCentavos: number) {
    setEnviandoAcao(true);
    try {
      await financeiroApi.caixaFechar({ sessaoId, contadoCentavos });
      toast('Caixa fechado.', 'success');
      carregar();
    } catch (e) {
      toast(mensagemDeErro(e), 'warning');
    } finally {
      setEnviandoAcao(false);
    }
  }

  return {
    periodoLabel: periodoLabelDeIso(atual.de),
    board,
    todasAsSessoes,
    estatisticasMes,
    sangriasMes,
    sangriasMaiorDestino: maiorDestino,
    diaCritico,
    mediaDiferencaCentavos,
    consultorInsight,
    vendasEspeciePercentual,
    destinosSangria,
    enviandoAcao,
    abrirCaixa,
    registrarSangria,
    registrarSuprimento,
    fecharCaixa,
    recarregar: carregar,
  };
}

export type PulsingWeekday = DiaSemanaAbrev | null;
