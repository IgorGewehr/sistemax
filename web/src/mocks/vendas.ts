// MOCK — trocar por API (vendasApi.listar) quando o endpoint de listagem existir.
// Produtos/categorias herdados do catálogo do PDV (docs/ui/mockups/pdv.html) p/ consistência entre módulos.
// "Hoje" é 16/07 (dia corrente do cenário de exemplo, mesmo `DATA_HOJE` que Compras usa) — as
// vendas de 16/07 alimentam `kpis.vendidoHojeCentavos`; as de 15/07 são "ontem" (base do delta).
import type { VendasMock } from '@/components/vendas/types';
import { reais } from '@/lib/money';

export const VENDAS_MOCK: VendasMock = {
  periodoLabel: 'Julho 2026',

  kpis: {
    vendidoHojeCentavos: reais(3182.4),
    vendidoHojeDeltaPct: 14,
    vendidoMesCentavos: reais(68430.9),
    vendidoMesDeltaPct: 9,
    ticketMedioCentavos: reais(46.82),
    ticketMedioDeltaPct: 3.5,
    numeroDeVendas: 462,
    numeroDeVendasEstornadas: 6,
  },

  historicoVendidoMesCentavos: [
    reais(52100), reais(58730), reais(61200), reais(59870), reais(63410),
  ], // + kpis.vendidoMesCentavos = 6º ponto do sparkline

  canais: ['Caixa 01', 'Caixa 02', 'Balcão'],
  operadores: ['Marina Souza', 'Bruno Lima', 'Igor'],

  vendas: [
    {
      id: 'v1042', numero: 'V-01042', dataHoraLabel: '16/07 14:32',
      canal: 'Caixa 02', operador: 'Marina Souza', clienteNome: null,
      status: 'Concluida',
      itens: [
        { produtoId: 'leite', nome: 'Leite Integral 1L', categoria: 'Mercearia', quantidade: 2, unidade: 'un', precoUnitarioCentavos: reais(5.49), descontoCentavos: 0, subtotalCentavos: reais(10.98) },
        { produtoId: 'pao', nome: 'Pão Francês', categoria: 'Padaria', quantidade: 0.6, unidade: 'kg', precoUnitarioCentavos: reais(16.9), descontoCentavos: 0, subtotalCentavos: reais(10.14) },
        { produtoId: 'cafe', nome: 'Café Torrado 500g', categoria: 'Mercearia', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(15.9), descontoCentavos: 0, subtotalCentavos: reais(15.9) },
      ],
      pagamentos: [{ metodo: 'Pix', valorCentavos: reais(37.02), valorRecebidoCentavos: null, trocoCentavos: 0 }],
      formasPagamento: ['Pix'],
      descontoCentavos: 0, subtotalCentavos: reais(37.02), totalCentavos: reais(37.02),
    },
    {
      id: 'v1038', numero: 'V-01038', dataHoraLabel: '16/07 13:22',
      canal: 'Caixa 01', operador: 'Igor', clienteNome: null,
      status: 'Concluida',
      itens: [
        { produtoId: 'refri', nome: 'Refrigerante Cola 2L', categoria: 'Bebidas', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(8.99), descontoCentavos: 0, subtotalCentavos: reais(8.99) },
        { produtoId: 'presunto', nome: 'Presunto Cozido', categoria: 'Frios', quantidade: 0.35, unidade: 'kg', precoUnitarioCentavos: reais(29.9), descontoCentavos: reais(1.5), subtotalCentavos: reais(9.0) },
      ],
      // pagamento dividido — Pix cobre parte, Dinheiro fecha o resto (com troco)
      pagamentos: [
        { metodo: 'Pix', valorCentavos: reais(10.0), valorRecebidoCentavos: null, trocoCentavos: 0 },
        { metodo: 'Dinheiro', valorCentavos: reais(7.99), valorRecebidoCentavos: reais(10.0), trocoCentavos: reais(2.01) },
      ],
      formasPagamento: ['Pix', 'Dinheiro'],
      descontoCentavos: reais(1.5), subtotalCentavos: reais(17.99) + reais(1.5), totalCentavos: reais(17.99),
    },
    {
      id: 'v1037', numero: 'V-01037', dataHoraLabel: '16/07 12:55',
      canal: 'Caixa 02', operador: 'Marina Souza', clienteNome: null,
      status: 'Estornada',
      itens: [
        { produtoId: 'mussarela', nome: 'Mussarela Fatiada', categoria: 'Frios', quantidade: 0.4, unidade: 'kg', precoUnitarioCentavos: reais(39.9), descontoCentavos: 0, subtotalCentavos: reais(15.96) },
        { produtoId: 'suco', nome: 'Suco de Laranja 1L', categoria: 'Bebidas', quantidade: 2, unidade: 'un', precoUnitarioCentavos: reais(7.49), descontoCentavos: 0, subtotalCentavos: reais(14.98) },
      ],
      pagamentos: [{ metodo: 'Voucher', valorCentavos: reais(30.94), valorRecebidoCentavos: null, trocoCentavos: 0 }],
      formasPagamento: ['Voucher'],
      descontoCentavos: 0, subtotalCentavos: reais(30.94), totalCentavos: reais(30.94),
      motivoEstorno: 'Cliente desistiu da compra após o pagamento — estorno solicitado no caixa.',
      estornadaEm: '16/07 13:05', estornadaPor: 'Marina Souza',
    },
    {
      id: 'v1035', numero: 'V-01035', dataHoraLabel: '16/07 12:10',
      canal: 'Balcão', operador: 'Bruno Lima', clienteNome: 'Carlos Menezes',
      status: 'Concluida',
      itens: [
        { produtoId: 'arroz', nome: 'Arroz Branco 5kg', categoria: 'Mercearia', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(24.9), descontoCentavos: 0, subtotalCentavos: reais(24.9) },
        { produtoId: 'feijao', nome: 'Feijão Carioca 1kg', categoria: 'Mercearia', quantidade: 2, unidade: 'un', precoUnitarioCentavos: reais(8.99), descontoCentavos: 0, subtotalCentavos: reais(8.99) + reais(8.99) },
        { produtoId: 'sabonete', nome: 'Sabonete Glicerinado', categoria: 'Higiene', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(3.49), descontoCentavos: 0, subtotalCentavos: reais(3.49) },
      ],
      pagamentos: [{ metodo: 'Dinheiro', valorCentavos: reais(24.9) + reais(8.99) + reais(8.99) + reais(3.49), valorRecebidoCentavos: reais(50.0), trocoCentavos: reais(50.0) - (reais(24.9) + reais(8.99) + reais(8.99) + reais(3.49)) }],
      formasPagamento: ['Dinheiro'],
      descontoCentavos: 0,
      subtotalCentavos: reais(24.9) + reais(8.99) + reais(8.99) + reais(3.49),
      totalCentavos: reais(24.9) + reais(8.99) + reais(8.99) + reais(3.49),
    },
    {
      id: 'v1030', numero: 'V-01030', dataHoraLabel: '16/07 11:45',
      canal: 'Caixa 01', operador: 'Marina Souza', clienteNome: null,
      status: 'Concluida',
      itens: [
        { produtoId: 'leite', nome: 'Leite Integral 1L', categoria: 'Mercearia', quantidade: 2, unidade: 'un', precoUnitarioCentavos: reais(5.49), descontoCentavos: 0, subtotalCentavos: reais(5.49) + reais(5.49) },
        { produtoId: 'pao', nome: 'Pão Francês', categoria: 'Padaria', quantidade: 0.5, unidade: 'kg', precoUnitarioCentavos: reais(16.9), descontoCentavos: 0, subtotalCentavos: reais(8.45) },
        { produtoId: 'detergente', nome: 'Detergente Neutro', categoria: 'Limpeza', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(2.99), descontoCentavos: 0, subtotalCentavos: reais(2.99) },
      ],
      pagamentos: [{ metodo: 'Debito', valorCentavos: reais(5.49) + reais(5.49) + reais(8.45) + reais(2.99), valorRecebidoCentavos: null, trocoCentavos: 0 }],
      formasPagamento: ['Debito'],
      descontoCentavos: 0,
      subtotalCentavos: reais(5.49) + reais(5.49) + reais(8.45) + reais(2.99),
      totalCentavos: reais(5.49) + reais(5.49) + reais(8.45) + reais(2.99),
    },
    {
      id: 'v1028', numero: 'V-01028', dataHoraLabel: '16/07 10:52',
      canal: 'Caixa 02', operador: 'Bruno Lima', clienteNome: 'Ana Paula Ribeiro',
      status: 'Concluida',
      itens: [
        { produtoId: 'arroz', nome: 'Arroz Branco 5kg', categoria: 'Mercearia', quantidade: 2, unidade: 'un', precoUnitarioCentavos: reais(24.9), descontoCentavos: 0, subtotalCentavos: reais(24.9) + reais(24.9) },
        { produtoId: 'mussarela', nome: 'Mussarela Fatiada', categoria: 'Frios', quantidade: 1, unidade: 'kg', precoUnitarioCentavos: reais(39.9), descontoCentavos: reais(2.0), subtotalCentavos: reais(39.9) - reais(2.0) },
        { produtoId: 'presunto', nome: 'Presunto Cozido', categoria: 'Frios', quantidade: 0.5, unidade: 'kg', precoUnitarioCentavos: reais(29.9), descontoCentavos: 0, subtotalCentavos: reais(14.95) },
        { produtoId: 'cafe', nome: 'Café Torrado 500g', categoria: 'Mercearia', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(15.9), descontoCentavos: 0, subtotalCentavos: reais(15.9) },
      ],
      pagamentos: [{ metodo: 'Credito', valorCentavos: reais(24.9) + reais(24.9) + (reais(39.9) - reais(2.0)) + reais(14.95) + reais(15.9), valorRecebidoCentavos: null, trocoCentavos: 0 }],
      formasPagamento: ['Credito'],
      descontoCentavos: reais(2.0),
      subtotalCentavos: reais(24.9) + reais(24.9) + reais(39.9) + reais(14.95) + reais(15.9),
      totalCentavos: reais(24.9) + reais(24.9) + (reais(39.9) - reais(2.0)) + reais(14.95) + reais(15.9),
    },
    {
      id: 'v1025', numero: 'V-01025', dataHoraLabel: '16/07 09:30',
      canal: 'Balcão', operador: 'Igor', clienteNome: null,
      status: 'Concluida',
      itens: [
        { produtoId: 'suco', nome: 'Suco de Laranja 1L', categoria: 'Bebidas', quantidade: 2, unidade: 'un', precoUnitarioCentavos: reais(7.49), descontoCentavos: 0, subtotalCentavos: reais(7.49) + reais(7.49) },
        { produtoId: 'refri', nome: 'Refrigerante Cola 2L', categoria: 'Bebidas', quantidade: 2, unidade: 'un', precoUnitarioCentavos: reais(8.99), descontoCentavos: 0, subtotalCentavos: reais(8.99) + reais(8.99) },
        { produtoId: 'papeltoalha', nome: 'Papel Toalha 2un', categoria: 'Limpeza', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(8.99), descontoCentavos: 0, subtotalCentavos: reais(8.99) },
      ],
      pagamentos: [{ metodo: 'Voucher', valorCentavos: reais(7.49) + reais(7.49) + reais(8.99) + reais(8.99) + reais(8.99), valorRecebidoCentavos: null, trocoCentavos: 0 }],
      formasPagamento: ['Voucher'],
      descontoCentavos: 0,
      subtotalCentavos: reais(7.49) + reais(7.49) + reais(8.99) + reais(8.99) + reais(8.99),
      totalCentavos: reais(7.49) + reais(7.49) + reais(8.99) + reais(8.99) + reais(8.99),
    },
    {
      id: 'v1019', numero: 'V-01019', dataHoraLabel: '15/07 18:40',
      canal: 'Caixa 01', operador: 'Marina Souza', clienteNome: 'Roberto Alves',
      status: 'Concluida',
      itens: [
        { produtoId: 'arroz', nome: 'Arroz Branco 5kg', categoria: 'Mercearia', quantidade: 3, unidade: 'un', precoUnitarioCentavos: reais(24.9), descontoCentavos: 0, subtotalCentavos: reais(24.9) * 3 },
        { produtoId: 'feijao', nome: 'Feijão Carioca 1kg', categoria: 'Mercearia', quantidade: 4, unidade: 'un', precoUnitarioCentavos: reais(8.99), descontoCentavos: 0, subtotalCentavos: reais(8.99) * 4 },
        { produtoId: 'cafe', nome: 'Café Torrado 500g', categoria: 'Mercearia', quantidade: 2, unidade: 'un', precoUnitarioCentavos: reais(15.9), descontoCentavos: 0, subtotalCentavos: reais(15.9) * 2 },
        { produtoId: 'mussarela', nome: 'Mussarela Fatiada', categoria: 'Frios', quantidade: 1.2, unidade: 'kg', precoUnitarioCentavos: reais(39.9), descontoCentavos: 0, subtotalCentavos: reais(47.88) },
        { produtoId: 'presunto', nome: 'Presunto Cozido', categoria: 'Frios', quantidade: 0.8, unidade: 'kg', precoUnitarioCentavos: reais(29.9), descontoCentavos: 0, subtotalCentavos: reais(23.92) },
      ],
      pagamentos: [{ metodo: 'CreditoLoja', valorCentavos: reais(24.9) * 3 + reais(8.99) * 4 + reais(15.9) * 2 + reais(47.88) + reais(23.92), valorRecebidoCentavos: null, trocoCentavos: 0 }],
      formasPagamento: ['CreditoLoja'],
      descontoCentavos: 0,
      subtotalCentavos: reais(24.9) * 3 + reais(8.99) * 4 + reais(15.9) * 2 + reais(47.88) + reais(23.92),
      totalCentavos: reais(24.9) * 3 + reais(8.99) * 4 + reais(15.9) * 2 + reais(47.88) + reais(23.92),
    },
    {
      id: 'v1015', numero: 'V-01015', dataHoraLabel: '15/07 17:12',
      canal: 'Caixa 02', operador: 'Igor', clienteNome: null,
      status: 'Concluida',
      itens: [
        { produtoId: 'pao', nome: 'Pão Francês', categoria: 'Padaria', quantidade: 0.8, unidade: 'kg', precoUnitarioCentavos: reais(16.9), descontoCentavos: 0, subtotalCentavos: reais(13.52) },
      ],
      pagamentos: [{ metodo: 'Outro', valorCentavos: reais(13.52), valorRecebidoCentavos: null, trocoCentavos: 0 }],
      formasPagamento: ['Outro'],
      descontoCentavos: 0, subtotalCentavos: reais(13.52), totalCentavos: reais(13.52),
    },
    {
      id: 'v1012', numero: 'V-01012', dataHoraLabel: '15/07 16:05',
      canal: 'Balcão', operador: 'Bruno Lima', clienteNome: 'Carlos Menezes',
      status: 'Concluida',
      itens: [
        { produtoId: 'leite', nome: 'Leite Integral 1L', categoria: 'Mercearia', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(5.49), descontoCentavos: 0, subtotalCentavos: reais(5.49) },
        { produtoId: 'suco', nome: 'Suco de Laranja 1L', categoria: 'Bebidas', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(7.49), descontoCentavos: 0, subtotalCentavos: reais(7.49) },
        { produtoId: 'sabonete', nome: 'Sabonete Glicerinado', categoria: 'Higiene', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(3.49), descontoCentavos: 0, subtotalCentavos: reais(3.49) },
        { produtoId: 'detergente', nome: 'Detergente Neutro', categoria: 'Limpeza', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(2.99), descontoCentavos: 0, subtotalCentavos: reais(2.99) },
      ],
      pagamentos: [{ metodo: 'Pix', valorCentavos: reais(5.49) + reais(7.49) + reais(3.49) + reais(2.99), valorRecebidoCentavos: null, trocoCentavos: 0 }],
      formasPagamento: ['Pix'],
      descontoCentavos: 0,
      subtotalCentavos: reais(5.49) + reais(7.49) + reais(3.49) + reais(2.99),
      totalCentavos: reais(5.49) + reais(7.49) + reais(3.49) + reais(2.99),
    },
    {
      id: 'v1009', numero: 'V-01009', dataHoraLabel: '15/07 15:47',
      canal: 'Caixa 01', operador: 'Marina Souza', clienteNome: null,
      status: 'Estornada',
      itens: [
        { produtoId: 'refri', nome: 'Refrigerante Cola 2L', categoria: 'Bebidas', quantidade: 2, unidade: 'un', precoUnitarioCentavos: reais(8.99), descontoCentavos: 0, subtotalCentavos: reais(8.99) + reais(8.99) },
        { produtoId: 'papeltoalha', nome: 'Papel Toalha 2un', categoria: 'Limpeza', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(8.99), descontoCentavos: 0, subtotalCentavos: reais(8.99) },
      ],
      pagamentos: [{ metodo: 'Dinheiro', valorCentavos: reais(8.99) * 3, valorRecebidoCentavos: reais(30.0), trocoCentavos: reais(30.0) - reais(8.99) * 3 }],
      formasPagamento: ['Dinheiro'],
      descontoCentavos: 0, subtotalCentavos: reais(8.99) * 3, totalCentavos: reais(8.99) * 3,
      motivoEstorno: 'Item errado no carrinho — cliente pediu para refazer a compra.',
      estornadaEm: '15/07 16:02', estornadaPor: 'Marina Souza',
    },
    {
      id: 'v1006', numero: 'V-01006', dataHoraLabel: '15/07 14:20',
      canal: 'Caixa 02', operador: 'Bruno Lima', clienteNome: 'Ana Paula Ribeiro',
      status: 'Concluida',
      itens: [
        { produtoId: 'arroz', nome: 'Arroz Branco 5kg', categoria: 'Mercearia', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(24.9), descontoCentavos: 0, subtotalCentavos: reais(24.9) },
        { produtoId: 'cafe', nome: 'Café Torrado 500g', categoria: 'Mercearia', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(15.9), descontoCentavos: 0, subtotalCentavos: reais(15.9) },
        { produtoId: 'mussarela', nome: 'Mussarela Fatiada', categoria: 'Frios', quantidade: 0.6, unidade: 'kg', precoUnitarioCentavos: reais(39.9), descontoCentavos: 0, subtotalCentavos: reais(23.94) },
        { produtoId: 'feijao', nome: 'Feijão Carioca 1kg', categoria: 'Mercearia', quantidade: 2, unidade: 'un', precoUnitarioCentavos: reais(8.99), descontoCentavos: 0, subtotalCentavos: reais(8.99) + reais(8.99) },
      ],
      // pagamento dividido — Débito cobre uma parte fixa, Crédito fecha o resto
      pagamentos: [
        { metodo: 'Debito', valorCentavos: reais(40.0), valorRecebidoCentavos: null, trocoCentavos: 0 },
        { metodo: 'Credito', valorCentavos: reais(24.9) + reais(15.9) + reais(23.94) + reais(8.99) + reais(8.99) - reais(40.0), valorRecebidoCentavos: null, trocoCentavos: 0 },
      ],
      formasPagamento: ['Debito', 'Credito'],
      descontoCentavos: 0,
      subtotalCentavos: reais(24.9) + reais(15.9) + reais(23.94) + reais(8.99) + reais(8.99),
      totalCentavos: reais(24.9) + reais(15.9) + reais(23.94) + reais(8.99) + reais(8.99),
    },
    {
      id: 'v1004', numero: 'V-01004', dataHoraLabel: '15/07 13:05',
      canal: 'Balcão', operador: 'Igor', clienteNome: null,
      status: 'Concluida',
      itens: [
        { produtoId: 'leite', nome: 'Leite Integral 1L', categoria: 'Mercearia', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(5.49), descontoCentavos: 0, subtotalCentavos: reais(5.49) },
        { produtoId: 'suco', nome: 'Suco de Laranja 1L', categoria: 'Bebidas', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(7.49), descontoCentavos: 0, subtotalCentavos: reais(7.49) },
        { produtoId: 'sabonete', nome: 'Sabonete Glicerinado', categoria: 'Higiene', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(3.49), descontoCentavos: 0, subtotalCentavos: reais(3.49) },
      ],
      pagamentos: [{ metodo: 'Pix', valorCentavos: reais(5.49) + reais(7.49) + reais(3.49), valorRecebidoCentavos: null, trocoCentavos: 0 }],
      formasPagamento: ['Pix'],
      descontoCentavos: 0,
      subtotalCentavos: reais(5.49) + reais(7.49) + reais(3.49),
      totalCentavos: reais(5.49) + reais(7.49) + reais(3.49),
    },
    {
      id: 'v1002', numero: 'V-01002', dataHoraLabel: '15/07 12:40',
      canal: 'Caixa 01', operador: 'Marina Souza', clienteNome: 'Roberto Alves',
      status: 'Concluida',
      itens: [
        { produtoId: 'arroz', nome: 'Arroz Branco 5kg', categoria: 'Mercearia', quantidade: 2, unidade: 'un', precoUnitarioCentavos: reais(24.9), descontoCentavos: 0, subtotalCentavos: reais(24.9) + reais(24.9) },
        { produtoId: 'presunto', nome: 'Presunto Cozido', categoria: 'Frios', quantidade: 0.6, unidade: 'kg', precoUnitarioCentavos: reais(29.9), descontoCentavos: 0, subtotalCentavos: reais(17.94) },
        { produtoId: 'cafe', nome: 'Café Torrado 500g', categoria: 'Mercearia', quantidade: 1, unidade: 'un', precoUnitarioCentavos: reais(15.9), descontoCentavos: 0, subtotalCentavos: reais(15.9) },
      ],
      pagamentos: [{ metodo: 'Credito', valorCentavos: reais(24.9) + reais(24.9) + reais(17.94) + reais(15.9), valorRecebidoCentavos: null, trocoCentavos: 0 }],
      formasPagamento: ['Credito'],
      descontoCentavos: 0,
      subtotalCentavos: reais(24.9) + reais(24.9) + reais(17.94) + reais(15.9),
      totalCentavos: reais(24.9) + reais(24.9) + reais(17.94) + reais(15.9),
    },
  ],
};
