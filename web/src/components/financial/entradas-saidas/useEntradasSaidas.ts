import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import {
  calcularConsultorFornecedores,
  calcularKpisAberto,
  deContasBancariasParaDisponiveis,
  deExtratoLinhas,
  saldoAcumuladoAte,
} from '@/lib/api/adapters/financeiro/entradasSaidas';
import { ApiError } from '@/lib/api/client';
import { financeiroApi } from '@/lib/api/financeiro';
import { addDays, todayIso } from '@/lib/date';
import { reais } from '@/lib/money';
import { useToast } from '@/lib/toast';

import {
  atrasados30MaisDias,
  buildBarras,
  buildTimeline,
  CATEGORIA_MAP_LANCAMENTO_RAPIDO,
  categoriaDrillStats,
  fixoVariavelPct,
  insertLancamentoOrdenado,
  quemMaisSubiu,
  totalDespesasCentavos,
} from './calc';
import {
  CATEGORIAS_EXEMPLO,
  CATEGORIAS_LANCAMENTO_RAPIDO_EXEMPLO,
  CONTAS_DISPONIVEIS_EXEMPLO,
  MESES_HISTORICO_EXEMPLO,
  RESUMO_PDV_MES_EXEMPLO,
  SPARKLINE_RECEBER_EXEMPLO,
} from './exemplos';
import type {
  BridgeNoteData,
  CategoriaDespesaId,
  ConsultorFornecedoresData,
  ContaDisponivel,
  EntradasSaidasKpis,
  FiltroAtivo,
  LancamentoRow,
  NovoLancamentoInput,
  SegFiltro,
  TimelineEntry,
} from './types';

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

function periodoMesAnterior(atual: { de: string }): { de: string; ate: string } {
  const fimDoMesAnterior = addDays(atual.de, -1);
  return { de: `${fimDoMesAnterior.slice(0, 7)}-01`, ate: fimDoMesAnterior };
}

function nomeMesDeIso(iso: string): string {
  const mesIndex = Number(iso.slice(5, 7)) - 1;
  return MESES_PT_MIN[mesIndex] ?? iso.slice(5, 7);
}

/** "2026-07-01" → "Julho 2026" — mesmo rótulo do pill de período no `PageHeader`. */
function periodoLabelDeIso(iso: string): string {
  const nome = nomeMesDeIso(iso);
  return `${nome.charAt(0).toUpperCase()}${nome.slice(1)} ${iso.slice(0, 4)}`;
}

/** Horizonte "sem limite prático" das linhas em aberto/atrasado — mesmo racional de
 * `ContasEmAbertoService` (10 anos pra frente), mas também bem pro passado pra pegar histórico de
 * pagos suficiente pro Super Consultor de Fornecedores calcular uma média de vários meses. */
const HORIZONTE_ABERTO_DE = '2015-01-01';
function horizonteAbertoAte(): string {
  return addDays(todayIso(), 365 * 10);
}

/**
 * Toda a lógica/estado de "Entradas & saídas" vive aqui — a página (`EntradasSaidas.tsx`)
 * permanece fina. `timeline` e `kpis` são `Recurso<T>` independentes (um bloco quebrado não
 * derruba o outro, mesmo padrão de `useBancario`): `timeline` vem de um `GET /financeiro/extrato`
 * do mês corrente; `kpis` (aberto/atrasado/resultado/fechamento + o insight de Fornecedores) vem de
 * um extrato de horizonte largo + `relatorios/dre` (mês atual e anterior) + `fluxo` (saldo
 * projetado de fim de mês, reusado — nunca duplicado). "Para onde foi o dinheiro"/Raio-X do mês
 * continuam ilustrativos (`exemplos.ts`) — o domínio real ainda não agrupa categoria por 6 meses
 * (docs/wiring/financeiro-telas-restantes.md §1).
 */
