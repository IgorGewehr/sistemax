// MOCK — trocar por API quando o backend de Configurações/RBAC existir.
import type { ConfiguracoesMock, Empresa } from '@/components/configuracoes/types';
import type { Usuario } from '@/lib/permissions';

export const EMPRESA_MOCK: Empresa = {
  nome: 'Empório Bom Sabor Comércio de Alimentos Ltda',
  nomeFantasia: 'Empório Bom Sabor',
  cnpj: '12.345.678/0001-90',
  telefone: '(51) 3222-4455',
  email: 'contato@emporiobomsabor.com.br',
  endereco: {
    logradouro: 'Rua das Palmeiras, 482',
    bairro: 'Centro',
    cidade: 'Porto Alegre',
    uf: 'RS',
    cep: '90010-150',
  },
  logoUrl: null,
};

/**
 * `u1` é o MESMO id de `USUARIO_SESSAO_MOCK` em `lib/permissions.ts` — "Meu perfil" e a linha
 * "(você)" da tabela de Usuários apontam pra uma única pessoa, nunca dois mocks divergentes.
 */
export const USUARIOS_MOCK: Usuario[] = [
  {
    id: 'u1',
    nome: 'Igor Gewehr',
    email: 'igor@sistemax.app',
    telefone: '(51) 99900-1122',
    papel: 'admin',
    status: 'ativo',
    overrides: [],
    criadoEm: '02/01/2024',
    ultimoAcessoEm: '17/07/2026',
  },
  {
    id: 'u2',
    nome: 'Marina Ferraz',
    email: 'marina@emporiobomsabor.com.br',
    telefone: '(51) 99811-2233',
    papel: 'founder',
    status: 'ativo',
    overrides: [],
    criadoEm: '15/11/2023',
    ultimoAcessoEm: '16/07/2026',
  },
  {
    id: 'u3',
    nome: 'Bruno Alencar',
    email: 'bruno@emporiobomsabor.com.br',
    telefone: '(51) 99733-4499',
    papel: 'manager',
    status: 'ativo',
    overrides: [],
    criadoEm: '20/02/2024',
    ultimoAcessoEm: '17/07/2026',
  },
  {
    id: 'u4',
    nome: 'Camila Rocha',
    email: 'camila@emporiobomsabor.com.br',
    telefone: '(51) 99622-8877',
    papel: 'operator',
    status: 'ativo',
    // Exemplo concreto de override: Camila não mexe no caixa (revogado do padrão de operator),
    // mas acompanha a entrada de mercadoria mesmo sem editar (concedido "ver" em Compras, que o
    // padrão de operator não dá).
    overrides: [
      { permissao: 'pdv:operarCaixa', efeito: 'revogar' },
      { permissao: 'compras:ver', efeito: 'conceder' },
    ],
    criadoEm: '03/05/2024',
    ultimoAcessoEm: '17/07/2026',
  },
  {
    id: 'u5',
    nome: 'Diego Nunes',
    email: 'diego@emporiobomsabor.com.br',
    telefone: '(51) 99544-3210',
    papel: 'operator',
    status: 'inativo',
    overrides: [],
    criadoEm: '10/08/2023',
    ultimoAcessoEm: '02/03/2026',
  },
  {
    id: 'u6',
    nome: 'Fernanda Lima',
    email: 'fernanda.contadora@escritoriolima.com.br',
    telefone: null,
    papel: 'viewer',
    status: 'ativo',
    overrides: [],
    criadoEm: '05/01/2025',
    ultimoAcessoEm: '10/07/2026',
  },
];

export const CONFIGURACOES_MOCK: ConfiguracoesMock = {
  empresa: EMPRESA_MOCK,
  usuarios: USUARIOS_MOCK,
};
