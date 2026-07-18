import { AlertTriangle } from 'lucide-react';
import { useMemo } from 'react';

import { DocGrid } from '@/components/financial/relatorios/DocGrid';
import { HistoryTable } from '@/components/financial/relatorios/HistoryTable';
import { InfoNote } from '@/components/financial/relatorios/InfoNote';
import { SubheadControls } from '@/components/financial/relatorios/SubheadControls';
import type { ReportsViewModel } from '@/components/financial/relatorios/types';
import { useRelatoriosController } from '@/components/financial/relatorios/useRelatoriosController';
import { useRelatoriosReais } from '@/components/financial/relatorios/useRelatoriosReais';
import { PageHeader } from '@/components/shared';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';
import { RELATORIOS_MOCK } from '@/mocks/financeiro/relatorios';

const BLOCO_LABEL: Record<'mrr' | 'aberto' | 'dreCompetencia' | 'extratoAccounts', string> = {
  mrr: 'MRR',
  aberto: 'Contas em aberto',
  dreCompetencia: 'DRE (competência)',
  extratoAccounts: 'Contas do extrato',
};

/**
 * Página fina — só compõe as seções de `docs/ui/mockups/relatorios.html` (fonte da verdade) e
 * repassa o estado do `useRelatoriosController`. Os cards MRR, Contas em aberto, DRE (regime de
 * competência) e a lista de contas do "Extrato por conta" são dado REAL — ver `useRelatoriosReais`.
 * Regime de caixa do DRE e a GERAÇÃO de pacote/extrato/histórico (PDF/Excel/ZIP, sem serviço no
 * backend) continuam ilustrativos — marcados com `MockBadge` nos próprios cards
 * (docs/wiring/financeiro-telas-restantes.md §5).
 */
export function Relatorios() {
  const c = useRelatoriosController(RELATORIOS_MOCK);
  const reais = useRelatoriosReais();

  const carregandoReais = reais.mrr.carregando || reais.aberto.carregando || reais.dreCompetencia.carregando || reais.extratoAccounts.carregando;

  const errosReais = (['mrr', 'aberto', 'dreCompetencia', 'extratoAccounts'] as const)
    .filter((chave) => !reais[chave].carregando && reais[chave].erro)
    .map((chave) => ({ chave, mensagem: reais[chave].erro as string }));

  const vm = useMemo<ReportsViewModel>(
    () => ({
      ...RELATORIOS_MOCK,
      mrr: reais.mrr.dado ?? RELATORIOS_MOCK.mrr,
      aberto: reais.aberto.dado ?? RELATORIOS_MOCK.aberto,
      dre: {
        ...RELATORIOS_MOCK.dre,
        byRegime: {
          ...RELATORIOS_MOCK.dre.byRegime,
          competencia: reais.dreCompetencia.dado ?? RELATORIOS_MOCK.dre.byRegime.competencia,
        },
      },
      extrato: {
        ...RELATORIOS_MOCK.extrato,
        accounts: reais.extratoAccounts.dado ?? RELATORIOS_MOCK.extrato.accounts,
      },
    }),
    [reais.mrr.dado, reais.aberto.dado, reais.dreCompetencia.dado, reais.extratoAccounts.dado],
  );

  return (
    <div>
      <PageHeader subtitle="Documentos prontos pra mandar pro seu contador ou sócios." />

      <SubheadControls
        periods={RELATORIOS_MOCK.periods}
        periodLabel={c.periodLabel}
        onSelectPeriod={c.setPeriodLabel}
        regime={c.regime}
        onSetRegime={c.setRegime}
      />
      <InfoNote />

      {!carregandoReais && errosReais.length > 0 && (
        <Surface padding="lg" className="mb-4.5 border-crit/40 bg-crit-soft/40">
          <div className="flex items-start gap-2.5 text-sm text-crit">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            <div>
              <p className="font-semibold">
                Não deu para carregar {errosReais.map((e) => BLOCO_LABEL[e.chave]).join(', ')} — mostrando dado ilustrativo nesses cards.
              </p>
              <ul className="mt-1 space-y-0.5 text-xs text-crit/80">
                {errosReais.map((e) => (
                  <li key={e.chave}>
                    {BLOCO_LABEL[e.chave]}: {e.mensagem}
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </Surface>
      )}

      {carregandoReais ? (
        <Surface padding="lg" className="mb-4.5 min-h-[280px]">
          <Skeleton className="h-56 w-full" />
        </Surface>
      ) : (
        <DocGrid
          vm={vm}
          regime={c.regime}
          docGenState={c.docGenState}
          onGeneratePdf={(cardId, docLabel) => c.generateDoc(cardId, docLabel, 'PDF')}
          onGenerateExcel={(cardId, docLabel) => c.generateDoc(cardId, docLabel, 'Excel')}
          onSend={c.sendDoc}
          selectedAccounts={c.selectedAccounts}
          onToggleAccount={c.toggleAccount}
          extratoSummary={c.extratoSummary}
          pacoteState={c.pacoteState}
          pacoteRevealed={c.pacoteRevealed}
          onGerarPacote={c.gerarPacote}
          onBaixarZip={c.baixarZip}
        />
      )}

      <HistoryTable rows={c.history} />
    </div>
  );
}
