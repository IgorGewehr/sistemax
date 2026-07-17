/**
 * Nota de referência da lente Assinaturas → tela dedicada — mockup: `.note-box`.
 * Copy fixa (não é dado derivado): explica que este é o resumo, e onde vive a tabela completa.
 */
import { Info } from 'lucide-react';

export function AssinNotaReferencia() {
  return (
    <div className="flex items-start gap-2.5 rounded-2xl border border-dashed border-border px-4 py-3.5 text-[12.5px] leading-relaxed text-muted-foreground">
      <Info className="mt-0.5 h-4 w-4 shrink-0 text-primary-600" />
      <span>
        Isto é o <strong className="font-bold text-foreground">resumo</strong> da lente Assinaturas — MRR por serviço, novos × churn e
        retenção da carteira, com os mesmos números da tela já aprovada. A{' '}
        <strong className="font-bold text-foreground">tabela completa</strong> (todas as 10 assinaturas, histórico por cliente, cobrança de
        atrasados) vive na tela dedicada <strong className="font-bold text-foreground">Financeiro › Recorrentes › Assinaturas</strong>, sem
        mudanças aqui.
      </span>
    </div>
  );
}
