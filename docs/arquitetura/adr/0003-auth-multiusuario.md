# ADR-0003 — Auth multi-usuário: `Usuario` persistido em SQLite atrás do mesmo PIN-pad, sessão Bearer sem papel cacheado, RBAC do front lido do usuário real via o `AuthProvider` que já existe

**Status:** Proposto · **Data:** 2026-07-17 · **Contexto do produto:** hoje o Bridge local
(`Host.Desktop`) autentica com **um único PIN de gerente** por instalação (`config.json`) e o
RBAC do front (`web/src/lib/permissions.ts`) já está pronto — papéis, permissões granulares,
overrides — mas roda 100% sobre um usuário **mockado fixo**, sem nenhuma ligação com a sessão
Bearer real que `web/src/lib/auth.tsx` já emite. Uma loja tem várias pessoas operando o mesmo
computador (caixa, gerente, técnico) e hoje elas são indistinguíveis para o sistema.

## Pergunta que este ADR responde
> "Como o Bridge evolui de PIN-único-por-instalação para autenticação MULTI-USUÁRIO real —
> `Usuario` persistido, PIN por pessoa, sessão Bearer por pessoa, e o RBAC que já existe no front
> passando a ler quem está de fato logado — sem jogar fora o que já roda hoje (`PinHasher`,
> `SessionStore`, `AuthProvider`/`AuthGate`, o teclado de PIN, `lib/permissions.ts` inteiro)?"

**Resposta curta:** **`Usuario` vira um agregado persistido em SQLite (novo módulo
`Identidade`, mesmo molde de `docs/persistencia/persistencia-sqlite.md`), mas o LOGIN CONTINUA
sendo só PIN — nenhum seletor de pessoa.** O PIN deixa de ser comparado contra `config.json` e
passa a ser verificado contra **todos os usuários ativos da instalação** (mesmo `PinHasher`
PBKDF2 de hoje, chamado uma vez por candidato); o primeiro que bater é quem logou. A sessão
Bearer (`SessionStore`) passa a carregar `UsuarioId` em vez de um `Papel` fixo, e o papel/overrides
são **resolvidos frescos do SQLite a cada request** (nunca cacheados no token) — revogar alguém
tem efeito imediato, não espera a sessão expirar. No front, `web/src/lib/auth.tsx` (que já existe,
já gerencia a sessão Bearer) ganha o usuário completo (`GET /api/auth/me`) e vira a ÚNICA fonte
de identidade; `useSessaoPermissoes()` (`lib/permissions.ts`) troca o mock por essa fonte sem
mudar UMA linha das funções puras (`permissoesEfetivas`, `usuarioPode`, `modulosVisiveisDe`, ...).
Tudo dentro do mesmo `sistemax.db` local — zero chamada de rede, local-first de ponta a ponta.

## Decisão

### 1. `Usuario` é um agregado novo, num módulo novo (`Identidade`) — não um apêndice do Bridge

`Usuario` tem invariantes de negócio reais (papel + overrides resolvem permissão efetiva; founder
nunca fica sem substituto; PIN nunca em texto puro) — isso é `Domain`, não HTTP plumbing. Segue
exatamente o molde 3-camadas já usado por `Financeiro`/`Estoque`/`Fiscal`:

