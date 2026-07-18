import type { PainelDoProjetoDto, ProjetoDto } from '@/lib/api/financeiro';
import { formatCentavosWhole } from '@/lib/money';
import { cn } from '@/lib/utils';

import type { Recurso } from './useProjetos';

interface ProjetoSeletorProps {
  projetos: ProjetoDto[];
  paineis: Record<string, Recurso<PainelDoProjetoDto>>;
  selecionadoId: string | null;
  onSelecionar: (id: string) => void;
}

/** Paleta cíclica dos `pdot` do mockup (primary/pos/azul) — projetos reais não têm cor própria no
 * domínio, então ciclamos por posição, igual ao mockup faz com 3 exemplos fixos. */
const DOT_CLASSES = ['bg-primary-600', 'bg-pos', 'bg-[hsl(215_60%_55%)]', 'bg-[hsl(38_92%_50%)]', 'bg-[hsl(268_50%_58%)]'];

/** `.projbar` do mockup — grade de cards clicáveis, um por projeto. Cada card mostra o MRR e a
 * contagem de assinaturas vindos do PRÓPRIO painel do projeto (não existe resumo na listagem). */
export function ProjetoSeletor({ projetos, paineis, selecionadoId, onSelecionar }: ProjetoSeletorProps) {
  return (
    <div className="mb-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
      {projetos.map((projeto, i) => {
        const painel = paineis[projeto.id];
        const ativo = projeto.id === selecionadoId;
        return (
          <button
            key={projeto.id}
            type="button"
            onClick={() => onSelecionar(projeto.id)}
            className={cn(
              'flex flex-col gap-0.5 rounded-2xl border bg-card px-4 py-3.5 text-left transition-all hover:-translate-y-0.5 hover:shadow-[0_10px_24px_-16px_hsl(var(--shadow)/0.6)]',
              ativo ? 'border-primary-600/55 shadow-[0_6px_18px_-12px_hsl(var(--primary)/0.5)]' : 'border-border',
            )}
          >
            <span className="flex items-center gap-2 text-sm font-bold text-foreground">
              <span className={cn('h-[9px] w-[9px] shrink-0 rounded-[3px]', DOT_CLASSES[i % DOT_CLASSES.length])} />
              {projeto.nome}
            </span>
            <span className="text-[11.5px] text-muted-foreground">{projeto.descricao || 'Sem descrição'}</span>
            <span className="num mt-2 text-xl font-bold tracking-tight text-foreground">
              {painel?.carregando ? (
                <span className="inline-block h-5 w-20 animate-pulse rounded bg-surface-2 align-middle" />
              ) : painel?.dado ? (
                <>
                  {formatCentavosWhole(painel.dado.receita.mrr.centavos)}
                  <small className="text-[13px] font-semibold text-muted-foreground">/mês</small>
                </>
              ) : (
                '—'
              )}
            </span>
            <span className="text-[11.5px] text-muted-foreground">
              {painel?.dado
                ? `${painel.dado.receita.assinaturasAtivas} assinatura${painel.dado.receita.assinaturasAtivas === 1 ? '' : 's'}${
                    painel.dado.capacidade.unidadesTotais > 0
                      ? ` · ${painel.dado.capacidade.unidadesUtilizadas}/${painel.dado.capacidade.unidadesTotais} licenças em uso`
                      : ''
                  }`
                : ' '}
            </span>
          </button>
        );
      })}
    </div>
  );
}
