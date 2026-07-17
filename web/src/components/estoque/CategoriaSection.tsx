import { MoveLeft } from 'lucide-react';

import { MoneyValue, SectionCard } from '@/components/shared';

import type { CategoriaResumo, EstoqueKpis } from './types';

const CORES = ['hsl(var(--primary))', 'hsl(var(--foreground) / 0.55)', 'hsl(var(--foreground) / 0.4)', 'hsl(var(--foreground) / 0.25)'];
const COR_RESTO = 'hsl(var(--foreground) / 0.15)';

interface CategoriaSectionProps {
  categorias: CategoriaResumo[];
  categoriaAtiva: CategoriaResumo | null;
  onSelecionar: (nome: string) => void;
  onVoltar: () => void;
  kpis: EstoqueKpis;
  semCustoMedio: number;
}

/**
 * Grid2 da Visão Geral (`#cardEsq`/`#cardDir` do mockup): "Valor por categoria" (esquerda, sempre,
 * clicável) × resumo do catálogo ou detalhe da categoria selecionada (direita, alterna). Sem o
 * gráfico "entradas × saídas, 6 semanas" do mockup — precisa de histórico de movimentação, que
 * esta API não tem; o drill mostra só totais reais (valor imobilizado, itens, abaixo do mínimo).
 */
export function CategoriaSection({ categorias, categoriaAtiva, onSelecionar, onVoltar, kpis, semCustoMedio }: CategoriaSectionProps) {
  const totalGeral = categorias.reduce((acc, c) => acc + c.valorCentavos, 0) || 1;

  return (
    <section className="mb-4 grid grid-cols-1 gap-4 lg:grid-cols-[1.15fr_1fr]">
      <SectionCard title="Valor por categoria" hint={categorias.length > 0 ? 'clique numa categoria →' : undefined}>
        {categorias.length === 0 ? (
          <p className="px-[18px] pb-4 pt-1 text-sm text-muted-foreground">Nenhum produto com controle de estoque ainda.</p>
        ) : (
          <div className="flex flex-col gap-1 px-3 pb-4 pt-1">
            {categorias.map((cat, i) => {
              const pct = (cat.valorCentavos / totalGeral) * 100;
              const cor = CORES[i] ?? COR_RESTO;
              return (
                <button
                  key={cat.nome}
                  type="button"
                  onClick={() => onSelecionar(cat.nome)}
                  className="flex w-full flex-col gap-1.5 rounded-[11px] px-2.5 py-2 text-left transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring active:brightness-95"
                >
                  <div className="flex items-center justify-between gap-2.5">
                    <span className="inline-flex items-center text-[13px] font-semibold">
                      <span className="mr-2 h-2.5 w-2.5 flex-none rounded-[3px]" style={{ background: cor }} />
                      {cat.nome}
                    </span>
                    <MoneyValue centavos={cat.valorCentavos} className="text-[12.5px] font-normal text-muted-foreground" />
                  </div>
                  <div className="h-2 overflow-hidden rounded-md bg-surface-2">
                    <div className="h-full rounded-md" style={{ width: `${pct.toFixed(1)}%`, background: cor }} />
                  </div>
                  <div className="flex justify-end">
                    <span className="num text-[12.5px] font-bold">{pct.toFixed(1).replace('.', ',')}%</span>
                  </div>
                </button>
              );
            })}
          </div>
        )}
      </SectionCard>

      {categoriaAtiva ? (
        <SectionCard
          title={
            <span className="inline-flex items-center gap-2">
              <button
                type="button"
                onClick={onVoltar}
                aria-label="Voltar"
                className="grid h-[26px] w-[26px] flex-none place-items-center rounded-lg bg-surface-2 text-foreground transition-colors hover:bg-primary-soft hover:text-primary-600 active:brightness-95"
              >
                <MoveLeft className="h-3.5 w-3.5" />
              </button>
              {categoriaAtiva.nome}
            </span>
          }
          hint={`${categoriaAtiva.itens.length} produto${categoriaAtiva.itens.length === 1 ? '' : 's'}`}
        >
          <div className="flex flex-col gap-2.5 px-[18px] pb-4 pt-1">
            <div className="rounded-xl bg-surface-2 px-3.5 py-3">
              <div className="text-xs font-semibold text-muted-foreground">Valor imobilizado</div>
              <div className="num mt-1 text-lg font-bold">
                <MoneyValue centavos={categoriaAtiva.valorCentavos} />
              </div>
              <div className="mt-0.5 text-xs text-faint">físico × custo médio nesta categoria</div>
            </div>
            <div className="rounded-xl bg-surface-2 px-3.5 py-3">
              <div className="text-xs font-semibold text-muted-foreground">Abaixo do mínimo</div>
              <div className="num mt-1 text-lg font-bold">
                {categoriaAtiva.itens.filter((i) => i.estado.code === 'baixo' || i.estado.code === 'zerado').length}
                <small className="text-sm font-semibold text-muted-foreground"> de {categoriaAtiva.itens.length}</small>
              </div>
            </div>
          </div>
        </SectionCard>
      ) : (
        <SectionCard title="Situação do catálogo" hint="carteira inteira">
          <div className="flex flex-col gap-2.5 px-[18px] pb-4 pt-1">
            <div className="rounded-xl bg-surface-2 px-3.5 py-3">
              <div className="text-xs font-semibold text-muted-foreground">Com controle de estoque</div>
              <div className="num mt-1 text-lg font-bold">
                {kpis.itensComSaldo}
                <small className="text-sm font-semibold text-muted-foreground"> de {kpis.produtosCadastrados}</small>
              </div>
            </div>
            <div className="rounded-xl bg-surface-2 px-3.5 py-3">
              <div className="text-xs font-semibold text-muted-foreground">Sem custo médio informado</div>
              <div className={`num mt-1 text-lg font-bold ${semCustoMedio > 0 ? 'text-warn' : ''}`}>{semCustoMedio}</div>
              <div className="mt-0.5 text-xs text-faint">valor em estoque desses itens fica subestimado</div>
            </div>
          </div>
        </SectionCard>
      )}
    </section>
  );
}
