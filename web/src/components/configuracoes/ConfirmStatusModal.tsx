import { Button } from '@/components/ui/Button';
import { Modal } from '@/components/ui/Modal';
import type { Usuario } from '@/lib/permissions';

interface ConfirmStatusModalProps {
  open: boolean;
  usuario: Usuario | undefined;
  onClose: () => void;
  onConfirmar: (usuarioId: string) => void;
}

/** Confirma ativar/desativar o acesso de um usuário — desativar não apaga histórico/autoria (audit
 *  trail preservado), só revoga o login. Mesmo padrão de `components/clientes/ConfirmStatusModal.tsx`. */
export function ConfirmStatusModal({ open, usuario, onClose, onConfirmar }: ConfirmStatusModalProps) {
  if (!usuario) return null;
  const vaiDesativar = usuario.status === 'ativo';

  return (
    <Modal open={open} onClose={onClose} title={vaiDesativar ? 'Desativar usuário' : 'Ativar usuário'}>
      <p className="mb-4 text-[13px] leading-relaxed text-muted-foreground">
        {vaiDesativar ? (
          <>
            <b className="font-semibold text-foreground">{usuario.nome}</b> perde o acesso ao sistema imediatamente. Nada do que ele
            já fez é apagado — vendas, OS e registros continuam com a autoria dele.
          </>
        ) : (
          <>
            <b className="font-semibold text-foreground">{usuario.nome}</b> volta a poder entrar no sistema, com o mesmo papel e
            permissões de antes.
          </>
        )}
      </p>
      <div className="flex justify-end gap-2.5">
        <Button variant="outline" size="sm" onClick={onClose}>
          Cancelar
        </Button>
        <Button variant={vaiDesativar ? 'danger' : 'primary'} size="sm" onClick={() => onConfirmar(usuario.id)}>
          {vaiDesativar ? 'Desativar' : 'Ativar'}
        </Button>
      </div>
    </Modal>
  );
}
