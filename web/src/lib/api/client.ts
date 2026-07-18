/**
 * Cliente HTTP tipado do Bridge local (`/api/*`, ver docs/arquitetura/bridge-http-local.md).
 * Único ponto que sabe: base URL relativa (mesma origem do Kestrel embutido — sem CORS), Bearer
 * de sessão, e como tratar 401 (sessão expirada → devolve pro login).
 *
 * Fetch puro (sem TanStack Query/axios) de propósito: `web/` ainda não tem essas dependências —
 * ver `package.json`. Os hooks de tela (`useEstoque`, `usePdv`, ...) embrulham isto com
 * `useState`/`useEffect`.
 */

export interface Money {
  centavos: number;
  moeda: string;
}

export function moneyToReais(m: Money | null | undefined): number {
  return (m?.centavos ?? 0) / 100;
}

export function reaisToCentavos(reais: number): number {
  return Math.round(reais * 100);
}

const SESSION_KEY = 'sistemax:session';
const BOOT_KEY = 'sistemax:boot-token';

export interface Session {
  token: string;
  businessId: string;
  papel: string;
  expiraEm: string;
  /** Espelha `Usuario.PinProvisorio` (ver `LoginResponse`/`BridgeEndpoints`) — `true` enquanto o
   * usuário não trocou o PIN default do seed (ex.: founder com "1234"). `AuthGate` usa isto para
   * bloquear o app inteiro atrás de `TrocarPinObrigatorio` até a troca ser concluída. */
  deveTrocarPin: boolean;
}

export function readSession(): Session | null {
  const raw = localStorage.getItem(SESSION_KEY);
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as Session;
    if (!parsed.token || new Date(parsed.expiraEm).getTime() <= Date.now()) return null;
    return parsed;
  } catch {
    return null;
  }
}

export function writeSession(session: Session | null): void {
  if (session) localStorage.setItem(SESSION_KEY, JSON.stringify(session));
  else localStorage.removeItem(SESSION_KEY);
}

/** O boot-token chega na URL da janela (`/?boot=...`, ver `PhotinoWindowLauncher`) só na
 * primeira navegação — guardamos em `sessionStorage` para sobreviver a um refresh da SPA sem
 * perder a capacidade de logar de novo. */
export function getBootToken(): string | null {
  const fromUrl = new URLSearchParams(window.location.search).get('boot');
  if (fromUrl) {
    sessionStorage.setItem(BOOT_KEY, fromUrl);
    return fromUrl;
  }
  return sessionStorage.getItem(BOOT_KEY);
}

export class ApiError extends Error {
  readonly codigo: string;
  readonly status: number;

  constructor(codigo: string, message: string, status: number) {
    super(message);
    this.name = 'ApiError';
    this.codigo = codigo;
    this.status = status;
  }
}

type UnauthorizedHandler = () => void;
let unauthorizedHandler: UnauthorizedHandler | null = null;

/** Chamado pelo <AuthProvider> — quando qualquer request tomar 401, a sessão é descartada e a UI
 * volta pra tela de PIN, sem cada tela precisar tratar isso individualmente. */
export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
  unauthorizedHandler = handler;
}

async function parseErrorBody(res: Response): Promise<{ codigo: string; mensagem: string }> {
  try {
    const body = (await res.json()) as { codigo?: string; mensagem?: string };
    return { codigo: body.codigo ?? 'erro_desconhecido', mensagem: body.mensagem ?? `Erro HTTP ${res.status}` };
  } catch {
    return { codigo: 'erro_desconhecido', mensagem: `Erro HTTP ${res.status}` };
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const session = readSession();
  const headers = new Headers(init.headers);
  if (init.body) headers.set('Content-Type', 'application/json');
  if (session) headers.set('Authorization', `Bearer ${session.token}`);

  const res = await fetch(`/api${path}`, { ...init, headers });

  if (res.status === 401) {
    writeSession(null);
    const { codigo, mensagem } = await parseErrorBody(res);
    unauthorizedHandler?.();
    throw new ApiError(codigo, mensagem, 401);
  }

  if (!res.ok) {
    const { codigo, mensagem } = await parseErrorBody(res);
    throw new ApiError(codigo, mensagem, res.status);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export const api = {
  get: <T>(path: string) => request<T>(path, { method: 'GET' }),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body !== undefined ? JSON.stringify(body) : undefined }),
  put: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PUT', body: body !== undefined ? JSON.stringify(body) : undefined }),
  patch: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'PATCH', body: body !== undefined ? JSON.stringify(body) : undefined }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
};

export async function login(pin: string): Promise<Session> {
  const boot = getBootToken();
  if (!boot) {
    throw new ApiError(
      'auth.boot_token_ausente',
      'Boot-token ausente — feche e reabra o app pela janela oficial do SistemaX.',
      401,
    );
  }

  const res = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Boot-Token': boot },
    body: JSON.stringify({ pin }),
  });

  if (!res.ok) {
    const { codigo, mensagem } = await parseErrorBody(res);
    throw new ApiError(codigo, mensagem, res.status);
  }

  const body = (await res.json()) as {
    token: string;
    businessId: string;
    papel: string;
    expiraEm: string;
    deveTrocarPin: boolean;
  };
  const session: Session = body;
  writeSession(session);
  return session;
}

/** Só descarta a sessão Bearer — o boot-token continua válido (a janela não foi fechada), então
 * a tela de PIN consegue logar de novo sem precisar reabrir o app. */
export function logout(): void {
  writeSession(null);
}

/** Autoatendimento — `POST /api/auth/trocar-pin` (ver `BridgeEndpoints`/`TrocarPinUseCase`). O
 * usuário logado troca o PRÓPRIO PIN provando que conhece o atual; sem permissão especial (ver
 * distinção de `PATCH /usuarios/{id}` no CLAUDE.md do bridge). É o caminho que
 * `TrocarPinObrigatorio` usa para encerrar `Usuario.PinProvisorio` no wizard de 1º-boot. */
export async function trocarPin(pinAtual: string, pinNovo: string): Promise<void> {
  await api.post<{ ok: boolean }>('/auth/trocar-pin', { pinAtual, pinNovo });
}
