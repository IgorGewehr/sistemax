# Configurações

Casca única do módulo com 5 seções claras (sub-nav lateral, não sub-rotas): **Perfil**, **Empresa**,
**Usuários & Permissões** (o RBAC), **Fiscal** e **Integrações** (as 2 últimas são placeholders
honestos — "em breve"). De propósito **sem** as trocentas opções que o saas-erp irmão acumulou:
poucas seções, cada uma com 1 responsabilidade clara.

## Arquitetura

- O modelo de permissões (`Papel`, `Modulo`, `Acao`, `Permissao`, `PermissaoOverride`, `Usuario`,
  `useSessaoPermissoes`) mora em **`@/lib/permissions.ts`**, não aqui — é a fonte única PRETENDIDA
  pra Sidebar/Dashboard também (ainda não migraram, ver cabeçalho de `lib/permissions.ts`). Este
  módulo consome esse arquivo, nunca redeclara um tipo paralelo.
- `types.ts` é o spec (SDD) do que É específico desta tela: `Empresa`, `SecaoConfiguracoes`,
  formulários. `calc.ts` tem toda a matemática pura (diff de overrides, máscara de CNPJ, validação
  de PIN/email) — testável sem `useState`/JSX.
- `useConfiguracoes.ts` concentra todo o estado (seção ativa, modais, CRUD de empresa/usuários);
  `Configuracoes.tsx` (página) → `ConfiguracoesHome.tsx` → `*Section.tsx` ficam finos, só compondo
  a partir do view-model que o hook devolve. Espelha `useClientes.ts`/`useCompras.ts`.
- `mocks/configuracoes.ts` implementa `ConfiguracoesMock` — trocar por API não muda nenhuma tela.

## Decisões que não são óbvias

- **Visibilidade de SEÇÃO dentro de Configurações usa o mesmo princípio de RBAC que
  Sidebar/Dashboard deveriam aplicar a módulo inteiro (e ainda não aplicam — hoje só Configurações
  consome `lib/permissions.ts` de fato)**, um nível abaixo: "Perfil" é sempre visível (é o
  próprio usuário mexendo nos próprios dados, nunca gated por permissão de módulo); as outras 4
  exigem `configuracoes:ver`; "Usuários & Permissões" exige especificamente
  `configuracoes:gerenciarUsuarios` (ver `secoesVisiveis` em `useConfiguracoes.ts`). Isso significa
  que um `manager` (que só tem `configuracoes:ver` por padrão) abre Configurações e vê Perfil,
  Empresa (só leitura — sem botão "Editar"), Fiscal e Integrações, mas NÃO vê "Usuários &
  Permissões" no menu.
- **Overrides de permissão são sempre um DIFF contra o padrão do papel, nunca o conjunto final
  persistido** (`alternarCelula`, `calc.ts`). Isso é o que faz um usuário continuar herdando
  automaticamente qualquer permissão nova que o papel ganhar no futuro, sem precisar re-migrar
  dados existentes. Trocar de papel no formulário reseta os overrides — eles foram desenhados
  contra o padrão anterior.
- **`founder` e `admin` têm o MESMO conjunto padrão de permissões** (`PAPEL_PERMISSOES_PADRAO` em
  `lib/permissions.ts`) — a diferença entre os dois não é "o que cada um pode fazer" (seria
  complexidade sem valor real pra um ERP pequeno), é uma regra de hierarquia à parte: só o
  `founder` é intocável (`ehIntocavel`) — ninguém desativa ou rebaixa o fundador da conta, nem
  outro admin. Isso evita o cenário "empresa fica sem nenhum founder ativo" sem precisar de uma
  segunda dimensão de permissão só pra isso.
- **A trava de founder cobre "desativar" E "trocar o papel", nos dois lugares (UI + hook)**:
  `UsuariosTable` desabilita o botão de status pra qualquer linha `ehIntocavel` (ninguém desativa
  founder, nem outro founder); `UsuarioFormModal` trava o `<select>` de Papel de um alvo já-founder
  pra quem não é founder (`papelTravado`) e nunca lista "Fundador" como opção pra quem não é founder
  (`papeisDisponiveis`) — evita mintar founder novo ou se autopromover. O bloqueio de UI é só
  affordance: quem decide de fato é `papelResolvidoParaSalvar` (`calc.ts`), chamado por
  `onSalvarUsuario` (`useConfiguracoes.ts`) sem confiar no que a tela já impediu — só outro
  `founder` consegue conceder ou remover esse papel de alguém, na prática (diferente de "desativar",
  que segue absoluto pra qualquer ator). `onConfirmarAlternarStatus` replica a mesma desconfiança
  pro status: reverifica `ehIntocavel` mesmo com o botão da tabela já desabilitado.
- **Ninguém se autodesativa pela tela de Usuários** — o botão de status também trava quando
  `usuario.id === usuarioSessaoId` (`ehVoce`), análogo ao tratamento de `intocavel`. Sem isso, um
  admin comum poderia revogar o próprio acesso sem ter certeza de que outro admin/founder está
  ativo pra reverter.
- **A sessão "efetiva" (`useConfiguracoes.ts`) deriva `pode`/`podeVer`/`modulosVisiveis` a partir de
  `usuarioSessao`, nunca do `useSessaoPermissoes()` mockado cru** — o mock devolve sempre o mesmo
  objeto congelado, então se o próprio admin logado edita o próprio papel/overrides pela tela de
  Usuários, `usuarioSessao` (recalculado via `usuarioById`) já refletia a mudança, mas os GATES
  (`secoesVisiveis`, `podeEditarEmpresa`, `podeAdministrarUsuarios`) ficavam presos ao papel
  original até recarregar a página. `sessaoEfetiva` resolve isso recalculando os mesmos campos do
  contrato `SessaoPermissoes` a partir do `usuarioSessao` já atualizado.
- **`PermissoesGrid` não é uma `<table>` de colunas fixas** — cada módulo tem um conjunto de ações
  diferente (`MODULO_ACOES`: a maioria só `ver`/`editar`; `pdv` também tem "abrir/fechar caixa";
  `fiscal` também tem "emitir"; `configuracoes` também tem "gerenciar usuários"). Uma tabela com
  todas as colunas deixaria a maioria das células vazias sem sentido — cada linha só mostra os
  toggles que existem pra aquele módulo.
- **Sem tom `info` em `chips.tsx`** (mesma lição de `components/clientes/chips.tsx`): o
  `StatusChip` de `components/shared` é vocabulário de caixa (sobra/falta/aberto/bateu), não serve
  pro status cadastral ativo/inativo de um usuário — daí o chip local. O badge de papel, por outro
  lado, reusa `ui/Badge` (é só uma etiqueta fechada de 5 valores, não precisa do "ponto" semântico
  de estado).
- **Alterar PIN não persiste nada localmente** (`onAlterarPin` em `useConfiguracoes.ts` é
  deliberadamente um no-op comentado) — validar "PIN atual confere" e gravar o novo PIN é
  responsabilidade do backend/Bridge real; fingir uma escrita local daria falsa sensação de que a
  troca já vale pra login de verdade.
- **Sem mockup aprovado** (ao contrário de Financeiro/Compras) — esta tela segue só o *padrão de
  arquitetura* (SDD, view-model tipado, componentes compartilhados), não a Lei 1 de "mockup como
  contrato".
