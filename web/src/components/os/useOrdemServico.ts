import { useMemo, useState } from 'react';

import { formatCentavos, type Centavos } from '@/lib/money';
import { useToast } from '@/lib/toast';
import { OS_MOCK } from '@/mocks/os';

import {
  HOJE,
  acaoPrimaria as acaoPrimariaDe,
  bucketDados,
  bucketDrillStats,
  bucketPorChave,
  buildConsultorInsight,
  buildKpis,
  ehTerminal,
  filtrarFila,
  operacaoStats,
  totalExecucaoAtual,
  totalOrcamento,
  uid,
} from './calc';
import type { BucketKey, CanalResposta, FiltroFila, FormaPagamento, OrdemServico, OsStatus, PecaOrcada } from './types';

/** Erro de validação inline — mesmo texto (com o código do invariante) que o mockup mostra em `#cur-erro`. */
export type AcaoErro = string | null;

/**
 * Toda a lógica/estado de "Ordem de Serviço" vive aqui — as telas (`OrdemServicoLista`,
 * `OrdemServicoDetalhe`) permanecem finas, só compondo seções a partir do que este hook devolve.
 * As mutações reproduzem exatamente as guardas e mensagens do agregado `OrdemDeServico` (C#) que
 * o mockup simula em JS — inclusive os textos de erro com o código do invariante.
 */
