import { ArrowLeft } from 'lucide-react';
import type { ReactNode } from 'react';

import { SectionCard, MoneyValue } from '@/components/shared';

import { bucketItensOrdenados, centavosOuTraco, diasDesde, entrouEm, valorAtual } from './calc';
import type { Bucket, BucketDrillStats, BucketKey, OperacaoStats } from './types';

interface FunilOperacaoSectionProps {
  buckets: Bucket[];
  onSelecionarEtapa: (key: BucketKey | null) => void;
  bucketSelecionado: Bucket | null;
  bucketSelecionadoStats: BucketDrillStats | null;
  operacao: OperacaoStats;
  onIrParaDetalhe: (numero: string) => void;
}

/**
 * A análise interativa (`.grid2` do mockup): card esquerdo é o funil "onde as OS travam" (ou o
 * drill de uma etapa), card direito é "Operação" (ou as estatísticas da etapa selecionada). Os
 * dois cards trocam de conteúdo juntos — `bucketSelecionado` (null = overview) governa ambos.
 */
export function FunilOperacaoSection({
  buckets,
  onSelecionarEtapa,
  bucketSelecionado,
  bucketSelecionadoStats,
  operacao,
  onIrParaDetalhe,
}: FunilOperacaoSectionProps) {
  return (
    <section className="grid grid-cols-1 gap-4 lg:grid-cols-[1.15fr_1fr]">
      {bucketSelecionado ? (
        <BucketDrill bucket={bucketSelecionado} onVoltar={() => onSelecionarEtapa(null)} onIrParaDetalhe={onIrParaDetalhe} />
      ) : (
        <FunilOverview buckets={buckets} onSelecionar={onSelecionarEtapa} />
      )}

      {bucketSelecionado && bucketSelecionadoStats ? (
        <BucketTiles bucket={bucketSelecionado} stats={bucketSelecionadoStats} />
      ) : (
        <OperacaoTiles operacao={operacao} />
      )}
    </section>
  );
}

function FunilOverview({ buckets, onSelecionar }: { buckets: Bucket[]; onSelecionar: (key: BucketKey) => void }) {
  const max = Math.max(1, ...buckets.map((b) => b.count));
  return (
    <SectionCard title="Onde as OS travam" hint="clique numa etapa →">
      <div className="flex flex-col gap-3 px-3.5 pb-4 pt-1">
        {buckets.map((b) => (
          <button
            key={b.key}
            type="button"
            onClick={() => onSelecionar(b.key)}
            className="flex w-full flex-col gap-1 rounded-[11px] px-2.5 py-2.5 text-left transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring active:brightness-95"
          >
            <div className="flex items-center justify-between gap-2.5">
              <span className="text-[13px] font-semibold">{b.label}</span>
              <span className="num text-[12.5px] text-muted-foreground">
                {b.count} · <MoneyValue centavos={centavosOuTraco(b.valor)} />
              </span>
            </div>
            <div className="h-2 overflow-hidden rounded-md bg-surface-2">
              <div className="h-full rounded-md bg-primary-600" style={{ width: `${(b.count / max) * 100}%` }} />
            </div>
          </button>
        ))}
      </div>
    </SectionCard>
  );
}

function BucketDrill({ bucket, onVoltar, onIrParaDetalhe }: { bucket: Bucket; onVoltar: () => void; onIrParaDetalhe: (numero: string) => void }) {
  const itens = bucketItensOrdenados(bucket);

  return (
    <SectionCard
      title={
        <span className="inline-flex items-center gap-2">
          <button
            type="button"
            onClick={onVoltar}
            aria-label="Voltar"
            className="grid h-[26px] w-[26px] flex-none place-items-center rounded-lg bg-surface-2 text-foreground transition-colors hover:bg-primary-soft hover:text-primary-600 active:brightness-95"
          >
            <ArrowLeft className="h-3.5 w-3.5" />
          </button>
          {bucket.label}
        </span>
      }
      hint={
        <>
          {bucket.count} OS · <MoneyValue centavos={centavosOuTraco(bucket.valor)} />
        </>
      }
    >
      <div className="flex flex-col gap-0.5 px-2.5 pb-3.5 pt-1">
        {itens.length === 0 && <div className="px-2.5 py-3 text-[13px] text-muted-foreground">Nenhuma OS nesta etapa.</div>}
        {itens.map((o) => {
          const dias = diasDesde(entrouEm(o, o.status) ?? o.abertaEm);
          return (
            <button
              key={o.numero}
              type="button"
              onClick={() => onIrParaDetalhe(o.numero)}
              className="flex w-full items-center justify-between gap-2.5 rounded-[11px] px-2.5 py-2.5 text-left transition-colors hover:bg-surface-2 active:brightness-95"
            >
              <span>
                <span className="text-[13px] font-semibold">
                  {o.numero} · {o.cliente}
                </span>
                <br />
                <span className="text-xs text-muted-foreground">
                  {o.equipamento} · <MoneyValue centavos={centavosOuTraco(valorAtual(o))} />
                </span>
              </span>
              <span className="whitespace-nowrap text-xs font-semibold text-warn">há {dias}d</span>
            </button>
          );
        })}
      </div>
    </SectionCard>
  );
}

function OperacaoTiles({ operacao }: { operacao: OperacaoStats }) {
  return (
    <SectionCard title="Operação">
      <div className="flex flex-col gap-2.5 px-4 pb-4 pt-1">
        <Stat k="Porta-a-porta" v={<>{operacao.portaAPortaDias.toFixed(1).replace('.', ',')} <Small>dias</Small></>} s="abertura até entrega" />
        <Stat k="Aprovação de orçamento" v={<>{operacao.taxaAprovacaoPct}<Small>%</Small></>} s={`${operacao.aprovadasCount} de ${operacao.decididasCount} decididos`} />
        <Stat k="Ticket médio" v={<MoneyValue centavos={operacao.ticketMedioCentavos} />} s="serviço + peças, todas as OS com valor" />
      </div>
    </SectionCard>
  );
}

function BucketTiles({ bucket, stats }: { bucket: Bucket; stats: BucketDrillStats }) {
  return (
    <SectionCard title={bucket.label}>
      <div className="flex flex-col gap-2.5 px-4 pb-4 pt-1">
        <Stat k="Tempo médio nesta etapa" v={<>{stats.tempoMedioDias.toFixed(1).replace('.', ',')} <Small>dias</Small></>} />
        <Stat k="Mais antiga na etapa" v={<>{stats.maisAntigaDias} <Small>dias</Small></>} />
        <Stat k="Valor parado na etapa" v={<MoneyValue centavos={centavosOuTraco(stats.valorCentavos)} />} />
      </div>
    </SectionCard>
  );
}

function Stat({ k, v, s }: { k: string; v: ReactNode; s?: string }) {
  return (
    <div className="rounded-xl bg-surface-2 px-3.5 py-3">
      <div className="text-xs font-semibold text-muted-foreground">{k}</div>
      <div className="num mt-1 text-[23px] font-bold tracking-tight">{v}</div>
      {s && <div className="mt-0.5 text-xs text-faint">{s}</div>}
    </div>
  );
}

function Small({ children }: { children: ReactNode }) {
  return <small className="text-[13px] font-semibold text-muted-foreground">{children}</small>;
}
