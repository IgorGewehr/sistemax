import { useCallback, useMemo, useState } from 'react';

import { useToast } from '@/lib/toast';

import { extratoSummaryLabel, getDocGenState, toggleAccountSelection } from './helpers';
import type { DocFormat, DocGenState, EnvioChannel, HistoryRow, Regime, ReportsViewModel } from './types';

type Channel = 'email' | 'whatsapp';

let historySeq = 0;
function nextHistoryId(): string {
  historySeq += 1;
  return `hist-runtime-${historySeq}`;
}

/**
 * Orquestra toda a interação da tela (período, regime, contas do extrato, geração/envio de
 * documentos, pacote de fechamento e histórico) — a página fica só compondo seções com o retorno
 * deste hook. Réplica fiel das funções do `<script>` do mockup (generateDoc/sendDoc/gerarPacote).
 */
export function useRelatoriosController(vm: ReportsViewModel) {
  const { toast } = useToast();

  const [periodLabel, setPeriodLabel] = useState<string>(
    vm.periods.find((p) => p.id === vm.defaultPeriodId)?.label ?? (vm.periods[0]?.label ?? ''),
  );
  const [regime, setRegime] = useState<Regime>(vm.defaultRegime);
  const [selectedAccounts, setSelectedAccounts] = useState<string[]>(['todas']);
  const [genState, setGenState] = useState<Record<string, DocGenState>>({});
  const [pacoteState, setPacoteState] = useState<DocGenState>('idle');
  const [pacoteRevealed, setPacoteRevealed] = useState(false);
  const [history, setHistory] = useState<HistoryRow[]>(vm.initialHistory);

  const addHistoryRow = useCallback(
    (document: string, format: string, generatedBy: string, channel: EnvioChannel) => {
      setHistory((prev) => [
        { id: nextHistoryId(), date: 'agora mesmo', document, format, generatedBy, channel, isNew: true },
        ...prev,
      ]);
    },
    [],
  );

  const generateDoc = useCallback(
    (cardId: string, docLabel: string, format: DocFormat) => {
      const key = `${cardId}:${format}`;
      setGenState((prev) => (prev[key] && prev[key] !== 'idle' ? prev : { ...prev, [key]: 'generating' }));
      window.setTimeout(() => {
        setGenState((prev) => ({ ...prev, [key]: 'done' }));
        toast(`✓ ${format} de "${docLabel}" gerado — download iniciado`, 'success');
        window.setTimeout(() => {
          setGenState((prev) => ({ ...prev, [key]: 'idle' }));
        }, 1300);
      }, 650);
    },
    [toast],
  );

  const sendDoc = useCallback(
    (docLabel: string, channel: Channel) => {
      const chLabel = channel === 'email' ? 'e-mail' : 'WhatsApp';
      toast(`✓ ${docLabel} enviado por ${chLabel} para o contador`, 'success');
      const format = docLabel.toLowerCase().includes('pacote') ? 'ZIP' : 'PDF';
      addHistoryRow(docLabel, format, 'Você', channel);
    },
    [toast, addHistoryRow],
  );

  const gerarPacote = useCallback(() => {
    setPacoteState((current) => {
      if (current !== 'idle') return current;
      window.setTimeout(() => {
        setPacoteState('done');
        setPacoteRevealed(true);
        toast('Pacote de fechamento gerado — 5 documentos, 1 arquivo .zip', 'success');
        addHistoryRow(vm.pacote.docLabel, 'ZIP', 'Você', null);
        window.setTimeout(() => setPacoteState('idle'), 1600);
      }, 900);
      return 'generating';
    });
  }, [toast, addHistoryRow, vm.pacote.docLabel]);

  const baixarZip = useCallback(() => {
    toast(`Download do ${vm.pacote.zipFileName} iniciado`, 'success');
  }, [toast, vm.pacote.zipFileName]);

  const toggleAccount = useCallback((accountId: string) => {
    setSelectedAccounts((prev) => toggleAccountSelection(prev, accountId));
  }, []);

  const extratoSummary = useMemo(
    () => extratoSummaryLabel(selectedAccounts, vm.extrato.accounts),
    [selectedAccounts, vm.extrato.accounts],
  );

  const docGenState = useCallback((cardId: string, format: DocFormat) => getDocGenState(genState, cardId, format), [genState]);

  return {
    periodLabel,
    setPeriodLabel,
    regime,
    setRegime,
    selectedAccounts,
    toggleAccount,
    extratoSummary,
    docGenState,
    generateDoc,
    sendDoc,
    pacoteState,
    pacoteRevealed,
    gerarPacote,
    baixarZip,
    history,
  };
}
