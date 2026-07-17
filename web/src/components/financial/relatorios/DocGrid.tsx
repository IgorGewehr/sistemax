import { AbertoCard } from './AbertoCard';
import { DreCard } from './DreCard';
import { ExtratoCard } from './ExtratoCard';
import { MrrCard } from './MrrCard';
import { PacoteCard } from './PacoteCard';
import type { DocFormat, DocGenState, Regime, ReportsViewModel } from './types';

type Channel = 'email' | 'whatsapp';

interface DocGridProps {
  vm: ReportsViewModel;
  regime: Regime;
  docGenState: (cardId: string, format: DocFormat) => DocGenState;
  onGeneratePdf: (cardId: string, docLabel: string) => void;
  onGenerateExcel: (cardId: string, docLabel: string) => void;
  onSend: (docLabel: string, channel: Channel) => void;
  selectedAccounts: string[];
  onToggleAccount: (id: string) => void;
  extratoSummary: string;
  pacoteState: DocGenState;
  pacoteRevealed: boolean;
  onGerarPacote: () => void;
  onBaixarZip: () => void;
}

/**
 * Réplica do `.doc-grid` do mockup: 3 colunas em telas largas (≥980px, breakpoint custom do
 * mockup — não é o `lg` padrão de 1024px do Tailwind) com o Pacote ocupando 2 linhas na coluna
 * central; 2 colunas entre 640px e 980px (Pacote ocupa a linha inteira); 1 coluna empilhada
 * abaixo de 640px, na ordem do DOM (DRE, Pacote, Extrato, Aberto, MRR — igual ao mockup).
 */
export function DocGrid({
  vm,
  regime,
  docGenState,
  onGeneratePdf,
  onGenerateExcel,
  onSend,
  selectedAccounts,
  onToggleAccount,
  extratoSummary,
  pacoteState,
  pacoteRevealed,
  onGerarPacote,
  onBaixarZip,
}: DocGridProps) {
  return (
    <section className="mb-4.5 grid grid-cols-1 gap-4 sm:grid-cols-2 min-[980px]:grid-cols-3">
      <DreCard
        className="sm:col-start-1 sm:row-start-1 min-[980px]:col-start-1 min-[980px]:row-start-1"
        dre={vm.dre}
        regime={regime}
        contact={vm.contact}
        pdfState={docGenState('dre', 'PDF')}
        excelState={docGenState('dre', 'Excel')}
        onGeneratePdf={() => onGeneratePdf('dre', vm.dre.docLabel)}
        onGenerateExcel={() => onGenerateExcel('dre', vm.dre.docLabel)}
        onSend={(channel) => onSend(vm.dre.docLabel, channel)}
      />

      <PacoteCard
        className="sm:col-start-1 sm:col-span-2 sm:row-start-2 min-[980px]:col-start-2 min-[980px]:col-span-1 min-[980px]:row-start-1 min-[980px]:row-span-2"
        pacote={vm.pacote}
        regime={regime}
        contact={vm.contact}
        state={pacoteState}
        revealed={pacoteRevealed}
        onGerar={onGerarPacote}
        onBaixar={onBaixarZip}
        onSend={(channel) => onSend(vm.pacote.docLabel, channel)}
      />

      <ExtratoCard
        className="sm:col-start-2 sm:row-start-1 min-[980px]:col-start-3 min-[980px]:row-start-1"
        extrato={vm.extrato}
        selectedAccounts={selectedAccounts}
        onToggleAccount={onToggleAccount}
        summaryLabel={extratoSummary}
        contact={vm.contact}
        pdfState={docGenState('extrato', 'PDF')}
        excelState={docGenState('extrato', 'Excel')}
        onGeneratePdf={() => onGeneratePdf('extrato', vm.extrato.docLabel)}
        onGenerateExcel={() => onGenerateExcel('extrato', vm.extrato.docLabel)}
        onSend={(channel) => onSend(vm.extrato.docLabel, channel)}
      />

      <AbertoCard
        className="sm:col-start-1 sm:row-start-3 min-[980px]:col-start-1 min-[980px]:row-start-2"
        aberto={vm.aberto}
        contact={vm.contact}
        pdfState={docGenState('aberto', 'PDF')}
        excelState={docGenState('aberto', 'Excel')}
        onGeneratePdf={() => onGeneratePdf('aberto', vm.aberto.docLabel)}
        onGenerateExcel={() => onGenerateExcel('aberto', vm.aberto.docLabel)}
        onSend={(channel) => onSend(vm.aberto.docLabel, channel)}
      />

      <MrrCard
        className="sm:col-start-2 sm:row-start-3 min-[980px]:col-start-3 min-[980px]:row-start-2"
        mrr={vm.mrr}
        contact={vm.contact}
        pdfState={docGenState('mrr', 'PDF')}
        excelState={docGenState('mrr', 'Excel')}
        onGeneratePdf={() => onGeneratePdf('mrr', vm.mrr.docLabel)}
        onGenerateExcel={() => onGenerateExcel('mrr', vm.mrr.docLabel)}
        onSend={(channel) => onSend(vm.mrr.docLabel, channel)}
      />
    </section>
  );
}