```
SistemaX.Modules.Identidade.Domain
  Usuario                 agregado — Reconstituir(...) para reidratação, sem regra/evento
  Papel                   enum: Founder, Admin, Manager, Operator, Viewer
  PapelHierarquia         Dictionary<Papel,int> — 100/80/60/40/20, MESMA convenção do
                          `ROLE_HIERARCHY` de `lib/permissions.ts` (o comentário lá já diz que é
                          convenção compartilhada do ecossistema — só materializamos o número
                          que já era pretendido dos dois lados)
  PermissaoOverride       (string Permissao, string Efeito) — Permissao é STRING OPAÇA no C#
                          (ex.: "financeiro:editar"); o Domain não conhece Modulo/Acao, só
                          guarda e devolve. Ver decisão #4 para por que isso é deliberado.
  PinHasher               MOVIDO de Bridge/PinHasher.cs para cá — é lógica de domínio de
                          Usuario (como o PIN vira hash), não HTTP. Zero linha de comportamento
                          muda (mesmo PBKDF2 210k, mesmo FixedTimeEquals); só o namespace/projeto.
                          Motivo estrutural, não cosmético: um módulo NUNCA pode depender do
                          Host (`Host.Desktop`) — é o Host que depende dos módulos — então
                          `Identidade.Domain`/`Infrastructure` não podem chamar um tipo que mora
                          em `Bridge/`.

SistemaX.Modules.Identidade.Application
  IUsuarioRepository            port: ObterPorIdAsync, ListarAsync(businessId, incluirInativos),
                                 Salvar(usuario)
  AutenticarPorPinUseCase        ver decisão #2
  CriarUsuarioUseCase / AlterarUsuarioUseCase / TrocarPinUseCase / ResetarPinUseCase
  Endpoints/IdentidadeEndpointsModule : IModuleEndpoints   ver tabela de rotas abaixo

SistemaX.Modules.Identidade.Infrastructure
  IdentidadeSchemaMigrationV1 : SqlModuleSchemaMigration   Modulo="identidade", Versao=1
  SqliteUsuarioRepository                                   mesmo par ExecutarAsync/ConsultarAsync
                                                             de SqliteFornecedorRepository
```

Schema (dentro do MESMO `sistemax.db` — `docs/persistencia/persistencia-sqlite.md` §4 já lista
isso como destino final de todo módulo, não um banco à parte):

```sql
CREATE TABLE IF NOT EXISTS usuarios (
  id TEXT PRIMARY KEY,                 -- ULID
  business_id TEXT NOT NULL,           -- tenant desta instalação — todo Usuario carrega, mesmo
                                        -- hoje sendo sempre o único BusinessId da instalação
  nome TEXT NOT NULL,
  email TEXT NOT NULL,
  telefone TEXT NULL,
  papel TEXT NOT NULL,                 -- 'founder'|'admin'|'manager'|'operator'|'viewer'
  status TEXT NOT NULL,                -- 'ativo'|'inativo'
  pin_hash TEXT NOT NULL,
  pin_salt TEXT NOT NULL,
  criado_em TEXT NOT NULL,
  ultimo_acesso_em TEXT NULL
);
CREATE INDEX IF NOT EXISTS ix_usuarios_business_id ON usuarios(business_id);

-- filho mutável — mesmo padrão de "parcelas/itens" do molde: DELETE WHERE usuario_id = @id +
-- reinsert dentro da mesma operação de Salvar(), nunca update linha a linha.
CREATE TABLE IF NOT EXISTS usuario_permissao_overrides (
  usuario_id TEXT NOT NULL REFERENCES usuarios(id) ON DELETE CASCADE,
  permissao TEXT NOT NULL,
  efeito TEXT NOT NULL,                -- 'conceder'|'revogar'
  PRIMARY KEY (usuario_id, permissao)
);
```

`Usuario.Reconstituir(...)` reidrata sem validar nem levantar evento, igual a
`Fornecedor.Reconstituir`/`Assinatura.Reconstituir`. Contract tests seguem o mesmo par
`InMemoryUsuarioRepositoryContractTests` + `SqliteUsuarioRepositoryContractTests` do molde.

### 2. Login continua sendo só PIN — verificado por busca entre os usuários ativos, não por seleção de pessoa

O client já tem `login(pin: string)` funcionando de ponta a ponta
(`web/src/lib/api/client.ts` → `POST /api/auth/login`) e a tela (`web/src/pages/Login.tsx`) é um
teclado numérico sem campo de usuário. **Não vamos trocar essa UX por um seletor de "quem é
você?"** — o pedido é "login por PIN por usuário", não "login por usuário depois PIN". A forma de
resolver PIN → pessoa sem um segundo campo:

```
AutenticarPorPinUseCase.ExecutarAsync(pin, businessId):
  candidatos = IUsuarioRepository.ListarAsync(businessId, incluirInativos: false)
               ordenados por UltimoAcessoEm desc (quem loga mais, loga de novo — reduz o caso
               médio; otimização pequena, não é o que garante corretude)
  para cada candidato:
    se PinHasher.Verificar(pin, candidato.PinHash, candidato.PinSalt) → sucesso, é esta pessoa
  nenhum bateu → falha genérica "PIN incorreto" (mesma mensagem de hoje — não revela se o PIN
  quase bateu em alguém, mesmo padrão de não-enumeração já usado no login atual)
```

