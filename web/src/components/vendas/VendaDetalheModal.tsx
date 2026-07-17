import { useEffect } from 'react';

import { MoneyValue } from '@/components/shared';
import { Modal } from '@/components/ui/Modal';

import { formatFormasPagamento, VENDA_STATUS_LABEL, VENDA_STATUS_TONE } from './calc';
import { Chip } from './chips';
import type { VendaRow } from './types';

interface VendaDetalheModalProps {
  venda: VendaRow;
  onClose: () => void;
}

/**
 * Comprovante da venda — SÓ LEITURA, mesmo para vendas estornadas (mostra motivo/quem/quando, não
 * oferece reverter). Estornar/editar é ação do PDV/Financeiro, fora do escopo desta tela — por
 * isso nenhum botão de ação aparece aqui (nem "Estornar", nem "Reimprimir").
 *
 * O `Modal` compartilhado (`@/components/ui/Modal`) fecha por X e clique fora, mas não trata Esc —
 * replicamos o mesmo listener que `ModalNovaSangria`/`ModalShell` do Financeiro já usam pra isso.
 */
export function VendaDetalheModal({ venda, onClose }: VendaDetalheModalProps) {
  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose();
    }
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  return (
    <Modal open title={`Venda ${venda.numero}`} onClose={onClose} className="max-w-lg">
      <div className="mb-4 flex flex-wrap items-center gap-x-2 gap-y-1.5 text-[13px] text-muted-foreground">
        <span>{venda.dataHoraLabel}</span>
        <span>·</span>
        <span>{venda.canal}</span>
        <span>·</span>
        <span>{venda.operador}</span>
        <span>·</span>
        <span>{venda.clienteNome ?? 'Consumidor final'}</span>
        <Chip tone={VENDA_STATUS_TONE[venda.status]} className="ml-auto">
          {VENDA_STATUS_LABEL[venda.status]}
        </Chip>
      </div>

      {venda.status === 'Estornada' && (
        <div className="mb-4 rounded-xl bg-crit-soft p-3 text-[13px]">
          <p className="font-semibold text-crit">
            Venda estornada
            {venda.estornadaEm ? ` em ${venda.estornadaEm}` : ''}
            {venda.estornadaPor ? ` por ${venda.estornadaPor}` : ''}.
          </p>
          {venda.motivoEstorno && <p className="mt-1 text-foreground/80">{venda.motivoEstorno}</p>}
        </div>
      )}

      <div className="mb-4">
        <h3 className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Itens</h3>
        <div className="divide-y divide-border/60 rounded-xl border border-border/60">
          {venda.itens.map((item) => (
            <div key={item.produtoId} className="flex items-center justify-between gap-3 px-3 py-2 text-[13px]">
              <div className="min-w-0">
                <div className="truncate font-medium text-foreground">{item.nome}</div>
                <div className="text-xs text-muted-foreground">
                  {item.quantidade}
                  {item.unidade} × <MoneyValue centavos={item.precoUnitarioCentavos} />
                  {item.descontoCentavos > 0 && (
                    <span className="text-crit">
                      {' '}
                      · − <MoneyValue centavos={item.descontoCentavos} />
                    </span>
                  )}
                </div>
              </div>
              <MoneyValue centavos={item.subtotalCentavos} className="flex-none font-semibold" />
            </div>
          ))}
        </div>
      </div>

      <div className="mb-4">
        <h3 className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">Pagamento</h3>
        <div className="divide-y divide-border/60 rounded-xl border border-border/60">
          {venda.pagamentos.map((p, i) => (
            <div key={`${p.metodo}-${i}`} className="flex items-center justify-between gap-3 px-3 py-2 text-[13px]">
              <span className="font-medium text-foreground">{p.metodo}</span>
              <div className="text-right">
                <MoneyValue centavos={p.valorCentavos} className="font-semibold" />
                {p.trocoCentavos > 0 && (
                  <div className="text-xs text-muted-foreground">
                    troco <MoneyValue centavos={p.trocoCentavos} />
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
        {venda.formasPagamento.length > 1 && (
          <p className="mt-1.5 text-xs text-muted-foreground">Pagamento dividido: {formatFormasPagamento(venda.formasPagamento)}.</p>
        )}
      </div>

      <div className="space-y-1 border-t border-border/60 pt-3 text-[13px]">
        <div className="flex items-center justify-between text-muted-foreground">
          <span>Subtotal</span>
          <MoneyValue centavos={venda.subtotalCentavos} />
        </div>
        {venda.descontoCentavos > 0 && (
          <div className="flex items-center justify-between text-crit">
            <span>Desconto</span>
            <MoneyValue centavos={-venda.descontoCentavos} signed />
          </div>
        )}
        <div className="flex items-center justify-between text-base font-bold text-foreground">
          <span>Total</span>
          <MoneyValue centavos={venda.totalCentavos} />
        </div>
      </div>
    </Modal>
  );
}
