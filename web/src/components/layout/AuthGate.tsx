import type { ReactNode } from 'react';

import { useAuth } from '@/lib/auth';
import { Login } from '@/pages/Login';
import { TrocarPinObrigatorio } from '@/pages/TrocarPinObrigatorio';

/** Porteiro da SPA — sem sessão Bearer válida, só a tela de PIN existe (nenhuma rota do App
 * monta, nenhuma chamada de API dispara sem token). Sessão com `deveTrocarPin` (PIN provisório do
 * seed — ver `Session`/`LoginResponse`) trava no wizard de 1º-boot: nenhuma rota do App monta até
 * a troca ser concluída. */
export function AuthGate({ children }: { children: ReactNode }) {
  const { session } = useAuth();
  if (!session) return <Login />;
  if (session.deveTrocarPin) return <TrocarPinObrigatorio />;
  return <>{children}</>;
}
