import { cn } from '@/lib/utils';

interface ToggleRowProps {
  titulo: string;
  descricao: string;
  ativo: boolean;
  salvando: boolean;
  onToggle: () => void;
}

/** Uma linha "Configuração › [switch]" — os dois toggles opt-in do Financeiro. `role="switch"`
 * nativo (sem lib de UI extra) — o `.optin .sw` do mockup era só decorativo (toast); aqui grava
 * de verdade em `PUT /financeiro/configuracoes`. */
export function ToggleRow({ titulo, descricao, ativo, salvando, onToggle }: ToggleRowProps) {
  return (
    <div className="flex items-start justify-between gap-4 rounded-2xl border border-border bg-card px-4 py-4 sm:px-5">
      <div className="min-w-0">
        <div className="flex items-center gap-2">
          <h3 className="text-sm font-bold text-foreground">{titulo}</h3>
          <span
            className={cn(
              'rounded-full px-2 py-0.5 text-[10.5px] font-bold uppercase tracking-wide',
              ativo ? 'bg-pos-soft text-pos' : 'bg-surface-2 text-muted-foreground',
            )}
          >
            {ativo ? 'Ativa' : 'Desligada'}
          </span>
        </div>
        <p className="mt-1 text-[13px] leading-relaxed text-muted-foreground">{descricao}</p>
      </div>

      <button
        type="button"
        role="switch"
        aria-checked={ativo}
        disabled={salvando}
        onClick={onToggle}
        className={cn(
          'relative h-[26px] w-[46px] shrink-0 rounded-full transition-colors disabled:opacity-60',
          ativo ? 'bg-pos' : 'bg-surface-2 ring-1 ring-inset ring-border',
        )}
      >
        <span
          className={cn(
            'absolute top-[3px] h-5 w-5 rounded-full bg-white shadow transition-transform',
            ativo ? 'translate-x-[23px]' : 'translate-x-[3px]',
          )}
        />
      </button>
    </div>
  );
}
