import { Pencil } from 'lucide-react';

import { SectionCard } from '@/components/shared';
import { Button } from '@/components/ui/Button';

import { iniciais } from './calc';
import type { Empresa } from './types';

interface EmpresaSectionProps {
  empresa: Empresa;
  /** Só quem tem `configuracoes:editar` vê o botão — gerentes, por exemplo, só visualizam (ver
   *  padrão do papel `manager` em `lib/permissions.ts`). */
  podeEditar: boolean;
  onAbrirEditar: () => void;
}

/** Seção "Empresa" — dados cadastrais exibidos em modo leitura; a edição vive só no modal
 *  (`EmpresaFormModal`), nunca inline nesta seção. */
export function EmpresaSection({ empresa, podeEditar, onAbrirEditar }: EmpresaSectionProps) {
  return (
    <SectionCard
      title="Empresa"
      actions={
        podeEditar && (
          <Button variant="outline" size="sm" icon={<Pencil className="h-3.5 w-3.5" />} onClick={onAbrirEditar}>
            Editar
          </Button>
        )
      }
      bodyClassName="p-4"
    >
      <div className="flex flex-wrap items-start gap-4">
        <span className="flex h-14 w-14 shrink-0 items-center justify-center overflow-hidden rounded-2xl bg-surface-2 text-lg font-bold text-foreground">
          {empresa.logoUrl ? <img src={empresa.logoUrl} alt="" className="h-full w-full object-cover" /> : iniciais(empresa.nomeFantasia)}
        </span>
        <div className="min-w-0 flex-1 space-y-1 text-sm">
          <p className="font-semibold text-foreground">{empresa.nomeFantasia}</p>
          <p className="text-muted-foreground">{empresa.nome}</p>
          <p className="text-muted-foreground">CNPJ {empresa.cnpj}</p>
          <p className="text-muted-foreground">
            {empresa.endereco.logradouro} · {empresa.endereco.bairro} · {empresa.endereco.cidade}/{empresa.endereco.uf} ·{' '}
            {empresa.endereco.cep}
          </p>
          <p className="text-muted-foreground">
            {empresa.telefone} · {empresa.email}
          </p>
        </div>
      </div>
    </SectionCard>
  );
}
