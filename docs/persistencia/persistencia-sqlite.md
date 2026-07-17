# Persistência SQLite — schema migrations + molde de repositório (F0)

> Entregue na F0 (fundação). Objetivo: qualquer dev/IA que for portar um dos 13 ports in-memory
> restantes na F1 consegue copiar o padrão daqui sem reabrir decisão nenhuma. Decisões de fundo
> (por que ADO.NET cru, por que um banco só, por que `Reconstituir`) estão em
> `docs/design/sistemax-production-plano.md` §4 — este documento é só o "como", com o código real
> já rodando como referência.

## 1. O que existe agora

- **`SistemaX.Infrastructure.Local/Migrations/`**
  - `IModuleSchemaMigration` — contrato que um módulo implementa para declarar uma migração de
    schema (`Modulo`, `Versao`, `Checksum`, `AplicarAsync`).
  - `SqlModuleSchemaMigration` — base abstrata para o caso comum (DDL puro). Só declare
    `Modulo`, `Versao` e `Sql`; `AplicarAsync`/`Checksum` vêm de graça.
  - `SchemaMigrationRunner` — aplica todas as migrações pendentes no boot: cria/lê
    `schema_migrations` (módulo, versão, quando, checksum), calcula o conjunto pendente por
    módulo (preservando a ordem topológica de registro do `ModuleRegistry`), tira **backup antes**
    via `IBackupManager` se há algo pendente, aplica cada migração numa transação própria e
    recusa **downgrade** (versão persistida maior que a maior declarada no código = erro fatal
    no boot).
  - `LocalInfraSchemaMigration` — migração v1 do módulo `"local"`: absorve o DDL que antes vivia
    solto em `LocalSchemaMigrator` (outbox, sequências, kv, log de crash-recovery). Registrada
    automaticamente por `AddSistemaXLocalInfrastructure()` — nenhum host precisa registrá-la.
  - `LocalSchemaMigrator` continua existindo só como o caminho de EMERGÊNCIA que
    `CorruptionRecoveryService` chama direto após um fail-open (precisa do schema mínimo de volta
    imediatamente, antes do runner rodar de novo). Fonte do DDL é a mesma (`LocalSchemaMigrator.Ddl`)
    — nunca duas cópias do mesmo SQL.
- **`SistemaX.Infrastructure.Local/UnitOfWork/ILocalSessao`** (+ `LocalSessao`) — a unidade de
  trabalho AMBIENTE (scoped) que um caso de uso inicia (`IniciarAsync`) e que os repositórios
  SQLite consultam (`Atual`) para decidir se participam da transação em andamento ou abrem
  conexão própria. Registrada `AddScoped` por `AddSistemaXLocalInfrastructure()`.
- **`ComprasSchemaMigrationV1`** + **`SqliteFornecedorRepository`**
  (`SistemaX.Modules.Compras.Infrastructure/Sqlite/`) — o repositório-molde: primeiro dos 14
  ports do sistema a ser persistido em SQLite seguindo o padrão novo (o `SqliteAssinaturaRepository`
  antigo é o precursor, mas ainda cria schema no construtor — não copie isso).
- **`Fornecedor.Reconstituir(...)`** (`SistemaX.Modules.Compras.Domain`) — reidratação sem regra
  de negócio nem evento, mesmo padrão de `Assinatura.Reconstituir`.
- **Contract tests** em `tests/SistemaX.Modules.Compras.Tests/Contracts/`:
  `FornecedorRepositoryContractTests` (abstrata) rodando 2× —
  `InMemoryFornecedorRepositoryContractTests` e `SqliteFornecedorRepositoryContractTests`.

## 2. Como portar o PRÓXIMO port in-memory (molde de 6 passos)

Use `Fornecedor`/`SqliteFornecedorRepository` como referência lado a lado.

1. **Agregado ganha `Reconstituir`** — um `static` que aceita o estado completo (incluindo
   filhos, se houver) e monta o objeto via o construtor privado existente, SEM validar nem
   chamar `Raise(...)`. Se o agregado tem filhos mutáveis (parcelas, itens), `Reconstituir`
   recebe as listas prontas.
