import { UserPlus } from 'lucide-react';

import { PageHeader } from '@/components/shared';
import { Button } from '@/components/ui/Button';

import { ClientesConsultor } from './ClientesConsultor';
import { ClientesTableSection } from './ClientesTableSection';
import { KpisRow } from './KpisRow';
import type { ClientesVm } from './useClientes';

interface ClientesHomeProps {
  vm: ClientesVm;
}

/** Home de Clientes — página fina: só compõe seções a partir do view-model do hook. */
export function ClientesHome({ vm }: ClientesHomeProps) {
  return (
    <div>
      <PageHeader
        subtitle="Quem compra de você, com que frequência, e quem sumiu."
        actions={
          <Button size="sm" icon={<UserPlus className="h-[15px] w-[15px]" strokeWidth={2.4} />} onClick={vm.onAbrirCriar}>
            Novo cliente
          </Button>
        }
      />

      <KpisRow
        kpis={vm.kpis}
        historicoAnteriorMensal={vm.totalClientesHistoricoMensal}
        aniversariantesAtivo={vm.filtro === 'aniversariantes'}
        semComprar90dAtivo={vm.filtro === 'semComprar90d'}
        onToggleAniversariantes={() => vm.onToggleFiltro('aniversariantes')}
        onToggleSemComprar90d={() => vm.onToggleFiltro('semComprar90d')}
      />

      <ClientesConsultor
        totalGastoVidaSemComprarCentavos={vm.totalGastoVidaSemComprarCentavos}
        onVerSumidos={() => vm.onToggleFiltro('semComprar90d')}
      />

      <ClientesTableSection
        filtro={vm.filtro}
        onToggleFiltro={vm.onToggleFiltro}
        buscaTexto={vm.buscaTexto}
        onChangeBusca={vm.onChangeBusca}
        clientesFiltrados={vm.clientesFiltrados}
        hojeLabel={vm.hojeLabel}
        onAbrirCliente={vm.irParaFicha}
      />
    </div>
  );
}
