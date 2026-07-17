import { ClipboardCheck } from 'lucide-react';

import { PageHeader, SectionCard } from '@/components/shared';
import { EmptyState } from '@/components/ui/EmptyState';

/** Aba "Inventários" — contagens físicas e divergências. Sem API de contagem no Bridge ainda;
 * mesmo tratamento honesto de `MovimentacoesView` em vez de fabricar inventários de exemplo. */
export function InventariosView() {
  return (
    <div>
      <PageHeader subtitle="Contagens físicas e as divergências que elas revelam." />
      <SectionCard title="Contagens">
        <div className="px-[18px] pb-6 pt-1">
          <EmptyState
            icon={<ClipboardCheck className="h-5 w-5" />}
            title="Ainda não disponível"
            description="Abrir uma contagem, registrar o físico por produto e fechar gerando ajustes por linha divergente depende de um módulo de inventário que o Bridge ainda não tem. Quando existir, esta aba lista cada contagem com seu status e a divergência em R$."
            className="border-none py-10"
          />
        </div>
      </SectionCard>
    </div>
  );
}
