import { FlaskConical } from 'lucide-react';

import { cn } from '@/lib/utils';

interface MockBadgeProps {
  /** Texto do tooltip (`title`) — por que ainda é mock, específico do bloco/tela. */
  titulo?: string;
  className?: string;
}

/**
 * Selo discreto "dados de exemplo" — marca honestamente um bloco/tela que ainda roda 100% sobre
 * `mocks/financeiro/*.ts` porque o endpoint .NET correspondente não existe (ver
 * docs/wiring/financeiro-api-contract.md). Nunca esconder isso: um número real e um de exemplo
 * lado a lado sem marcação são indistinguíveis pro usuário, e isso é pior que não mostrar nada.
 */
export function MockBadge({ titulo = 'Dados de exemplo — ainda sem endpoint no backend.', className }: MockBadgeProps) {
  return (
    <span
      title={titulo}
      className={cn(
        'inline-flex items-center gap-1 whitespace-nowrap rounded-full border border-dashed border-border px-2 py-0.5 text-[10.5px] font-semibold text-muted-foreground',
        className,
      )}
    >
      <FlaskConical className="h-3 w-3" />
      dados de exemplo
    </span>
  );
}
