import { AnimatePresence, motion } from 'framer-motion';
import { NavLink, Outlet, useLocation } from 'react-router-dom';

import { cn } from '@/lib/utils';

/**
 * Casca do módulo Financeiro — a barra de abas de sub-seções (o `.tabs` dos mockups). As 6 abas,
 * na ordem exata do contrato (`docs/ui/financeiro-ui.md` §2), vêm dos mockups aprovados. Cada
 * página renderiza seu próprio cabeçalho (eyebrow + título + ações) conforme o seu mockup.
 *
 * Sem aba "Consultor" (o Super Consultor é inline e read-only — Lei 2) e sem menu "Mais/em breve".
 */
type FinanceiroTab = { to: string; label: string; end?: boolean };

const TABS: FinanceiroTab[] = [
  { to: '/financeiro', label: 'Visão geral', end: true },
  { to: '/financeiro/entradas-saidas', label: 'Entradas & saídas' },
  { to: '/financeiro/recorrentes', label: 'Recorrentes' },
  { to: '/financeiro/bancario', label: 'Bancário' },
  { to: '/financeiro/fluxo-de-caixa', label: 'Fluxo de caixa' },
  { to: '/financeiro/relatorios', label: 'Relatórios' },
];

export function FinanceiroLayout() {
  const location = useLocation();

  return (
    <div className="flex h-full flex-col">
      <nav
        aria-label="Seções do Financeiro"
        className="flex shrink-0 items-center gap-1 overflow-x-auto border-b border-border/70 px-4 py-2 scrollbar-hide sm:px-6"
      >
        {TABS.map((tab) => (
          <NavLink
            key={tab.to}
            to={tab.to}
            end={tab.end}
            className={({ isActive }) =>
              cn(
                'group relative shrink-0 rounded-lg px-3 py-1.5 text-sm font-medium transition-colors',
                isActive ? 'text-foreground' : 'text-muted-foreground hover:text-foreground',
              )
            }
          >
            {({ isActive }) => (
              <>
                {tab.label}
                {isActive && (
                  <motion.span
                    layoutId="financeiro-tab-underline"
                    className="absolute inset-x-2 -bottom-[9px] h-0.5 rounded-full bg-primary-600"
                    transition={{ duration: 0.25 }}
                  />
                )}
              </>
            )}
          </NavLink>
        ))}
      </nav>

      <div className="flex-1 overflow-y-auto">
        <AnimatePresence mode="wait">
          <motion.div
            key={location.pathname}
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.28, ease: [0, 0, 0.2, 1] }}
            className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:py-8"
          >
            <Outlet />
          </motion.div>
        </AnimatePresence>
      </div>
    </div>
  );
}
