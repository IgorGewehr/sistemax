import { PageHeader } from '@/components/shared';

import { semCustoMedioDe } from './calc';
import { CategoriaSection } from './CategoriaSection';
import { ConsultorCard } from './ConsultorCard';
import { KpisRow } from './KpisRow';
import type { EstoqueVm } from './useEstoque';

interface VisaoGeralViewProps {
  vm: EstoqueVm;
}

/** Aba "Visão geral" — 1:1 com `view-geral` do mockup (ver README.md p/ o que não tem API ainda). */
export function VisaoGeralView({ vm }: VisaoGeralViewProps) {
  return (
    <div>
      <PageHeader subtitle="O que você tem, o que está acabando, e quanto capital está parado." />
      <KpisRow kpis={vm.kpis} />
      <ConsultorCard
        resumo={vm.consultor}
        onVerProdutosComProblema={vm.irParaProdutosComProblema}
        onVerTodosProdutos={() => vm.irParaTab('produtos')}
      />
      <CategoriaSection
        categorias={vm.categorias}
        categoriaAtiva={vm.categoriaAtiva}
        onSelecionar={vm.selecionarCategoria}
        onVoltar={vm.voltarCategorias}
        kpis={vm.kpis}
        semCustoMedio={semCustoMedioDe(vm.itens)}
      />
    </div>
  );
}
