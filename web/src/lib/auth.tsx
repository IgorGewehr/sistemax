import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

import {
  ApiError,
  getBootToken,
  login as loginRequest,
  logout as logoutSession,
  readSession,
  setUnauthorizedHandler,
  trocarPin as trocarPinRequest,
  writeSession,
  type Session,
} from './api/client';

interface AuthContextValue {
  session: Session | null;
  login: (pin: string) => Promise<void>;
  logout: () => void;
  /** Autoatendimento de troca de PIN (wizard de 1º-boot) — ver `TrocarPinObrigatorio`. Ao
   * suceder, zera `session.deveTrocarPin` localmente (sem precisar de novo login) e persiste. */
  trocarPin: (pinAtual: string, pinNovo: string) => Promise<void>;
  loading: boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/**
 * Fonte da verdade de sessão da SPA — espelha o papel do `AuthProvider` do saas-erp (mesma
 * convenção CLAUDE.md), só que aqui a sessão é o Bearer local do Bridge (PIN → token 12h), não
 * Firebase Auth. `setUnauthorizedHandler` (client.ts) devolve qualquer 401 de qualquer chamada
 * pra este estado, sem cada tela precisar tratar token expirado individualmente.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(() => readSession());
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setUnauthorizedHandler(() => setSession(null));
    return () => setUnauthorizedHandler(null);
  }, []);

  const login = useCallback(async (pin: string) => {
    setLoading(true);
    try {
      const novaSessao = await loginRequest(pin);
      setSession(novaSessao);
    } finally {
      setLoading(false);
    }
  }, []);

  const logout = useCallback(() => {
    logoutSession();
    setSession(null);
  }, []);

  const trocarPin = useCallback(async (pinAtual: string, pinNovo: string) => {
    setLoading(true);
    try {
      await trocarPinRequest(pinAtual, pinNovo);
      setSession((atual) => {
        if (!atual) return atual;
        const atualizada: Session = { ...atual, deveTrocarPin: false };
        writeSession(atualizada);
        return atualizada;
      });
    } finally {
      setLoading(false);
    }
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ session, login, logout, trocarPin, loading }),
    [session, login, logout, trocarPin, loading],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth deve ser usado dentro de <AuthProvider>');
  return ctx;
}

export { ApiError, getBootToken };
