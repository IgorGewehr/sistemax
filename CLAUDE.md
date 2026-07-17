# SistemaX — Guia para IA e humanos

> **Leia tudo antes de modificar código.**
> Este é o documento de governança — regras duras que valem para todo PR, todo módulo, todo
> vertical, toda IA. A "constituição" completa (o porquê de cada decisão estrutural, diagramas,
> mapa de pastas) está em `docs/arquitetura/ARCHITECTURE.md`. Guias passo a passo para criar
> módulo/vertical novo estão em `docs/arquitetura/COMO-CRIAR-UM-MODULO.md` e
> `docs/arquitetura/COMO-CRIAR-UM-VERTICAL.md`. Modelo de dados do Financeiro em
> `docs/financeiro/`. Lições de crash-safety/sync/hardware em `docs/robustez/`.

---

## 1. Regras duras (não-negociáveis)

Estas regras se aplicam a **todo** PR, toda feature, toda IA. Se uma delas parecer impedir uma
entrega rápida, o problema é o design da feature, não a regra.

### R1 — Dinheiro é sempre `Money` (centavos-inteiros)

Todo valor monetário — preço, custo, saldo, total, imposto, comissão — é `SistemaX.SharedKernel.Money`.
Por baixo é `long Centavos`. **Nunca `decimal`/`double`/`float` cru em Domain**, mesmo que
pareça "só uma exibição" ou "só uma soma simples". Ponto flutuante é inaceitável num sistema cujo
coração é o financeiro.

```csharp
// ✅ CORRETO
public Money PrecoUnitario { get; }
public Money Total => Itens.Aggregate(Money.Zero, (acc, i) => acc + i.Subtotal);

// ❌ ERRADO
public decimal PrecoUnitario { get; }   // arredondamento de ponto flutuante = bug de dinheiro
```

Conversão de/para reais só em `Money.DeReais()` / `Money.Formatado()` — nunca `(decimal)centavos / 100`
espalhado pelo código.

### R2 — Domain nunca depende de infraestrutura

`Modules.{X}.Domain` e `Verticals.{Y}` (camada Domain) só referenciam `SharedKernel` e
`Modules.Abstractions`. Nunca `Microsoft.Data.Sqlite`, `Microsoft.EntityFrameworkCore`,
`System.Net.Http`, drivers de hardware, ou qualquer pacote de I/O. Se um agregado "precisa" de
uma dessas, a lógica não é Domain — é Application ou Infrastructure vazando pra dentro. Exceção
aceitável: bibliotecas de valor puro sem I/O (ex.: `Ulid` para gerar Id).

```xml
<!-- ✅ CORRETO — Domain só isso -->
<ProjectReference Include="...\SharedKernel\SistemaX.SharedKernel.csproj" />
<ProjectReference Include="...\Modules.Abstractions\SistemaX.Modules.Abstractions.csproj" />
<PackageReference Include="Ulid" Version="1.3.4" />

<!-- ❌ ERRADO num .csproj de Domain -->
<PackageReference Include="Microsoft.Data.Sqlite" Version="..." />
```

### R3 — Financeiro só é alimentado por eventos de integração idempotentes

Nenhum módulo escreve direto em `ContaAReceber`/`ContaAPagar`/`MovimentoFinanceiro`/
`LancamentoContabil` (ver `docs/financeiro/financeiro-datamodel.md`). O único canal de entrada é
publicar um `IIntegrationEvent` catalogado em `Modules.Abstractions/IntegrationEvents.cs`,
**sempre depois do commit** da transação de origem — nunca antes, nunca no meio dela. Todo
evento carrega `ChaveIdempotencia` **derivada do id do fato de origem, nunca de timestamp**
(lição do bug de granularidade de 1s documentado em `docs/robustez/robustez-hardware-licoes.md`
§3). Reprocessar o mesmo evento é NO-OP no assinante — nunca duplica lançamento.

