import { useState } from 'react';

import { formatCentavos } from '@/lib/money';

import { itensQueEntram, parcelasResumoTxt, pendentesPadrao } from './calc';
import { ConferenciaHeader } from './ConferenciaHeader';
import { CustoEntradaCard } from './CustoEntradaCard';
import { FooterBarConferencia } from './FooterBarConferencia';
import { ItensPadraoCard } from './ItensPadraoCard';
import { ParcelasCard } from './ParcelasCard';
import { ThreeWayCard } from './ThreeWayCard';
import type { Fornecedor, NotaEntrada, NotaEntradaPadrao, NotaEntradaPedido } from './types';
import type { ComprasVm } from './useCompras';

interface ConferenciaViewProps {
  vm: ComprasVm;
  nota: NotaEntrada;
  fornecedor: Fornecedor;
}

/** Tela de conferência de entrada de nota — 1:1 com `view-conferencia` de `docs/ui/mockups/compras.html`. */
export function ConferenciaView({ vm, nota, fornecedor }: ConferenciaViewProps) {
  const readonly = nota.status === 'recebida' || nota.status === 'estornada';
  const threeWay = nota.pedidoId !== null;

  return (
    <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:py-8">
      <ConferenciaHeader nota={nota} fornecedor={fornecedor} threeWay={threeWay} onVoltar={vm.irParaHome} />
      {/* O discriminante `pedidoId` narrowa `nota` — por isso o corpo vira dois componentes dedicados
          em vez de um ternário aqui mesmo (o TS não propaga a narrow de uma variável booleana). */}
      {nota.pedidoId !== null ? (
        <ConferenciaPedidoBody vm={vm} nota={nota} readonly={readonly} />
      ) : (
        <ConferenciaPadraoBody vm={vm} nota={nota} readonly={readonly} />
      )}
    </div>
  );
}

function ConferenciaPedidoBody({ vm, nota, readonly }: { vm: ComprasVm; nota: NotaEntradaPedido; readonly: boolean }) {
  const pendCount = nota.itens.filter((it) => it.divergencia && !it.divergenciaResolucao).length;

  return (
    <>
      <ThreeWayCard
        nota={nota}
        readonly={readonly}
        onDivergenciaChange={(nItem, chave) => vm.onDivergenciaChange(nota.id, nItem, chave)}
        onFisicoChange={(nItem, valor) => vm.onFisicoChange(nota.id, nItem, valor)}
      />
      {!readonly && (
        <div className="mt-4">
          <FooterBarConferencia
            itensQueEntram={itensQueEntram(nota.itens)}
            parcelasTxt={parcelasResumoTxt(nota.parcelas, formatCentavos)}
            podeConfirmar={pendCount === 0}
            pendCount={pendCount}
            onDescartar={() => vm.onDescartarNota(nota.id)}
            onConfirmar={() => vm.onConfirmarRecebimento(nota.id, nota.jaPago ?? false)}
          />
        </div>
      )}
    </>
  );
}

function ConferenciaPadraoBody({ vm, nota, readonly }: { vm: ComprasVm; nota: NotaEntradaPadrao; readonly: boolean }) {
  const [jaPago, setJaPago] = useState(nota.jaPago ?? false);
  const pendCount = pendentesPadrao(nota.itens).length;

  return (
    <>
      <div className="grid grid-cols-1 items-start gap-4 lg:grid-cols-[1.55fr_1fr]">
        <ItensPadraoCard itens={nota.itens} readonly={readonly} onAcao={(nItem, acao) => vm.onAcaoItemPadrao(nota.id, nItem, acao)} />
        <div className="flex flex-col gap-4">
          <CustoEntradaCard nota={nota} readonly={readonly} />
          {nota.parcelas.length > 0 && <ParcelasCard parcelas={nota.parcelas} readonly={readonly} jaPago={jaPago} onChangeJaPago={setJaPago} />}
        </div>
      </div>
      {!readonly && (
        <div className="mt-4">
          <FooterBarConferencia
            itensQueEntram={itensQueEntram(nota.itens)}
            parcelasTxt={parcelasResumoTxt(nota.parcelas, formatCentavos)}
            podeConfirmar={pendCount === 0}
            pendCount={pendCount}
            onDescartar={() => vm.onDescartarNota(nota.id)}
            onConfirmar={() => vm.onConfirmarRecebimento(nota.id, jaPago)}
          />
        </div>
      )}
    </>
  );
}
