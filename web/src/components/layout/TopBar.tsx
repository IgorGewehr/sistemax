import { AnimatePresence, motion } from 'framer-motion';
import { LogOut, Menu, Moon, Search, Sun } from 'lucide-react';

import { useAuth } from '@/lib/auth';
import { useTheme } from '@/lib/theme';
import { cn } from '@/lib/utils';

interface TopBarProps {
  onMobileMenuToggle: () => void;
}

function ThemeToggle() {
  const { isDark, toggleDark } = useTheme();
  return (
    <button
      type="button"
      onClick={toggleDark}
      className="relative flex h-9 w-9 items-center justify-center rounded-xl text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
      title={isDark ? 'Modo claro' : 'Modo escuro'}
    >
      <AnimatePresence mode="wait" initial={false}>
        {isDark ? (
          <motion.span key="moon" initial={{ rotate: -90, scale: 0, opacity: 0 }} animate={{ rotate: 0, scale: 1, opacity: 1 }} exit={{ rotate: 90, scale: 0, opacity: 0 }} transition={{ duration: 0.2 }}>
            <Moon className="h-[17px] w-[17px]" />
          </motion.span>
        ) : (
          <motion.span key="sun" initial={{ rotate: 90, scale: 0, opacity: 0 }} animate={{ rotate: 0, scale: 1, opacity: 1 }} exit={{ rotate: -90, scale: 0, opacity: 0 }} transition={{ duration: 0.2 }}>
            <Sun className="h-[17px] w-[17px]" />
          </motion.span>
        )}
      </AnimatePresence>
    </button>
  );
}

function DensityToggle() {
  const { density, setDensity } = useTheme();
  return (
    <div className="hidden items-center gap-1 rounded-full bg-secondary p-1 md:flex">
      {(['simples', 'avancado'] as const).map((d) => (
        <button
          key={d}
          type="button"
          onClick={() => setDensity(d)}
          className={cn(
            'rounded-full px-3 py-1 text-2xs font-semibold capitalize transition-colors',
            density === d ? 'bg-card text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground',
          )}
        >
          {d === 'simples' ? 'Modo simples' : 'Modo avançado'}
        </button>
      ))}
    </div>
  );
}

export function TopBar({ onMobileMenuToggle }: TopBarProps) {
  const { logout } = useAuth();

  return (
    <header className="flex h-16 shrink-0 items-center gap-3 border-b border-border/70 bg-background/80 px-4 backdrop-blur-md sm:px-6">
      <button
        type="button"
        onClick={onMobileMenuToggle}
        className="flex h-9 w-9 items-center justify-center rounded-xl text-muted-foreground hover:bg-secondary sm:hidden"
        aria-label="Abrir menu"
      >
        <Menu className="h-5 w-5" />
      </button>

      <div className="relative hidden max-w-xs flex-1 items-center sm:flex">
        <Search className="pointer-events-none absolute left-3 h-4 w-4 text-muted-foreground/60" />
        <input
          placeholder="Buscar em Financeiro…"
          className="w-full rounded-xl border border-transparent bg-secondary/70 py-2 pl-9 pr-3 text-sm text-foreground outline-none placeholder:text-muted-foreground/60 focus:border-border focus:bg-background"
        />
      </div>

      <div className="ml-auto flex items-center gap-2">
        <DensityToggle />
        <ThemeToggle />
        <button
          type="button"
          onClick={logout}
          title="Sair"
          className="flex h-9 w-9 items-center justify-center rounded-xl text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground"
        >
          <LogOut className="h-[17px] w-[17px]" />
        </button>
        <div className="ml-1 flex h-9 w-9 items-center justify-center rounded-full bg-gradient-red text-xs font-bold text-white">
          IG
        </div>
      </div>
    </header>
  );
}