```csharp
// ❌ ERRADO — módulo de origem escrevendo direto no Financeiro
await contaAReceberRepositorio.Criar(new ContaAReceber(...));

// ✅ CORRETO — módulo de origem só emite o fato; Financeiro decide o que fazer com ele
Raise(new VendaConcluidaDomainEvent(Id, TenantId, Total, formaPagamento));
// ...Application, após commit: bus.PublishAsync(domainEvent.ParaEventoDeIntegracao());
```

### R4 — Status sem FSM é proibido

Toda entidade com campo de estado usa `enum` + mapa explícito de transições permitidas, validado
por `Fsm<TStatus>.ValidarTransicao()` (`Modules.Abstractions/Fsm.cs`) **antes** de qualquer
mutação de `Status`. Nunca `entidade.Status = X` fora de um método que passou por essa checagem.
Transição inválida retorna `Result.Falhar(...)` (regra de negócio esperada), nunca lança exceção.

```csharp
// ❌ ERRADO
venda.Status = StatusVenda.Concluida;  // pula a checagem — pode ir de Estornada pra Concluida

// ✅ CORRETO
var transicao = Fsm<StatusVenda>.ValidarTransicao(Status, StatusVenda.Concluida, TransicoesPermitidas);
if (transicao.Falha) return transicao;
Status = StatusVenda.Concluida;
```

### R5 — Módulo/vertical se pluga via `IModule` — proibido `if (vertical == ...)`

O Core (Hosts, Infrastructure) nunca conhece um módulo concreto. Ele enumera `IModule` via
`ModuleRegistry` e fala só com o contrato. Habilitar um vertical numa instalação = adicionar seu
`IModule` à lista passada ao registry. Desabilitar = não adicionar — um módulo não registrado não
carrega absolutamente nada.

```csharp
// ❌ PROIBIDO em qualquer lugar do Core
if (vertical == "posto") { /* ... */ }
switch (moduloAtivo) { case "assistencia": ...; }

// ✅ CORRETO
new ModuleRegistry().Adicionar(new FinanceiroModule()).Adicionar(new AssistenciaModule())
    .RegistrarTodos(services, contexto);
```

### R6 — Identidade de negócio é ULID, nunca timestamp/autoincremento

Todo `Id` de agregado é `Ulid.NewUlid().ToString()` — ordenável por criação, gerado no terminal
sem depender do servidor (essencial para operar offline no PDV), sem colisão entre terminais.
`Guid.NewGuid()` puro continua OK para `EventId` interno (não é chave de negócio nem de
idempotência); `ChaveIdempotencia` de evento de integração é sempre derivada do `Id` ULID do fato
de origem.

### R7 — Todo módulo pensa nas 3 camadas de execução

`IModule.Registrar(services, contexto)` recebe `IModuleContext.Camada`
(`Pdv`/`ServidorDeLoja`/`Nuvem`). Um módulo pode (e frequentemente deve) registrar coisas
diferentes por camada — nunca assuma que seu módulo só roda num lugar só. Ver
`docs/arquitetura/ARCHITECTURE.md` §2 para o porquê da topologia de 3 camadas.

### R8 — Testes de invariante são obrigatórios para dinheiro e FSM

Toda operação sobre `Money` (soma, subtração, conversão de reais, comparação de moeda) tem teste.
Toda FSM tem teste de transição válida E de transição inválida (verificando o `Result.Falha` e o
código de erro, não só que "deu erro"). Ver `tests/SistemaX.Modules.Financeiro.Tests/` para o
padrão (xUnit).

---

## 2. Stack

