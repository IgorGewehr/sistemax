import { ConfiguracoesHome } from '@/components/configuracoes/ConfiguracoesHome';
import { useConfiguracoes } from '@/components/configuracoes/useConfiguracoes';

/**
 * Configurações — casca única com sub-nav lateral (Perfil, Empresa, Usuários & Permissões, Fiscal,
 * Integrações). Página fina: todo o estado vive em `useConfiguracoes`.
 */
export function Configuracoes() {
  const vm = useConfiguracoes();

  return (
    <div className="mx-auto max-w-5xl px-4 py-6 sm:px-6 lg:py-8">
      <ConfiguracoesHome vm={vm} />
    </div>
  );
}
