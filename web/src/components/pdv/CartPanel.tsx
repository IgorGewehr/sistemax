import { AnimatePresence, motion } from 'framer-motion';
import { ShoppingCart } from 'lucide-react';

import { MoneyValue } from '@/components/shared';
import { Button } from '@/components/ui/Button';
import { Kbd } from '@/components/ui/Kbd';
import type { VendaDto } from '@/lib/api/vendas';

interface CartPanelProps {
  venda: VendaDto;
  /** Variante somente-leitura usada no resumo da tela de Pagamento (`buildCartHtml(true)` do mockup) — sem ações, sem botão "Pagamento". */
  readonly?: boolean;
  onIrParaPagamento?: () => void;
  className?: string;
}

/**
 * O carrinho (`.cart-card` do mockup): lista de itens + rodapé de totais. Os valores vêm 100%
 * do `VendaDto` devolvido pela API (subtotal por item, subtotal da venda, desconto, total) —
 * nada é recalculado no cliente, regra dura do módulo.
 */
export function CartPanel({ venda, readonly = false, onIrParaPagamento, className }: CartPanelProps) {
  return (
    <section className={`surface flex min-h-0 flex-col overflow-hidden rounded-2xl ${className ?? ''}`}>
      <header className="flex-none border-b border-border/70 px-4 py-3.5">
        <div className="flex items-center gap-2 text-sm font-bold text-foreground">
          <ShoppingCart className="h-4 w-4 text-muted-foreground" />
          <span>Venda</span>
          <span className="num truncate text-muted-foreground" title={venda.id}>
            #{venda.id.slice(0, 8)}
          </span>
        </div>
      </header>

      <div className="flex-1 overflow-y-auto px-2 py-2">
        {venda.itens.length === 0 ? (
          <p className="px-2 py-8 text-center text-sm text-muted-foreground">
            Carrinho vazio — bipe ou toque um produto para começar.
          </p>
        ) : (
          <ul className="flex flex-col gap-1">
            <AnimatePresence initial={false}>
              {venda.itens.map((item) => (
                <motion.li
                  key={item.id}
                  initial={{ opacity: 0, y: 6 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0 }}
                  className="rounded-xl px-2.5 py-2.5"
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <div className="truncate text-[13.5px] font-semibold text-foreground">{item.descricao}</div>
                      <div className="num mt-0.5 text-xs text-muted-foreground">
                        {item.quantidade} × <MoneyValue centavos={item.precoUnitario.centavos} />
                      </div>
                    </div>
                    <MoneyValue centavos={item.subtotal.centavos} className="shrink-0 text-[13.5px] font-bold text-foreground" />
                  </div>
                </motion.li>
              ))}
            </AnimatePresence>
          </ul>
        )}
      </div>

      <footer className="flex-none border-t border-border/70 px-4 pb-4 pt-3">
        <div className="flex items-center justify-between py-0.5 text-[12.5px] text-muted-foreground">
          <span>Subtotal</span>
          <MoneyValue centavos={venda.subtotalItens.centavos} />
        </div>
        {venda.descontoVenda.centavos > 0 && (
          <div className="flex items-center justify-between py-0.5 text-[12.5px] text-warn">
            <span>Desconto</span>
            <MoneyValue centavos={venda.descontoVenda.centavos} className="text-warn" />
          </div>
        )}
        <div className="my-2 flex items-baseline justify-between">
          <span className="text-xs font-bold uppercase tracking-wide text-muted-foreground">Total</span>
          <MoneyValue centavos={venda.total.centavos} className="text-[32px] font-bold tracking-tight text-foreground" />
        </div>
        {!readonly && (
          <Button
            variant="primary"
            size="touch"
            className="w-full justify-between"
            disabled={venda.itens.length === 0}
            onClick={onIrParaPagamento}
          >
            Pagamento
            <Kbd className="bg-white/15 text-white">F10</Kbd>
          </Button>
        )}
      </footer>
    </section>
  );
}
