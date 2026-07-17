import { UserPlus } from 'lucide-react';

import { SectionCard } from '@/components/shared';
import { Button } from '@/components/ui/Button';
import type { Usuario } from '@/lib/permissions';

import { UsuariosTable } from './UsuariosTable';

interface UsuariosSectionProps {
  usuarios: Usuario[];
  usuarioSessaoId: string;
  podeAdministrarUsuarios: boolean;
  onNovoUsuario: () => void;
  onEditarUsuario: (usuarioId: string) => void;
  onAlternarStatus: (usuarioId: string) => void;
}

/** Seção "Usuários & Permissões" — só quem tem `configuracoes:gerenciarUsuarios` chega até aqui
 *  (o item some do `ConfiguracoesNav` pra quem não tem, ver `useConfiguracoes.ts`); o botão
 *  "+ Novo usuário" confere `podeAdministrarUsuarios` de novo, defesa em profundidade. */
export function UsuariosSection({
  usuarios,
  usuarioSessaoId,
  podeAdministrarUsuarios,
  onNovoUsuario,
  onEditarUsuario,
  onAlternarStatus,
}: UsuariosSectionProps) {
  return (
    <SectionCard
      title="Usuários"
      hint={`${usuarios.length} usuário${usuarios.length === 1 ? '' : 's'}`}
      actions={
        podeAdministrarUsuarios && (
          <Button size="sm" icon={<UserPlus className="h-[15px] w-[15px]" strokeWidth={2.4} />} onClick={onNovoUsuario}>
            Novo usuário
          </Button>
        )
      }
      bodyClassName="overflow-x-auto"
    >
      <UsuariosTable
        usuarios={usuarios}
        usuarioSessaoId={usuarioSessaoId}
        podeAdministrarUsuarios={podeAdministrarUsuarios}
        onEditar={onEditarUsuario}
        onAlternarStatus={onAlternarStatus}
      />
    </SectionCard>
  );
}
