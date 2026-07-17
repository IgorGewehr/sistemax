import { Badge } from '@/components/ui/Badge';
import { PAPEL_LABEL, type Papel } from '@/lib/permissions';
import { cn } from '@/lib/utils';

const PAPEL_TONE: Record<Papel, 'primary' | 'info' | 'neutral'> = {
  founder: 'primary',
  admin: 'primary',
  manager: 'info',
  operator: 'neutral',
  viewer: 'neutral',
};

/** Badge de papel — reusa `ui/Badge` (vocabulário genérico de tag) em vez de um chip local: papel é
 *  uma etiqueta fechada de 5 valores conhecidos, não precisa do "ponto" semântico de estado que
 *  `UsuarioStatusChip` usa abaixo. */
export function PapelBadge({ papel }: { papel: Papel }) {
  return <Badge tone={PAPEL_TONE[papel]}>{PAPEL_LABEL[papel]}</Badge>;
}

type StatusUsuario = 'ativo' | 'inativo';

const STATUS_CLASSES: Record<StatusUsuario, string> = {
  ativo: 'text-pos bg-pos-soft',
  inativo: 'text-muted-foreground bg-surface-2',
};

/** Chip de status cadastral do usuário (ativo/inativo) — vocabulário próprio deste módulo, mesma
 *  convenção de `components/clientes/chips.tsx` (o `StatusChip` de `components/shared` é vocabulário
 *  de caixa — sobra/falta/aberto/bateu —, não serve aqui). */
export function UsuarioStatusChip({ status }: { status: StatusUsuario }) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-0.5 text-[11.5px] font-semibold',
        STATUS_CLASSES[status],
      )}
    >
      <span className={cn('h-1.5 w-1.5 rounded-full', status === 'ativo' ? 'bg-pos' : 'bg-muted-foreground')} />
      {status === 'ativo' ? 'Ativo' : 'Inativo'}
    </span>
  );
}