2. **Migração** — uma classe `{Modulo}SchemaMigrationV{n}` herdando `SqlModuleSchemaMigration`
   no namespace `{Modulo}.Infrastructure.Sqlite`. `Versao` é a PRÓXIMA versão livre do módulo
   (`ComprasSchemaMigrationV1` já é a v1 de `"compras"` — o próximo port de Compras nasce v2).
   DDL idempotente (`CREATE TABLE/INDEX IF NOT EXISTS`), nomes de coluna `snake_case`, `id TEXT
   PRIMARY KEY` (ULID), FK `ON DELETE CASCADE` só pai→filho do MESMO agregado.
3. **Repositório** — implementa o port existente (`I{Agregado}Repository`), construtor recebe
   `ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao`. TODA operação passa
   por `ExecutarAsync`/`ConsultarAsync<T>` (copie os dois métodos privados de
   `SqliteFornecedorRepository` literalmente — são o "cola" entre sessão ambiente e conexão
   curta). Escrita = `INSERT ... ON CONFLICT(id) DO UPDATE SET <campos mutáveis>`; filhos mutáveis
   = `DELETE WHERE pai_id = @id` + reinsert dentro da mesma operação; filhos imutáveis por
   invariante (razão contábil, histórico) = INSERT only.
4. **Módulo de Infrastructure** — no `{Modulo}InfrastructureModule.Registrar(...)`, adicione o
   branch `contexto.Configuracao["persistencia"] == "sqlite"` (copie o padrão de
   `ComprasInfrastructureModule`): `AddScoped<I..., Sqlite...>()` +
   `services.AddModuleSchemaMigration<{Migration}>()` no branch sqlite; mantém
   `AddSingleton<..., InMemory...>()` no branch default. **Nunca** registre o repositório Sqlite
   como `Singleton` — ele depende de `ILocalSessao`, que é `Scoped` (dependência cativa).
5. **Contract test** — copie `FornecedorRepositoryContractTests` (a classe ABSTRATA) trocando o
   port/agregado; os NOMES dos métodos de teste documentam o contrato, não a implementação.
   Duas subclasses: `InMemory...ContractTests` (`CriarRepositorio()` → `new InMemory...()`) e
   `Sqlite...ContractTests` (arquivo temp por teste — **nunca** `:memory:` puro, que abre um
   banco novo a cada conexão; use um caminho em `Path.GetTempPath()` e aplique a migração direto,
   como em `SqliteFornecedorRepositoryContractTests`).
6. **Rode os dois** — `dotnet test` deve mostrar o dobro de casos (InMemory + Sqlite) passando
   idênticos. Só então o port está "pronto para SQLite" pela definição deste projeto.

## 3. Coisas que NÃO fazem parte do molde (faça diferente de propósito)

- **Não** crie a tabela no construtor do repositório (era o padrão do
  `SqliteAssinaturaRepository` original — a F1 deve migrar esse repositório para o novo padrão
  também, ele não é mais o exemplo a copiar).
- **Não** valide regra de negócio dentro de `Reconstituir` — é reidratação, não fato novo (R6).
- **Não** publique evento de integração dentro do repositório — quem publica é o caso de uso,
  DEPOIS do `sessao.CommitAsync()` (R3/R5). `ILocalSessao.EnqueueOutboxAsync` existe para o caso
  de uso enfileirar o evento de sync na MESMA transação antes do commit.
- **Não** registre o repositório Sqlite como `Singleton`.

## 4. O que fica para a F1 (não incluído nesta fase)

- Portar os outros 12 ports (Financeiro: 6 incluindo o próprio `Assinatura`; Vendas: 1;
  Estoque: 3; Compras: 2 restantes) para dentro do MESMO `sistemax.db` — hoje só `fornecedores`
  vive lá; `assinaturas` continua no arquivo separado `sistemax-financeiro.db` do demo antigo.
- `OutboxDispatcherService` (drena `outbox_messages` e publica no bus — fecha o buraco
  at-least-once do `InProcessIntegrationEventBus`, hoje só documentado nele).
- Validar o `checksum` gravado contra o da migração no código (hoje só se grava; detectar edição
  indevida de uma migração já aplicada é próximo passo).
- Generic Host + Serilog + `config.json` por instalação no `Host.Desktop` (hoje o demo ainda é
  script top-level; a chamada a `LocalDatabaseBootstrapper.BootstrapAsync()` já está no lugar
  certo para sobreviver a essa migração).
