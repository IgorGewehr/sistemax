import { motion } from 'framer-motion';
import { CheckCircle2 } from 'lucide-react';

import { MoneyValue } from '@/components/shared';
import { Button } from '@/components/ui/Button';
import { Kbd } from '@/components/ui/Kbd';
import type { VendaDto } from '@/lib/api/vendas';

interface SucessoScreenProps {
  venda: VendaDto;
  onNovaVenda: () => void;
  carregando: boolean;
}

/** Tela 3 — recibo de conclusão (`#screenSucesso` do mockup). Sem NFC-e/reimpressão: nenhum dos dois tem contrato de API ainda. */
export function SucessoScreen({ venda, onNovaVenda, carregando }: SucessoScreenProps) {
  const trocoTotal = venda.pagamentos.reduce((acc, p) => acc + p.troco.centavos, 0);

  return (
    <div className="flex flex-1 items-center justify-center">
      <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} className="surface w-full max-w-[440px] rounded-2xl px-10 py-9 text-center">
        <motion.div
          initial={{ opacity: 0, scale: 0.6 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ duration: 0.35, ease: [0.2, 1.4, 0.4, 1] }}
          className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-pos-soft text-pos"
        >
          <CheckCircle2 className="h-8 w-8" />
        </motion.div>
        <h2 className="font-display text-base font-bold text-foreground">Venda concluída</h2>
        <div className="num mt-0.5 text-[13px] text-muted-foreground" title={venda.id}>
          #{venda.id.slice(0, 8)}
        </div>

        <div className="my-6">
          <div className="text-xs font-bold uppercase tracking-wide text-muted-foreground">{trocoTotal > 0 ? 'Troco' : 'Total pago'}</div>
          <MoneyValue centavos={trocoTotal > 0 ? trocoTotal : venda.totalPago.centavos} className="mt-1 block text-[48px] font-extrabold tracking-tight text-pos" />
        </div>

        <Button variant="primary" size="touch" className="w-full justify-between" disabled={carregando} onClick={onNovaVenda}>
          {carregando ? 'Abrindo…' : 'Nova venda'}
          {!carregando && <Kbd className="bg-white/15 text-white">Enter</Kbd>}
        </Button>
      </motion.div>
    </div>
  );
}
