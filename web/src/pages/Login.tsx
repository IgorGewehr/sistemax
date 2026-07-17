import { motion } from 'framer-motion';
import { Delete, Lock } from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import { Surface } from '@/components/ui/Surface';
import { api } from '@/lib/api/client';
import { useAuth, ApiError, getBootToken } from '@/lib/auth';
import { cn } from '@/lib/utils';

const TAMANHO_PIN_PADRAO = 4;

/** Versão do rodapé — busca uma vez em `/api/health` (anônimo, ver BridgeEndpoints), que já expõe
 * a mesma `VersaoAssembly` usada no publish/pack (ADR-0004 item 3, fonte única de versão).
 * Silenciosamente ignorado se falhar: isto é só um detalhe cosmético, nunca deve travar o login. */
function useVersaoDoHost(): string | null {
  const [versao, setVersao] = useState<string | null>(null);

  useEffect(() => {
    let cancelado = false;
    api
      .get<{ versao?: string }>('/health')
      .then((resposta) => {
        if (!cancelado) setVersao(resposta.versao ?? null);
      })
      .catch(() => {
        // cosmético — sem versão no rodapé, sem erro visível pro operador.
      });
    return () => {
      cancelado = true;
    };
  }, []);

  return versao;
}

/**
 * Tela de PIN — primeira coisa que a SPA mostra quando não há sessão Bearer válida (ver
 * `AuthProvider`/`AuthGate`). Troca boot-token (da URL `/?boot=...`, gravado pelo
 * `PhotinoWindowLauncher`) + PIN por um token de sessão (`POST /api/auth/login`).
 */
export function Login() {
  const { login, loading } = useAuth();
  const [pin, setPin] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const boot = getBootToken();
  const versao = useVersaoDoHost();

  async function submeter(pinAtual: string) {
    if (!pinAtual) return;
    setErro(null);
    try {
      await login(pinAtual);
    } catch (e) {
      const mensagem = e instanceof ApiError ? e.message : 'Não foi possível entrar. Tente novamente.';
      setErro(mensagem);
      setPin('');
    }
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    void submeter(pin);
  }

  function digitar(d: string) {
    if (loading) return;
    setErro(null);
    setPin((atual) => (atual.length >= 8 ? atual : atual + d));
  }

  function apagar() {
    if (loading) return;
    setPin((atual) => atual.slice(0, -1));
  }

  return (
    <div className="flex min-h-dvh w-full items-center justify-center bg-background px-4">
      <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.35 }} className="w-full max-w-sm">
        <Surface rounded="2xl" padding="lg" className="flex flex-col items-center gap-5 text-center">
          <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-gradient-red text-xl font-bold text-white shadow-red">
            SX
          </span>
          <div>
            <h1 className="font-display text-xl font-bold text-foreground">SistemaX</h1>
            <p className="mt-1 text-sm text-muted-foreground">Digite o PIN do gerente para continuar</p>
          </div>

          {!boot && (
            <div className="w-full rounded-xl bg-amber-50 px-3 py-2 text-xs font-medium text-amber-700 dark:bg-amber-500/10 dark:text-amber-400">
              Boot-token ausente — abra o app pela janela oficial do SistemaX (não por um link direto).
            </div>
          )}

          <form onSubmit={onSubmit} className="flex w-full flex-col items-center gap-5">
            <div className="flex items-center gap-2.5" aria-live="polite" aria-label={`PIN com ${pin.length} dígitos`}>
              {Array.from({ length: Math.max(TAMANHO_PIN_PADRAO, pin.length) }).map((_, i) => (
                <span
                  key={i}
                  className={cn(
                    'h-3.5 w-3.5 rounded-full border-2 transition-colors',
                    i < pin.length ? 'border-primary-600 bg-primary-600' : 'border-border bg-transparent',
                  )}
                />
              ))}
            </div>

            <input
              type="password"
              inputMode="numeric"
              autoComplete="off"
              autoFocus
              value={pin}
              onChange={(e) => {
                setErro(null);
                setPin(e.target.value.replace(/\D/g, '').slice(0, 8));
              }}
              className="sr-only"
              aria-label="PIN"
            />

            {erro && (
              <motion.p initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="-mt-2 text-sm font-medium text-red-600 dark:text-red-400">
                {erro}
              </motion.p>
            )}

            <div className="grid w-full grid-cols-3 gap-2.5">
              {['1', '2', '3', '4', '5', '6', '7', '8', '9'].map((d) => (
                <button
                  key={d}
                  type="button"
                  onClick={() => digitar(d)}
                  className="num flex h-14 items-center justify-center rounded-xl bg-secondary text-lg font-semibold text-foreground transition-colors hover:bg-secondary/70 active:brightness-95"
                >
                  {d}
                </button>
              ))}
              <button
                type="button"
                onClick={apagar}
                aria-label="Apagar"
                className="flex h-14 items-center justify-center rounded-xl text-muted-foreground transition-colors hover:bg-secondary/60 active:brightness-95"
              >
                <Delete className="h-5 w-5" />
              </button>
              <button
                key="0"
                type="button"
                onClick={() => digitar('0')}
                className="num flex h-14 items-center justify-center rounded-xl bg-secondary text-lg font-semibold text-foreground transition-colors hover:bg-secondary/70 active:brightness-95"
              >
                0
              </button>
              <div />
            </div>

            <Button type="submit" variant="primary" size="touch" className="w-full" disabled={!pin || loading} icon={<Lock className="h-4 w-4" />}>
              {loading ? 'Entrando…' : 'Entrar'}
            </Button>
          </form>
        </Surface>
        {versao && <p className="mt-3 text-center text-xs text-muted-foreground">SistemaX v{versao}</p>}
      </motion.div>
    </div>
  );
}
