import type { ReactNode } from 'react';

import { SectionCard } from '@/components/shared';
import { EmptyState } from '@/components/ui/EmptyState';

interface EmBreveSectionProps {
  titulo: string;
  icon: ReactNode;
  descricao: string;
}

/** Placeholder honesto — usado por "Fiscal" e "Integrações". Nunca "em breve" genérico: cada
 *  instância explica exatamente o que vai morar ali e como o sistema já funciona sem essa seção
 *  hoje (princípio "toda tela vazia ensina", mesmo do resto do app). */
export function EmBreveSection({ titulo, icon, descricao }: EmBreveSectionProps) {
  return (
    <SectionCard title={titulo}>
      <EmptyState icon={icon} title="Em breve" description={descricao} className="border-none" />
    </SectionCard>
  );
}
