import { motion, AnimatePresence } from 'framer-motion';
import { Delete, KeyRound, LogOut, ShieldCheck } from 'lucide-react';
import { useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import { Surface } from '@/components/ui/Surface';
import { useAuth, ApiError } from '@/lib/auth';
import { ehPinTrivial } from '@/lib/pin';
import { cn } from '@/lib/utils';

const TAMANHO_PIN_PADRAO = 4;
const PIN_MINIMO = 4;
const PIN_MAXIMO = 8;

type Etapa = 'atual' | 'novo' | 'confirmar';

const CONTEUDO_POR_ETAPA: Record<Etapa, { titulo: string; subtitulo: string; rotulo: string }> = {
  atual: {
    titulo: 'Confirme o PIN atual',
    subtitulo: 'Antes de continuar, digite o PIN que você acabou de usar para entrar.',
    rotulo: 'Continuar',
  },
  novo: {
    titulo: 'Escolha um novo PIN',
    subtitulo: 'Por segurança, o PIN padrão precisa ser trocado antes de usar o sistema.',
    rotulo: 'Continuar',
  },
  confirmar: {
    titulo: 'Confirme o novo PIN',
    subtitulo: 'Digite o mesmo PIN mais uma vez.',
    rotulo: 'Salvar PIN',
  },
};

/**
 * Wizard de 1º-boot — obrigatório (ver `AuthGate`): quando `session.deveTrocarPin` é `true` (PIN
 * provisório do seed, ex.: founder com "1234"), esta tela substitui o app inteiro até a troca ser
 * concluída com sucesso. Fluxo em 3 etapas (PIN atual → novo → confirmação) porque
 * `POST /api/auth/trocar-pin` exige `pinAtual` (autoatendimento — ver `TrocarPinUseCase`) e não há
 * como reaproveitar com segurança o PIN digitado na tela de <Login> (não é persistido).
 */
export function TrocarPinObrigatorio() {
  const { trocarPin, logout, loading } = useAuth();
  const [etapa, setEtapa] = useState<Etapa>('atual');
  const [pinAtual, setPinAtual] = useState('');
  const [pinNovo, setPinNovo] = useState('');
  const [valor, setValor] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  const conteudo = CONTEUDO_POR_ETAPA[etapa];

  function digitar(d: string) {
    if (loading) return;
    setErro(null);
    setValor((atual) => (atual.length >= PIN_MAXIMO ? atual : atual + d));
  }

  function apagar() {
    if (loading) return;
    setValor((atual) => atual.slice(0, -1));
  }

  function validarFormato(pin: string): string | null {
    if (pin.length < PIN_MINIMO || pin.length > PIN_MAXIMO) {
      return `O PIN deve ter entre ${PIN_MINIMO} e ${PIN_MAXIMO} dígitos.`;
    }
    return null;
  }

  async function avancar() {
    if (loading || !valor) return;
    setErro(null);

    if (etapa === 'atual') {
      const erroFormato = validarFormato(valor);
      if (erroFormato) {
        setErro(erroFormato);
        return;
      }
      setPinAtual(valor);
      setValor('');
      setEtapa('novo');
      return;
    }

    if (etapa === 'novo') {
      const erroFormato = validarFormato(valor);
      if (erroFormato) {
        setErro(erroFormato);
        return;
      }
      if (ehPinTrivial(valor)) {
        setErro('Esse PIN é muito óbvio (sequência ou dígitos repetidos). Escolha outro.');
        setValor('');
        return;
      }
      if (valor === pinAtual) {
        setErro('O novo PIN precisa ser diferente do atual.');
        setValor('');
        return;
      }
      setPinNovo(valor);
      setValor('');
      setEtapa('confirmar');
      return;
    }

    // etapa === 'confirmar'
    if (valor !== pinNovo) {
      setErro('Os PINs não coincidem. Digite o novo PIN novamente.');
      setValor('');
      return;
    }

    try {
      await trocarPin(pinAtual, pinNovo);
      // sucesso: AuthProvider zera session.deveTrocarPin — AuthGate libera o app sozinho.
    } catch (e) {
      const mensagem = e instanceof ApiError ? e.message : 'Não foi possível trocar o PIN. Tente novamente.';
      setErro(mensagem);
      // PIN atual errado ou duplicado: volta pro início — mais seguro que deixar reenviar o mesmo valor.
      setPinAtual('');
      setPinNovo('');
      setValor('');
      setEtapa('atual');
    }
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    void avancar();
  }

  return (
    <div className="flex min-h-dvh w-full items-center justify-center bg-background px-4">
      <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.35 }} className="w-full max-w-sm">
        <Surface rounded="2xl" padding="lg" className="flex flex-col items-center gap-5 text-center">
          <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-gradient-red text-xl font-bold text-white shadow-red">
            <ShieldCheck className="h-7 w-7" />
          </span>

          <div className="flex items-center gap-1.5">
            {(['atual', 'novo', 'confirmar'] as Etapa[]).map((e) => (
              <span key={e} className={cn('h-1.5 w-6 rounded-full transition-colors', e === etapa ? 'bg-primary-600' : 'bg-secondary')} />
            ))}
          </div>

          <AnimatePresence mode="wait">
            <motion.div key={etapa} initial={{ opacity: 0 }} animate={{ opacity: 1 }} transition={{ duration: 0.2 }}>
              <h1 className="font-display text-xl font-bold text-foreground">{conteudo.titulo}</h1>
              <p className="mt-1 text-sm text-muted-foreground">{conteudo.subtitulo}</p>
            </motion.div>
          </AnimatePresence>

          <form onSubmit={onSubmit} className="flex w-full flex-col items-center gap-5">
            <div className="flex items-center gap-2.5" aria-live="polite" aria-label={`PIN com ${valor.length} dígitos`}>
              {Array.from({ length: Math.max(TAMANHO_PIN_PADRAO, valor.length) }).map((_, i) => (
                <span
                  key={i}
                  className={cn(
                    'h-3.5 w-3.5 rounded-full border-2 transition-colors',
                    i < valor.length ? 'border-primary-600 bg-primary-600' : 'border-border bg-transparent',
                  )}
                />
              ))}
            </div>

            <input
              type="password"
              inputMode="numeric"
              autoComplete="off"
              autoFocus
              value={valor}
              onChange={(e) => {
                setErro(null);
                setValor(e.target.value.replace(/\D/g, '').slice(0, PIN_MAXIMO));
              }}
              className="sr-only"
              aria-label={conteudo.titulo}
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

            <Button type="submit" variant="primary" size="touch" className="w-full" disabled={!valor || loading} icon={<KeyRound className="h-4 w-4" />}>
              {loading ? 'Salvando…' : conteudo.rotulo}
            </Button>
          </form>

          <button
            type="button"
            onClick={logout}
            disabled={loading}
            className="flex items-center gap-1.5 text-xs font-medium text-muted-foreground transition-colors hover:text-foreground active:brightness-95 disabled:pointer-events-none disabled:opacity-50"
          >
            <LogOut className="h-3.5 w-3.5" />
            Cancelar e sair
          </button>
        </Surface>
      </motion.div>
    </div>
  );
}
