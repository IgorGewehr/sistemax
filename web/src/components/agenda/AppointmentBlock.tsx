import { motion } from 'framer-motion';

import { MoneyValue } from '@/components/shared';
import { cn } from '@/lib/utils';

import { getBlockHeight, getBlockTop, HOUR_HEIGHT, START_HOUR, STATUS_LABEL, STATUS_TONE_CLASSES } from './calc';
import type { Agendamento } from './types';

interface AppointmentBlockProps {
  agendamento: Agendamento;
  onClick: (agendamento: Agendamento) => void;
  /** Fonte um pouco menor — usado na Semana (colunas mais estreitas que o Dia). */
  compact?: boolean;
}

/**
 * Bloco absoluto de um agendamento no grid de Dia/Semana — porte do `AppointmentBlock` do
 * saas-erp (L409-531). Layout em camadas conforme a altura real do bloco (heurística
 * `isTiny/showService/showTimeRange/showProfissional/showPrice`): um slot de 15min só cabe
 * nome+hora; um de 2h cabe tudo. Cor vem 100% de `STATUS_TONE_CLASSES` — zero hex/`style` inline.
 */
export function AppointmentBlock({ agendamento, onClick, compact = false }: AppointmentBlockProps) {
  const tone = STATUS_TONE_CLASSES[agendamento.status];
  const textoTone = tone.borda.replace('border-l-', 'text-');
  const height = getBlockHeight(agendamento.duracaoMin, HOUR_HEIGHT);
  const top = getBlockTop(agendamento.horaInicio, START_HOUR, HOUR_HEIGHT);

  const isTiny = height < 36;
  const showService = height >= 50 && !!agendamento.servicoNome;
  const showTimeRange = height >= 60;
  const showProfissional = height >= 84 && agendamento.profissionalNomes.length > 0;
  const profDisplay =
    agendamento.profissionalNomes.length === 0
      ? ''
      : agendamento.profissionalNomes.length === 1
        ? agendamento.profissionalNomes[0]
        : `${agendamento.profissionalNomes[0]} +${agendamento.profissionalNomes.length - 1}`;
  const showPrice = height >= 110 && agendamento.precoCentavos > 0;

  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ duration: 0.2 }}
      whileHover={{ scale: 1.02, zIndex: 50 }}
      onClick={(e) => {
        e.stopPropagation();
        onClick(agendamento);
      }}
      title={`${agendamento.clienteNome} · ${agendamento.horaInicio}–${agendamento.horaFim} · ${STATUS_LABEL[agendamento.status]}`}
      className={cn(
        // `rounded-r-lg` (não `rounded-lg`) de propósito: arredondar o canto onde a borda
        // esquerda de 3px encosta cria um artefato de "meia-lua" no Chromium (o traço do
        // border-radius soma com o border-left grosso). Só os cantos direitos arredondam.
        'absolute left-1 right-1 z-10 cursor-pointer overflow-hidden rounded-r-lg',
        'border-l-[3px] transition-shadow duration-200 hover:shadow-lg hover:shadow-black/10',
        tone.borda,
        tone.fundo,
        agendamento.status === 'cancelado' && 'opacity-50',
      )}
      style={{ top: `${top}px`, height: `${height}px` }}
    >
      <div className={cn('flex h-full min-w-0 flex-col px-2', isTiny ? 'justify-center py-0.5' : 'justify-start gap-0.5 py-1.5')}>
        <div className={cn('min-w-0 truncate font-semibold leading-tight', compact ? 'text-[12px]' : 'text-[13px]', textoTone)}>
          {agendamento.clienteNome}
        </div>

        {isTiny ? (
          <div className="truncate text-[10px] leading-tight text-muted-foreground">
            {agendamento.horaInicio}
            {agendamento.servicoNome ? ` · ${agendamento.servicoNome}` : ''}
          </div>
        ) : (
          <>
            {showService && <div className="truncate text-[10px] leading-tight text-muted-foreground">{agendamento.servicoNome}</div>}
            <div className="truncate text-[10px] leading-tight text-muted-foreground">
              {showTimeRange ? `${agendamento.horaInicio} – ${agendamento.horaFim}` : agendamento.horaInicio}
            </div>
            {showProfissional && (
              <div
                className="flex items-center gap-1 truncate text-[10px] leading-tight text-muted-foreground"
                title={agendamento.profissionalNomes.join(', ')}
              >
                <span className="opacity-70">·</span>
                {profDisplay}
              </div>
            )}
            {showPrice && (
              <div className={cn('mt-auto truncate text-[10px] font-medium leading-tight', textoTone)}>
                <MoneyValue centavos={agendamento.precoCentavos} />
              </div>
            )}
          </>
        )}
      </div>
    </motion.div>
  );
}
