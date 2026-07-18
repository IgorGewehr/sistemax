import { ChevronDown, FolderKanban } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

import { ProjetoPainel } from '@/components/financial/projetos/ProjetoPainel';
import { ProjetoSeletor } from '@/components/financial/projetos/ProjetoSeletor';
import { useProjetos } from '@/components/financial/projetos/useProjetos';
import { OptInEmptyState, PageHeader } from '@/components/shared';
import { EmptyState } from '@/components/ui/EmptyState';
import { Skeleton } from '@/components/ui/Skeleton';
import { Surface } from '@/components/ui/Surface';
import { useToast } from '@/lib/toast';

/**
 * Financeiro › Projetos — 1:1 com `docs/ui/mockups/projeto.html`: unit economics por linha de
 * produto. Dado REAL: `GET /financeiro/configuracoes` + `GET /financeiro/projetos` +
 * `GET /financeiro/projetos/{id}/painel` (ver `useProjetos`). Opt-in: `analisePorProjetoAtiva`
 * desligado ⇒ estado vazio elegante em vez de tabela/gráfico vazios (nunca dado fabricado).
 */
export function Projetos() {
  const vm = useProjetos();
  const { toast } = useToast();
  const navigate = useNavigate();

  const ativo = vm.configuracao.dado?.analisePorProjetoAtiva ?? null;

  return (
    <div>
      <PageHeader
        subtitle="Unit economics por linha de produto — o que cada projeto ganha, custa e devolve."
        actions={
          <>
            {ativo !== null && (
              <button
                type="button"
                onClick={() => navigate('/financeiro/configuracoes')}
                className={
                  ativo
                    ? 'inline-flex items-center gap-2 rounded-xl border border-pos/40 bg-pos-soft px-3 py-1.5 text-xs font-semibold text-pos'
                    : 'inline-flex items-center gap-2 rounded-xl border border-border bg-surface-2 px-3 py-1.5 text-xs font-semibold text-muted-foreground'
                }
                title="Ver/alterar em Configurações"
              >
                <span className={`h-[9px] w-[16px] rounded-full ${ativo ? 'bg-pos' : 'bg-faint'}`} />
                Análise por Projeto · {ativo ? 'Ativa' : 'Desligada'}
              </button>
            )}
            <button
              type="button"
              onClick={() => toast('Trocar a janela recarregaria as métricas do projeto no novo período.', 'info')}
              className="inline-flex items-center gap-2 rounded-xl border border-border bg-card px-3 py-2 text-sm font-semibold text-foreground"
            >
              Janela · mês corrente
              <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
            </button>
          </>
        }
      />

      <Conteudo vm={vm} />
    </div>
  );
}

function Conteudo({ vm }: { vm: ReturnType<typeof useProjetos> }) {
  if (vm.configuracao.carregando || vm.projetos.carregando) {
    return (
      <div className="space-y-4">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <Surface key={i} padding="md" className="min-h-[100px]">
              <Skeleton className="h-4 w-28" />
              <Skeleton className="mt-3 h-6 w-20" />
            </Surface>
          ))}
        </div>
        <Surface padding="lg" className="min-h-[280px]">
          <Skeleton className="h-4 w-56" />
          <Skeleton className="mt-6 h-[220px] w-full" />
        </Surface>
      </div>
    );
  }

  if (vm.configuracao.erro) {
    return (
      <Surface padding="lg">
        <EmptyState
          icon={<FolderKanban className="h-5 w-5" />}
          title="Não deu para carregar"
          description={vm.configuracao.erro}
          className="border-none py-6"
        />
      </Surface>
    );
  }

  if (vm.configuracao.dado && !vm.configuracao.dado.analisePorProjetoAtiva) {
    return (
      <OptInEmptyState
        titulo="Análise por Projeto está desligada"
        descricao="Ative a Análise por Projeto em Configurações para acompanhar MRR, margem, capacidade e ROI por linha de produto."
      />
    );
  }

  if (vm.projetos.erro) {
    return (
      <Surface padding="lg">
        <EmptyState
          icon={<FolderKanban className="h-5 w-5" />}
          title="Não deu para carregar os projetos"
          description={vm.projetos.erro}
          className="border-none py-6"
        />
      </Surface>
    );
  }

  if (!vm.projetos.dado || vm.projetos.dado.length === 0) {
    return (
      <EmptyState
        icon={<FolderKanban className="h-5 w-5" />}
        title="Nenhum projeto cadastrado ainda"
        description="Crie um projeto para começar a taguear assinaturas, ativos e tempo — e ver o unit economics de cada linha de produto."
      />
    );
  }

  return (
    <>
      <ProjetoSeletor
        projetos={vm.projetos.dado}
        paineis={vm.paineis}
        selecionadoId={vm.selecionadoId}
        onSelecionar={vm.selecionar}
      />
      <ProjetoPainel painel={vm.painelAtivo} />
    </>
  );
}
