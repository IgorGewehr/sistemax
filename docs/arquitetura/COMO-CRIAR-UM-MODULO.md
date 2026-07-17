# Como criar um módulo core

> Pré-requisito: leia `docs/arquitetura/ARCHITECTURE.md` inteiro primeiro, principalmente §3
> (`IModule`), §5 (evento de domínio vs integração) e §6 (regras de camada). Este guia assume
> que você já entende o "porquê"; aqui é só o "como", passo a passo, usando **Vendas** como
> exemplo trabalhado — todo passo aponta pro arquivo real em
> `src/Modules/Vendas/SistemaX.Modules.Vendas.Domain/`.

Módulo core = capacidade que praticamente todo negócio usa (Vendas, Estoque, Compras, Clientes...).
Se o que você está construindo só existe para um segmento específico de negócio, é um
**vertical**, não um módulo — vá para `docs/arquitetura/COMO-CRIAR-UM-VERTICAL.md` em vez deste.

---

## Passo 0 — decida o código e o nome

`Codigo` é o identificador estável e único do módulo (ex.: `"vendas"`) — usado em
`IModule.Codigo`, em `DependeDe` de outros módulos, e em qualquer configuração de instalação
("quais módulos este cliente tem habilitado"). Uma vez publicado, **não muda** — trate como uma
chave pública.

## Passo 1 — crie o(s) projeto(s)

Estrutura mínima (o que existe hoje para Vendas):

```
src/Modules/{Nome}/
  SistemaX.Modules.{Nome}.Domain/         obrigatório desde o dia 1
  SistemaX.Modules.{Nome}.Application/    quando houver orquestração real (casos de uso, publicação de evento)
  SistemaX.Modules.{Nome}.Infrastructure/ quando houver persistência/I/O real
```

`SistemaX.Modules.Vendas.Domain.csproj` (referência real, copie o padrão):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\..\SharedKernel\SistemaX.SharedKernel\SistemaX.SharedKernel.csproj" />
    <ProjectReference Include="..\..\Abstractions\SistemaX.Modules.Abstractions\SistemaX.Modules.Abstractions.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Ulid" Version="1.3.4" />
  </ItemGroup>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

Note o que **não** está aqui: nenhuma referência a `Microsoft.Data.Sqlite`, `Microsoft.EntityFrameworkCore`,
`System.Net.Http`, ou qualquer coisa de I/O. `Domain` só referencia `SharedKernel` +
`Modules.Abstractions` (ver ARCHITECTURE.md §6). `Ulid` é aceitável porque é uma função pura de
geração de valor, sem I/O.

Adicione o(s) projeto(s) ao `SistemaX.slnx` na pasta `/src/Modules/{Nome}/`.

## Passo 2 — modele o agregado

Um agregado é uma classe que herda `AggregateRoot<TId>` (SharedKernel). `TId` é `string`
contendo um **ULID** — nunca timestamp, nunca autoincremento (regra dura: ULID é ordenável por
criação e gerado no terminal, sem precisar do servidor — essencial pra operar offline no PDV).

```csharp
// Venda.cs — trecho real, veja o arquivo completo
public sealed class Venda : AggregateRoot<string>
{
    private readonly List<ItemDeVenda> _itens = new();

    public string TenantId { get; private set; } = string.Empty;
    public StatusVenda Status { get; private set; }
    public IReadOnlyList<ItemDeVenda> Itens => _itens.AsReadOnly();
    public Money Total => _itens.Aggregate(Money.Zero, static (acc, i) => acc + i.Subtotal);

    private Venda() { }  // reidratação — repositório usa isto; código de app usa Abrir()

    public static Venda Abrir(string tenantId) => new()
    {
        Id = Ulid.NewUlid().ToString(),
        TenantId = tenantId,
        Status = StatusVenda.Aberta
    };
}
```

Pontos que valem a pena copiar do exemplo:
- **Construtor privado + factory estático (`Abrir`)** — força todo mundo a passar pelas
  invariantes de criação, nunca deixa um `new Venda()` com campos obrigatórios vazios escapar.
- **Dinheiro é sempre `Money`** (`ItemDeVenda.PrecoUnitario`, `Venda.Total`) — nunca `decimal`
  cru num agregado, mesmo que pareça "só uma exibição".
