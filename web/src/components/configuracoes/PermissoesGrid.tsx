import { Check } from 'lucide-react';

import {
  ACAO_LABEL,
  MODULOS,
  MODULO_ACOES,
  MODULO_LABEL,
  type Acao,
  type Modulo,
  type Papel,
  type PermissaoOverride,
} from '@/lib/permissions';
import { cn } from '@/lib/utils';

import { celulaLigada } from './calc';

interface PermissoesGridProps {
  papel: Papel;
  overrides: PermissaoOverride[];
  onToggle: (modulo: Modulo, acao: Acao) => void;
}

/**
 * Grid módulo × ação do modal de usuário. Não é uma `<table>` de colunas fixas porque cada módulo
 * tem um conjunto de ações diferente (`MODULO_ACOES`) — uma tabela com todas as colunas deixaria a
 * maioria das células vazias sem sentido (só `pdv` tem "abrir/fechar caixa", só `fiscal` tem
 * "emitir", só `configuracoes` tem "gerenciar usuários"). Cada linha só mostra os toggles que
 * existem pra aquele módulo.
 */
export function PermissoesGrid({ papel, overrides, onToggle }: PermissoesGridProps) {
  return (
    <div className="divide-y divide-border/60 rounded-xl border border-border/60">
      {MODULOS.map((modulo) => {
        const personalizado = overrides.some((o) => o.permissao.startsWith(`${modulo}:`));
        return (
          <div key={modulo} className="flex flex-wrap items-center justify-between gap-2.5 px-3 py-2.5">
            <span className="flex items-center gap-1.5 text-[13px] font-medium text-foreground">
              {MODULO_LABEL[modulo]}
              {personalizado && (
                <span className="rounded-full bg-primary-50 px-1.5 py-0.5 text-2xs font-semibold text-primary-600 dark:bg-primary-500/15 dark:text-primary-300">
                  personalizado
                </span>
              )}
            </span>
            <div className="flex flex-wrap items-center gap-1.5">
              {MODULO_ACOES[modulo].map((acao) => {
                const ligado = celulaLigada(papel, overrides, modulo, acao);
                return (
                  <button
                    key={acao}
                    type="button"
                    onClick={() => onToggle(modulo, acao)}
                    className={cn(
                      'inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-[11.5px] font-semibold transition-colors active:brightness-95',
                      ligado ? 'bg-pos-soft text-pos' : 'bg-surface-2 text-muted-foreground hover:text-foreground',
                    )}
                  >
                    {ligado && <Check className="h-3 w-3" strokeWidth={3} />}
                    {ACAO_LABEL[acao]}
                  </button>
                );
              })}
            </div>
          </div>
        );
      })}
    </div>
  );
}
