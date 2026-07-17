// MOCK — trocar por API quando `GET /financeiro/relatorios` existir (ver docs/sdd-roadmap.md).
// Números portados 1:1 de docs/ui/mockups/relatorios.html (fonte da verdade).
import type { ReportsViewModel } from '@/components/financial/relatorios/types';
import { reais } from '@/lib/money';

export const RELATORIOS_MOCK: ReportsViewModel = {
  periods: [
    { id: '2026-07', label: 'Julho 2026' },
    { id: '2026-06', label: 'Junho 2026' },
    { id: '2026-05', label: 'Maio 2026' },
    { id: '2026-q2', label: '2º trimestre 2026' },
  ],
  defaultPeriodId: '2026-07',
  defaultRegime: 'competencia',

  contact: {
    emailLabel: 'E-mail do contador',
    email: 'contador@vegacontabil.com.br',
    whatsappLabel: 'WhatsApp do contador',
    whatsapp: '+55 47 99123-4567',
  },

  dre: {
    docLabel: 'DRE do mês',
    periodLabel: 'Julho 2026',
    byRegime: {
      competencia: {
        regimeLabel: 'competência',
        topLine: { label: 'Receita bruta', valueCentavos: reais(42_380) },
        deductionLines: [
          { label: '(–) Impostos', valueCentavos: reais(3_680) },
          { label: '(–) Despesas e custos', valueCentavos: reais(34_470) },
        ],
        totalLine: { label: 'Resultado do mês', valueCentavos: reais(4_230) },
        delta: { direction: 'up', label: '▲ 12% vs Junho (R$ 3.780)' },
        bridgeNote: [
          { text: 'O DRE mostra ' },
          { text: 'R$ 4.230', bold: true },
          { text: ' de resultado (competência) — mas o caixa só recebeu ' },
          { text: 'R$ 1.230', bold: true },
          { text: ' este mês. R$ 3.000 ainda estão pra receber.' },
        ],
      },
      caixa: {
        regimeLabel: 'caixa',
        topLine: { label: 'Recebido no caixa', valueCentavos: reais(39_380) },
        deductionLines: [
          { label: '(–) Pago no caixa', valueCentavos: reais(34_470) },
          { label: '(–) Impostos pagos', valueCentavos: reais(3_680) },
        ],
        totalLine: { label: 'Resultado no caixa', valueCentavos: reais(1_230) },
        delta: { direction: 'down', label: '▼ R$ 3.000 abaixo da competência' },
        bridgeNote: [
          { text: 'No regime de competência (o que seu contador normalmente pede), o resultado seria ' },
          { text: 'R$ 4.230', bold: true },
          { text: ' — R$ 3.000 já foram vendidos mas ainda não entraram no caixa.' },
        ],
      },
    },
  },

  pacote: {
    docLabel: 'Fechamento julho (pacote)',
    zipFileName: 'fechamento-julho-2026.zip',
    checklist: [
      { label: 'DRE do mês' },
      { label: 'Extratos por conta', count: '(3 contas)' },
      { label: 'Contas em aberto', count: '(a pagar e a receber)' },
      { label: 'Fechamentos de caixa', count: '(23 sessões)' },
      { label: 'Pendências de conciliação', count: '(7 itens)' },
    ],
    resultLineByRegime: {
      competencia: [
        { text: 'Resultado do mês: ' },
        { text: 'R$ 4.230 (competência)', bold: true },
        { text: ' · última geração 01/07 por Igor' },
      ],
      caixa: [
        { text: 'Resultado do mês: ' },
        { text: 'R$ 1.230 (caixa)', bold: true },
        { text: ' · última geração 01/07 por Igor' },
      ],
    },
  },

  extrato: {
    docLabel: 'Extrato por conta',
    accounts: [
      { id: 'todas', label: 'Todas' },
      { id: 'itau', label: 'Itaú PJ' },
      { id: 'nubank', label: 'Nubank PJ' },
      { id: 'caixa', label: 'Caixa loja' },
    ],
    defaultFrom: '2026-07-01',
    defaultTo: '2026-07-16',
  },

  aberto: {
    docLabel: 'Contas em aberto',
    receberEmAberto: reais(9_430),
    receberAtrasado: reais(1_890),
    pagarEmAberto: reais(7_210),
    agingBuckets: [
      { id: '0-15', label: '0–15d', amountCentavos: reais(900), colorVar: 'hsl(var(--warn) / 0.55)' },
      { id: '15-30', label: '15–30d', amountCentavos: reais(600), colorVar: 'hsl(var(--warn))' },
      { id: '+30', label: '+30d', amountCentavos: reais(390), colorVar: 'hsl(var(--crit))' },
    ],
  },

  mrr: {
    docLabel: 'Relatório MRR',
    condicaoLabel: 'Visível porque vende serviço recorrente',
    mrr: reais(6_077),
    churnMes: reais(1_239),
    arrEstimado: reais(72_924),
  },

  initialHistory: [
    {
      id: 'hist-1',
      date: '01/07/2026',
      document: 'Fechamento junho (pacote)',
      format: 'ZIP',
      generatedBy: 'Igor',
      channel: 'email',
    },
    {
      id: 'hist-2',
      date: '01/07/2026',
      document: 'DRE junho',
      format: 'Excel',
      generatedBy: 'Igor',
      channel: null,
    },
    {
      id: 'hist-3',
      date: '20/06/2026',
      document: 'Extrato Itaú PJ (maio)',
      format: 'PDF',
      generatedBy: 'Ana',
      channel: 'whatsapp',
    },
    {
      id: 'hist-4',
      date: '01/06/2026',
      document: 'Fechamento maio (pacote)',
      format: 'ZIP',
      generatedBy: 'Igor',
      channel: 'email',
    },
  ],
};
