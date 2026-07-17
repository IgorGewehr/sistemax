import { ArrowLeft } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';

import { formatDateShort } from '@/lib/format';
import { cn } from '@/lib/utils';

import { atrasada, diasDesde, ehTerminal } from './calc';
import { OsStatusChip } from './OsStatusChip';
import type { OrdemServico } from './types';

interface DetalheHeaderProps {
  os: OrdemServico;
  onVoltar: () => void;
  onCancelar: (numero: string) => void;
  onImprimir: () => void;
}

/** Cabeçalho do detalhe (`.voltar` + `.os-head` do mockup) — título, meta, e o menu "Ações ⋯". */
export function DetalheHeader({ os, onVoltar, onCancelar, onImprimir }: DetalheHeaderProps) {
  const desde = diasDesde(os.abertaEm);
  const prazoTxt = os.prazo ? `prazo ${formatDateShort(os.prazo)}${atrasada(os) ? ' · atrasada' : ''}` : 'sem prazo definido';

  return (
    <div className="mb-1">
      <button type="button" onClick={onVoltar} className="mb-1.5 inline-flex items-center gap-1.5 py-1.5 text-sm font-semibold text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-[15px] w-[15px]" strokeWidth={2.4} /> Ordens de Serviço
      </button>

      <div className="flex flex-wrap items-start justify-between gap-3.5">
        <div>
          <div className="flex flex-wrap items-center gap-2.5 text-[19px] font-bold tracking-tight">
            {os.numero} · {os.equipamento} · {os.cliente} <OsStatusChip status={os.status} atrasada={atrasada(os)} />
          </div>
          <div className="mt-1 text-[13px] text-muted-foreground">
            {os.telefone} · aberta há {desde}d · {prazoTxt}
          </div>
        </div>

        <AcoesMenu os={os} onCancelar={onCancelar} onImprimir={onImprimir} />
      </div>
    </div>
  );
}

function AcoesMenu({ os, onCancelar, onImprimir }: { os: OrdemServico; onCancelar: (numero: string) => void; onImprimir: () => void }) {
  const [aberto, setAberto] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!aberto) return;
    function fechar() {
      setAberto(false);
    }
    document.addEventListener('click', fechar, { once: true });
    return () => document.removeEventListener('click', fechar);
  }, [aberto]);

  const terminal = ehTerminal(os.status);

  return (
    <div ref={wrapRef} className="relative">
      <button
        type="button"
        onClick={(e) => {
          e.stopPropagation();
          setAberto((v) => !v);
        }}
        className="rounded-[10px] border border-border bg-card px-[11px] py-2 text-[13px] font-semibold text-foreground hover:bg-surface-2"
      >
        Ações ⋯
      </button>
      {aberto && (
        <div className="absolute right-0 top-[calc(100%+6px)] z-10 min-w-[168px] rounded-xl border border-border bg-card p-1.5 shadow-lg">
          <button
            type="button"
            disabled={terminal}
            onClick={() => onCancelar(os.numero)}
            className={cn(
              'w-full rounded-lg px-2.5 py-2 text-left text-sm',
              terminal ? 'cursor-not-allowed text-faint' : 'text-crit hover:bg-surface-2',
            )}
          >
            Cancelar OS
          </button>
          <button type="button" onClick={onImprimir} className="w-full rounded-lg px-2.5 py-2 text-left text-sm text-foreground hover:bg-surface-2">
            Imprimir via/recibo
          </button>
          <div className="px-2.5 pb-1.5 pt-0.5 text-[11px] text-faint">impressão: fase D do roteiro</div>
        </div>
      )}
    </div>
  );
}