| Camada | Tecnologia |
|---|---|
| Engine/host (Windows) | **.NET 10** (C#) |
| UI | **React + Tailwind + Framer Motion** em **WebView2** |
| DB local (cada máquina) | **SQLite** (WAL, `synchronous=NORMAL`) |
| Servidor de loja (LAN) | .NET — `Store.Server` |
| Nuvem | **ASP.NET Core + PostgreSQL** — `Cloud.Api` |
| Sync | outbox transacional + idempotência ULID + cursor monotônico + conflito por-entidade |
| Testes | xUnit |

Comandos: `dotnet restore`, `dotnet build`, `dotnet test`.

---

## 3. Workflow ao codar uma feature

```
1. LER     →  docs/arquitetura/ARCHITECTURE.md (se ainda não internalizou o modelo)
              Modules.Abstractions/IntegrationEvents.cs (o evento que você precisa já existe?)

2. MODELAR →  Agregado em {Modulo}.Domain: AggregateRoot<string> com Id ULID.
              FSM explícita se há Status (Fsm<TStatus> + mapa de transições).
              Evento de domínio privado, com ParaEventoDeIntegracao() se há side-effect
              cross-módulo (ver COMO-CRIAR-UM-MODULO.md / COMO-CRIAR-UM-VERTICAL.md).

3. CÓDIGO  →  Domain sem dependência de infra (R2). Dinheiro sempre Money (R1).
              Se novo módulo/vertical: IModule.Registrar, sem if sobre módulo concreto (R5).

4. TESTE   →  Invariante de Money + de FSM (R8). dotnet build verde nos projetos tocados.

5. DOC     →  Atualizar docs/arquitetura/ARCHITECTURE.md (mapa de pastas, catálogo de eventos)
              se mudou inventário de módulos/eventos.
```

---

## 4. Multi-tenant — `TenantId`

Todo agregado que representa um fato de negócio de uma loja/cliente carrega `TenantId`. Todo
evento de integração carrega `TenantId` (ver `Modules.Abstractions/IntegrationEvents.cs` — todo
record já tem esse campo). Quando a camada de persistência nascer, toda query filtra por
`TenantId` — mesma disciplina do R1 do saas-erp (`businessId`), adaptada ao vocabulário deste
repo: aqui, "tenant" é a loja/instalação, não necessariamente uma empresa multi-filial (isso é
modelado como múltiplos `TenantId` de uma mesma conta na Nuvem — detalhe de `Cloud.Api`, fora do
escopo deste documento).

---

## 5. Mapa de módulos (referência rápida)

Mapa completo e comentado em `docs/arquitetura/ARCHITECTURE.md` §8. Resumo:

```
src/
  SharedKernel/            Money, AggregateRoot, DomainEvent, Result — primitivos puros
  Modules/
    Abstractions/          IModule, IIntegrationEvent(+Handler+Bus), ModuleRegistry, Fsm<T>
    Financeiro/             ❤️ o coração — Domain/Application/Infrastructure
    Vendas/                 módulo core — exemplo EMISSOR (Venda → VendaConcluida)
  Verticals/
    Assistencia/            1º vertical/MVP — exemplo de PLUGAGEM (OrdemDeServico → OsFaturada)
  Infrastructure/
    Local/                  SQLite, transação atômica, backup/recovery
    Sync/                   motor de sync 3 camadas, outbox, conflito por-entidade
    Hardware/               impressora, balança, TEF, gaveta, scanner
  Hosts/
    Host.Desktop/           composition root do PDV (WebView2)
    Store.Server/           servidor de loja (LAN)
    Cloud.Api/              ASP.NET Core + Postgres
web/                        app React (UI)
tests/                      xUnit
docs/
  arquitetura/              este + ARCHITECTURE.md + guias de módulo/vertical
  financeiro/               modelo de dados, features, UX do coração financeiro
  robustez/                 lições de crash-safety/durabilidade/sync/hardware
```

---

## 6. Convenções de código

### C#
- `Nullable` + `ImplicitUsings` habilitados em todo projeto (`Directory.Build.props`).
- Sem `dynamic`, sem `object` cru no boundary de domínio — tipos fortes, `record` para objeto de
  valor e evento, `class` para agregado/entidade com identidade.
- Nomes de tipo/membro em **português** (`Venda`, `Concluir`, `TransicoesPermitidas`) — o
  domínio de negócio deste projeto é PT-BR; mantenha consistência com o que já existe
  (`Money`, `Result`, `IModule` são os únicos termos técnicos em inglês, porque são conceitos de
  infraestrutura de software, não de domínio de negócio).
- `Result`/`Result<T>` para erro de regra de negócio esperada; exceção só para o que o tipo não
  conseguiu impedir em compilação (bug de programação, concorrência, falha de infra).

### Domain
- Construtor privado + factory estático (`Abrir`, `Criar`) em todo agregado — nunca deixe um
  `new Agregado()` com campos obrigatórios vazios escapar do módulo.
- Toda coleção filha de agregado é exposta como `IReadOnlyList<T>`, nunca `List<T>` mutável.
- Totais/somas são propriedades computadas a partir da coleção-fonte, nunca campo cacheado.

### Eventos
- Evento de domínio: `record : DomainEvent`, privado ao módulo, nome no passado
  (`VendaConcluidaDomainEvent`).
- Evento de integração: `record : IIntegrationEvent`, catalogado em
  `Modules.Abstractions/IntegrationEvents.cs`, nome no passado sem sufixo (`VendaConcluida`).
- Tradução domínio→integração é um método puro no próprio evento de domínio
  (`ParaEventoDeIntegracao()`) — nunca lógica de mapeamento espalhada em vários lugares.

---

## 7. Erros comuns — não faça

```csharp
// ❌ Dinheiro fora de Money
public decimal Total { get; set; }

// ❌ Domain referenciando infraestrutura
// (num .csproj de Modules.X.Domain)
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" ... />

// ❌ Escrever direto numa entidade financeira a partir de outro módulo
await _db.ContasAReceber.AddAsync(new ContaAReceber(...));
// → publique VendaConcluida; deixe o Financeiro decidir o que criar

// ❌ Mudar status sem checar FSM
venda.Status = StatusVenda.Concluida;
// → passe por Fsm<StatusVenda>.ValidarTransicao antes

// ❌ if/switch sobre módulo concreto no Core
if (contexto.VerticalAtivo == "posto") { ConfigurarPosto(); }
// → IModule.Registrar cuida disso; Core só enumera IModule

// ❌ Id sequencial/autoincremento ou baseado em timestamp
public int Id { get; set; }  // ou: $"{DateTime.UtcNow.Ticks}"
// → Ulid.NewUlid().ToString()

// ❌ Publicar evento de integração antes do commit
Raise(new VendaConcluidaDomainEvent(...));
await bus.PublishAsync(evento);   // e SÓ DEPOIS persistir a Venda — ordem errada
// → persista primeiro, publique depois do commit confirmado

// ❌ ChaveIdempotencia derivada de timestamp
public string ChaveIdempotencia => $"venda.concluida:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
// → derive do Id do fato de origem: $"venda.concluida:{VendaId}"
```

---

## 8. Quando atualizar este documento

- Mudou regra dura (R1–R8) → reescreva a regra, não adicione exceção.
- Adicionou módulo/vertical novo → atualize o mapa em §5 **e**
  `docs/arquitetura/ARCHITECTURE.md` §1.1/§8.
- Encontrou erro comum recorrente em PR/review → adicione exemplo em §7.
- Mudou stack/dependência transversal → §2.

Este arquivo é **enxuto por design**. Diagramas, o porquê de cada decisão estrutural e o mapa
de pastas comentado vivem em `docs/arquitetura/ARCHITECTURE.md`. Se você se pegou escrevendo
descrição de implementação aqui, está no lugar errado.

---

## 9. Documentos relacionados

| Arquivo | Conteúdo |
|---|---|
| `docs/arquitetura/ARCHITECTURE.md` | A constituição: Core+Verticais, topologia 3 camadas, `IModule`, fluxo financeiro por eventos, regras de camada, stack, mapa de pastas |
| `docs/arquitetura/COMO-CRIAR-UM-MODULO.md` | Passo a passo pra módulo core novo (exemplo: Vendas) |
| `docs/arquitetura/COMO-CRIAR-UM-VERTICAL.md` | Passo a passo pra vertical novo (exemplo: Assistência) |
| `docs/financeiro/financeiro-datamodel.md` | Modelo de dados do coração financeiro, catálogo de eventos, regime caixa vs competência |
| `docs/robustez/robustez-hardware-licoes.md` | Crash-safety, durabilidade SQLite, sync 3 camadas, drivers de hardware |
| `README.md` | Visão geral de 1 página, comandos de build |
