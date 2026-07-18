import { Settings2 } from 'lucide-react';

import { ToggleRow } from '@/components/financial/configuracoes/ToggleRow';
import { useFinanceiroConfiguracoes } from '@/components/financial/configuracoes/useFinanceiroConfiguracoes';
import { PageHeader } from '@/components/shared';
import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';

/**
 * Financeiro › Configurações — os dois toggles opt-in do módulo (nenhum mockup próprio: os
 * mockups `projeto.html`/`roi-negocio.html` só mostram um selo `.optin` decorativo no header,
 * sem tela de administração — esta página É onde o toggle vira real). Desligado por padrão em
 * todo tenant novo (`ConfiguracaoFinanceiraTenant.Padrao`): ligar aqui não muda nenhum número já
 * existente, só passa a expor as telas Projetos/Investimento & ROI.
 */
export function Configuracoes() {
  const vm = useFinanceiroConfiguracoes();

  return (
    <div>
      <PageHeader subtitle="Recursos opt-in do Financeiro — desligados não mudam nenhum número já existente." />

      {vm.config.carregando ? (
        <div className="space-y-3">
          <Surface padding="lg" className="min-h-[92px]">
            <Skeleton className="h-4 w-40" />
            <Skeleton className="mt-3 h-3 w-72" />
          </Surface>
          <Surface padding="lg" className="min-h-[92px]">
            <Skeleton className="h-4 w-40" />
            <Skeleton className="mt-3 h-3 w-72" />
          </Surface>
        </div>
      ) : vm.config.erro || !vm.config.dado ? (
        <Surface padding="lg">
          <EmptyState
            icon={<Settings2 className="h-5 w-5" />}
            title="Não deu para carregar"
            description={vm.config.erro ?? 'Tente novamente em instantes.'}
            className="border-none py-6"
          />
        </Surface>
      ) : (
        <div className="space-y-3">
          <ToggleRow
            titulo="Análise por Projeto"
            descricao="MRR, margem em camadas, capacidade/ociosidade de licenças, payback, ROI, churn e LTV por linha de produto — aba Projetos."
            ativo={vm.config.dado.analisePorProjetoAtiva}
            salvando={vm.salvando === 'analisePorProjetoAtiva'}
            onToggle={() => vm.alternar('analisePorProjetoAtiva')}
          />
          <ToggleRow
            titulo="Imobilizado & ROI"
            descricao="Registro de bens (depreciação linear), aportes de capital de giro e o painel de ROI do negócio — payback, TIR e a curva investido × recuperado."
            ativo={vm.config.dado.imobilizadoRoiAtivo}
            salvando={vm.salvando === 'imobilizadoRoiAtivo'}
            onToggle={() => vm.alternar('imobilizadoRoiAtivo')}
          />
        </div>
      )}
    </div>
  );
}