- **Totais são recalculados, nunca cacheados num campo** — `Total` é uma propriedade computada
  a partir de `_itens`, não um campo que alguém pode esquecer de atualizar.

## Passo 3 — modele a FSM (se o agregado tem `Status`)

Toda entidade com estados usa `Fsm<TStatus>` (`Modules.Abstractions/Fsm.cs`) — nunca
`entidade.Status = X` livre em qualquer método.

```csharp
private static Result ValidarOuTransicionar(StatusVenda de, StatusVenda para) =>
    Fsm<StatusVenda>.ValidarTransicao(de, para, TransicoesPermitidas);

private static readonly IReadOnlyDictionary<StatusVenda, StatusVenda[]> TransicoesPermitidas =
    new Dictionary<StatusVenda, StatusVenda[]>
    {
        [StatusVenda.Aberta] = [StatusVenda.Concluida],
        [StatusVenda.Concluida] = [StatusVenda.Estornada],
        [StatusVenda.Estornada] = []
    };
```

Cada método público que muda de estado (`Concluir`, `Estornar`) chama
`Fsm<StatusVenda>.ValidarTransicao` **antes** de qualquer outra validação de negócio, e retorna
o `Result` de falha imediatamente se a transição não é permitida. Veja `Venda.Concluir()` e
`Venda.Estornar()` no arquivo real para o padrão completo — inclusive como combinar a checagem de
FSM com outras invariantes (ex.: "não pode concluir sem itens").

`Fsm<TStatus>.ValidarTransicao` retorna `Result`, nunca lança exceção — transição inválida é
regra de negócio esperada (usuário/robô tentou uma ação fora de ordem), não bug. Reserve exceção
para o que o tipo não conseguiu impedir em compilação (ex.: `ArgumentException` no
`Venda.Abrir()` se `tenantId` vier vazio — isso é erro de programação do caller, não fluxo de
negócio).

## Passo 4 — declare o evento de domínio

O evento de domínio é **privado ao módulo** — só ele sabe que esse tipo existe. Ele carrega os
dados que, mais tarde, vão virar o evento de integração.

```csharp
public sealed record VendaConcluidaDomainEvent(
    string VendaId, string TenantId, Money Total, string FormaPagamento) : DomainEvent
{
    public VendaConcluida ParaEventoDeIntegracao() => new(
        VendaId: VendaId, TenantId: TenantId,
        TotalCentavos: Total.Centavos, FormaPagamento: FormaPagamento,
        OcorridoEm: OccurredOn);
}
```

`VendaConcluida` (o tipo de retorno) já existe em `Modules.Abstractions/IntegrationEvents.cs` —
**primeiro confira se o evento de integração que você precisa já está catalogado lá.** Se não
estiver, adicione (é um novo `record : IIntegrationEvent` com `ChaveIdempotencia` derivada do id
do fato de origem — nunca de timestamp, ver ARCHITECTURE.md §4.3) e coordene com quem for
implementar o handler no Financeiro, já que é ele quem vai consumir.

No método do agregado que causa a mudança relevante, chame `Raise(...)` — isso só ACUMULA o
evento em `AggregateRoot.DomainEvents`, não publica nada:

```csharp
public Result Concluir(string formaPagamento)
{
    var transicao = Fsm<StatusVenda>.ValidarTransicao(Status, StatusVenda.Concluida, TransicoesPermitidas);
    if (transicao.Falha) return transicao;
    // ...outras validações...

    Status = StatusVenda.Concluida;
    FormaPagamento = formaPagamento;
    Raise(new VendaConcluidaDomainEvent(Id, TenantId, Total, formaPagamento));
    return Result.Ok();
}
```

## Passo 5 — Application publica DEPOIS do commit (quando essa camada existir)

Este esqueleto de Vendas só tem `Domain` (ver a partição do repo em que ele foi escrito) — mas o
contrato de responsabilidade já está fixado nos comentários de `VendaDomainEvents.cs`. Quando
`Vendas.Application` nascer, o fluxo é:

1. Application chama `venda.Concluir(formaPagamento)`.
2. Application persiste a `Venda` (repositório da Infrastructure) — **commit da transação
   local**.
