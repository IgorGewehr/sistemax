# Como criar um vertical

> Pré-requisito: leia `docs/arquitetura/ARCHITECTURE.md` inteiro primeiro (§1 explica a
> distinção módulo core vs vertical; §3 é o contrato `IModule`). Se ainda não leu
> `docs/arquitetura/COMO-CRIAR-UM-MODULO.md`, leia também — este guia assume os mesmos passos de
> modelagem de agregado/FSM/evento e só acrescenta o que é ESPECÍFICO de vertical: a plugagem via
> `IModule` e a disciplina de "nunca vaza pro Core". Exemplo trabalhado: **Assistência Técnica**
> (`src/Verticals/Assistencia/SistemaX.Verticals.Assistencia/`).

---

## O que torna algo um "vertical" (e não um módulo core)

Um vertical modela um **tipo de negócio específico** — só existe pra quem vende aquilo. Um
Mercado não tem Ordem de Serviço de assistência técnica; uma Assistência Técnica não tem PDV de
posto de gasolina com bicos e tanques. Cada um desses é um vertical: **opcional por instalação**,
habilitado só quando aquele cliente contratou aquele segmento.

Tecnicamente, um vertical **não é uma categoria especial** para o Core — é só mais um `IModule`.
A diferença é 100% de produto (o que vem habilitado por padrão) e de localização no repo
(`src/Verticals/{Nome}/` em vez de `src/Modules/{Nome}/`). Se você se pegar precisando de uma
interface ou mecanismo que "só funciona pra vertical", pare — isso é sinal de que a abstração
está no lugar errado. O mesmo `IModule`, o mesmo `ModuleRegistry`, o mesmo par evento de
domínio/evento de integração servem os dois igualmente.

---

## Passo 0 — desenhe o fluxo de negócio como uma FSM

Antes de escrever código, desenhe os estados e as transições em texto simples. Para Assistência,
o fluxo é: **equipamento → defeito → diagnóstico → orçamento → aprovação → peças + mão-de-obra →
faturamento**, com dois desvios possíveis (cliente reprova o orçamento; OS é cancelada em
qualquer ponto antes de faturar). Isso virou `StatusOrdemServico`:

```
Aberta ──diagnóstico──► EmDiagnostico ──orçamento──► AguardandoAprovacao ──aprova──► Aprovada
  │                          │                              │                          │
  └──cancela──► Cancelada ◄──┴──cancela────────────┴──reprova──► Reprovada    cancela──┘
                                                                                        ▼
                                                                                   EmExecucao ──cancela──► Cancelada
                                                                                        │
                                                                                  concluiExecução
                                                                                        ▼
                                                                                    Concluida ──fatura──► Faturada
```

`Reprovada`, `Cancelada` e `Faturada` são terminais — nenhuma transição sai delas. Ver o diagrama
ASCII completo (com nome de método por seta) no XML doc de
`src/Verticals/Assistencia/SistemaX.Verticals.Assistencia/StatusOrdemServico.cs`.

Este passo importa mais para vertical do que para módulo core porque um vertical normalmente
modela um processo de negócio inteiro (não uma entidade isolada) — errar a FSM aqui significa
retrabalho grande depois.

## Passo 1 — crie o projeto em `src/Verticals/{Nome}/`

```
src/Verticals/{Nome}/
  SistemaX.Verticals.{Nome}/            Domain (mesma disciplina de dependência do módulo core:
                                          só SharedKernel + Modules.Abstractions)
    (futuro: .Application/.Infrastructure, mesmo padrão de módulo core)
```

Note que o vertical Assistência não separa `.Domain` no nome do projeto (é só
`SistemaX.Verticals.Assistencia`) — é convenção aceitável para o MVP de um vertical pequeno; se
crescer, pode virar `SistemaX.Verticals.Assistencia.Domain` + `.Application` +
`.Infrastructure`, mesmo padrão de Vendas/Financeiro. `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\..\..\Modules\Abstractions\SistemaX.Modules.Abstractions\SistemaX.Modules.Abstractions.csproj" />
    <ProjectReference Include="..\..\..\SharedKernel\SistemaX.SharedKernel\SistemaX.SharedKernel.csproj" />
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

Adicione ao `SistemaX.slnx` na pasta `/src/Verticals/{Nome}/`.

## Passo 2 — modele objetos de valor e o agregado

Mesma disciplina do módulo core (ver COMO-CRIAR-UM-MODULO.md Passo 2): construtor privado +
factory estático, Id ULID, dinheiro sempre `Money`. Um vertical de processo (como Assistência)
tipicamente acumula estado ao longo de várias etapas — em vez de um construtor gigante, cada
etapa tem seu próprio método que só é chamável no estado certo:

```csharp
public static OrdemDeServico Abrir(string tenantId, Equipamento equipamento, string defeitoRelatado) => new()
{
    Id = Ulid.NewUlid().ToString(),
    TenantId = tenantId,
    Equipamento = equipamento,
    DefeitoRelatado = defeitoRelatado,
    Status = StatusOrdemServico.Aberta
};

