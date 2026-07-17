import { AlertCircle } from 'lucide-react';
import type { ReactNode } from 'react';

import { MoneyValue } from '@/components/shared';
import { formatDateShort } from '@/lib/format';
import { cn } from '@/lib/utils';

import { addDias, ehTerminal, entrouEm, indiceAtualDaLinha, passosDaLinhaDoTempo, TITULOS, totalOrcamento } from './calc';
import { CorpoAberta, CorpoAguardandoAprovacao, CorpoAprovada, CorpoDiagnostico, CorpoExecucao, CorpoPronta } from './StepBodies';
import type { OrdemServico, OsStatus } from './types';
import type { UseOrdemServico } from './useOrdemServico';

interface TimelineProps {
  os: OrdemServico;
  vm: UseOrdemServico;
}

/**
 * Linha do tempo vertical da FSM (`.timeline` do mockup) — um passo por status percorrido, mais
 * os "fantasmas" do que ainda vem. Reconstrói exatamente a lógica do mockup: `jaPassou` vira
 * item colapsável, o passo no índice atual vira o formulário interativo, o resto vira ghost.
 */
export function Timeline({ os, vm }: TimelineProps) {
  const passos = passosDaLinhaDoTempo(os);
  const indiceAtual = indiceAtualDaLinha(os, passos);

  return (
    <div className="mt-4 flex flex-col">
      {passos.map((passo, i) => {
        const em = entrouEm(os, passo);
        const ehAtualDeVerdade = i === indiceAtual && os.status !== 'Cancelada';
        const jaPassou = i < indiceAtual || (i === indiceAtual && (os.status === 'Cancelada' || ehTerminal(os.status)));

        if (jaPassou) return <StepConcluido key={passo} os={os} passo={passo} em={em} />;
        if (ehAtualDeVerdade) return <StepAtual key={passo} os={os} passo={passo} vm={vm} />;
        // Cancelada não tem "próximos passos" a antecipar — a linha do tempo simplesmente para aqui.
        if (i > indiceAtual) return os.status === 'Cancelada' ? null : <StepFantasma key={passo} passo={passo} />;
        return null;
      })}
      {os.status === 'Cancelada' && <BannerCancelada os={os} />}
    </div>
  );
}

const railBase = 'relative border-l-2 py-0 pb-[22px] pl-5 last:pb-0.5';
const dotBase = 'absolute -left-[7px] top-0.5 h-3 w-3 rounded-full border-2 bg-card';

function StepConcluido({ os, passo, em }: { os: OrdemServico; passo: OsStatus; em: Date | null }) {
  const titulo = TITULOS[passo];
  const quando = em ? `${formatDateShort(em)} · por ${os.tecnico || '—'}` : '';

  return (
    <div className={cn(railBase, 'border-pos/40')}>
      <span className={cn(dotBase, 'border-pos bg-pos')} />
      <details className="group">
        <summary className="flex cursor-pointer list-none items-baseline gap-2.5 text-[12.5px] font-bold uppercase tracking-wide [&::-webkit-details-marker]:hidden">
          <span className="flex-1">
            ✔ {titulo.toUpperCase()} <span className="font-medium normal-case tracking-normal text-muted-foreground">{quando}</span>
          </span>
          <span className="text-faint transition-transform group-open:rotate-0 -rotate-90">▾</span>
        </summary>
        <div className="mt-1.5 text-[13px] leading-relaxed text-muted-foreground">
          <ResumoPassoConcluido os={os} passo={passo} />
        </div>
      </details>
    </div>
  );
}

/** Resumo de cada passo já concluído — mesmo texto do `renderPassoConcluido` do mockup, por status. */
function ResumoPassoConcluido({ os, passo }: { os: OrdemServico; passo: OsStatus }) {
  const b = (children: ReactNode) => <b className="font-semibold text-foreground">{children}</b>;

  switch (passo) {
    case 'Aberta':
      return (
        <>
          {os.marca} {os.modelo} · série {os.serie} · acessórios: {os.acessorios}
          <br />
          Defeito relatado: &quot;{b(os.defeito)}&quot;
        </>
      );
    case 'EmDiagnostico':
      return <>{os.diagnostico}</>;
    case 'AguardandoAprovacao': {
      if (!os.orcamento) return null;
      const pecasSum = os.orcamento.pecas.reduce((s, p) => s + p.preco * p.qtd, 0);
      return (
        <>
          Peças <MoneyValue centavos={pecasSum} /> + mão de obra <MoneyValue centavos={os.orcamento.maoDeObra} /> ={' '}
          {b(<MoneyValue centavos={totalOrcamento(os.orcamento)} />)}
        </>
      );
    }
    case 'Aprovada':
      return <>Aprovado via {os.aprovacao?.canal}</>;
    case 'Reprovada':
      return (
        <>
          Reprovado via {os.aprovacao?.canal}
          {os.motivoReprovacao ? ` — "${os.motivoReprovacao}"` : ''}
        </>
      );
    case 'EmExecucao': {
      const pecasRef = os.pecasExecucao ?? os.orcamento?.pecas ?? [];
      const aplicadas = os.pecasExecucao ? os.pecasExecucao.filter((p) => p.aplicada).length : pecasRef.length;
      return (
        <>
          Execução concluída — {aplicadas}/{pecasRef.length} peças aplicadas
        </>
      );
    }
    case 'Pronta':
      return <>Pronta desde {formatDateShort(entrouEm(os, 'Pronta'))}</>;
    case 'Entregue': {
      if (!os.dataEntrega) return null;
      const garantiaAte = addDias(os.dataEntrega, os.garantiaDias ?? 0);
      return (
        <>
          Entregue {formatDateShort(os.dataEntrega)} · {os.formaPagamento}
          {os.desconto ? (
            <>
              {' '}
              · desconto <MoneyValue centavos={os.desconto} />
            </>
          ) : null}
          <br />
          Serviço <MoneyValue centavos={os.valorServico} /> + peças <MoneyValue centavos={os.valorPecas} /> ={' '}
          {b(<MoneyValue centavos={(os.valorServico ?? 0) + (os.valorPecas ?? 0)} />)}
          <br />
          Garantia {os.garantiaDias} dias — até {formatDateShort(garantiaAte)}
        </>
      );
    }
    case 'DevolvidaSemReparo':
      return (
        <>
          Devolvido sem reparo {formatDateShort(os.dataEntrega)}
          {os.taxaDiagnostico ? (
            <>
              {' '}
              · taxa de diagnóstico <MoneyValue centavos={os.taxaDiagnostico} />
            </>
          ) : (
            ' · sem taxa de diagnóstico'
          )}
        </>
      );
    default:
      return null;
  }
}