Isso só funciona porque **PIN precisa ser único entre os usuários ATIVOS da instalação** — não
por regra digitada em algum lugar, mas estruturalmente: ao **criar** ou **trocar** um PIN
(`CriarUsuarioUseCase`/`TrocarPinUseCase`/`ResetarPinUseCase`), o caso de uso roda o MESMO loop de
verificação contra todos os outros ativos com o PIN em texto puro que acabou de chegar no
request (ele só existe nesse instante, antes de virar hash) — se bater em alguém, rejeita com
`usuario.pin_duplicado` (nomeado, nunca falha muda — mesma régua de "nunca inventar um resultado"
do ADR-0002). O usuário escolhe outro PIN e tenta de novo.

**Trade-off explícito, não escondido:** verificar PIN é `O(usuários ativos)` chamadas PBKDF2
(210k iterações cada). Numa loja com 10–20 funcionários isso é dezenas de ms no pior caso (nenhum
bate) — imperceptível num teclado numérico manual. Se uma instalação um dia tiver uma folha muito
maior, o próprio uso (verificação é *embaraçosamente paralela* — cada candidato é independente)
permite paralelizar sem redesenhar nada; não fazemos isso agora porque não há hoje nenhuma
instalação perto dessa escala (YAGNI).

O rate-limit de tentativas (`SessionStore.EstaBloqueado`/`RegistrarTentativaFalha`, 5 tentativas →
lockout 60s) **continua exatamente como está, sem nenhuma mudança** — e isso não é acidente: como
o PIN ainda não identifica ninguém até bater em um candidato, o lockout SÓ PODE ser global
(por instalação), nunca por `usuarioId`. É o desenho de hoje já certo para o modelo de amanhã.

### 3. Sessão Bearer ganha `UsuarioId`; papel/overrides NUNCA ficam cacheados no token

`Sessao` (`SessionStore.cs`) hoje é `(Token, BusinessId, Papel, ExpiraEm)` com `Papel` sempre
`"gerente"` fixo. Passa a ser:

```csharp
public sealed record Sessao(string Token, string BusinessId, string UsuarioId, DateTimeOffset ExpiraEm);
```

Sem `Papel`. `BearerSessionMiddleware`, depois de validar o token, resolve o `Usuario` **na hora**
via `IUsuarioRepository.ObterPorIdAsync(usuarioId)` e:
- se `Status != Ativo` → mata a sessão e devolve 401 `auth.usuario_inativo` (mesmo se o token
  ainda não expirou — é assim que "desativei o funcionário" tem efeito imediato, não em até 12h);
- se ativo → grava `businessId`, `usuarioId`, `papel`, `overrides` em `HttpContext.Items` para o
  endpoint de módulo ler (mesma extensão `SessaoHttpContextExtensions`, só ganhando os dois campos
  novos ao lado de `BusinessIdItemKey`).

**Por que não cachear papel/overrides no token** (alternativa mais óbvia, e mais barata em I/O):
resolveria mais rápido, mas um admin revogando o acesso de alguém "no papel" continuaria válido
por até 12h (TTL da sessão) — errado para um sistema com controle de acesso de verdade. Um `SELECT`
no SQLite local (WAL, no mesmo disco, sem rede) custa microssegundos; pagar isso por request em
troca de revogação imediata é a mesma lógica de "reconciliação autoritativa > merge cego" do
ADR-0001 — aqui, "fonte da verdade sempre lida > cache que pode mentir".

`ultimo_acesso_em` é atualizado (`UPDATE usuarios SET ultimo_acesso_em = @agora WHERE id = @id`)
logo após autenticar com sucesso — é um timestamp de auditoria, sobrescrever sempre é seguro, não
precisa de idempotência dedicada (não é criação de recurso nem replay de evento).

`POST /api/auth/logout` é NOVO (hoje `logout()` em `client.ts` só apaga o token do
`localStorage` — o Bearer continua válido no servidor até o TTL, o que num PDV com várias pessoas
revezando é um buraco real: alguém sai, esquece de fechar, a sessão do colega anterior continua
"aberta" no servidor). O endpoint chama `SessionStore.Revogar(token)` (remoção direta do
dicionário) — simples, sem side-effect cross-módulo.

