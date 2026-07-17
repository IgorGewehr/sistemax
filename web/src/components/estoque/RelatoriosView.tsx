import { FileWarning } from 'lucide-react';
import { useState } from 'react';

import { PageHeader, SectionCard } from '@/components/shared';
import { EmptyState } from '@/components/ui/EmptyState';
import { formatDate } from '@/lib/format';
import { cn } from '@/lib/utils';

import { PosicaoValorizadaReport } from './PosicaoValorizadaReport';
import type { EstoqueVm } from './useEstoque';

interface RelatorioMeta {
  id: string;
  rid: string;
  nome: string;
  desc: string;
  disponivel: boolean;
  motivo?: string;
}

/** Galeria de relatórios (`RELATORIOS`/`.rel-gallery` do mockup) — mesmos 7 cartões, mas só "R1 ·
 * Posição valorizada" roda com dado real hoje. Os demais precisam de histórico de consumo/venda ou
 * de APIs (movimentações, inventário) que o Bridge ainda não tem — cada um diz exatamente o quê. */
const RELATORIOS: RelatorioMeta[] = [
  { id: 'posicao', rid: 'R1', nome: 'Posição valorizada', desc: 'físico × custo médio, agora', disponivel: true },
  {
    id: 'abc',
    rid: 'R2',
    nome: 'Curva ABC',
    desc: '80/15/5 por valor de saída',
    disponivel: false,
    motivo: 'Precisa do consumo histórico por produto (razão de movimentações), que o Bridge ainda não expõe.',
  },
  {
    id: 'giro',
    rid: 'R3',
    nome: 'Giro & cobertura',
    desc: 'parados + dias de estoque',
    disponivel: false,
    motivo: 'Giro anualizado e cobertura em dias dependem do consumo histórico — ainda não capturado.',
  },
  {
    id: 'ruptura',
    rid: 'R4',
    nome: 'Ruptura',
    desc: 'venda perdida estimada',
    disponivel: false,
    motivo: 'Precisa da velocidade de venda por produto (histórico de saída), não disponível ainda.',
  },
  {
    id: 'kardex',
    rid: 'R5',
    nome: 'Kardex',
    desc: 'razão por produto',
    disponivel: false,
    motivo: 'Depende do razão de movimentações — mesma API pendente da aba Movimentações.',
  },
  {
    id: 'inventario',
    rid: 'R6',
    nome: 'Inventário',
    desc: 'divergências da contagem',
    disponivel: false,
    motivo: 'Depende do módulo de contagens físicas — mesma API pendente da aba Inventários.',
  },
  {
    id: 'sugestao',
    rid: 'R7',
    nome: 'Sugestão de compra',
    desc: 'no ponto de reposição',
    disponivel: false,
    motivo: 'Precisa do ponto de reposição e do consumo médio por produto — nenhum dos dois é capturado hoje.',
  },
];

interface RelatoriosViewProps {
  vm: EstoqueVm;
}

export function RelatoriosView({ vm }: RelatoriosViewProps) {
  const [ativo, setAtivo] = useState<string>('posicao');
  const meta = RELATORIOS.find((r) => r.id === ativo) ?? RELATORIOS[0];

  return (
    <div>
      <PageHeader subtitle="Inteligência do estoque — hoje só a Posição valorizada roda com dado real de ponta a ponta." />

      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        {RELATORIOS.map((r) => (
          <button
            key={r.id}
            type="button"
            onClick={() => setAtivo(r.id)}
            className={cn(
              'flex flex-col gap-1 rounded-2xl border border-border bg-card p-3.5 text-left transition-colors',
              r.id === ativo ? 'border-primary-600 bg-primary-soft' : 'hover:border-primary-600/40',
              !r.disponivel && 'opacity-70',
            )}
          >
            <span className="text-[11px] font-bold tracking-wide text-primary-600">{r.rid}</span>
            <span className="text-[13.5px] font-bold text-foreground">{r.nome}</span>
            <span className="text-xs text-muted-foreground">
              {r.desc}
              {!r.disponivel && ' · em breve'}
            </span>
          </button>
        ))}
      </div>

      <SectionCard title={`Preview — ${meta.nome}`} hint={meta.desc}>
        {meta.disponivel ? (
          <PosicaoValorizadaReport
            categorias={vm.categorias}
            totalCentavos={vm.kpis.valorEmEstoqueCentavos}
            totalItens={vm.kpis.itensComSaldo}
            dataLabel={formatDate(new Date())}
          />
        ) : (
          <div className="px-[18px] pb-6 pt-1">
            <EmptyState icon={<FileWarning className="h-5 w-5" />} title="Ainda não disponível" description={meta.motivo ?? ''} className="border-none py-8" />
          </div>
        )}
      </SectionCard>
    </div>
  );
}
