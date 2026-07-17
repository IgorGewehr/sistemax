import { Building2, Plug, Receipt, UserCircle, Users } from 'lucide-react';
import type { ComponentType } from 'react';

import { cn } from '@/lib/utils';

import type { SecaoConfiguracoes } from './types';

interface NavItem {
  id: SecaoConfiguracoes;
  label: string;
  icon: ComponentType<{ className?: string }>;
}

const ITENS: NavItem[] = [
  { id: 'perfil', label: 'Perfil', icon: UserCircle },
  { id: 'empresa', label: 'Empresa', icon: Building2 },
  { id: 'usuarios', label: 'Usuários & Permissões', icon: Users },
  { id: 'fiscal', label: 'Fiscal', icon: Receipt },
  { id: 'integracoes', label: 'Integrações', icon: Plug },
];

interface ConfiguracoesNavProps {
  secaoAtiva: SecaoConfiguracoes;
  /**
   * Some do menu (não só desabilita) quando o usuário não tem a permissão — ex.: "Usuários &
   * Permissões" exige `configuracoes:gerenciarUsuarios`. Visibilidade de SEÇÃO deriva de
   * permissão, mesmo princípio que a Sidebar já aplica a módulo inteiro (`lib/permissions.ts`);
   * aqui é o mesmo princípio um nível abaixo, dentro do próprio módulo Configurações.
   */
  secoesVisiveis: SecaoConfiguracoes[];
  onTrocarSecao: (secao: SecaoConfiguracoes) => void;
}

/** Sub-nav lateral das 5 seções de Configurações (tabs em telas estreitas). */
export function ConfiguracoesNav({ secaoAtiva, secoesVisiveis, onTrocarSecao }: ConfiguracoesNavProps) {
  return (
    <nav aria-label="Seções de Configurações" className="flex gap-1 overflow-x-auto pb-1 scrollbar-hide md:flex-col md:overflow-visible md:pb-0">
      {ITENS.filter((item) => secoesVisiveis.includes(item.id)).map((item) => {
        const ativo = item.id === secaoAtiva;
        return (
          <button
            key={item.id}
            type="button"
            onClick={() => onTrocarSecao(item.id)}
            className={cn(
              'flex shrink-0 items-center gap-2.5 rounded-xl px-3 py-2.5 text-left text-sm font-medium transition-colors',
              ativo
                ? 'bg-primary-50 text-primary-700 dark:bg-primary-500/15 dark:text-primary-300'
                : 'text-muted-foreground hover:bg-surface-2 hover:text-foreground',
            )}
          >
            <item.icon className="h-4 w-4 shrink-0" />
            <span className="whitespace-nowrap md:whitespace-normal">{item.label}</span>
          </button>
        );
      })}
    </nav>
  );
}
