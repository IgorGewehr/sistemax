import { Check, MoveLeft } from 'lucide-react';

import type { Fornecedor, NotaEntrada } from './types';

interface ConferenciaHeaderProps {
  nota: NotaEntrada;
  fornecedor: Fornecedor;
  threeWay: boolean;
  onVoltar: () => void;
}

/** Cabeçalho da tela de conferência (`.conf-head` + banners de só-leitura do mockup). */
export function ConferenciaHeader({ nota, fornecedor, threeWay, onVoltar }: ConferenciaHeaderProps) {
  return (
    <div className="mb-4">
      <div className="flex flex-wrap items-center justify-between gap-3.5">
        <div className="inline-flex items-center gap-2.5">
          <button
            type="button"
            onClick={onVoltar}
            aria-label="Voltar para Compras"
            className="grid h-[26px] w-[26px] flex-none place-items-center rounded-lg bg-surface-2 text-foreground transition-colors hover:bg-primary-soft hover:text-primary-600 active:brightness-95"
          >
            <MoveLeft className="h-3.5 w-3.5" />
          </button>
          <div className="flex flex-col gap-0.5">
            <div className="text-xs font-semibold text-muted-foreground">{threeWay ? 'CONFERÊNCIA CONTRA PEDIDO' : 'ENTRADA DE NOTA'}</div>
            <h1 className="text-xl font-bold tracking-tight">
              NF-e {nota.numero} · {fornecedor.nome}
              {fornecedor.cnpj && ` · CNPJ ${fornecedor.cnpj}`}
            </h1>
          </div>
        </div>
        {nota.chave && <div className="num text-[11px] tracking-wide text-faint">chave {nota.chave}</div>}
      </div>

      {nota.status === 'recebida' && (
        <>
          <div className="mt-3.5 flex items-center gap-2.5 rounded-xl bg-pos-soft px-4 py-3 text-[13px] text-pos">
            <Check className="h-4 w-4 flex-none" strokeWidth={2.4} />
            Recebida em {nota.recebidaEm} por <b className="font-bold">{nota.recebidaPor}</b> — estoque creditado e conta a pagar gerada.
            {nota.jaPago && ' Paga no ato.'}
          </div>
          {nota.jaPago && (
            <div className="mt-2 flex items-center gap-2.5 rounded-xl bg-warn-soft px-4 py-3 text-[13px] text-warn">
              ⚠ Esta compra já tem pagamento registrado — estornar vai gerar uma reversão financeira, não um cancelamento simples.
            </div>
          )}
        </>
      )}

      {nota.status === 'estornada' && (
        <div className="mt-3.5 flex items-center gap-2.5 rounded-xl bg-surface-2 px-4 py-3 text-[13px] text-muted-foreground">
          ↺ Estornada em {nota.estornadaEm} por <b className="font-bold">{nota.estornadaPor}</b> — {nota.motivoEstorno}
        </div>
      )}
    </div>
  );
}
