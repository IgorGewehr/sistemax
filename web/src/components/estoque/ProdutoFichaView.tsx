import { History, MoveLeft } from 'lucide-react';

import { MoneyValue, SectionCard } from '@/components/shared';
import { EmptyState } from '@/components/ui/EmptyState';
import { Surface } from '@/components/ui/Surface';

import { fmtQty } from './calc';
import { EstadoChip } from './chips';
import type { ProdutoComSaldo } from './types';

interface ProdutoFichaViewProps {
  item: ProdutoComSaldo;
  onVoltar: () => void;
}

/**
 * Ficha do produto (`renderFichaProduto` do mockup) — os 4 stat-cards vêm 100% do saldo real.
 * Sem o gráfico "Consumo × entradas" e sem Kardex: os dois dependem do razão de movimentações,
 * que esta API não expõe ainda (ver README.md) — em vez de inventar números, mostra um estado
 * vazio explicando o que falta.
 */
export function ProdutoFichaView({ item, onVoltar }: ProdutoFichaViewProps) {
  const { produto, saldo, estado } = item;

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-start justify-between gap-4">
        <div>
          <button
            type="button"
            onClick={onVoltar}
            aria-label="Voltar"
            className="mb-2 grid h-[26px] w-[26px] place-items-center rounded-lg bg-surface-2 text-foreground transition-colors hover:bg-primary-soft hover:text-primary-600 active:brightness-95"
          >
            <MoveLeft className="h-3.5 w-3.5" />
          </button>
          <div className="font-display text-xl font-bold tracking-tight text-foreground">{produto.nome}</div>
          <div className="mt-0.5 text-[13px] text-muted-foreground">
            SKU {produto.sku} · {produto.categoria ?? 'Sem categoria'}
          </div>
        </div>
        <EstadoChip code={estado.code} label={estado.label} />
      </div>

      <section className="mb-4 grid grid-cols-2 gap-3.5 lg:grid-cols-4">
        <Surface className="p-3.5 sm:p-4">
          <div className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground">Disponível</div>
          <div className="num mt-2 text-xl font-bold">
            {produto.controlaEstoque ? fmtQty(saldo?.disponivel.milesimos, produto.unidade) : '—'}
          </div>
        </Surface>
        <Surface className="p-3.5 sm:p-4">
          <div className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground">Reservado</div>
          <div className="num mt-2 text-xl font-bold">
            {produto.controlaEstoque ? fmtQty(saldo?.reservado.milesimos, produto.unidade) : '—'}
          </div>
        </Surface>
        <Surface className="p-3.5 sm:p-4">
          <div className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground">Custo médio</div>
          <div className="num mt-2 text-xl font-bold">
            {produto.controlaEstoque && saldo ? <MoneyValue centavos={saldo.custoMedio.centavos} /> : '—'}
          </div>
        </Surface>
        <Surface className="p-3.5 sm:p-4">
          <div className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground">Valor em estoque</div>
          <div className="num mt-2 text-xl font-bold">
            {produto.controlaEstoque && saldo ? <MoneyValue centavos={saldo.valorTotal.centavos} /> : '—'}
          </div>
        </Surface>
      </section>

      <SectionCard title="Kardex — últimos movimentos" hint="razão por produto">
        <div className="px-[18px] pb-5 pt-1">
          <EmptyState
            icon={<History className="h-5 w-5" />}
            title="Sem histórico de movimentações"
            description="O razão de movimentações por produto ainda não tem API própria no Bridge — quando existir, o kardex completo (entradas, saídas, reservas, ajustes) aparece aqui."
            className="border-none py-8"
          />
        </div>
      </SectionCard>
    </div>
  );
}
