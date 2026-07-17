import { Pencil, Power } from 'lucide-react';

import { ehIntocavel, type Usuario } from '@/lib/permissions';

import { PapelBadge, UsuarioStatusChip } from './chips';

interface UsuariosTableProps {
  usuarios: Usuario[];
  usuarioSessaoId: string;
  /** Colunas de ação (editar/ativar-desativar) só existem pra quem administra usuários — evita
   *  vazar affordance de escrita pra quem só teria a seção visível por engano de estado local. */
  podeAdministrarUsuarios: boolean;
  onEditar: (usuarioId: string) => void;
  onAlternarStatus: (usuarioId: string) => void;
}

/** Tabela de "Usuários & Permissões". O fundador (`ehIntocavel`) nunca pode ser desativado por
 *  ninguém — evita o cenário "empresa sem nenhum founder ativo". A própria linha logada (`ehVoce`)
 *  também trava o botão de status: ninguém se autodesativa por esta tela, mesmo um admin comum,
 *  pra não travar o próprio acesso sem ter outro admin/founder ativo por perto. */
export function UsuariosTable({ usuarios, usuarioSessaoId, podeAdministrarUsuarios, onEditar, onAlternarStatus }: UsuariosTableProps) {
  if (usuarios.length === 0) {
    return <p className="px-4 py-6 text-center text-sm text-muted-foreground">Nenhum usuário cadastrado.</p>;
  }

  return (
    <table className="w-full min-w-[640px] border-collapse">
      <thead>
        <tr className="text-left text-2xs font-semibold uppercase tracking-wide text-muted-foreground">
          <th className="px-4 py-2.5 font-semibold">Usuário</th>
          <th className="px-4 py-2.5 font-semibold">Papel</th>
          <th className="px-4 py-2.5 font-semibold">Status</th>
          <th className="px-4 py-2.5 font-semibold">Último acesso</th>
          {podeAdministrarUsuarios && <th className="px-4 py-2.5 text-right font-semibold">Ações</th>}
        </tr>
      </thead>
      <tbody className="divide-y divide-border/60">
        {usuarios.map((usuario) => {
          const ehVoce = usuario.id === usuarioSessaoId;
          const intocavel = ehIntocavel(usuario);
          const statusBloqueado = intocavel || ehVoce;
          return (
            <tr key={usuario.id}>
              <td className="px-4 py-2.5">
                <span className="font-semibold text-foreground">{usuario.nome}</span>
                {ehVoce && <span className="ml-1.5 text-xs text-muted-foreground">(você)</span>}
                <div className="text-xs text-muted-foreground">{usuario.email}</div>
              </td>
              <td className="px-4 py-2.5">
                <PapelBadge papel={usuario.papel} />
              </td>
              <td className="px-4 py-2.5">
                <UsuarioStatusChip status={usuario.status} />
              </td>
              <td className="px-4 py-2.5 font-mono text-xs text-muted-foreground">{usuario.ultimoAcessoEm ?? 'nunca acessou'}</td>
              {podeAdministrarUsuarios && (
                <td className="px-4 py-2.5">
                  <div className="flex items-center justify-end gap-1">
                    <button
                      type="button"
                      onClick={() => onEditar(usuario.id)}
                      className="flex h-8 w-8 items-center justify-center rounded-lg text-muted-foreground hover:bg-surface-2 hover:text-foreground"
                      aria-label={`Editar ${usuario.nome}`}
                    >
                      <Pencil className="h-4 w-4" />
                    </button>
                    <button
                      type="button"
                      disabled={statusBloqueado}
                      onClick={() => onAlternarStatus(usuario.id)}
                      title={
                        intocavel
                          ? 'O fundador não pode ser desativado'
                          : ehVoce
                            ? 'Você não pode desativar a própria conta'
                            : undefined
                      }
                      className="flex h-8 w-8 items-center justify-center rounded-lg text-muted-foreground hover:bg-surface-2 hover:text-foreground disabled:pointer-events-none disabled:opacity-30"
                      aria-label={usuario.status === 'ativo' ? `Desativar ${usuario.nome}` : `Ativar ${usuario.nome}`}
                    >
                      <Power className="h-4 w-4" />
                    </button>
                  </div>
                </td>
              )}
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
