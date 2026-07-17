import { Users } from 'lucide-react';

import { cn } from '@/lib/utils';

import type { AgendaVm } from './useAgenda';

interface ProfissionalFilterBarProps {
  vm: AgendaVm;
}

function iniciais(nome: string): string {
  return nome
    .split(' ')
    .map((n) => n[0])
    .filter(Boolean)
    .slice(0, 2)
    .join('')
    .toUpperCase();
}

/** Chips "Todos" + 1 por profissional (iniciais + nome), toggle de filtro — clique novamente no
 *  já-selecionado volta pra "Todos". Porte do PROFESSIONAL FILTER BAR (L3740-3790) do saas-erp. */
export function ProfissionalFilterBar({ vm }: ProfissionalFilterBarProps) {
  return (
    <div className="flex flex-none items-center gap-2 overflow-x-auto border-b border-border/60 bg-card px-4 py-2 sm:px-6">
      <Users className="h-3.5 w-3.5 flex-none text-muted-foreground" />
      <button
        type="button"
        onClick={() => vm.setProfissionalFiltro('todos')}
        className={cn(
          'inline-flex flex-none items-center gap-1.5 whitespace-nowrap rounded-lg border px-3 py-1.5 text-xs font-medium transition-colors active:brightness-95',
          vm.profissionalFiltro === 'todos'
            ? 'border-primary-300 bg-primary-soft text-primary-600 dark:border-primary-500/30'
            : 'border-border text-muted-foreground hover:border-border/80 hover:bg-secondary/60',
        )}
      >
        Todos
      </button>
      {vm.profissionais.map((p) => {
        const ativo = vm.profissionalFiltro === p.id;
        return (
          <button
            key={p.id}
            type="button"
            onClick={() => vm.setProfissionalFiltro(ativo ? 'todos' : p.id)}
            className={cn(
              'inline-flex flex-none items-center gap-1.5 whitespace-nowrap rounded-lg border px-3 py-1.5 text-xs font-medium transition-colors active:brightness-95',
              ativo
                ? 'border-primary-300 bg-primary-soft text-primary-600 dark:border-primary-500/30'
                : 'border-border text-muted-foreground hover:border-border/80 hover:bg-secondary/60',
            )}
          >
            <span
              className={cn(
                'flex h-5 w-5 flex-none items-center justify-center rounded-full text-[10px] font-bold',
                ativo ? 'bg-primary-600 text-white' : 'bg-surface-2 text-muted-foreground',
              )}
            >
              {iniciais(p.nome)}
            </span>
            <span className="hidden sm:inline">{p.nome.split(' ')[0]}</span>
          </button>
        );
      })}
    </div>
  );
}