### 4. `overrides` trafega como string opaca no C# — o vocabulário Módulo/Ação continua vivendo só no front

`Identidade.Domain` armazena `PermissaoOverride(string Permissao, string Efeito)` e **não**
redeclara `Modulo`/`Acao`/`MODULO_ACOES` em C#. Isso é deliberado: o catálogo de módulos/ações do
produto (`lib/permissions.ts` → `MODULOS`, `MODULO_ACOES`) já é hoje só consumido por UI (o
próprio arquivo documenta: "HOJE só o módulo Configurações... de fato consome este arquivo";
Sidebar/Dashboard nem migraram ainda). Duplicar esse catálogo no C# obrigaria um deploy do Host
toda vez que o front ganhasse um módulo ou ação nova — o oposto do espírito do ADR-0002 (dado
configurável > código). O servidor guarda e devolve `overrides` **verbatim**; quem interpreta
"isso quer dizer `financeiro:editar`" continua sendo só `permissoesEfetivas()` no front, com as
MESMAS funções puras de hoje, zero linha alterada.

**Consequência aceita:** um override com uma string de permissão inválida (typo, módulo que não
existe mais) fica salvo silenciosamente — mas é inofensivo: `permissoesEfetivas()` só dá `.add`/
`.delete` num `Set`, uma chave que nenhuma tela verifica não muda nada visível. Autoexplicativo o
bastante para não precisar de validação dupla nesta fatia. **Fora de escopo desta fatia** (nomeado,
não esquecido): autorização fina por `Modulo`/`Acao` em CADA endpoint do Bridge — hoje e depois
deste ADR, o servidor só sabe fazer autorização GROSSA por hierarquia de papel (decisão #6); a
tabela completa de permissões continua sendo só um gate de UI. Ver `lib/permissions.ts` linhas
5–11 — o próprio arquivo já nomeia essa lacuna (Sidebar/Dashboard sem RBAC real) como pendência
conhecida, anterior a este ADR.

### 5. `lib/auth.tsx` (que já existe) vira a única fonte de identidade — `lib/permissions.ts` não muda uma função pura

Hoje há DOIS mundos de auth completamente desconectados no front:

| | Hoje | Depois deste ADR |
|---|---|---|
| `web/src/lib/auth.tsx` (`AuthProvider`/`useAuth`) | sessão Bearer real (`token`, `businessId`, `papel: string` fixo) — já funciona ponta-a-ponta | ganha `usuarioAtual: Usuario \| null` — busca `GET /api/auth/me` assim que a sessão Bearer é válida (login OU boot com `readSession()` ainda dentro do TTL) |
| `web/src/lib/permissions.ts` (`useSessaoPermissoes`) | usuário MOCKADO fixo (`USUARIO_SESSAO_MOCK`), zero rede, desconectado do `auth.tsx` | lê `usuarioAtual` de `useAuth()` (o MESMO contexto acima) e computa `pode`/`podeVer`/`modulosVisiveis`/`podeAdministrarUsuarios` com as funções puras que já existem, sem mudar nenhuma delas |
| `web/src/components/layout/AuthGate.tsx` | só olha `session !== null` — libera `<App/>` com o papel ainda podendo ser genérico | passa a olhar `usuarioAtual !== null` também — `<App/>` (e portanto qualquer tela que chame `useSessaoPermissoes()`) só monta depois que o RBAC real já carregou. **Fail-closed por construção**: nenhum componente consegue rodar com o mock nem com um "meio-carregado" — ou o gate mostra o teclado de PIN / um spinner curto, ou o app já está com o `Usuario` de verdade |
| `web/src/components/configuracoes/useConfiguracoes.ts` | `useState<Usuario[]>(CONFIGURACOES_MOCK.usuarios)` — mutações são só `setUsuarios` local, nada persiste | troca a semente por `GET /api/usuarios`; cada mutação (criar, editar papel/overrides, ativar/desativar, trocar PIN) chama o endpoint real em vez de `setUsuarios`. `UsuariosTable`/`UsuarioFormModal`/`PermissoesGrid`/`calc.ts` **não mudam** — todos operam sobre o mesmo tipo `Usuario[]` de sempre |

A razão de caber tão bem: o próprio `lib/permissions.ts` já foi escrito com esse seam em mente —
o comentário no topo do arquivo já diz "quando existir sessão de verdade... troca-se só o CORPO
deste hook" — e `client.ts` já tem `setUnauthorizedHandler` com um comentário citando
"`<AuthProvider>`" antes mesmo dele existir de fato. Este ADR não inventa a costura, só a fecha.

Endpoints novos (`Identidade.Application.Endpoints.IdentidadeEndpointsModule`, registrado como
`IModuleEndpoints` igual a `Financeiro`/`Estoque`; `businessId` sempre de
`HttpContext.ObterBusinessId()`, nunca de query/corpo — mesma R1 do bridge documentada em
`bridge-http-local.md` §2):

| Rota | Auth | O que faz |
|---|---|---|
| `GET /api/auth/me` | Bearer (qualquer sessão válida) | `Usuario` completo do `usuarioId` da sessão — é o que alimenta `useSessaoPermissoes()` real |
| `GET /api/usuarios` | Bearer + `podeAdministrarUsuarios` | lista usuários (ativos+inativos) do `businessId` da sessão — alimenta `UsuariosTable` |
| `POST /api/usuarios` | Bearer + `podeAdministrarUsuarios` | cria (`nome`, `email`, `telefone`, `papel`, `pin` inicial) — 409 `usuario.pin_duplicado` se colidir |
| `PATCH /api/usuarios/{id}` | Bearer + `podeAdministrarUsuarios` | altera nome/email/telefone/papel/status — 422 `usuario.founder_intocavel` se a operação deixaria zero founders ativos |
| `PUT /api/usuarios/{id}/overrides` | Bearer + `podeAdministrarUsuarios` | substitui a lista de overrides (delete+reinsert) |
| `POST /api/usuarios/{id}/resetar-pin` | Bearer + `podeAdministrarUsuarios` | define novo PIN para outra pessoa (ex.: esqueceu) |
| `POST /api/auth/trocar-pin` | Bearer (sobre si mesmo) | exige PIN atual + novo — alimenta o formulário `AlterarPinFormValues` que já existe em `components/configuracoes/types.ts` |
| `POST /api/auth/logout` | Bearer | revoga o token no `SessionStore` (novo, ver decisão #3) |

`POST /api/auth/login` continua em `Bridge/BridgeEndpoints.cs` — **não migra para o módulo**. O
boot-token, o rate-limit global e a emissão de sessão são concerns de EDGE do Host (existem antes
de qualquer módulo estar "pronto para servir domínio"), exatamente como o comentário atual do
arquivo já justifica para `/api/health`. O que muda dentro do handler é só a chamada interna:
em vez de comparar `corpo.Pin` contra `config.PinAdminHash`, delega para
`AutenticarPorPinUseCase` (Identidade.Application) resolvido via DI. Bridge continua sendo a
borda; Identidade continua sendo o domínio — a mesma divisão que já existe entre
`BridgeEndpoints` (Host) e `FinanceiroEndpointsModule`/`EstoqueEndpointsModule` (módulo), só que
agora o Host CHAMA para dentro do módulo em vez de decidir sozinho.

### 6. Autorização grossa por hierarquia, hoje mesmo — `PodeAdministrarUsuarios`/`EhIntocavel` existem também em C#

As únicas ações que este ADR introduz de fato no servidor (criar/editar/desativar OUTRO usuário)
precisam de um gate real, não só o botão desabilitado no front. `Usuario` (Domain) ganha os dois
métodos espelhando `lib/permissions.ts` literalmente função por função:

```csharp
public bool PodeAdministrarUsuarios() => PapelHierarquia.Valores[Papel] >= PapelHierarquia.Valores[Papel.Admin];
public bool EhIntocavel() => Papel == Papel.Founder;
```

É uma duplicação deliberada e mínima (duas funções puras de uma linha, não a tabela inteira de
permissões — essa continua só no front, decisão #4) porque a UI e a API precisam responder à
MESMA pergunta em momentos diferentes: a UI esconde o botão antes de qualquer request; a API
recusa a operação mesmo que alguém monte o request na mão. `AlterarUsuarioUseCase` chama
`ListarAsync(businessId, apenasAtivos: true)` e conta founders em memória antes de aplicar uma
mudança de papel/status que reduziria esse conjunto a zero — sem query dedicada de `COUNT`, o `N`
é sempre pequeno (é a mesma lista que `GET /api/usuarios` já devolve).

### 7. Migração do PIN único existente + PIN de recuperação (break-glass) — reaproveitando os campos que já existem em `config.json`

`HostConfig.PinAdminHash`/`PinAdminSalt` não são apagados — **mudam de papel**. No primeiro boot
depois desta mudança (schema de `identidade` aplicado e a tabela `usuarios` ainda vazia):

1. Se `usuarios` está vazia E `config.json` tem um `PinAdminHash` (instalação existente, F1 de
   hoje) → cria **um** `Usuario` com `papel: Founder`, nome `NomeLoja` + " (administrador)",
   reaproveitando o MESMO hash/salt que já está no arquivo (compatível — mesmo PBKDF2, zero
   re-hash necessário). Ninguém perde acesso na virada.
2. Um NOVO PIN de recuperação é gerado nesse mesmo boot (aleatório, nunca copiado do PIN do
   founder migrado) e gravado nos MESMOS campos `PinAdminHash`/`PinAdminSalt` de `config.json` —
   só que agora com um significado diferente: não é mais "o PIN de login", é o **PIN de
   emergência da instalação**. É impresso UMA VEZ no log (Serilog, console + arquivo, já
   documentado em `bridge-http-local.md` §8) com um aviso destacado — mesmo padrão de "senha de
   bootstrap impressa uma vez só" que ferramentas de infra sérias usam, porque um sistema
   local-first não tem "esqueci minha senha" por e-mail: não há e-mail, não há rede.
3. Um endpoint `POST /api/auth/recuperacao` (Bridge, mesma família de `/api/auth/login`: exige
   boot-token) troca esse PIN de recuperação por uma sessão Bearer de curtíssima duração (15min,
   bem menor que as 12h normais — reduz a janela de uma credencial tão poderosa) que só serve para
   `POST /api/usuarios` (recriar/reativar um founder). Não é uma sessão "normal" — é um modo de
   emergência de uso único e propósito único.

**Por que reaproveitar os campos em vez de apagá-los:** é literalmente "sem jogar fora o que
existe" — o par hash/salt, o `PinHasher`, o formato do `config.json` já resolvem o problema de
"guardar um segredo local com segurança"; só a SEMÂNTICA de para que serve esse segredo muda.

## Por que NÃO um seletor de usuário antes do PIN

É o desenho mais comum em POS de mercado (Toast, Square: toque no seu nome, depois PIN) e
resolveria a colisão de PIN de graça (identidade já escolhida, o PIN só confirma). Rejeitado
porque **o pedido explícito era "login por PIN por usuário"**, não "seleção de usuário" — e
porque o código que já roda hoje (`login(pin: string)` em `client.ts`, a tela de teclado em
`Login.tsx`) assume um único campo. Trocar a UX custaria uma tela nova e um contrato de API novo
para resolver um problema (colisão de PIN) que já tem solução mais barata (unicidade garantida na
escrita, decisão #2) sem gerar NENHUM código novo do lado da UI de login.

## Por que NÃO CRDT/sincronizar `Usuario` entre nós nesta fatia

O ADR-0001 já separa o que converge sozinho (G-Set do log de eventos, LWW-register de config) do
que precisa de autoridade única (estoque, numeração fiscal). `Usuario` hoje não tem ESSE problema
simplesmente porque **não há nó para sincronizar com**: `Host.Desktop` é o único processo, com um
único `sistemax.db`, por loja, nesta fase do produto (`Infrastructure/Sync` continua esqueleto,
conforme o próprio ADR-0001 registra em "Estado atual no repo"). Se um dia uma loja tiver mais de
um terminal físico rodando `Host.Desktop` (PDV 1 e PDV 2, cada um com seu próprio banco), `Usuario`
é um bom candidato ao balde "CRDT são" do ADR-0001 — especificamente **LWW-register por campo**
(mesma categoria de "config/preferências"): dois admins editando o papel da mesma pessoa em dois
terminais ao mesmo tempo é um conflito de configuração, não uma invariante financeira/fiscal que
quebra a lei se convergir errado. Não implementamos isso agora porque não existe hoje mais de um
nó por loja — é decisão adiada por ausência de problema, não esquecida.

## Consequências

- **(+)** Zero regressão de UX: o teclado de PIN continua sendo a única tela de login; quem já
  sabe usar o sistema não percebe a mudança, só ganha "cada PIN agora é de uma pessoa".
- **(+)** Revogação de acesso é imediata (papel/overrides/status lidos frescos por request),
  não presa a um TTL de sessão de até 12h.
- **(+)** `lib/permissions.ts` sai deste ADR com ZERO linhas de lógica alteradas — só o corpo de
  `useSessaoPermissoes` (que já era documentado como "seam pro backend") e o `USUARIO_SESSAO_MOCK`
  saem; toda a álgebra de papel+overrides→permissão efetiva, todo o RBAC, continua sendo o mesmo
  código já revisado e em produção na tela de Configurações.
- **(+)** `PinHasher`/`SessionStore` (rate-limit) sobrevivem quase intactos — o primeiro só muda
  de endereço (Bridge → Identidade.Domain), o segundo nem isso na parte de lockout.
- **(+)** Resolve o problema real de "esqueci a senha" num sistema sem rede (PIN de recuperação
  de uso único, curto TTL) sem inventar um fluxo de e-mail que não faz sentido local-first.
- **(−)** Verificar PIN é O(usuários ativos) chamadas PBKDF2 — aceitável na escala de uma loja
  (dezenas de funcionários), precisaria de paralelização se um dia crescer muito além disso.
- **(−)** Duplicação deliberada (mas mínima) de duas regras de hierarquia (`PodeAdministrarUsuarios`/
  `EhIntocavel`) entre TS e C# — necessária porque só o servidor pode de fato IMPEDIR uma ação
  maliciosa, não só escondê-la na UI.
- **(−)** Autorização fina por módulo/ação continua só no front nesta fatia (decisão #4) — um
  cliente que fale HTTP direto (não a SPA) ainda só é barrado por papel bruto (`admin`+ para
  gerenciar usuários), não pelo grid completo de permissões. Nomeado como gap explícito, não
  novo: é a MESMA lacuna que `lib/permissions.ts` já documenta hoje para Sidebar/Dashboard.

## Estado atual no repo

Nada do código acima existe ainda — este documento é a espec que outro processo (`.NET`,
`Bridge/`, módulos) implementa a partir daqui; este ADR só descreve o desenho, não o entrega.
Hoje, para registro do ponto de partida exato:

- `src/Hosts/SistemaX.Host.Desktop/Bridge/{HostConfig,PinHasher,SessionStore,
  BearerSessionMiddleware,BridgeEndpoints}.cs` implementam o PIN único por instalação descrito em
  `docs/arquitetura/bridge-http-local.md` — `papel: "gerente"` hardcoded no login, sem
  `IUsuarioRepository`, sem módulo `Identidade`.
- `web/src/lib/permissions.ts` tem o RBAC completo (papéis, módulos, ações, permissões,
  overrides, `useSessaoPermissoes`) rodando 100% sobre `USUARIO_SESSAO_MOCK` — nenhuma linha toca
  rede.
- `web/src/lib/auth.tsx` + `web/src/components/layout/AuthGate.tsx` + `web/src/pages/Login.tsx`
  já implementam o ciclo de sessão Bearer real (boot-token → PIN → token → `AuthGate` bloqueando
  `<App/>` sem sessão) — é o seam que a decisão #5 estende, não recria.
- `web/src/components/configuracoes/{useConfiguracoes.ts,UsuariosTable.tsx,UsuarioFormModal.tsx,
  PermissoesGrid.tsx}` + `web/src/mocks/configuracoes.ts` implementam a tela "Usuários &
  Permissões" inteira sobre dados mockados locais — o próprio `mocks/configuracoes.ts` já tem o
  comentário `// MOCK — trocar por API quando o backend de Configurações/RBAC existir`, exatamente
  o que este ADR entrega o desenho para fazer.
- `docs/persistencia/persistencia-sqlite.md` documenta o molde de módulo+SQLite (`Reconstituir`,
  `SqlModuleSchemaMigration`, `ExecutarAsync`/`ConsultarAsync`, contract tests InMemory+Sqlite)
  que o módulo `Identidade` desta decisão segue à risca, como `Compras`/`Fiscal` já seguiram.
