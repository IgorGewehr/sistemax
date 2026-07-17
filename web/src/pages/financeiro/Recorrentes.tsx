import { ChevronDown, Plus, Repeat } from 'lucide-react';
import { useState } from 'react';

import { AssinaturasTabelaReal } from '@/components/financial/recorrentes/AssinaturasTabelaReal';
import { AssinResumoReal } from '@/components/financial/recorrentes/AssinResumoReal';
import { FixasTabelaReal } from '@/components/financial/recorrentes/FixasTabelaReal';
import { LensSwitch } from '@/components/financial/recorrentes/LensSwitch';
import { PainelAssinaturas } from '@/components/financial/recorrentes/PainelAssinaturas';
import { PainelContasFixas } from '@/components/financial/recorrentes/PainelContasFixas';
import type { LenteRecorrentes } from '@/components/financial/recorrentes/types';
import { useAssinaturasDetalhe } from '@/components/financial/recorrentes/useAssinaturasDetalhe';
import { useContasFixasReal } from '@/components/financial/recorrentes/useContasFixasReal';
import { useReceitaRecorrente } from '@/components/financial/recorrentes/useReceitaRecorrente';
import { PageHeader } from '@/components/shared';
import { Button } from '@/components/ui/Button';
import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';
import { useToast } from '@/lib/toast';
import { RECORRENTES_MOCK } from '@/mocks/financeiro/recorrentes';

const LENS_COPY: Record<LenteRecorrentes, { titulo: string; subtitulo: string; botao: string }> = {
  fixas: {
    titulo: 'Contas fixas',
    subtitulo: 'O que custa existir todo mês — antes de vender qualquer coisa.',
    botao: 'Nova recorrência',
  },
  assinaturas: {
    titulo: 'Assinaturas',
    subtitulo: 'Seus softwares, os clientes que pagam por eles, e o que isso vale de verdade.',
    botao: 'Nova assinatura',
  },
};

/**
 * Financeiro › Recorrentes — página fina: só compõe as seções por lente.
 * Mockups: `docs/ui/mockups/recorrentes.html` (Contas fixas) +
 * `docs/ui/mockups/financeiro-assinaturas.html` (números do resumo de Assinaturas).
 * O resumo agregado de Assinaturas (MRR/ARR/ticket médio) e as duas tabelas nominais "Todas as ..."
 * já são dado REAL — ver `useReceitaRecorrente`/`useAssinaturasDetalhe`/`useContasFixasReal`. O
 * retrato analítico com histórico de 6/12 meses (`RetratoFixo`, MRR por serviço, retenção da
 * carteira) continua ilustrativo (`RECORRENTES_MOCK`) — esse cruzamento ainda não tem read-model
 * (docs/wiring/financeiro-telas-restantes.md §2).
 */
export function Recorrentes() {
  const [lens, setLens] = useState<LenteRecorrentes>('fixas');
  const { toast } = useToast();
  const copy = LENS_COPY[lens];
  const resumoReal = useReceitaRecorrente();
  const assinaturasDetalhe = useAssinaturasDetalhe();
  const fixasReal = useContasFixasReal();

  return (
    <div>
      <PageHeader
        subtitle={copy.subtitulo}
        actions={
          <>
            <div className="inline-flex items-center gap-2 rounded-xl border border-border bg-card px-3 py-2 text-sm font-semibold text-foreground">
              {RECORRENTES_MOCK.periodoLabel}
              <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
            </div>
            <Button
              icon={<Plus className="h-[15px] w-[15px]" strokeWidth={2.4} />}
              onClick={() => toast(`Cadastro de ${lens === 'fixas' ? 'nova recorrência' : 'nova assinatura'} — formulário aberto.`, 'info')}
            >
              {copy.botao}
            </Button>
          </>
        }
      />

      <LensSwitch lens={lens} onChange={setLens} />

      {lens === 'fixas' ? (
        <>
          <PainelContasFixas data={RECORRENTES_MOCK.fixas} />
          {fixasReal.carregando ? (
            <Surface padding="lg" className="mb-4 min-h-[160px]">
              <Skeleton className="h-32 w-full" />
            </Surface>
          ) : fixasReal.erro || !fixasReal.dado ? (
            <Surface padding="lg" className="mb-4">
              <EmptyState
                icon={<Repeat className="h-5 w-5" />}
                title="Não deu para carregar as recorrências"
                description={fixasReal.erro ?? ''}
                className="border-none py-6"
              />
            </Surface>
          ) : (
            <FixasTabelaReal itens={fixasReal.dado} />
          )}
        </>
      ) : (
        <>
          <AssinResumoReal recurso={resumoReal} />
          {assinaturasDetalhe.carregando ? (
            <Surface padding="lg" className="mb-4 min-h-[160px]">
              <Skeleton className="h-32 w-full" />
            </Surface>
          ) : assinaturasDetalhe.erro || !assinaturasDetalhe.dado ? (
            <Surface padding="lg" className="mb-4">
              <EmptyState
                icon={<Repeat className="h-5 w-5" />}
                title="Não deu para carregar as assinaturas"
                description={assinaturasDetalhe.erro ?? ''}
                className="border-none py-6"
              />
            </Surface>
          ) : (
            <AssinaturasTabelaReal itens={assinaturasDetalhe.dado} />
          )}
          <PainelAssinaturas data={RECORRENTES_MOCK.assinaturas} />
        </>
      )}
    </div>
  );
}