export function useEntradasSaidas() {
  const { toast } = useToast();

  const [rows, setRows] = useState<LancamentoRow[]>([]);
  const [timeline, setTimeline] = useState<Recurso<null>>(inicial);
  const [kpis, setKpis] = useState<Recurso<EntradasSaidasKpis>>(inicial);
  const [bridge, setBridge] = useState<Recurso<BridgeNoteData>>(inicial);
  const [consultorFornecedores, setConsultorFornecedores] = useState<Recurso<ConsultorFornecedoresData>>(inicial);
  // Select de contas do modal — real via `GET /financeiro/contas-bancarias` (mesmo endpoint do
  // Bancário); cai pro exemplo ilustrativo só se a chamada falhar (não é um "número" exibido na
  // tela, é um select de formulário — sem `MockBadge` por não ser dado analítico).
  const [contasDisponiveis, setContasDisponiveis] = useState<ContaDisponivel[]>(CONTAS_DISPONIVEIS_EXEMPLO);

  const [segFiltro, setSegFiltro] = useState<SegFiltro>('tudo');
  const [filtroAtivo, setFiltroAtivo] = useState<FiltroAtivo | null>(null);
  const [categoriaSelecionadaId, setCategoriaSelecionadaId] = useState<CategoriaDespesaId | null>(null);
  const [cobradosIds, setCobradosIds] = useState<ReadonlySet<string>>(new Set());
  const [modalBaixaRowId, setModalBaixaRowId] = useState<string | null>(null);
  const [modalLancarAberto, setModalLancarAberto] = useState(false);
  const [modalDetalheRowId, setModalDetalheRowId] = useState<string | null>(null);
  const proximoIdRef = useRef(1);
  const analiseRef = useRef<HTMLDivElement | null>(null);

  const carregarTimeline = useCallback(() => {
    setTimeline(inicial());
    const atual = periodoMesAtual();
    financeiroApi
      .extrato(atual.de, atual.ate)
      .then((dto) => {
        setRows(deExtratoLinhas(dto.linhas));
        setTimeline({ dado: null, erro: null, carregando: false });
      })
      .catch((e: unknown) => setTimeline({ dado: null, erro: mensagemDeErro(e), carregando: false }));
  }, []);

  const carregarKpis = useCallback(() => {
    setKpis(inicial());
    setBridge(inicial());
    setConsultorFornecedores(inicial());

    const atual = periodoMesAtual();
    const anterior = periodoMesAnterior(atual);

    Promise.all([
      financeiroApi.extrato(HORIZONTE_ABERTO_DE, horizonteAbertoAte()),
      financeiroApi.relatoriosDre(atual.de, atual.ate),
      financeiroApi.relatoriosDre(anterior.de, anterior.ate),
      financeiroApi.fluxo(14, 45),
    ])
      .then(([abertoDto, dreAtualDto, dreAnteriorDto, fluxoDto]) => {
        const linhasHorizonte = deExtratoLinhas(abertoDto.linhas);
        const kpisAberto = calcularKpisAberto(linhasHorizonte);

        const resultadoMesCentavos = dreAtualDto.resultadoOperacional.centavos;
        const resultadoAnteriorCentavos = dreAnteriorDto.resultadoOperacional.centavos;
        const resultadoDeltaPct =
          resultadoAnteriorCentavos !== 0
            ? Math.round(((resultadoMesCentavos - resultadoAnteriorCentavos) / Math.abs(resultadoAnteriorCentavos)) * 100)
            : 0;

        const fechamentoCaixaCentavos = saldoAcumuladoAte(fluxoDto.pontos, atual.ate);

        const kpisReais: EntradasSaidasKpis = {
          ...kpisAberto,
          sparklineReceber: SPARKLINE_RECEBER_EXEMPLO,
          resultadoMesCentavos,
          resultadoDeltaPct,
          resultadoComparadoMes: nomeMesDeIso(anterior.de),
          fechamentoCaixaCentavos,
        };
        setKpis({ dado: kpisReais, erro: null, carregando: false });
        setBridge({
          dado: {
            resultadoCentavos: resultadoMesCentavos,
            caixaCentavos: fechamentoCaixaCentavos,
            diferimentoCentavos: resultadoMesCentavos - fechamentoCaixaCentavos,
          },
          erro: null,
          carregando: false,
        });
        setConsultorFornecedores({ dado: calcularConsultorFornecedores(linhasHorizonte, atual.de), erro: null, carregando: false });
      })
      .catch((e: unknown) => {
        const erro = mensagemDeErro(e);
        setKpis({ dado: null, erro, carregando: false });
        setBridge({ dado: null, erro, carregando: false });
        setConsultorFornecedores({ dado: null, erro, carregando: false });
      });
  }, []);

  useEffect(() => {
    carregarTimeline();
    carregarKpis();
    financeiroApi
      .contasBancarias()
      .then((dtos) => {
        const contas = deContasBancariasParaDisponiveis(dtos);
        if (contas.length > 0) setContasDisponiveis(contas);
      })
      .catch(() => {
        // Mantém o fallback ilustrativo — não é um KPI, é um select de formulário.
      });
  }, [carregarTimeline, carregarKpis]);

  const totalDespesas = useMemo(() => totalDespesasCentavos(CATEGORIAS_EXEMPLO), []);
  const barras = useMemo(() => buildBarras(CATEGORIAS_EXEMPLO), []);
  const fixoVariavel = useMemo(() => fixoVariavelPct(CATEGORIAS_EXEMPLO), []);
  const liderAlta = useMemo(() => quemMaisSubiu(CATEGORIAS_EXEMPLO), []);
  const atrasados30 = useMemo(() => atrasados30MaisDias(rows), [rows]);

  const categoriaSelecionada = useMemo(
    () => (categoriaSelecionadaId ? (CATEGORIAS_EXEMPLO.find((c) => c.id === categoriaSelecionadaId) ?? null) : null),
    [categoriaSelecionadaId],
  );
  const categoriaDrill = useMemo(
    () => (categoriaSelecionada ? categoriaDrillStats(categoriaSelecionada, totalDespesas) : null),
    [categoriaSelecionada, totalDespesas],
  );

  const timelineEntries = useMemo<TimelineEntry[]>(() => buildTimeline(rows, segFiltro, filtroAtivo), [rows, segFiltro, filtroAtivo]);
  const totalAtrasados = useMemo(() => rows.filter((r) => r.status === 'atrasado').length, [rows]);
  const hintLinhaDoTempo = `${rows.length} lançamentos · ${totalAtrasados} atrasados`;

  function aplicarFiltro(filtro: FiltroAtivo | null) {
    setFiltroAtivo(filtro);
    analiseRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }
  function limparFiltro() {
    aplicarFiltro(null);
  }
  function selecionarCategoria(id: CategoriaDespesaId | null) {
    setCategoriaSelecionadaId(id);
  }

  function abrirBaixa(rowId: string) {
    setModalBaixaRowId(rowId);
  }
  function fecharBaixa() {
    setModalBaixaRowId(null);
  }
  function confirmarBaixa(rowId: string, conta: string) {
    setRows((prev) =>
      prev.map((r): LancamentoRow => (r.id === rowId ? { ...r, status: 'pago', conta, origem: 'Baixa manual', diasAtraso: undefined } : r)),
    );
    fecharBaixa();
    const destino = conta === 'Caixa da loja' ? 'Fluxo de Caixa' : 'Bancário';
    toast(`✓ Baixado em ${conta} — já aparece em ${destino}.`, 'success');
  }

  function cobrar(rowId: string) {
    const row = rows.find((r) => r.id === rowId);
    if (!row) return;
    setCobradosIds((prev) => new Set(prev).add(rowId));
    toast(`Cobrança enviada por WhatsApp para ${row.desc}.`, 'success');
  }

  function abrirLancar() {
    setModalLancarAberto(true);
  }
  function fecharLancar() {
    setModalLancarAberto(false);
  }
  function salvarLancamento(input: NovoLancamentoInput) {
    if (!input.descricao.trim() || !(input.valorReais > 0) || !input.vencimento) {
      toast('Preencha descrição, valor e vencimento.', 'warning');
      return;
    }
    const novo: LancamentoRow = {
      id: `local-${proximoIdRef.current++}`,
      data: input.vencimento,
      desc: input.descricao.trim(),
      sub: input.recorrente ? 'lançamento recorrente' : null,
      categoria: CATEGORIA_MAP_LANCAMENTO_RAPIDO[input.categoriaLabel] ?? 'servicos',
      tipo: input.tipo,
      status: 'previsto',
      valorCentavos: reais(input.valorReais),
    };
    setRows((prev) => insertLancamentoOrdenado(prev, novo));
    fecharLancar();
    toast('Lançamento criado ✓ — apareceu na linha do tempo.', 'success');
  }

  function abrirDetalhe(rowId: string) {
    setModalDetalheRowId(rowId);
  }
  function fecharDetalhe() {
    setModalDetalheRowId(null);
  }

  function verExtratoCompleto() {
    toast('Levaria para Bancário → extrato do período.', 'info');
  }

  return {
    periodoLabel: periodoLabelDeIso(periodoMesAtual().de),
    timelineCarregando: timeline.carregando,
    timelineErro: timeline.erro,
    kpis,
    bridge,
    consultorFornecedores,
    resumoPdvMes: RESUMO_PDV_MES_EXEMPLO,
    mesesHistorico: MESES_HISTORICO_EXEMPLO,
    // REAL — GET /financeiro/contas-bancarias (fallback ilustrativo só se a chamada falhar).
    contasDisponiveis,
    categoriasLancamentoRapido: CATEGORIAS_LANCAMENTO_RAPIDO_EXEMPLO,

    barras,
    fixoVariavel,
    liderAlta,
    atrasados30,
    categoriaSelecionada,
    categoriaDrill,
    selecionarCategoria,
    analiseRef,

    segFiltro,
    setSegFiltro,
    filtroAtivo,
    limparFiltro,

    timeline: timelineEntries,
    hintLinhaDoTempo,
    cobradosIds,

    modalBaixa: { aberto: modalBaixaRowId !== null, row: rows.find((r) => r.id === modalBaixaRowId) ?? null },
    abrirBaixa,
    fecharBaixa,
    confirmarBaixa,
    cobrar,

    modalLancarAberto,
    abrirLancar,
    fecharLancar,
    salvarLancamento,

    modalDetalhe: { aberto: modalDetalheRowId !== null, row: rows.find((r) => r.id === modalDetalheRowId) ?? null },
    abrirDetalhe,
    fecharDetalhe,

    verExtratoCompleto,
    onClickConsultorFornecedores: () => aplicarFiltro({ type: 'categoria', value: 'cmv-fornecedor', label: 'Fornecedores' }),
    onClickAtrasadosTile: () => aplicarFiltro({ type: 'status', value: 'atrasado', label: 'Atrasados' }),
  };
}
