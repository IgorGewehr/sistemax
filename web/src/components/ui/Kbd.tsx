import { cn } from '@/lib/utils';

/** Badge de tecla — reforça a ergonomia de navegação por teclado (estilo PDV/balcão). */
export function Kbd({ children, className }: { children: string; className?: string }) {
  return <kbd className={cn('kbd', className)}>{children}</kbd>;
}