public Result RegistrarDiagnostico(string diagnostico) { /* Aberta → EmDiagnostico */ }
public Result EnviarOrcamento(Money maoDeObra) { /* EmDiagnostico → AguardandoAprovacao */ }
public Result Aprovar() { /* AguardandoAprovacao → Aprovada */ }
public Result IniciarExecucao() { /* Aprovada → EmExecucao */ }
public Result AdicionarPeca(...) { /* só quando Status == EmExecucao */ }
public Result ConcluirExecucao() { /* EmExecucao → Concluida */ }
public Result Faturar() { /* Concluida → Faturada — levanta o evento */ }
```

Veja `OrdemDeServico.cs` completo para os detalhes de cada validação (ex.: `AdicionarPeca` não é
uma transição de FSM — é uma operação permitida só **dentro** de um estado específico, checada
por comparação direta de `Status`, não por `Fsm<T>`; use `Fsm<T>` só para mudança de estado, não
para toda regra condicionada a estado).

## Passo 3 — o evento de domínio, exatamente como um módulo core

```csharp
public sealed record OsFaturadaDomainEvent(
    string OrdemServicoId, string TenantId, Money ValorServico, Money ValorPecas) : DomainEvent
{
    public OsFaturada ParaEventoDeIntegracao() => new(
        OrdemServicoId: OrdemServicoId, TenantId: TenantId,
        ValorServicoCentavos: ValorServico.Centavos, ValorPecasCentavos: ValorPecas.Centavos,
        OcorridoEm: OccurredOn);
}
```

`OsFaturada` já está catalogado em `Modules.Abstractions/IntegrationEvents.cs` (o vertical MVP
foi desenhado para reusar um contrato que já existia — na prática, verifique sempre se o evento
que você precisa já está lá antes de criar um novo). O ponto central: **este é o MESMO mecanismo
de tradução domínio→integração que Vendas usa.** Não existe "versão vertical" do padrão.

## Passo 4 — `{Nome}Module : IModule` — o ponto de plugagem

```csharp
public sealed class AssistenciaModule : IModule
{
    public string Codigo => "assistencia";
    public string Nome => "Assistência Técnica";
    public IReadOnlyCollection<string> DependeDe => [];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        // repositório, handlers de comando, handler que traduz e publica o evento de
        // integração após commit, migração de schema local — tudo entra aqui quando
        // Application/Infrastructure existirem.

        if (contexto.Camada == CamadaExecucao.Pdv)
        {
            // registro específico do PDV, ex.: impressão de orçamento/nota de serviço
        }
    }
}
```

Isto é **tudo** que o Core precisa saber sobre o vertical existir. Confira: nenhuma outra parte
do repo (Hosts, Infrastructure) tem uma linha de código que menciona `"assistencia"` ou
`AssistenciaModule` diretamente — só o próprio host, no momento de montar a lista de módulos
habilitados nesta instalação (ver Passo 5).

`DependeDe` normalmente é vazio para um vertical: ele **publica** eventos de integração (que
qualquer assinante, incluindo Financeiro, consome sem o vertical precisar saber que o assinante
existe), mas raramente **chama** outro módulo diretamente. Se você sentir necessidade de listar
`"financeiro"` em `DependeDe` porque seu vertical "precisa" chamar algo do Financeiro
diretamente, pare — isso quase sempre quer dizer que falta um evento de integração novo, não uma
dependência de registro.

## Passo 5 — habilitar/desabilitar por instalação

Um vertical existe pra ser opcional. A decisão de "este cliente comprou Assistência" vira, no
host, simplesmente incluir ou não `new AssistenciaModule()` na lista passada ao
`ModuleRegistry`:

```csharp
var modulosHabilitados = configuracao.GetSection("Modulos").Get<string[]>()!; // ex.: lido de appsettings/DB
var registry = new ModuleRegistry().Adicionar(new FinanceiroModule()).Adicionar(new VendasModule());

if (modulosHabilitados.Contains("assistencia"))
    registry.Adicionar(new AssistenciaModule());

registry.RegistrarTodos(services, contexto);
```

Note que essa checagem `Contains("assistencia")` é a ÚNICA menção ao código do vertical em código
de host — e mesmo essa não é um `if (vertical == "posto") { lógica de posto }`, é só "incluir ou
não na lista". Nenhuma lógica de negócio de Assistência mora fora de
`SistemaX.Verticals.Assistencia`.

## Passo 6 — o resto é igual ao módulo core

Testes de invariante (Passo 8 de COMO-CRIAR-UM-MODULO.md), atualização de docs (Passo 9) — os
mesmos passos se aplicam. A única coisa realmente diferente entre "criar um módulo" e "criar um
vertical" é este documento até aqui: o resto do ciclo de vida é idêntico por design.

---

## Checklist rápido (específico de vertical)

- [ ] FSM desenhada em texto ANTES do código (estados, transições, terminais).
- [ ] Projeto em `src/Verticals/{Nome}/`, mesma disciplina de dependência de Domain (só `SharedKernel` + `Modules.Abstractions`).
- [ ] `{Nome}Module : IModule` implementado — é o único ponto de contato com o Core.
- [ ] `DependeDe` vazio ou justificado (chamada direta real, não side-effect que deveria ser evento).
- [ ] Nenhuma menção ao código do vertical fora da própria pasta, exceto a linha de "incluir na lista" no host.
- [ ] Evento(s) de integração emitidos catalogados em `Modules.Abstractions/IntegrationEvents.cs`.
- [ ] `dotnet build` verde no projeto do vertical.
- [ ] Linha nova na tabela de verticais em `ARCHITECTURE.md` §1.1 e no mapa de pastas §8.
