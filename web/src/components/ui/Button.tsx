import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react';

import { cn } from '@/lib/utils';

type Variant = 'primary' | 'secondary' | 'ghost' | 'outline' | 'danger';
type Size = 'sm' | 'md' | 'lg' | 'touch';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
  icon?: ReactNode;
  iconPosition?: 'left' | 'right';
}

const VARIANT_CLASSES: Record<Variant, string> = {
  primary:
    'bg-gradient-red text-white shadow-red hover:shadow-red-lg hover:brightness-105 active:brightness-95',
  secondary:
    'bg-secondary text-secondary-foreground hover:bg-secondary/70 dark:hover:bg-white/[0.08]',
  ghost: 'text-foreground/80 hover:bg-black/[0.04] dark:hover:bg-white/[0.06] hover:text-foreground',
  outline: 'border border-border bg-transparent hover:bg-black/[0.03] dark:hover:bg-white/[0.05]',
  danger: 'bg-red-600 text-white hover:bg-red-700 shadow-red',
};

const SIZE_CLASSES: Record<Size, string> = {
  sm: 'h-8 px-3 text-xs gap-1.5 rounded-lg',
  md: 'h-10 px-4 text-sm gap-2 rounded-xl',
  lg: 'h-12 px-5 text-md gap-2 rounded-xl',
  /** Alvo de toque grande — ergonomia de PDV/balcão (>= 48px). */
  touch: 'h-12 px-6 text-base gap-2 rounded-xl min-w-[48px]',
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { className, variant = 'primary', size = 'md', icon, iconPosition = 'left', children, ...rest },
  ref,
) {
  return (
    <button
      ref={ref}
      className={cn(
        'inline-flex items-center justify-center font-medium whitespace-nowrap',
        'transition-all duration-150 active:brightness-95',
        'disabled:opacity-50 disabled:pointer-events-none',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background',
        VARIANT_CLASSES[variant],
        SIZE_CLASSES[size],
        className,
      )}
      {...rest}
    >
      {icon && iconPosition === 'left' && <span className="shrink-0">{icon}</span>}
      {children}
      {icon && iconPosition === 'right' && <span className="shrink-0">{icon}</span>}
    </button>
  );
});
