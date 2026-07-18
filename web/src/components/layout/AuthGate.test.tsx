// @vitest-environment jsdom
import '@testing-library/jest-dom/vitest';

import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { AuthGate } from './AuthGate';

const useAuthMock = vi.fn();

vi.mock('@/lib/auth', () => ({
  useAuth: () => useAuthMock(),
}));

vi.mock('@/pages/Login', () => ({
  Login: () => <div>Tela de Login (mock)</div>,
}));

vi.mock('@/pages/TrocarPinObrigatorio', () => ({
  TrocarPinObrigatorio: () => <div>Wizard de troca de PIN (mock)</div>,
}));

/**
 * Cobre a lógica de roteamento do porteiro isoladamente (Login/TrocarPinObrigatorio mockados) —
 * ver `TrocarPinObrigatorio.test.tsx` pro fluxo real do wizard + liberação automática do app.
 */
describe('AuthGate', () => {
  afterEach(cleanup);

  it('sem sessão: mostra Login e não monta nenhuma rota do app', () => {
    useAuthMock.mockReturnValue({ session: null });

    render(
      <AuthGate>
        <div>Rota real do App</div>
      </AuthGate>,
    );

    expect(screen.getByText('Tela de Login (mock)')).toBeInTheDocument();
    expect(screen.queryByText('Rota real do App')).not.toBeInTheDocument();
    expect(screen.queryByText('Wizard de troca de PIN (mock)')).not.toBeInTheDocument();
  });

  it('sessão com deveTrocarPin=true: trava no wizard, nenhuma rota do app monta', () => {
    useAuthMock.mockReturnValue({ session: { deveTrocarPin: true } });

    render(
      <AuthGate>
        <div>Rota real do App</div>
      </AuthGate>,
    );

    expect(screen.getByText('Wizard de troca de PIN (mock)')).toBeInTheDocument();
    expect(screen.queryByText('Rota real do App')).not.toBeInTheDocument();
    expect(screen.queryByText('Tela de Login (mock)')).not.toBeInTheDocument();
  });

  it('sessão com deveTrocarPin=false: libera as rotas reais do app', () => {
    useAuthMock.mockReturnValue({ session: { deveTrocarPin: false } });

    render(
      <AuthGate>
        <div>Rota real do App</div>
      </AuthGate>,
    );

    expect(screen.getByText('Rota real do App')).toBeInTheDocument();
    expect(screen.queryByText('Wizard de troca de PIN (mock)')).not.toBeInTheDocument();
    expect(screen.queryByText('Tela de Login (mock)')).not.toBeInTheDocument();
  });
});
