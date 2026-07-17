import { createPortal } from 'react-dom';

import { useToast } from '@/lib/toast';

/**
 * FAB "⊕ Lançar" do mockup — copy diz que é global a todas as telas do Financeiro, mas só a Visão
 * Geral existe hoje, então o botão mora aqui. Via portal pro `document.body`: a transição do
 * `FinanceiroLayout` anima `y` com framer-motion, o que deixa um `transform` residente no
 * container e cria containing block pra `position: fixed` — sem o portal o botão ficaria preso ao
 * alto/à altura do conteúdo da página em vez de fixo no canto da viewport.
 */
export function LancarFab() {
  const { toast } = useToast();

  return createPortal(
    <button
      type="button"
      aria-label="Lançar"
      title="Lançar"
      onClick={() => toast('⊕ Lançamento rápido — disponível em todas as telas do Financeiro.', 'info')}
      className="fixed bottom-6 right-6 z-[90] grid h-[52px] w-[52px] place-items-center rounded-2xl bg-primary-600 text-2xl text-white shadow-red-lg transition-transform hover:-translate-y-0.5 hover:brightness-105 active:brightness-95"
    >
      +
    </button>,
    document.body,
  );
}
