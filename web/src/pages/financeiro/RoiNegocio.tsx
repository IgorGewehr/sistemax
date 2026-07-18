import { ChevronDown, TrendingUp } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

import { AportesList } from '@/components/financial/roi-negocio/AportesList';
import { ConsultorRoi } from '@/components/financial/roi-negocio/ConsultorRoi';
import { ImobilizadoTable } from '@/components/financial/roi-negocio/ImobilizadoTable';
import { KpisRoi } from '@/components/financial/roi-negocio/KpisRoi';
import { RoiChartCard } from '@/components/financial/roi-negocio/RoiChartCard';
import { useRoiNegocio } from '@/components/financial/roi-negocio/useRoiNegocio';
import { OptInEmptyState, PageHeader } from '@/components/shared';
import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';
import { formatDate } from '@/lib/format';
import { useToast } from '@/lib/toast';

/**
 * Financeiro › Investimento & ROI — 1:1 com `docs/ui/mockups/roi-negocio.html`: imobilizado +
 * painel de ROI do negócio (payback simples/descontado, TIR, curva investido×recuperado). Dado
 * REAL: `GET /financeiro/configuracoes` + `GET /financeiro/roi-negocio` (404 com o toggle
 * desligado) + `GET /financeiro/imobilizado` + `GET /financeiro/aportes` (ver `useRoiNegocio`).
 */
export function RoiNegocio() {
  const vm = useRoiNegocio();
  const { toast } = useToast();
  const navigate = useNavigate();

  const ativo = vm.configuracao.dado?.imobilizadoRoiAtivo ?? null;

  return (
    <div>
      <PageHeader
        subtitle="Quanto entrou de investimento, quanto o negócio devolveu — e em quantos meses ele se paga."
        actions={
          <>
            {ativo !== null && (
              <button
                type="button"
                onClick={() => navigate('/financeiro/configuracoes')}
                className={
                  ativo
                    ? 'inline-flex items-center gap-2 rounded-xl border border-pos/40 bg-pos-soft px-3 py-1.5 text-xs font-semibold text-pos'
                    : 'inline-flex items-center gap-2 rounded-xl border border-border bg-surface-2 px-3 py-1.5 text-xs font-semibold text-muted-foreground'
                }
                title="Ver/alterar em Configurações"
              >
                <span className={`h-[9px] w-[16px] rounded-full ${ativo ? 'bg-pos' : 'bg-faint'}`} />
                Imobilizado &amp; ROI · {ativo ? 'Ativo' : 'Desligado'}
              </button>
            )}
            <button
              type="button"
              onClick={() => toast('Trocar o marco recalcularia payback, TIR e a curva a partir do novo início.', 'info')}
              className="inline-flex items-center gap-2 rounded-xl border border-border bg-card px-3 py-2 text-sm font-semibold text-foreground"
            >
              Desde a abertura{vm.roi.dado ? ` · ${formatDate(vm.roi.dado.marcoInicial)}` : ''}
              <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
            </button>
          </>
        }
      />

      <Conteudo vm={vm} />
    </div>
  );
}

function Conteudo({ vm }: { vm: ReturnType<typeof useRoiNegocio> }) {
  if (vm.configuracao.carregando) {
    return (
      <div className="space-y-4">
        <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Surface key={i} padding="lg" className="min-h-[110px]">
              <Skeleton className="h-3 w-24" />
              <Skeleton className="mt-3 h-7 w-20" />
            </Surface>
          ))}
        </div>
        <Surface padding="lg" className="min-h-[260px]">
          <Skeleton className="h-4 w-56" />
          <Skeleton className="mt-6 h-[180px] w-full" />
        </Surface>
      </div>
    );
  }

  if (vm.configuracao.erro) {
    return (
      <Surface padding="lg">
        <EmptyState icon={<TrendingUp className="h-5 w-5" />} title="Não deu para carregar" description={vm.configuracao.erro} className="border-none py-6" />
      </Surface>
    );
  }

  if (vm.configuracao.dado && !vm.configuracao.dado.imobilizadoRoiAtivo) {
    return (
      <OptInEmptyState
        titulo="Imobilizado & ROI está desligado"
        descricao="Ative Imobilizado & ROI em Configurações para registrar bens, aportes e acompanhar o payback e a TIR do negócio."
      />
    );
  }

  if (vm.roi.carregando) {
    return (
      <div className="space-y-4">
        <div className="grid grid-cols-1 gap-3.5 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Surface key={i} padding="lg" className="min-h-[110px]">
              <Skeleton className="h-3 w-24" />
              <Skeleton className="mt-3 h-7 w-20" />
            </Surface>
          ))}
        </div>
        <Surface padding="lg" className="min-h-[260px]">
          <Skeleton className="h-4 w-56" />
          <Skeleton className="mt-6 h-[180px] w-full" />
        </Surface>
      </div>
    );
  }

  if (vm.roi.erro) {
    return (
      <Surface padding="lg">
        <EmptyState icon={<TrendingUp className="h-5 w-5" />} title="Não deu para carregar o painel de ROI" description={vm.roi.erro} className="border-none py-6" />
      </Surface>
    );
  }

  if (!vm.roi.dado) {
    return (
      <EmptyState
        icon={<TrendingUp className="h-5 w-5" />}
        title="Ainda sem dados de investimento"
        description="Registre um bem em Imobilizado ou um aporte de capital para o painel de ROI começar a calcular."
      />
    );
  }

  const bens = vm.imobilizado.dado ?? [];
  const aportes = vm.aportes.dado ?? [];

  return (
    <>
      <KpisRoi roi={vm.roi.dado} />
      <RoiChartCard roi={vm.roi.dado} />

      <section className="mb-4 grid grid-cols-1 items-stretch gap-4 lg:grid-cols-[1.3fr_1fr]">
        {vm.imobilizado.erro ? (
          <Surface padding="lg">
            <EmptyState icon={<TrendingUp className="h-5 w-5" />} title="Não deu para carregar o imobilizado" description={vm.imobilizado.erro} className="border-none py-6" />
          </Surface>
        ) : bens.length === 0 ? (
          <Surface padding="lg" className="flex items-center justify-center text-sm text-muted-foreground">
            Nenhum bem registrado ainda.
          </Surface>
        ) : (
          <ImobilizadoTable bens={bens} />
        )}

        {vm.aportes.erro ? (
          <Surface padding="lg">
            <EmptyState icon={<TrendingUp className="h-5 w-5" />} title="Não deu para carregar os aportes" description={vm.aportes.erro} className="border-none py-6" />
          </Surface>
        ) : (
          <AportesList aportes={aportes} />
        )}
      </section>

      <ConsultorRoi roi={vm.roi.dado} bens={bens} />
    </>
  );
}
