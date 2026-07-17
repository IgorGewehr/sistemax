import { AnimatePresence, motion } from 'framer-motion';
import { Plug, Receipt } from 'lucide-react';

import { PageHeader } from '@/components/shared';

import { ConfiguracoesNav } from './ConfiguracoesNav';
import { ConfirmStatusModal } from './ConfirmStatusModal';
import { EmBreveSection } from './EmBreveSection';
import { EmpresaFormModal } from './EmpresaFormModal';
import { EmpresaSection } from './EmpresaSection';
import { PerfilSection } from './PerfilSection';
import type { ConfiguracoesVm } from './useConfiguracoes';
import { UsuarioFormModal } from './UsuarioFormModal';
import { UsuariosSection } from './UsuariosSection';

interface ConfiguracoesHomeProps {
  vm: ConfiguracoesVm;
}

/**
 * Casca de "Configurações" — sub-nav lateral + conteúdo da seção ativa. Página fina: todo o estado
 * vem de `useConfiguracoes`; aqui só se decide o que renderizar. Sem sub-rotas de propósito (a
 * troca de seção é estado local, não navegação) — mesma escolha de "Compras" (`vm.view.kind`), pra
 * não precisar coordenar com o roteamento que o orquestrador ainda vai ligar.
 */
export function ConfiguracoesHome({ vm }: ConfiguracoesHomeProps) {
  return (
    <div>
      <PageHeader subtitle="Perfil, empresa, usuários e permissões — tudo num só lugar." />

      <div className="grid grid-cols-1 gap-5 md:grid-cols-[220px_1fr]">
        <ConfiguracoesNav secaoAtiva={vm.secaoAtiva} secoesVisiveis={vm.secoesVisiveis} onTrocarSecao={vm.onTrocarSecao} />

        <AnimatePresence mode="wait">
          <motion.div
            key={vm.secaoAtiva}
            initial={{ opacity: 0, y: 8 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.22 }}
          >
            {vm.secaoAtiva === 'perfil' && (
              <PerfilSection usuario={vm.usuarioSessao} onSalvarPerfil={vm.onSalvarPerfil} onAlterarPin={vm.onAlterarPin} />
            )}

            {vm.secaoAtiva === 'empresa' && (
              <>
                <EmpresaSection empresa={vm.empresa} podeEditar={vm.podeEditarEmpresa} onAbrirEditar={vm.onAbrirEditarEmpresa} />
                <EmpresaFormModal
                  open={vm.editandoEmpresa}
                  empresa={vm.empresa}
                  onClose={vm.onFecharEditarEmpresa}
                  onSalvar={vm.onSalvarEmpresa}
                />
              </>
            )}

            {vm.secaoAtiva === 'usuarios' && (
              <>
                <UsuariosSection
                  usuarios={vm.usuarios}
                  usuarioSessaoId={vm.usuarioSessao.id}
                  podeAdministrarUsuarios={vm.podeAdministrarUsuarios}
                  onNovoUsuario={vm.onAbrirCriarUsuario}
                  onEditarUsuario={vm.onAbrirEditarUsuario}
                  onAlternarStatus={vm.onAbrirConfirmarStatus}
                />
                <UsuarioFormModal
                  open={vm.modalUsuarioForm !== null}
                  modo={vm.modalUsuarioForm?.modo ?? 'criar'}
                  usuarioEmEdicao={vm.usuarioEmEdicao}
                  sessaoPapel={vm.sessao.papel}
                  onClose={vm.onFecharModalUsuario}
                  onSalvar={vm.onSalvarUsuario}
                />
                <ConfirmStatusModal
                  open={vm.usuarioEmConfirmacao !== undefined}
                  usuario={vm.usuarioEmConfirmacao}
                  onClose={vm.onFecharConfirmarStatus}
                  onConfirmar={vm.onConfirmarAlternarStatus}
                />
              </>
            )}

            {vm.secaoAtiva === 'fiscal' && (
              <EmBreveSection
                titulo="Fiscal"
                icon={<Receipt className="h-5 w-5" />}
                descricao="Certificado digital, regime tributário e séries de numeração vão morar aqui. Hoje a emissão já funciona a partir do PDV/Ordens com os dados padrão da Empresa."
              />
            )}

            {vm.secaoAtiva === 'integracoes' && (
              <EmBreveSection
                titulo="Integrações"
                icon={<Plug className="h-5 w-5" />}
                descricao="WhatsApp, gateway de NF-e e meios de pagamento vão se conectar por aqui. Por enquanto, cada módulo já funciona de forma independente."
              />
            )}
          </motion.div>
        </AnimatePresence>
      </div>
    </div>
  );
}
