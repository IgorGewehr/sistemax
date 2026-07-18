import { AlertTriangle } from 'lucide-react';

import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';
import type { PainelDoProjetoDto } from '@/lib/api/financeiro';

import { CapacidadeInvestimentoSection } from './CapacidadeInvestimentoSection';
import { ConsultorProjeto } from './ConsultorProjeto';
import { KpisProjeto } from './KpisProjeto';
import { MargemLtvSection } from './MargemLtvSection';
import type { Recurso } from './useProjetos';

interface ProjetoPainelProps {
  painel: Recurso<PainelDoProjetoDto>;
}

/** Composição do painel do projeto selecionado — `GET /financeiro/projetos/{id}/painel`. */
export function ProjetoPainel({ painel }: ProjetoPainelProps) {
  if (painel.carregando) {
    return (
      <div className="space-y-4">
        <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-2 lg:grid-cols-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <Surface key={i} padding="lg" className="min-h-[110px]">
              <Skeleton className="h-3 w-24" />
              <Skeleton className="mt-3 h-7 w-20" />
            </Surface>
          ))}
        </div>
        <Surface padding="lg" className="min-h-[220px]">
          <Skeleton className="h-4 w-56" />
          <Skeleton className="mt-6 h-[160px] w-full" />
        </Surface>
      </div>
    );
  }

  if (painel.erro || !painel.dado) {
    return (
      <Surface padding="lg">
        <EmptyState
          icon={<AlertTriangle className="h-5 w-5" />}
          title="Não deu para carregar o painel"
          description={painel.erro ?? 'Tente novamente em instantes.'}
          className="border-none py-6"
        />
      </Surface>
    );
  }

  return (
    <div>
      <KpisProjeto painel={painel.dado} />
      <CapacidadeInvestimentoSection painel={painel.dado} />
      <MargemLtvSection painel={painel.dado} />
      <ConsultorProjeto painel={painel.dado} />
    </div>
  );
}