3. **Só depois do commit confirmado**, Application itera `venda.DomainEvents`, chama
   `evt.ParaEventoDeIntegracao()` em cada um, e publica via `IIntegrationEventBus.PublishAsync`.
4. Application chama `venda.ClearDomainEvents()`.

Nunca publique de dentro do método do agregado, nunca publique antes do commit confirmado — ver
ARCHITECTURE.md §4.2 para o diagrama de sequência completo e o porquê.

## Passo 6 — implemente `IModule` (quando o módulo tiver o que registrar)

Um módulo core só precisa de `IModule` quando há algo real para registrar no container
(handlers, repositório, endpoints). Padrão mínimo:

```csharp
public sealed class VendasModule : IModule
{
    public string Codigo => "vendas";
    public string Nome => "Vendas";
    public IReadOnlyCollection<string> DependeDe => [];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        services.AddScoped<IVendaRepositorio, VendaRepositorioSqlite>();
        services.AddScoped<IIntegrationEventHandler<...>, ...>(); // se Vendas também ASSINA algo
        // registro condicional por camada, se aplicável:
        if (contexto.Camada == CamadaExecucao.Pdv) { /* ... */ }
    }
}
```

Veja `AssistenciaModule` (`src/Verticals/Assistencia/SistemaX.Verticals.Assistencia/AssistenciaModule.cs`)
para o exemplo completo comentado — o mecanismo é idêntico para módulo core ou vertical.

## Passo 7 — registre no host

O host (dono de outra partição — não mexa lá direto, mas é bom saber o que vai acontecer)
adiciona seu módulo ao `ModuleRegistry`:

```csharp
new ModuleRegistry()
    .Adicionar(new FinanceiroModule())
    .Adicionar(new VendasModule())
    .RegistrarTodos(services, contexto);
```

Se `VendasModule.DependeDe` listar algo que não foi adicionado, `RegistrarTodos` lança na hora —
falha de boot, não bug silencioso depois.

## Passo 8 — escreva os testes de invariante

Regra dura do projeto: toda operação que envolve `Money` tem teste (arredondamento, soma,
subtração, comparação de moeda). Toda transição de FSM tem teste de "transição válida passa" e
"transição inválida retorna `Result.Falha` com o código de erro certo" — nunca só o caminho
feliz. Ver `tests/SistemaX.Modules.Financeiro.Tests/` para o padrão do projeto (xUnit).

## Passo 9 — atualize a documentação

- Adicione uma linha na tabela de módulos em `ARCHITECTURE.md` §1.1 (se for núcleo) e no mapa de
  pastas §8.
- Se o módulo emitir evento de integração novo, adicione ao catálogo em
  `Modules.Abstractions/IntegrationEvents.cs` **e** na tabela de `ARCHITECTURE.md` §4.3.
- Se o módulo mexe com dinheiro de alguma forma nova, confira se `docs/financeiro/financeiro-datamodel.md`
  já cobre o caso — se não, é conversa com quem é dono do Financeiro antes de inventar um novo
  fato financeiro.

---

## Checklist rápido

- [ ] `Codigo` decidido e estável.
- [ ] Projeto(s) criado(s), `.csproj` só referencia `SharedKernel` + `Modules.Abstractions` (+ `Ulid` se precisar de Id) na camada Domain.
- [ ] Agregado herda `AggregateRoot<string>` com Id ULID, construtor privado + factory estático.
- [ ] Dinheiro sempre `Money`, nunca `decimal`/`double` cru.
- [ ] `Status` (se houver) só muda via `Fsm<TStatus>.ValidarTransicao` contra um mapa explícito.
- [ ] Evento de domínio privado ao módulo, com `ParaEventoDeIntegracao()` se há side-effect cross-módulo.
- [ ] Evento de integração catalogado em `Modules.Abstractions/IntegrationEvents.cs` com `ChaveIdempotencia` derivada do id do fato (nunca timestamp).
- [ ] `IModule` implementado (quando houver o que registrar), sem `if` sobre outro módulo concreto.
- [ ] Testes de invariante de dinheiro e de FSM.
- [ ] `dotnet build` verde nos projetos tocados.
- [ ] Docs atualizados (`ARCHITECTURE.md`, catálogo de eventos).
