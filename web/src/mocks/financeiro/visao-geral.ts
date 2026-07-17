// MOCK — trocar por API (GET /financeiro/visao-geral) quando a rota existir.
// Dados portados 1:1 de `docs/ui/mockups/visao-geral.html` (hero, decomposição, gráfico de 30
// dias e próximos vencimentos). Todo valor monetário nasce em reais no mock (`reais()`) e vira
// centavos — nunca float de reais no restante do app.
// O bloco ⑤ (Super Consultor) NÃO está mais aqui: virou dado real via `GET /financeiro/consultor`
// (ver `useVisaoGeral` + `adapters/financeiro/consultor.ts`).
import type { VisaoGeralViewModel } from '@/components/financial/visao-geral/types';
import { reais } from '@/lib/money';

const VALORES_DIARIOS_REAIS = [
  6200, 6450, 6300, 7100, 7050, 7400, 7300, 7600, 7550, 7900, 7850, 8100, 8050, 8300, 8250, 8400,
  8550, 9440, 9140, 7040, 7240, 7860, 7460, 7110, 5810, 5610, -1340, -2200, -1850, -900,
];

export const visaoGeralMock: VisaoGeralViewModel = {
  periodoLabel: 'Julho 2026',

  disponivel: {
    livreDeVerdadeCentavos: reais(3300),
    noBancoEGaveta: {
      label: 'No banco e na gaveta hoje',
      valorCentavos: reais(8400),
      tone: 'pos',
      arrowLabel: 'Bancário →',
      drill: { rota: '/financeiro/bancario', mensagem: '→ Bancário — saldo por conta (banco + gaveta)' },
    },
    jaTemDono: {
      label: 'Já tem dono',
      sublabel: '(15 dias + imposto)',
      valorCentavos: reais(-5100),
      tone: 'crit',
      arrowLabel: 'E&S →',
      drill: {
        rota: '/financeiro/entradas-saidas',
        mensagem: '→ Entradas & Saídas — filtro: vencendo em 15 dias + imposto',
      },
    },
  },

  lucroDoMes: {
    lucroCentavos: reais(4230),
    deltaPercentual: 12,
    deltaDirecao: 'up',
    // R$ 0,18 por R$ 1 vendido — já em centavos (não passa por `reais()`, que é pra valores inteiros de reais).
    margemPorRealCentavos: 18,
    aReceberCentavos: reais(3800),
    verDeOndeVeio: {
      rota: '/financeiro/entradas-saidas',
      mensagem: '→ Entradas & Saídas — composição do resultado do mês',
    },
  },

  timeline: {
    valoresDiarios: VALORES_DIARIOS_REAIS.map(reais),
    hojeIndex: 15, // dia 16 = hoje
    eventosPorDia: {
      18: { descricao: 'Recebimento · Mercado São João', deltaCentavos: reais(890), tone: 'pos' },
      20: { descricao: 'Aluguel', deltaCentavos: reais(-2100), tone: 'crit' },
      22: { descricao: 'Recebimento · Auto Peças Silva', deltaCentavos: reais(620), tone: 'pos' },
      23: { descricao: 'Fornecedor Sul', deltaCentavos: reais(-400), tone: 'crit' },
      25: { descricao: 'Imposto (DAS)', deltaCentavos: reais(-1300), tone: 'crit' },
      27: { descricao: 'Folha de pagamento + outros compromissos', deltaCentavos: reais(-6950), tone: 'crit' },
      30: { descricao: 'Recebimentos diversos', deltaCentavos: reais(950), tone: 'pos' },
    },
    mesLabel: '07',
  },

  proximosVencimentos: [
    {
      dataLabel: 'sáb 18/07',
      valorCentavos: reais(890),
      tone: 'pos',
      descricao: 'Mercado São João',
      drill: {
        rota: '/financeiro/entradas-saidas',
        mensagem: '→ Entradas & Saídas — recebimento de Mercado São João, R$ 890, vence 18/07',
      },
    },
    {
      dataLabel: 'seg 20/07',
      valorCentavos: reais(-2100),
      tone: 'crit',
      descricao: 'Aluguel',
      drill: { rota: '/financeiro/entradas-saidas', mensagem: '→ Entradas & Saídas — Aluguel, R$ 2.100, vence 20/07' },
    },
    {
      dataLabel: 'qua 22/07',
      valorCentavos: reais(620),
      tone: 'pos',
      descricao: 'Auto Peças Silva',
      drill: {
        rota: '/financeiro/entradas-saidas',
        mensagem: '→ Entradas & Saídas — recebimento de Auto Peças Silva, R$ 620, vence 22/07',
      },
    },
    {
      dataLabel: 'qui 23/07',
      valorCentavos: reais(-400),
      tone: 'crit',
      descricao: 'Fornecedor Sul',
      drill: {
        rota: '/financeiro/entradas-saidas',
        mensagem: '→ Entradas & Saídas — Fornecedor Sul, R$ 400, vence 23/07',
      },
    },
  ],
};
