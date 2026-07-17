// MOCK — trocar por GET /api/clientes quando a API existir.
import type { ClientesMock } from '@/components/clientes/types';
import { reais } from '@/lib/money';

export const CLIENTES_MOCK: ClientesMock = {
  hojeLabel: '16/07/2026',
  totalClientesHistoricoMensal: [9, 10, 11, 12, 13], // fev–jun/2026; jul (13 ativos) é o 6º ponto, calculado

  clientes: [
    {
      id: 'c1', nome: 'Marli Aparecida Souza',
      telefone: '(11) 98765-4321', email: 'marli.souza@gmail.com',
      aniversario: '12/03', enderecoResumo: 'Vila Mariana, São Paulo',
      observacoes: 'Cliente desde a inauguração — levava ração + petiscos toda semana.',
      tags: ['vip'], status: 'ativo', criadoEm: '04/02/2021',
      ultimaVisita: '20/03/2026', comprasCount: 47,
      ticketMedioCentavos: reais(89), totalGasto12mCentavos: reais(1780), totalGastoVidaCentavos: reais(9840),
    },
    {
      id: 'c2', nome: 'Roberto Carlos Andrade',
      telefone: '(11) 97654-3210', email: 'roberto.andrade@hotmail.com',
      aniversario: '03/11', enderecoResumo: 'Tatuapé, São Paulo', observacoes: null,
      tags: [], status: 'ativo', criadoEm: '18/06/2023',
      ultimaVisita: '14/07/2026', comprasCount: 12,
      ticketMedioCentavos: reais(145), totalGasto12mCentavos: reais(980), totalGastoVidaCentavos: reais(1740),
    },
    {
      id: 'c3', nome: 'Juliana Ferreira Lima',
      telefone: '(11) 96543-2109', email: 'ju.lima@outlook.com',
      aniversario: '16/07', enderecoResumo: 'Moema, São Paulo',
      observacoes: 'Prefere contato por WhatsApp.',
      tags: [], status: 'ativo', criadoEm: '02/09/2022',
      ultimaVisita: '10/07/2026', comprasCount: 23,
      ticketMedioCentavos: reais(67), totalGasto12mCentavos: reais(1120), totalGastoVidaCentavos: reais(2340),
    },
    {
      id: 'c4', nome: 'Patrícia Gomes Nascimento',
      telefone: '(11) 95432-1098', email: 'patricia.nascimento@gmail.com',
      aniversario: null, enderecoResumo: null, observacoes: 'Indicada pela Juliana Lima.',
      tags: [], status: 'ativo', criadoEm: '14/07/2026',
      ultimaVisita: null, comprasCount: 0,
      ticketMedioCentavos: 0, totalGasto12mCentavos: 0, totalGastoVidaCentavos: 0,
    },
    {
      id: 'c5', nome: 'Eduardo Martins Cardoso',
      telefone: '(11) 94321-0987', email: 'eduardo.cardoso@gmail.com',
      aniversario: '22/09', enderecoResumo: 'Santana, São Paulo', observacoes: null,
      tags: [], status: 'ativo', criadoEm: '11/11/2020',
      ultimaVisita: '05/03/2026', comprasCount: 31,
      ticketMedioCentavos: reais(210), totalGasto12mCentavos: reais(650), totalGastoVidaCentavos: reais(6510),
    },
    {
      id: 'c6', nome: 'Fernanda Rocha Teixeira',
      telefone: null, email: null,
      aniversario: null, enderecoResumo: null,
      observacoes: 'Cliente pediu remoção da base em 20/01/2025 (LGPD).',
      tags: [], status: 'inativo', criadoEm: '03/05/2019',
      ultimaVisita: '20/01/2025', comprasCount: 5,
      ticketMedioCentavos: reais(60), totalGasto12mCentavos: 0, totalGastoVidaCentavos: reais(300),
    },
    {
      id: 'c7', nome: 'Antônio Carlos Pereira',
      telefone: '(11) 93210-9876', email: 'antonio.pereira@gmail.com',
      aniversario: '30/07', enderecoResumo: 'Perdizes, São Paulo',
      observacoes: 'Compra pra revenda — sempre em quantidade.',
      tags: ['vip', 'atacado'], status: 'ativo', criadoEm: '22/01/2019',
      ultimaVisita: '15/07/2026', comprasCount: 89,
      ticketMedioCentavos: reais(320), totalGasto12mCentavos: reais(5200), totalGastoVidaCentavos: reais(28400),
    },
    {
      id: 'c8', nome: 'Camila Beatriz Nogueira',
      telefone: '(11) 92109-8765', email: 'camila.nogueira@gmail.com',
      aniversario: '19/12', enderecoResumo: 'Ipiranga, São Paulo', observacoes: null,
      tags: [], status: 'ativo', criadoEm: '08/08/2023',
      ultimaVisita: '01/07/2026', comprasCount: 9,
      ticketMedioCentavos: reais(112), totalGasto12mCentavos: reais(780), totalGastoVidaCentavos: reais(1008),
    },
    {
      id: 'c9', nome: 'Lucas Gabriel Ramos',
      telefone: '(11) 91098-7654', email: 'lucas.ramos@gmail.com',
      aniversario: null, enderecoResumo: null, observacoes: null,
      tags: [], status: 'ativo', criadoEm: '02/07/2026',
      ultimaVisita: '05/07/2026', comprasCount: 1,
      ticketMedioCentavos: reais(45), totalGasto12mCentavos: reais(45), totalGastoVidaCentavos: reais(45),
    },
    {
      id: 'c10', nome: 'Sandra Regina Vieira',
      telefone: '(11) 90987-6543', email: 'sandra.vieira@gmail.com',
      aniversario: '27/01', enderecoResumo: 'Vila Prudente, São Paulo', observacoes: null,
      tags: [], status: 'ativo', criadoEm: '14/03/2022',
      ultimaVisita: '12/04/2026', comprasCount: 18,
      ticketMedioCentavos: reais(98), totalGasto12mCentavos: reais(450), totalGastoVidaCentavos: reais(1764),
    },
    {
      id: 'c11', nome: 'Paulo Henrique Costa',
      telefone: '(11) 89876-5432', email: 'paulo.costa@gmail.com',
      aniversario: '08/07', enderecoResumo: 'Brooklin, São Paulo', observacoes: null,
      tags: [], status: 'ativo', criadoEm: '30/10/2021',
      ultimaVisita: '09/07/2026', comprasCount: 27,
      ticketMedioCentavos: reais(83), totalGasto12mCentavos: reais(1560), totalGastoVidaCentavos: reais(2241),
    },
    {
      id: 'c12', nome: 'Beatriz Souza Almeida',
      telefone: '(11) 88765-4321', email: 'bia.almeida@gmail.com',
      aniversario: '05/02', enderecoResumo: 'Vila Madalena, São Paulo',
      observacoes: 'Vem toda terça de manhã.',
      tags: [], status: 'ativo', criadoEm: '19/01/2020',
      ultimaVisita: '13/07/2026', comprasCount: 112,
      ticketMedioCentavos: reais(72), totalGasto12mCentavos: reais(3450), totalGastoVidaCentavos: reais(8064),
    },
    {
      id: 'c13', nome: 'Rafael Augusto Barbosa',
      telefone: '(11) 87654-3210', email: 'rafael.barbosa@hotmail.com',
      aniversario: '14/06', enderecoResumo: 'Santo Amaro, São Paulo', observacoes: null,
      tags: [], status: 'ativo', criadoEm: '25/05/2024',
      ultimaVisita: '06/07/2026', comprasCount: 6,
      ticketMedioCentavos: reais(99), totalGasto12mCentavos: reais(594), totalGastoVidaCentavos: reais(594),
    },
    {
      id: 'c14', nome: 'Vanessa Cristina Duarte',
      telefone: '(11) 86543-2109', email: 'vanessa.duarte@gmail.com',
      aniversario: '17/10', enderecoResumo: 'Campo Belo, São Paulo',
      observacoes: 'Era cliente frequente — sumiu após reclamação de atraso na entrega.',
      tags: [], status: 'ativo', criadoEm: '10/02/2018',
      ultimaVisita: '30/12/2025', comprasCount: 64,
      ticketMedioCentavos: reais(86), totalGasto12mCentavos: reais(172), totalGastoVidaCentavos: reais(5504),
    },
  ],

  // Só os clientes usados pra demonstrar a Ficha têm histórico detalhado — igual ao
  // `historicoCustoDemo` de Compras (fixo, não é por-fornecedor ainda no mock).
  historicoPorCliente: {
    c1: [
      { id: 'h1', data: '20/03/2026', tipo: 'venda', descricao: '4 itens · Ração + Areia higiênica', valorCentavos: reais(112) },
      { id: 'h2', data: '18/02/2026', tipo: 'venda', descricao: '2 itens · Antipulgas', valorCentavos: reais(78) },
      { id: 'h3', data: '21/01/2026', tipo: 'venda', descricao: '5 itens · Ração + Petiscos + Brinquedo', valorCentavos: reais(134) },
    ],
    c3: [
      { id: 'h4', data: '10/07/2026', tipo: 'venda', descricao: '3 itens · Shampoo + Ração', valorCentavos: reais(71) },
      { id: 'h5', data: '02/06/2026', tipo: 'os', descricao: 'OS #418 · Banho e tosa', valorCentavos: reais(90), statusLabel: 'Concluída' },
    ],
    c7: [
      { id: 'h6', data: '15/07/2026', tipo: 'venda', descricao: '18 itens · Pedido atacado mensal', valorCentavos: reais(410) },
      { id: 'h7', data: '15/06/2026', tipo: 'venda', descricao: '15 itens · Pedido atacado mensal', valorCentavos: reais(365) },
      { id: 'h8', data: '15/05/2026', tipo: 'venda', descricao: '20 itens · Pedido atacado mensal', valorCentavos: reais(482) },
    ],
    c12: [
      { id: 'h9', data: '13/07/2026', tipo: 'venda', descricao: '2 itens · Ração', valorCentavos: reais(68) },
      { id: 'h10', data: '06/07/2026', tipo: 'os', descricao: 'OS #431 · Tosa higiênica', valorCentavos: reais(55), statusLabel: 'Concluída' },
    ],
  },
};
