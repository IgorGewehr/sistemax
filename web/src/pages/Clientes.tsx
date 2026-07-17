import { ClienteFormModal } from '@/components/clientes/ClienteFormModal';
import { ClientesHome } from '@/components/clientes/ClientesHome';
import { ClienteView } from '@/components/clientes/ClienteView';
import { ConfirmStatusModal } from '@/components/clientes/ConfirmStatusModal';
import { useClientes } from '@/components/clientes/useClientes';

/**
 * Clientes — página fina: todo o estado/lógica vive em `useClientes` (hook); aqui só se decide
 * qual das 2 "telas" renderizar (Home · Ficha de 1 cliente — a mesma rota alterna entre elas, como
 * em Compras) e se compõem os modais de CRUD, que ficam disponíveis sobre qualquer view.
 */
export function Clientes() {
  const vm = useClientes();

  return (
    <>
      {vm.view.kind === 'ficha' ? (
        <ClienteView vm={vm} cliente={vm.view.cliente} />
      ) : (
        <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:py-8">
          <ClientesHome vm={vm} />
        </div>
      )}

      <ClienteFormModal
        open={vm.modalForm !== null}
        modo={vm.modalForm?.modo ?? 'criar'}
        clienteEmEdicao={vm.clienteEmEdicao}
        onClose={vm.onFecharModalForm}
        onSalvar={vm.onSalvarCliente}
      />

      <ConfirmStatusModal
        open={vm.modalConfirmStatus !== null}
        cliente={vm.clienteEmConfirmacao}
        onClose={vm.onFecharConfirmStatus}
        onConfirmar={vm.onConfirmarToggleStatus}
      />
    </>
  );
}
