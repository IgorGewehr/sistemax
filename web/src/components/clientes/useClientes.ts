import { useMemo, useState } from 'react';

import { CLIENTES_MOCK } from '@/mocks/clientes';

import { buildKpis, clienteById, filtrarClientes, somaGastoVidaCentavos } from './calc';
import type { Cliente, ClienteFormValues, FiltroClientes } from './types';

type Rota = { kind: 'home' } | { kind: 'ficha'; clienteId: string };

export type ClientesView = { kind: 'home' } | { kind: 'ficha'; cliente: Cliente };

type ModalForm = { modo: 'criar' } | { modo: 'editar'; clienteId: string };

/**
 * Todo o estado/lógica de "Clientes" vive aqui — `Clientes.tsx`/`ClientesHome.tsx`/`ClienteView.tsx`
 * permanecem finos, só compondo seções a partir do que este hook devolve. Cálculo derivado (KPIs,
 * segmentos, filtros) vem de `calc.ts`, puro e testável. Espelha `useCompras.ts`.
 */
export function useClientes() {
  const mock = CLIENTES_MOCK;
  const [clientes, setClientes] = useState<Cliente[]>(mock.clientes);
  const [rota, setRota] = useState<Rota>({ kind: 'home' });
  const [filtro, setFiltro] = useState<FiltroClientes>('todos');
  const [buscaTexto, setBuscaTexto] = useState('');
  const [modalForm, setModalForm] = useState<ModalForm | null>(null);
  const [modalConfirmStatus, setModalConfirmStatus] = useState<string | null>(null); // clienteId

  // ───────────────────────── Navegação (2 "telas" da mesma rota, como Compras) ─────────────────────────

  const view: ClientesView = useMemo(() => {
    if (rota.kind === 'ficha') {
      const cliente = clienteById(clientes, rota.clienteId);
      if (cliente) return { kind: 'ficha', cliente };
    }
    return { kind: 'home' };
  }, [rota, clientes]);

  function irParaFicha(clienteId: string) {
    setRota({ kind: 'ficha', clienteId });
  }
  function irParaHome() {
    setRota({ kind: 'home' });
  }

  // ───────────────────────── Home: KPIs, filtro/busca da tabela ─────────────────────────

  const kpis = useMemo(() => buildKpis(clientes, mock.hojeLabel), [clientes, mock.hojeLabel]);
  /** Cifra do card do Super Consultor — sempre somada do segmento real (`kpis.semComprar90d`),
   *  nunca hardcoded na copy do componente (ver `ClientesConsultor.tsx`). */
  const totalGastoVidaSemComprarCentavos = useMemo(() => somaGastoVidaCentavos(kpis.semComprar90d), [kpis.semComprar90d]);

  const buscaNormalizada = buscaTexto.trim().toLowerCase();
  const clientesFiltrados = useMemo(
    () => filtrarClientes(clientes, filtro, buscaNormalizada, mock.hojeLabel),
    [clientes, filtro, buscaNormalizada, mock.hojeLabel],
  );

  function onToggleFiltro(novo: FiltroClientes) {
    setFiltro((atual) => (atual === novo ? 'todos' : novo));
  }

  // ───────────────────────── CRUD via modal ─────────────────────────

  /** Cliente sendo editado, resolvido contra `clientes` (não `clientesFiltrados` — o modal precisa
   *  achar o cliente mesmo que ele não apareça na lista filtrada/buscada no momento). */
  const clienteEmEdicao = modalForm?.modo === 'editar' ? clienteById(clientes, modalForm.clienteId) : undefined;
  /** Idem para o modal de confirmar desativar/reativar. */
  const clienteEmConfirmacao = modalConfirmStatus ? clienteById(clientes, modalConfirmStatus) : undefined;

  function onSalvarCliente(valores: ClienteFormValues) {
    if (modalForm?.modo === 'editar') {
      const clienteId = modalForm.clienteId;
      setClientes((prev) => prev.map((c) => (c.id === clienteId ? { ...c, ...valores } : c)));
    } else {
      const novo: Cliente = {
        id: `c${Date.now()}`,
        ...valores,
        status: 'ativo',
        criadoEm: mock.hojeLabel,
        ultimaVisita: null,
        comprasCount: 0,
        ticketMedioCentavos: 0,
        totalGasto12mCentavos: 0,
        totalGastoVidaCentavos: 0,
      };
      setClientes((prev) => [novo, ...prev]);
    }
    setModalForm(null);
  }

  function onConfirmarToggleStatus(clienteId: string) {
    setClientes((prev) => prev.map((c) => (c.id === clienteId ? { ...c, status: c.status === 'ativo' ? 'inativo' : 'ativo' } : c)));
    setModalConfirmStatus(null);
  }

  return {
    hojeLabel: mock.hojeLabel,
    historicoPorCliente: mock.historicoPorCliente,
    totalClientesHistoricoMensal: mock.totalClientesHistoricoMensal,

    view,
    irParaFicha,
    irParaHome,

    kpis,
    totalGastoVidaSemComprarCentavos,
    filtro,
    onToggleFiltro,
    buscaTexto,
    onChangeBusca: setBuscaTexto,
    clientesFiltrados,

    modalForm,
    clienteEmEdicao,
    onAbrirCriar: () => setModalForm({ modo: 'criar' }),
    onAbrirEditar: (clienteId: string) => setModalForm({ modo: 'editar', clienteId }),
    onFecharModalForm: () => setModalForm(null),
    onSalvarCliente,

    modalConfirmStatus,
    clienteEmConfirmacao,
    onAbrirConfirmStatus: setModalConfirmStatus,
    onFecharConfirmStatus: () => setModalConfirmStatus(null),
    onConfirmarToggleStatus,
  };
}

export type ClientesVm = ReturnType<typeof useClientes>;
