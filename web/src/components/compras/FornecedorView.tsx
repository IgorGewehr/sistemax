import { MoveLeft } from 'lucide-react';

import { SectionCard } from '@/components/shared';
import { Button } from '@/components/ui/Button';

import { FORNECEDOR_STATUS_LABEL, FORNECEDOR_STATUS_TONE } from './calc';
import { Chip } from './chips';
import { DeParasCard } from './DeParasCard';
import { FornecedorStats } from './FornecedorStats';
import { LineChart } from './LineChart';
import { NotasFornecedorTable } from './NotasFornecedorTable';
import type { Fornecedor } from './types';
import type { ComprasVm } from './useCompras';

interface FornecedorViewProps {
  vm: ComprasVm;
  fornecedor: Fornecedor;
}

/** Drill de fornecedor — 1:1 com `view-fornecedor` de `docs/ui/mockups/compras.html` (Tela 9.5). */
export function FornecedorView({ vm, fornecedor }: FornecedorViewProps) {
  const notasDoFornecedor = vm.notas.filter((n) => n.fornecedorId === fornecedor.id);
  const vinculosDoFornecedor = vm.vinculos.filter((v) => v.fornecedorId === fornecedor.id);

  return (
    <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:py-8">
      <div className="mb-4 inline-flex items-center gap-2.5">
        <button
          type="button"
          onClick={vm.irParaHome}
          aria-label="Voltar"
          className="grid h-[26px] w-[26px] flex-none place-items-center rounded-lg bg-surface-2 text-foreground transition-colors hover:bg-primary-soft hover:text-primary-600 active:brightness-95"
        >
          <MoveLeft className="h-3.5 w-3.5" />
        </button>
        <div className="flex flex-col gap-0.5">
          <div className="text-xs font-semibold text-muted-foreground">FORNECEDOR</div>
          <h1 className="text-xl font-bold tracking-tight">{fornecedor.nome}</h1>
        </div>
      </div>

      <div className="mb-4 flex flex-wrap items-start justify-between gap-4">
        <div className="text-[13px] text-muted-foreground">
          {fornecedor.cnpj ? `CNPJ ${fornecedor.cnpj}` : 'sem CNPJ (compra sem NF-e)'} ·{' '}
          <Chip tone={FORNECEDOR_STATUS_TONE[fornecedor.status]}>{FORNECEDOR_STATUS_LABEL[fornecedor.status]}</Chip>
        </div>
        <Button size="sm" onClick={vm.onNovoPedidoForn}>
          + Novo pedido de compra
        </Button>
      </div>

      <FornecedorStats fornecedor={fornecedor} />

      <section className="mb-4 grid grid-cols-1 gap-4 lg:grid-cols-2">
        <SectionCard title="Histórico de custo · itens mais comprados">
          <div className="px-[18px] pb-1 pt-2">
            <LineChart series={vm.historicoCustoDemo} />
          </div>
          <div className="flex flex-wrap gap-4 px-[18px] pb-3.5 pt-1 text-xs text-muted-foreground">
            {vm.historicoCustoDemo.map((s) => (
              <span key={s.nome} className="inline-flex items-center gap-1.5">
                <i className="inline-block h-2.5 w-2.5 rounded-[3px]" style={{ background: s.cor === 'primary' ? 'hsl(var(--primary))' : 'hsl(var(--foreground) / 0.5)' }} />
                {s.nome}
              </span>
            ))}
          </div>
        </SectionCard>

        <SectionCard title="Notas deste fornecedor" bodyClassName="mt-2 overflow-x-auto">
          <NotasFornecedorTable notas={notasDoFornecedor} onAbrirNota={vm.irParaConferencia} />
        </SectionCard>
      </section>

      <DeParasCard vinculos={vinculosDoFornecedor} />
    </div>
  );
}
