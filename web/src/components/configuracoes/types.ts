import type { Papel, PermissaoOverride, Usuario } from '@/lib/permissions';

/**
 * View-model do módulo Configurações (SDD — este arquivo é o spec). O modelo de permissões em si
 * (`Papel`, `Modulo`, `Acao`, `Permissao`, `PermissaoOverride`, `Usuario`) mora em
 * `@/lib/permissions` — é reusado por Sidebar/Dashboard, não só por Configurações, então a fonte
 * única fica em `lib/`. Este arquivo só acrescenta o que é específico DESTA TELA: Empresa e os
 * formulários (papel/overrides continuam vindo de `lib/permissions`, nunca redeclarados aqui).
 */

export type SecaoConfiguracoes = 'perfil' | 'empresa' | 'usuarios' | 'fiscal' | 'integracoes';

export interface EnderecoEmpresa {
  logradouro: string;
  bairro: string;
  cidade: string;
  uf: string;
  cep: string;
}

export interface Empresa {
  nome: string;
  nomeFantasia: string;
  cnpj: string;
  telefone: string;
  email: string;
  endereco: EnderecoEmpresa;
  /** `null` = sem logo cadastrado — usa iniciais do nome fantasia (`iniciais()` em `calc.ts`). */
  logoUrl: string | null;
}

/** Formulário de editar empresa — hoje é o mesmo shape de `Empresa` (tudo é editável); um tipo à
 *  parte facilita o dia em que algum campo de `Empresa` passar a ser só-leitura/derivado. */
export type EmpresaFormValues = Empresa;

export interface PerfilFormValues {
  nome: string;
  email: string;
  telefone: string;
}

export interface AlterarPinFormValues {
  pinAtual: string;
  pinNovo: string;
  pinConfirmacao: string;
}

/** Formulário de criar/editar usuário — papel define o padrão de permissões; `overrides` é editado
 *  pelo `PermissoesGrid`, nunca como campo de texto solto. */
export interface UsuarioFormValues {
  nome: string;
  email: string;
  telefone: string;
  papel: Papel;
  overrides: PermissaoOverride[];
}

/** Dados de exemplo — hoje mock (`mocks/configuracoes.ts`), amanhã API, sem mudar nenhuma tela. */
export interface ConfiguracoesMock {
  empresa: Empresa;
  usuarios: Usuario[];
}