const GHOST_SUB: Partial<Record<OsStatus, string>> = {
  Aprovada: 'aguardando decisão do cliente',
  EmExecucao: 'peças + mão de obra final',
  Pronta: 'avisará o cliente (WhatsApp)',
  Entregue: 'pagamento + garantia + recibo',
  DevolvidaSemReparo: 'taxa de diagnóstico, se houver',
};

function StepFantasma({ passo }: { passo: OsStatus }) {
  return (
    <div className={cn(railBase, 'border-border')}>
      <span className={cn(dotBase, 'border-border bg-surface-2')} />
      <div className="text-[12.5px] font-bold uppercase tracking-wide text-faint">○ {TITULOS[passo].toUpperCase()}</div>
      <div className="mt-1.5 text-[13px] leading-relaxed text-muted-foreground">{GHOST_SUB[passo] ?? ''}</div>
    </div>
  );
}

function BannerCancelada({ os }: { os: OrdemServico }) {
  return (
    <div className={cn(railBase, 'border-primary-600')}>
      <span
        className={cn(dotBase, 'left-[-8px] h-3.5 w-3.5 border-primary-600 bg-primary-600')}
        style={{ boxShadow: '0 0 0 4px hsl(var(--primary-soft))' }}
      />
      <div className="text-[12.5px] font-bold uppercase tracking-wide">
        ✕ CANCELADA <span className="font-medium normal-case tracking-normal text-muted-foreground">{formatDateShort(entrouEm(os, 'Cancelada'))}</span>
      </div>
      <div className="mt-3 flex items-start gap-2.5 rounded-xl bg-crit-soft px-3.5 py-3 text-[13px] text-crit">
        <AlertCircle className="mt-px h-4 w-4 flex-none" />
        <div>Motivo: {os.motivoCancelamento}</div>
      </div>
    </div>
  );
}

function StepAtual({ os, passo, vm }: { os: OrdemServico; passo: OsStatus; vm: UseOrdemServico }) {
  const titulo = TITULOS[passo];
  return (
    <div className={cn(railBase, 'border-primary-600')}>
      <span className={cn(dotBase, 'left-[-8px] h-3.5 w-3.5 border-primary-600 bg-primary-600')} style={{ boxShadow: '0 0 0 4px hsl(var(--primary-soft))' }} />
      <div className="text-[12.5px] font-bold uppercase tracking-wide">
        ● {titulo.toUpperCase()} <span className="font-medium normal-case tracking-normal text-muted-foreground">em andamento</span>
      </div>
      <div className="mt-3 rounded-xl bg-surface-2 px-4 py-3.5" key={os.numero}>
        <CorpoDoPassoAtual os={os} passo={passo} vm={vm} />
      </div>
    </div>
  );
}

/**
 * Dispatch pro corpo do passo atual. `Reprovada` não tem corpo no mockup aprovado — a transição
 * pra `DevolvidaSemReparo` não tem UI própria ali (gap real do protótipo, não inventamos aqui).
 */
function CorpoDoPassoAtual({ os, passo, vm }: { os: OrdemServico; passo: OsStatus; vm: UseOrdemServico }) {
  switch (passo) {
    case 'Aberta':
      return <CorpoAberta os={os} vm={vm} />;
    case 'EmDiagnostico':
      return <CorpoDiagnostico os={os} vm={vm} />;
    case 'AguardandoAprovacao':
      return <CorpoAguardandoAprovacao os={os} vm={vm} />;
    case 'Aprovada':
      return <CorpoAprovada os={os} vm={vm} />;
    case 'EmExecucao':
      return <CorpoExecucao os={os} vm={vm} />;
    case 'Pronta':
      return <CorpoPronta os={os} vm={vm} />;
    default:
      return null;
  }
}
