import { Eyebrow, MoneyValue } from '@/components/shared';
import type { PagamentoDeVendaDto } from '@/lib/api/vendas';

interface PagamentosListProps {
  pagamentos: PagamentoDeVendaDto[];
}

const METODO_LABEL: Record<string, string> = {
  Dinheiro: 'Dinheiro',
  Debito: 'Débito',
  Credito: 'Crédito',
  Pix: 'Pix',
  Voucher: 'Voucher',
  CreditoLoja: 'Crédito de loja',
  Outro: 'Outros',
};

/** `.pagamentos-list` do mockup — só leitura: não há endpoint para remover um pagamento já registrado. */
export function PagamentosList({ pagamentos }: PagamentosListProps) {
  return (
    <div>
      <Eyebrow className="mb-2">Pagamentos</Eyebrow>
      {pagamentos.length === 0 ? (
        <p className="rounded-xl bg-surface-2 px-3 py-3.5 text-center text-sm text-muted-foreground">Nenhum pagamento ainda.</p>
      ) : (
        <ul className="flex flex-col gap-1.5">
          {pagamentos.map((p) => (
            <li key={p.id} className="flex items-center gap-2 rounded-xl bg-surface-2 px-3 py-2.5">
              <div className="min-w-0 flex-1">
                <div className="text-[12.5px] font-bold text-foreground">{METODO_LABEL[p.metodo] ?? p.metodo}</div>
                {p.troco.centavos > 0 && (
                  <div className="text-[11px] text-muted-foreground">
                    Troco <MoneyValue centavos={p.troco.centavos} />
                  </div>
                )}
              </div>
              <MoneyValue centavos={p.valor.centavos} className="shrink-0 text-[13.5px] font-bold text-foreground" />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
