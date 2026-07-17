import { KeyRound, Pencil } from 'lucide-react';
import { useState } from 'react';

import { SectionCard } from '@/components/shared';
import { Button } from '@/components/ui/Button';
import type { Usuario } from '@/lib/permissions';

import { AlterarPinModal } from './AlterarPinModal';
import { iniciais } from './calc';
import { PapelBadge } from './chips';
import { PerfilEditModal } from './PerfilEditModal';
import type { AlterarPinFormValues, PerfilFormValues } from './types';

interface PerfilSectionProps {
  usuario: Usuario;
  onSalvarPerfil: (valores: PerfilFormValues) => void;
  onAlterarPin: (valores: AlterarPinFormValues) => void;
}

/** Seção "Perfil" — dados do usuário logado. Sempre acessível, não depende de nenhuma permissão de
 *  `configuracoes`: todo mundo edita o próprio perfil e PIN, mesmo quem não administra o módulo
 *  (ver `secoesVisiveis` em `useConfiguracoes.ts`). */
export function PerfilSection({ usuario, onSalvarPerfil, onAlterarPin }: PerfilSectionProps) {
  const [editandoPerfil, setEditandoPerfil] = useState(false);
  const [alterandoPin, setAlterandoPin] = useState(false);

  return (
    <SectionCard
      title="Meu perfil"
      actions={
        <Button variant="outline" size="sm" icon={<Pencil className="h-3.5 w-3.5" />} onClick={() => setEditandoPerfil(true)}>
          Editar
        </Button>
      }
      bodyClassName="p-4"
    >
      <div className="flex flex-wrap items-center gap-4">
        <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-gradient-red text-lg font-bold text-white shadow-red">
          {iniciais(usuario.nome)}
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-base font-semibold text-foreground">{usuario.nome}</span>
            <PapelBadge papel={usuario.papel} />
          </div>
          <div className="text-sm text-muted-foreground">{usuario.email}</div>
          {usuario.telefone && <div className="text-sm text-muted-foreground">{usuario.telefone}</div>}
        </div>
        <Button variant="outline" size="sm" icon={<KeyRound className="h-3.5 w-3.5" />} onClick={() => setAlterandoPin(true)}>
          Alterar PIN
        </Button>
      </div>

      <PerfilEditModal
        open={editandoPerfil}
        usuario={usuario}
        onClose={() => setEditandoPerfil(false)}
        onSalvar={(valores) => {
          onSalvarPerfil(valores);
          setEditandoPerfil(false);
        }}
      />
      <AlterarPinModal
        open={alterandoPin}
        onClose={() => setAlterandoPin(false)}
        onSalvar={(valores) => {
          onAlterarPin(valores);
          setAlterandoPin(false);
        }}
      />
    </SectionCard>
  );
}
