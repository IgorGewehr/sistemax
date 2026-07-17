import { SectionCard } from '@/components/shared';

import type { Vinculo } from './types';

interface DeParasCardProps {
  vinculos: Vinculo[];
}

/** "De-paras aprendidos" — VinculoProdutoFornecedor (código do fornecedor → produto do catálogo). */
export function DeParasCard({ vinculos }: DeParasCardProps) {
  return (
    <SectionCard title="De-paras aprendidos" hint="fornecedor + código → produto do catálogo (editável)">
      {vinculos.length === 0 ? (
        <p className="px-[18px] pb-4 text-[13px] text-muted-foreground">
          Nenhum de-para aprendido ainda — a próxima nota deste fornecedor vai exigir conferência manual item a item.
        </p>
      ) : (
        <div className="pb-1">
          {vinculos.map((v) => (
            <div
              key={v.cprod}
              className="grid grid-cols-2 items-center gap-2.5 border-b border-border/60 px-[18px] py-2.5 text-[13px] last:border-b-0 sm:grid-cols-[1.2fr_1fr_1fr_auto]"
            >
              <div>
                <div className="font-semibold">{v.produto}</div>
                <div className="num text-xs text-muted-foreground">cProd {v.cprod}</div>
              </div>
              <div className="num text-[12.5px]">{v.fator}</div>
              <div className="num">unid. NF: {v.unidadeNf}</div>
              <div className="text-right text-[11.5px] text-faint sm:text-left">
                aprendido da NF {v.notaOrigem} em {v.data}
              </div>
            </div>
          ))}
        </div>
      )}
    </SectionCard>
  );
}
