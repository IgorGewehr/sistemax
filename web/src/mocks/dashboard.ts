// MOCK — trocar por agregação real (Vendas + Financeiro + Estoque + Compras + OS) quando as
// respectivas APIs existirem. Cada campo aqui já implementa o tipo que a API vai devolver — trocar
// mock→API é trocar a origem do `DashboardViewModel`, não a tela.
import type { DashboardViewModel } from '@/components/dashboard/types';
import { reais } from '@/lib/money';

export const DASHBOARD_MOCK: DashboardViewModel = {
  kpis: [
    {
      modulo: 'vendas',
      label: 'Faturamento hoje',
      formato: 'moeda',
      valorCentavos: reais(4820),
      tone: 'pos',
      deltaPercentual: 12,
      deltaDirecao: 'up',
      foot: '38 vendas · ticket médio R$ 126,80',
      hero: true,
      // Vendas ainda não tem rota própria (task de desenhar/construir em aberto) — drill só toast.
      drill: { rota: '/vendas', mensagem: 'Vendas ainda não tem tela própria — chegando em breve.', disponivel: false },
    },
    {
      modulo: 'financeiro',
      label: 'Disponível em caixa',
      formato: 'moeda',
      valorCentavos: reais(18450),
      tone: 'pos',
      foot: 'R$ 6.200 em contas + R$ 500 na gaveta, já sem o que tem dono',
      drill: { rota: '/financeiro', mensagem: 'Abrindo a Visão Geral do Financeiro.' },
    },
    {
      modulo: 'estoque',
      label: 'Estoque crítico',
      formato: 'contagem',
      valorContagem: 4,
      tone: 'crit',
      foot: 'maior risco: Copo descartável 300ml (8 un)',
      drill: { rota: '/estoque', mensagem: 'Veja os produtos abaixo do mínimo.' },
    },
    {
      modulo: 'compras',
      label: 'Compras a conferir',
      formato: 'contagem',
      valorContagem: 2,
      tone: 'warn',
      foot: '1 com divergência de preço',
      drill: { rota: '/compras', mensagem: 'Abrindo as notas pendentes de conferência.' },
    },
    {
      modulo: 'os',
      label: 'Ordens em aberto',
      formato: 'contagem',
      valorContagem: 7,
      tone: 'neutro',
      foot: '2 aguardando aprovação do cliente há mais de 2 dias',
      drill: { rota: '/ordens', mensagem: 'Veja a fila de ordens de serviço.' },
    },
  ],

  atencao: [
    {
      modulo: 'estoque',
      moduloLabel: 'Estoque',
      severidade: 'crit',
      titulo: 'Copo descartável 300ml acabando',
      detalhe: '8 un restantes · média de venda de 45 un por sábado',
      drill: { rota: '/estoque', mensagem: 'Veja o produto crítico no Estoque.' },
    },
    {
      modulo: 'financeiro',
      moduloLabel: 'Financeiro',
      severidade: 'warn',
      titulo: 'Aluguel vence amanhã',
      detalhe: 'Sexta 18/07 · conta Sicoob',
      valorCentavos: reais(3300),
      drill: { rota: '/financeiro', mensagem: 'Abrindo Entradas & Saídas com o vencimento.' },
    },
    {
      modulo: 'compras',
      moduloLabel: 'Compras',
      severidade: 'warn',
      titulo: 'Nota 8790/1 com divergência de preço',
      detalhe: 'Arroz agulhinha 5kg veio R$ 50,40 mais caro que o combinado no pedido',
      drill: { rota: '/compras', mensagem: 'Abrindo a conferência da nota 8790/1.' },
    },
    {
      modulo: 'os',
      moduloLabel: 'Ordens',
      severidade: 'crit',
      titulo: '2 orçamentos parados há mais de 2 dias',
      detalhe: 'OS-0041 (iPhone 12) e OS-0038 (Notebook Dell) aguardando aprovação do cliente',
      drill: { rota: '/ordens', mensagem: 'Veja os orçamentos aguardando aprovação.' },
    },
  ],

  consultor: {
    itemNome: 'Copo descartável 300ml',
    quantidadeRestante: 8,
    unidade: 'un',
    mediaVendaLabel: 'média de 45 un por sábado',
    previsaoLabel: 'No ritmo atual, falta produto já no próximo sábado.',
    drill: { rota: '/estoque', mensagem: 'Abrindo o Estoque no produto crítico.' },
  },
};
