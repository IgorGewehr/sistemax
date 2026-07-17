/**
 * Resumo REAL da lente Assinaturas — direto de `GET /financeiro/receita-recorrente`
 * (`useReceitaRecorrente`), sem passar pelos cálculos derivados de `RECORRENTES_MOCK` que
 * alimentam `AssinKpis`/`AssinAnalisePanel` logo abaixo. Vive num bloco PRÓPRIO de propósito —
 * nunca misturado num mesmo card com número mock (ver `MockBadge`: "um número real e um de
 * exemplo lado a lado sem marcação são indistinguíveis pro usuário"). Aqui a marcação é a seção:
 * este bloco não tem badge (é real); o painel abaixo mantém o dele.
 */
import { Database } from 'lucide-react';
import type { ReactNode } from 'react';

import { MoneyWhole } from '@/components/shared';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';
import { cn } from '@/lib/utils';

import type { UseReceitaRecorrenteResult } from './useReceitaRecorrente';

interface AssinResumoRealProps {
  recurso: UseReceitaRecorrenteResult;
  className?: string;
}

interface TileProps {
  label: string;
  value: ReactNode;
}

function Tile({ label, value }: TileProps) {
  return (
    <div className="min-w-0">
      <div className="truncate text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{label}</div>
      <div className="num mt-1 truncate text-lg font-bold text-foreground">{value}</div>
    </div>
  );
}

export function AssinResumoReal({ recurso, className }: AssinResumoRealProps) {
  const { dado, erro, carregando } = recurso;

  return (
    <Surface padding="none" className={cn('mb-4 flex items-center gap-4 overflow-x-auto p-3.5 sm:p-4', className)}>
      <div className="flex shrink-0 items-center gap-1.5 text-xs font-semibold text-primary-600">
        <Database className="h-3.5 w-3.5" />
        Direto do backend agora
      </div>

      {carregando ? (
        <div className="flex flex-1 gap-6">
          <Skeleton className="h-9 w-24" />
          <Skeleton className="h-9 w-24" />
          <Skeleton className="h-9 w-24" />
          <Skeleton className="h-9 w-24" />
        </div>
      ) : erro ? (
        <p className="text-sm text-muted-foreground">Não deu para carregar o resumo real: {erro}</p>
      ) : (
        dado && (
          <div className="grid flex-1 grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-4">
            <Tile label="Assinaturas ativas" value={dado.assinaturasAtivasCount} />
            <Tile label="MRR" value={<MoneyWhole centavos={dado.mrrCentavos} />} />
            <Tile label="ARR" value={<MoneyWhole centavos={dado.arrCentavos} />} />
            <Tile label="Ticket médio" value={<MoneyWhole centavos={dado.ticketMedioCentavos} />} />
          </div>
        )
      )}
    </Surface>
  );
}
