import type { ReactNode } from 'react';

import { useAuth } from '@/lib/auth';
import { Login } from '@/pages/Login';

/** Porteiro da SPA — sem sessão Bearer válida, só a tela de PIN existe (nenhuma rota do App
 * monta, nenhuma chamada de API dispara sem token). */
export function AuthGate({ children }: { children: ReactNode }) {
  const { session } = useAuth();
  if (!session) return <Login />;
  return <>{children}</>;
}
