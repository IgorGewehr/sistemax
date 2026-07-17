import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

/** Combina classes condicionais e resolve conflitos do Tailwind (padrão shadcn/ServicePro). */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