export function useOrdemServico() {
  const { toast } = useToast();
  const [lista, setLista] = useState<OrdemServico[]>(OS_MOCK);

  const [tela, setTela] = useState<'lista' | 'detalhe'>('lista');
  const [numeroSelecionado, setNumeroSelecionado] = useState<string | null>(null);
  const [filtroFila, setFiltroFila] = useState<FiltroFila>('ativas');
  const [buscaFila, setBuscaFila] = useState('');
  const [etapaDrillAtual, setEtapaDrillAtual] = useState<BucketKey | null>(null);

  function irParaDetalhe(numero: string) {
    setNumeroSelecionado(numero);
    setTela('detalhe');
  }
  function voltarParaLista() {
    setTela('lista');
    setNumeroSelecionado(null);
  }

  function atualizar(numero: string, aplicar: (os: OrdemServico) => OrdemServico) {
    setLista((prev) => prev.map((o) => (o.numero === numero ? aplicar(o) : o)));
  }

  // ── Ações rápidas (linha da fila e corpo do passo atual chamam as mesmas) ──

  function iniciarExecucao(numero: string) {
    atualizar(numero, (o) => ({
      ...o,
      status: 'EmExecucao' as OsStatus,
      maoDeObraFinal: o.orcamento!.maoDeObra,
      pecasExecucao: o.orcamento!.pecas.map((p, i) => ({ ...p, linhaId: 'l' + i, origem: 'orcada' as const, aplicada: false })),
      historico: [...o.historico, { para: 'EmExecucao' as OsStatus, em: HOJE }],
    }));
    toast(`${numero} · execução iniciada — peças reservadas baixadas para a bancada.`);
  }

  function concluirExecucao(numero: string) {
    atualizar(numero, (o) => ({ ...o, status: 'Pronta' as OsStatus, historico: [...o.historico, { para: 'Pronta' as OsStatus, em: HOJE }] }));
    toast(`${numero} · pronta para retirada.`);
  }

  // ── Ações do passo atual (chamadas pelos corpos da linha do tempo) ──

  function registrarDiagnostico(numero: string, input: { tecnico: string; diagnostico: string; previsaoTxt: string }): AcaoErro {
    const tecnico = input.tecnico.trim();
    const diagnostico = input.diagnostico.trim();
    if (!tecnico) return 'os.tecnico_obrigatorio — atribua um técnico antes de registrar o diagnóstico.';
    if (!diagnostico) return 'os.diagnostico_obrigatorio — diagnóstico não pode ser vazio.';

    let prazo: Date | null = null;
    const previsaoTxt = input.previsaoTxt.trim();
    if (previsaoTxt) {
      const [d, m, a] = previsaoTxt.split('/').map(Number);
      if (d && m && a) prazo = new Date(a, m - 1, d);
    }

    atualizar(numero, (o) => ({
      ...o,
      tecnico,
      diagnostico,
      prazo: prazo ?? o.prazo,
      status: 'EmDiagnostico' as OsStatus,
      historico: [...o.historico, { para: 'EmDiagnostico' as OsStatus, em: HOJE }],
    }));
    toast(`${numero} · diagnóstico registrado.`);
    return null;
  }

  function enviarOrcamento(
    numero: string,
    input: { desc: string; qtdTxt: string; precoTxt: string; maoTxt: string; validadeTxt: string },
  ): AcaoErro {
    const validade = parseInt(input.validadeTxt, 10);
    if (!validade || validade <= 0) return 'os.validade_invalida — validade deve ser maior que zero dias.';
    if (input.maoTxt === '' || parseFloat(input.maoTxt) < 0) return 'os.mao_de_obra_invalida — mão de obra não pode ser negativa.';

    const desc = input.desc.trim();
    const pecas: PecaOrcada[] = [];
    if (desc) {
      const qtd = parseInt(input.qtdTxt, 10);
      if (!qtd || qtd <= 0) return 'os.quantidade_invalida — quantidade de peça prevista deve ser maior que zero.';
      pecas.push({
        desc,
        produtoId: 'prod-' + desc.toLowerCase().replace(/\s+/g, '-').slice(0, 20),
        qtd,
        preco: Math.round(parseFloat(input.precoTxt || '0') * 100),
      });
    }

    const maoDeObra = Math.round(parseFloat(input.maoTxt) * 100);
    atualizar(numero, (o) => ({
      ...o,
      orcamento: { pecas, maoDeObra, validadeDias: validade, enviadoEm: HOJE },
      status: 'AguardandoAprovacao' as OsStatus,
      historico: [...o.historico, { para: 'AguardandoAprovacao' as OsStatus, em: HOJE }],
    }));
    toast(`${numero} · orçamento enviado — ${formatCentavos(totalOrcamento({ pecas, maoDeObra, validadeDias: validade, enviadoEm: HOJE }))}.`);
    return null;
  }

  function decidir(numero: string, decisao: 'Aprovada' | 'Reprovada', canal: CanalResposta | null, motivo: string): AcaoErro {
    if (!canal) return 'Selecione o canal pelo qual o cliente respondeu.';

    if (decisao === 'Aprovada') {
      atualizar(numero, (o) => ({
        ...o,
        aprovacao: { decisao, canal, em: HOJE },
        status: 'Aprovada' as OsStatus,
        historico: [...o.historico, { para: 'Aprovada' as OsStatus, em: HOJE }],
      }));
      toast(`${numero} · aprovada — peças de catálogo reservadas no estoque.`);
    } else {
      atualizar(numero, (o) => ({
        ...o,
        aprovacao: { decisao, canal, em: HOJE },
        motivoReprovacao: motivo.trim() || null,
        status: 'Reprovada' as OsStatus,
        historico: [...o.historico, { para: 'Reprovada' as OsStatus, em: HOJE }],
      }));
      toast(`${numero} · reprovada pelo cliente.`);
    }
    return null;
  }

  function aplicarPeca(numero: string, linhaId: string) {
    const os = lista.find((o) => o.numero === numero);
    const peca = os?.pecasExecucao?.find((p) => p.linhaId === linhaId);
    if (!peca) return;
    atualizar(numero, (o) => ({
      ...o,
      pecasExecucao: o.pecasExecucao!.map((p) => (p.linhaId === linhaId ? { ...p, aplicada: true } : p)),
    }));
    toast(`Peça "${peca.desc}" aplicada${peca.produtoId ? ' — baixa de estoque registrada' : ''}.`);
  }

  function adicionarPecaExtra(numero: string, input: { desc: string; qtdTxt: string; precoTxt: string; avisado: boolean }): AcaoErro {
    const desc = input.desc.trim();
    if (!desc) return 'Descreva a peça extra.';
    const qtd = parseInt(input.qtdTxt, 10);
    if (!qtd || qtd <= 0) return 'os.quantidade_invalida — quantidade deve ser maior que zero.';
    const preco = parseFloat(input.precoTxt);
    if (Number.isNaN(preco) || preco < 0) return 'os.preco_invalido — preço unitário não pode ser negativo.';
    if (!input.avisado) return 'os.peca_extra_exige_aviso — confirme que o cliente foi avisado do novo valor.';

    atualizar(numero, (o) => ({
      ...o,
      pecasExecucao: [
        ...(o.pecasExecucao ?? []),
        { linhaId: uid(), desc, produtoId: 'prod-extra-' + uid(), qtd, preco: Math.round(preco * 100), origem: 'extra' as const, aplicada: true },
      ],
    }));
    toast(`Peça extra "${desc}" adicionada — cliente avisado do novo valor.`);
    return null;
  }

  function ajustarMaoDeObra(numero: string, input: { valorTxt: string; avisado: boolean }): AcaoErro {
    const os = lista.find((o) => o.numero === numero);
    if (!os?.orcamento) return null;
    const valor = parseFloat(input.valorTxt);
    if (Number.isNaN(valor) || valor < 0) return 'os.mao_de_obra_invalida — mão de obra não pode ser negativa.';
    const novoCents = Math.round(valor * 100);
    if (novoCents > os.orcamento.maoDeObra && !input.avisado) {
      return 'os.aumento_mao_de_obra_exige_aviso — aumentar acima do orçado exige confirmar que o cliente foi avisado.';
    }
    atualizar(numero, (o) => ({ ...o, maoDeObraFinal: novoCents }));
    toast(`${numero} · mão de obra ajustada para ${formatCentavos(novoCents)}.`);
    return null;
  }

  function entregar(numero: string, input: { forma: FormaPagamento; descontoTxt: string; garantiaTxt: string }): AcaoErro {
    const os = lista.find((o) => o.numero === numero);
    if (!os) return null;
    const desconto = Math.round(parseFloat(input.descontoTxt || '0') * 100);
    const garantiaDias = parseInt(input.garantiaTxt || '0', 10);
    const total = totalExecucaoAtual(os);
    if (Number.isNaN(desconto) || desconto < 0) return 'os.desconto_invalido — desconto não pode ser negativo.';
    if (garantiaDias < 0) return 'os.garantia_invalida — dias de garantia não pode ser negativo.';
    if (desconto > total) return 'os.desconto_maior_que_total — desconto não pode ser maior que o total da OS.';

    const maoAtual = os.maoDeObraFinal ?? os.orcamento!.maoDeObra;
    const pecasAtual = total - maoAtual;
    const valorServico: Centavos = Math.max(0, maoAtual - desconto);
    const descontoRestante = Math.max(0, desconto - maoAtual);
    const valorPecas: Centavos = pecasAtual - descontoRestante;

    atualizar(numero, (o) => ({
      ...o,
      status: 'Entregue' as OsStatus,
      formaPagamento: input.forma,
      desconto,
      garantiaDias,
      dataEntrega: HOJE,
      valorServico,
      valorPecas,
      historico: [...o.historico, { para: 'Entregue' as OsStatus, em: HOJE }],
    }));
    toast(
      total > 0
        ? `${numero} · entregue — OsFaturada emitida (${formatCentavos(valorServico + valorPecas)}).`
        : `${numero} · entregue — OS de garantia, sem fato financeiro.`,
    );
    return null;
  }

  function cancelar(numero: string) {
    const os = lista.find((o) => o.numero === numero);
    if (!os || ehTerminal(os.status)) return;
    atualizar(numero, (o) => ({
      ...o,
      motivoCancelamento: 'cancelada pelo operador',
      status: 'Cancelada' as OsStatus,
      historico: [...o.historico, { para: 'Cancelada' as OsStatus, em: HOJE }],
    }));
    toast(`${numero} · cancelada — reservas de estoque liberadas.`);
    voltarParaLista();
  }

  // ── Stubs read-only fora do escopo deste protótipo (textos exatos do mockup) ──
  function abrirNovaOs() {
    toast('Abertura de nova OS — fora do escopo deste protótipo (fluxo de 6 campos descrito no plano).');
  }
  function imprimirViaRecibo() {
    toast('Impressão térmica 80mm — stub (Fase D do roteiro).');
  }

  // ── Derivações ──────────────────────────────────────────────────────────

  const osSelecionada = useMemo(
    () => (tela === 'detalhe' ? (lista.find((o) => o.numero === numeroSelecionado) ?? null) : null),
    [tela, numeroSelecionado, lista],
  );

  const kpis = useMemo(() => buildKpis(lista), [lista]);
  const consultor = useMemo(() => buildConsultorInsight(lista), [lista]);
  const buckets = useMemo(() => bucketDados(lista), [lista]);
  const bucketSelecionado = useMemo(() => (etapaDrillAtual ? bucketPorChave(lista, etapaDrillAtual) : null), [lista, etapaDrillAtual]);
  const bucketSelecionadoStats = useMemo(() => (bucketSelecionado ? bucketDrillStats(bucketSelecionado) : null), [bucketSelecionado]);
  const operacao = useMemo(() => operacaoStats(lista), [lista]);
  const filaItens = useMemo(() => filtrarFila(lista, filtroFila, buscaFila), [lista, filtroFila, buscaFila]);
  const ativasCount = useMemo(() => lista.filter((o) => !ehTerminal(o.status)).length, [lista]);

  function acaoPrimaria(os: OrdemServico) {
    return acaoPrimariaDe(os, { irParaDetalhe, iniciarExecucao, concluirExecucao });
  }

  return {
    tela,
    osSelecionada,
    irParaDetalhe,
    voltarParaLista,

    kpis,
    consultor,
    abrirNovaOs,

    buckets,
    etapaDrillAtual,
    selecionarEtapa: setEtapaDrillAtual,
    bucketSelecionado,
    bucketSelecionadoStats,
    operacao,

    filtroFila,
    setFiltroFila,
    buscaFila,
    setBuscaFila,
    filaItens,
    totalCount: lista.length,
    ativasCount,
    encerradasCount: lista.length - ativasCount,
    acaoPrimaria,

    registrarDiagnostico,
    enviarOrcamento,
    decidir,
    iniciarExecucao,
    aplicarPeca,
    adicionarPecaExtra,
    ajustarMaoDeObra,
    concluirExecucao,
    entregar,
    cancelar,
    imprimirViaRecibo,
  };
}

export type UseOrdemServico = ReturnType<typeof useOrdemServico>;
