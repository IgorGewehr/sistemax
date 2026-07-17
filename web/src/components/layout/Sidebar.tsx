import { motion } from 'framer-motion';
import {
  Boxes,
  Calendar,
  ChevronLeft,
  ChevronRight,
  ClipboardList,
  LayoutDashboard,
  Settings,
  ShoppingBag,
  ShoppingCart,
  Users,
  Wallet,
  Wrench,
} from 'lucide-react';
import { NavLink } from 'react-router-dom';

import { useSessaoPermissoes, type Modulo } from '@/lib/permissions';
import { cn } from '@/lib/utils';

interface SidebarProps {
  collapsed: boolean;
  onToggle: () => void;
}

interface NavItem {
  to: string;
  label: string;
  icon: React.ElementType;
  live?: boolean;
}

const ITEMS: NavItem[] = [
  { to: '/dashboard', label: 'Dashboard', icon: LayoutDashboard, live: true },
  { to: '/pdv', label: 'PDV', icon: ShoppingCart, live: true },
  { to: '/vendas', label: 'Vendas', icon: ClipboardList, live: true },
  { to: '/financeiro', label: 'Financeiro', icon: Wallet, live: true },
  { to: '/estoque', label: 'Estoque', icon: Boxes, live: true },
  { to: '/compras', label: 'Compras', icon: ShoppingBag, live: true },
  { to: '/ordens', label: 'Ordens', icon: Wrench, live: true },
  { to: '/clientes', label: 'Clientes', icon: Users, live: true },
  { to: '/agenda', label: 'Agenda', icon: Calendar, live: true },
];

/** Sidebar global do ERP — os itens visíveis se adaptam às permissões do usuário (RBAC via
 *  `useSessaoPermissoes`); `live` marca os módulos ainda "em breve". */
export function Sidebar({ collapsed, onToggle }: SidebarProps) {
  const sessao = useSessaoPermissoes();
  return (
    <motion.aside
      animate={{ width: collapsed ? 76 : 232 }}
      transition={{ duration: 0.25, ease: [0.4, 0, 0.2, 1] }}
      className="relative flex h-full shrink-0 flex-col border-r border-sidebar-border bg-sidebar"
    >
      <div className="flex h-16 items-center gap-2.5 px-4">
        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-gradient-red text-sm font-bold text-white shadow-red">
          SX
        </span>
        {!collapsed && <span className="font-display text-[15px] font-bold text-sidebar-foreground">SistemaX</span>}
      </div>

      <nav className="flex-1 space-y-0.5 px-2.5 py-2">
        {ITEMS.filter((item) => sessao.podeVer(item.to.slice(1) as Modulo)).map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            className={({ isActive }) =>
              cn(
                'group relative flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors',
                isActive && item.live
                  ? 'sidebar-item-active text-white'
                  : item.live
                    ? 'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground'
                    : 'cursor-not-allowed text-sidebar-foreground/35',
              )
            }
            onClick={(e) => {
              if (!item.live) e.preventDefault();
            }}
            title={collapsed ? item.label : undefined}
          >
            <item.icon className="h-[18px] w-[18px] shrink-0" />
            {!collapsed && (
              <span className="flex-1 truncate">{item.label}</span>
            )}
            {!collapsed && !item.live && (
              <span className="rounded-full bg-black/5 px-1.5 py-0.5 text-2xs font-semibold text-sidebar-foreground/40 dark:bg-white/5">
                em breve
              </span>
            )}
          </NavLink>
        ))}
      </nav>

      <div className="space-y-0.5 px-2.5 py-2">
        <NavLink
          to="/configuracoes"
          className={({ isActive }) =>
            cn(
              'flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors',
              isActive
                ? 'sidebar-item-active text-white'
                : 'text-sidebar-foreground/80 hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
            )
          }
        >
          <Settings className="h-[18px] w-[18px] shrink-0" />
          {!collapsed && <span>Configurações</span>}
        </NavLink>
      </div>

      <button
        type="button"
        onClick={onToggle}
        className="absolute -right-3 top-16 flex h-6 w-6 items-center justify-center rounded-full border border-sidebar-border bg-sidebar text-sidebar-foreground/60 shadow-sm hover:text-sidebar-foreground"
        aria-label={collapsed ? 'Expandir menu' : 'Recolher menu'}
      >
        {collapsed ? <ChevronRight className="h-3.5 w-3.5" /> : <ChevronLeft className="h-3.5 w-3.5" />}
      </button>
    </motion.aside>
  );
}
