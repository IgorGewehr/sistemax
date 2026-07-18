// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';

import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthGate } from '@/components/layout/AuthGate';
import * as ClientModule from '@/lib/api/client';
import { AuthProvider } from '@/lib/auth';

const { ApiError } = ClientModule;
type Session = ClientModule.Session;

const trocarPinMock = vi.fn();
const apiGetMock = vi.fn();
let currentSession: Session | null = null;

vi.mock('@/lib/api/client', async () => {
  const actual = await vi.importActual<typeof ClientModule>('@/lib/api/client');
  return {
    ...actual,
    readSession: () => currentSession,
    writeSession: (s: Session | null) => {
      currentSession = s;
    },
    getBootToken: () => 'boot-fake',
    setUnauthorizedHandler: () => {},
    login: vi.fn(),
    logout: vi.fn(),
    trocarPin: (...args: unknown[]) => trocarPinMock(...args),
    api: { ...actual.api, get: (...args: unknown[]) => apiGetMock(...args) },
  };
});

const SESSAO_PROVISORIA: Session = {
  token: 'tok-1',
  businessId: 'biz-1',
  papel: 'founder',
  expiraEm: new Date(Date.now() + 3600_000).toISOString(),
  deveTrocarPin: true,
};

function renderApp() {
  return render(
    <AuthProvider>
      <AuthGate>
        <div>Rota real do App</div>
      </AuthGate>
    </AuthProvider>,
  );
}

/** Digita um PIN clicando nos botões numéricos do teclado (mesmos botões persistem entre etapas). */
function digitar(pin: string) {
  for (const d of pin) {
    fireEvent.click(screen.getByRole('button', { name: d }));
  }
}

/**
 * Integração AuthGate + TrocarPinObrigatorio + AuthProvider (só `lib/api/client` mockado) —
 * cobre o que antes só estava "confirmado por inspeção de código" (ver pendência): bloqueio de
 * PIN trivial/reuso, e liberação automática do app pelo AuthGate após `trocarPin()` suceder.
 */
describe('Wizard de 1º-boot (TrocarPinObrigatorio + AuthGate)', () => {
  beforeEach(() => {
    currentSession = { ...SESSAO_PROVISORIA };
    trocarPinMock.mockReset();
    apiGetMock.mockReset().mockResolvedValue({});
  });

  afterEach(cleanup);

  it('trava o app inteiro atrás do wizard, recusa PIN trivial e reuso, e libera sozinho após sucesso', async () => {
    trocarPinMock.mockResolvedValue(undefined);
    renderApp();

    // AuthGate: nenhuma rota do app monta enquanto deveTrocarPin é true.
    expect(screen.getByText('Confirme o PIN atual')).toBeInTheDocument();
    expect(screen.queryByText('Rota real do App')).not.toBeInTheDocument();

    // Passo 1 — PIN atual (formato válido, 4 dígitos).
    digitar('4827');
    fireEvent.click(screen.getByRole('button', { name: 'Continuar' }));
    await waitFor(() => expect(screen.getByText('Escolha um novo PIN')).toBeInTheDocument());

    // Passo 2 — recusa PIN trivial (dígitos repetidos), não avança de etapa.
    digitar('1111');
    fireEvent.click(screen.getByRole('button', { name: 'Continuar' }));
    expect(
      screen.getByText('Esse PIN é muito óbvio (sequência ou dígitos repetidos). Escolha outro.'),
    ).toBeInTheDocument();
    expect(screen.getByText('Escolha um novo PIN')).toBeInTheDocument();

    // Passo 2 — recusa reuso do PIN atual, não avança de etapa.
    digitar('4827');
    fireEvent.click(screen.getByRole('button', { name: 'Continuar' }));
    expect(screen.getByText('O novo PIN precisa ser diferente do atual.')).toBeInTheDocument();
    expect(screen.getByText('Escolha um novo PIN')).toBeInTheDocument();

    // Passo 2 — PIN novo válido, não-trivial e diferente do atual → avança.
    digitar('1902');
    fireEvent.click(screen.getByRole('button', { name: 'Continuar' }));
    await waitFor(() => expect(screen.getByText('Confirme o novo PIN')).toBeInTheDocument());

    // Passo 3 — confirmação não bate.
    digitar('1111');
    fireEvent.click(screen.getByRole('button', { name: 'Salvar PIN' }));
    expect(screen.getByText('Os PINs não coincidem. Digite o novo PIN novamente.')).toBeInTheDocument();
    expect(screen.getByText('Confirme o novo PIN')).toBeInTheDocument();

    // Passo 3 — confirmação bate → chama trocarPin(pinAtual, pinNovo); AuthProvider zera
    // deveTrocarPin e o AuthGate libera o app sozinho, sem reload nem navegação manual.
    digitar('1902');
    fireEvent.click(screen.getByRole('button', { name: 'Salvar PIN' }));

    await waitFor(() => expect(trocarPinMock).toHaveBeenCalledWith('4827', '1902'));
    await waitFor(() => expect(screen.getByText('Rota real do App')).toBeInTheDocument());
    expect(screen.queryByText('Confirme o novo PIN')).not.toBeInTheDocument();
    expect(currentSession?.deveTrocarPin).toBe(false);
  });

  it('PIN atual incorreto: API rejeita, mostra erro e reinicia no passo inicial sem liberar o app', async () => {
    trocarPinMock.mockRejectedValue(new ApiError('auth.pin_invalido', 'PIN atual incorreto.', 400));
    renderApp();

    digitar('4827');
    fireEvent.click(screen.getByRole('button', { name: 'Continuar' }));
    await waitFor(() => expect(screen.getByText('Escolha um novo PIN')).toBeInTheDocument());

    digitar('1902');
    fireEvent.click(screen.getByRole('button', { name: 'Continuar' }));
    await waitFor(() => expect(screen.getByText('Confirme o novo PIN')).toBeInTheDocument());

    digitar('1902');
    fireEvent.click(screen.getByRole('button', { name: 'Salvar PIN' }));

    await waitFor(() => expect(screen.getByText('PIN atual incorreto.')).toBeInTheDocument());
    expect(screen.getByText('Confirme o PIN atual')).toBeInTheDocument(); // voltou pro passo inicial
    expect(screen.queryByText('Rota real do App')).not.toBeInTheDocument();
    expect(currentSession?.deveTrocarPin).toBe(true); // sessão não foi liberada
  });
});
