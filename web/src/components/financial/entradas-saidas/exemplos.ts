// Conteúdo ILUSTRATIVO — não vem de mock global (ver `mocks/financeiro/`), mas também não é dado
// real: são os blocos de Entradas & Saídas que docs/wiring/financeiro-telas-restantes.md §1 marca
// como sem read-model ainda ("Para onde foi o dinheiro" com histórico de 6 meses, o painel do
// Super Consultor de Fornecedores com média histórica, o sparkline diário de "a receber"). Linha
// do tempo, os 4 KPIs de topo e o bridge de competência×caixa JÁ são reais — ver `useEntradasSaidas.ts`.
import { reais } from '@/lib/money';

import type { CategoriaDespesaResumo, ConsultorFornecedoresData, ContaDisponivel, HeroSparkline, ResumoPdvMes } from './types';

export const CATEGORIAS_EXEMPLO: CategoriaDespesaResumo[] = [
  {
    id: 'folha',
    nome: 'Folha',
    cor: 'primary',
    fixo: true,
    totalCentavos: reais(4900),
    historicoCentavos: [4600, 4600, 4900, 4900, 4900, 4900].map(reais),
    maiorLancamento: { desc: 'Folha de julho', valorCentavos: reais(4900) },
  },
  {
    id: 'fornecedores',
    nome: 'Fornecedores',
    cor: 'fg-62',
    fixo: false,
    totalCentavos: reais(3100),
    historicoCentavos: [2150, 2300, 2050, 2240, 2175, 3100].map(reais),
    maiorLancamento: { desc: 'Distribuidora Sul · 15/07', valorCentavos: reais(1630) },
  },
  {
    id: 'aluguel',
    nome: 'Aluguel',
    cor: 'fg-48',
    fixo: true,
    totalCentavos: reais(2100),
    historicoCentavos: [2100, 2100, 2100, 2100, 2100, 2100].map(reais),
    maiorLancamento: { desc: 'Aluguel da loja', valorCentavos: reais(2100) },
  },
  {
    id: 'impostos',
    nome: 'Impostos',
    cor: 'fg-36',
    fixo: false,
    totalCentavos: reais(1240),
    historicoCentavos: [980, 1120, 1050, 1190, 1200, 1240].map(reais),
    maiorLancamento: { desc: 'DAS · 06/07', valorCentavos: reais(1240) },
  },
  {
    id: 'software',
    nome: 'Software',
    cor: 'fg-26',
    fixo: true,
    totalCentavos: reais(540),
    historicoCentavos: [420, 430, 460, 490, 510, 540].map(reais),
    maiorLancamento: { desc: 'Assinatura ERP · 03/07', valorCentavos: reais(330) },
  },
  {
    id: 'marketing',
    nome: 'Marketing',
    cor: 'fg-18',
    fixo: false,
    totalCentavos: reais(310),
    historicoCentavos: [150, 220, 280, 200, 260, 310].map(reais),
    maiorLancamento: { desc: 'Anúncios Instagram · 07/07', valorCentavos: reais(310) },
  },
];

export const CONSULTOR_FORNECEDORES_EXEMPLO: ConsultorFornecedoresData = {
  deltaPct: 42,
  mediaHistoricaCentavos: reais(2183),
  totalMesCentavos: reais(3100),
  qtdPagamentos: 3,
};

export const SPARKLINE_RECEBER_EXEMPLO: HeroSparkline = {
  pathLinha: 'M0,22 L52,20 L104,24 L156,15 L208,12 L260,9',
  pathArea: 'M0,22 L52,20 L104,24 L156,15 L208,12 L260,9 L260,34 L0,34 Z',
};

export const MESES_HISTORICO_EXEMPLO = ['Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul'];

export const CONTAS_DISPONIVEIS_EXEMPLO: ContaDisponivel[] = [
  { nome: 'Itaú PJ', tag: 'banco' },
  { nome: 'Nubank PJ', tag: 'banco' },
  { nome: 'Stone', tag: 'banco' },
  { nome: 'Caixa da loja', tag: 'espécie' },
];

export const CATEGORIAS_LANCAMENTO_RAPIDO_EXEMPLO = {
  entrada: ['Serviços', 'Produtos', 'Outra receita'],
  saida: ['Folha', 'Fornecedores', 'Aluguel', 'Impostos', 'Software', 'Marketing', 'Outra despesa'],
};

export const RESUMO_PDV_MES_EXEMPLO: ResumoPdvMes = {
  qtdVendas: 12,
  totalCentavos: reais(9782),
};
