# ARCHITECTURE.md — a constituição do SistemaX

> Se você só vai ler um documento antes de codar, é este. Ele não muda a cada feature — mudanças
> aqui são mudanças de REGRA, não de detalhe. Detalhe técnico de um domínio específico vive em
> `docs/financeiro/` (modelo de dados do coração financeiro) e `docs/robustez/` (lições de
> crash-safety, sync e hardware extraídas do Supermarket-OS). Este documento amarra os dois a uma
> única arquitetura coerente e explica o "porquê" por trás de cada decisão estrutural.

---

## 0. Em uma frase

**SistemaX é um Core financeiro com Verticais plugáveis, rodando em 3 camadas offline-first,
onde todo módulo fala com o Financeiro só por eventos de integração idempotentes — nunca por
chamada direta.**

Cada palavra dessa frase é uma seção deste documento.

---

## 1. O modelo Core + Verticais

### 1.1 Duas categorias de módulo

| Categoria | O que é | Exemplos | Sempre ligado? |
|---|---|---|---|
| **Módulo core** | Capacidade que praticamente todo negócio de varejo/serviço usa | `Financeiro`, `Vendas`, (futuros: `Estoque`, `Compras`, `Clientes`) | Financeiro sempre; os demais tipicamente sim, mas continuam plugáveis pelo mesmo mecanismo |
| **Vertical** | Modelo de negócio específico de um segmento — só existe para quem vende aquilo | `Assistencia` (MVP), futuros: `Posto`, `Mercado`, `Oficina` | Não — habilitado por instalação, conforme o que o cliente contratou |

A distinção é **de produto**, não de mecanismo técnico. Estruturalmente, um módulo core e um
vertical são a MESMA coisa para o Core do sistema: uma implementação de `IModule`. Não existe
API especial de "vertical" — `AssistenciaModule` implementa exatamente a mesma interface que um
futuro `VendasModule` implementaria. Isso é deliberado: se amanhã "Assistência" virar tão comum
que devesse ser um módulo core, **nada muda no código dela** — só a decisão de produto sobre
"vem habilitado por padrão ou não".

### 1.2 A regra de ouro: o Core nunca conhece um módulo concreto

```
❌ PROIBIDO em qualquer lugar do Core (Hosts/, Infrastructure/):

    if (vertical == "posto") { ... }
    switch (moduloAtivo) { case "assistencia": ...; case "mercado": ...; }
    services.AddScoped<IAlgumaCoisa, ImplementacaoDoPosto>();  // hard-coded fora do próprio módulo

✅ CORRETO: o Core só enumera IModule e chama o contrato

    foreach (var modulo in modulosHabilitadosNestaInstalacao)
        modulo.Registrar(services, contexto);
```

O mecanismo concreto é `SistemaX.Modules.Abstractions.ModuleRegistry` (ver
`src/Modules/Abstractions/SistemaX.Modules.Abstractions/ModuleRegistry.cs`): ele recebe uma lista
de `IModule`, valida o grafo de dependências (`IModule.DependeDe`), ordena topologicamente e
chama `Registrar()` de cada um, nessa ordem. Um host (ex.: `Host.Desktop`) monta essa lista a
partir de configuração — "quais módulos/verticais este cliente comprou" — e delega tudo ao
registry. **Habilitar um vertical = adicionar seu `IModule` à lista. Desabilitar = não
adicionar.** Um módulo não adicionado não carrega absolutamente nada: zero rota HTTP, zero
serviço no container, zero migração de schema, zero superfície de falha para os demais módulos.

```
┌─────────────────────────────────────────────────────────────────────┐
│                              HOST (ex.: Host.Desktop)                │
│                                                                       │
│   IEnumerable<IModule> módulos = LerConfiguracaoDaInstalacao();      │
│   //   ["financeiro", "vendas", "assistencia"]  ← esta loja           │
│   //   ["financeiro", "vendas"]                 ← aquela loja         │
│                                                                       │
│   new ModuleRegistry()                                              │
│       .Adicionar(new FinanceiroModule())                            │
│       .Adicionar(new VendasModule())                                │
│       .Adicionar(new AssistenciaModule())   ← só se habilitado       │
│       .RegistrarTodos(services, contexto);                          │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              │ Core NUNCA sabe que "Assistencia" existe como conceito.
                              │ Ele só conhece IModule.
                              ▼
        ┌───────────────┐  ┌───────────────┐  ┌────────────────────┐
        │ Financeiro-    │  │ Vendas.Domain  │  │ Assistencia.Domain │
        │ Module         │  │ (+ futuro:      │  │ (+ futuro:          │
        │ (Domain +       │  │  Application/   │  │  Application/       │
        │  Application +  │  │  Infra)         │  │  Infra)             │
        │  Infrastructure)│  │                │  │  AssistenciaModule  │
        └───────────────┘  └───────────────┘  └────────────────────┘
```

Ver `docs/arquitetura/COMO-CRIAR-UM-MODULO.md` e `docs/arquitetura/COMO-CRIAR-UM-VERTICAL.md`
para o passo a passo de implementar um novo.

---

## 2. Topologia offline de 3 camadas — e o porquê

> **Estado (2026-07):** esta topologia é o **alvo arquitetural**. Hoje roda **só a camada do
> PDV/Host local** (SQLite + Bridge HTTP). `Store.Server` e `Cloud.Api` são esqueletos e o motor de
> `Infrastructure.Sync` existe no código mas **não está registrado no composition root** — nada
> sincroniza ainda. Ver o callout de Status no `README.md`.

```
        PDV 1   PDV 2   PDV 3  ...  PDV 9        (caixa/balcão — 9 terminais numa loja)
          │       │       │          │
          └───────┴───────┴──────────┘
                       │  LAN (latência ~0, não depende do ISP)
                       ▼
             ┌─────────────────────┐
             │  SERVIDOR DA LOJA    │   ← fonte da verdade LOCAL da loja
             │  (Store.Server)      │     sobrevive a queda de internet
             └──────────┬───────────┘
                        │  internet (pode cair, pode ser lenta)
                        ▼
             ┌─────────────────────┐
             │       NUVEM          │   ← consolidação multi-loja, BI, multi-tenant
             │      (Cloud.Api)     │
             └─────────────────────┘
```

### 2.1 Por que 3 camadas e não 2 (PDV ↔ nuvem direto)

`docs/robustez/robustez-hardware-licoes.md` §4 documenta o caso estudado (Supermarket-OS): um
modelo de 2 camadas (terminal ↔ um único servidor) resolve "a loja continua VENDENDO offline"
(cada PDV persiste local), mas **não resolve "tempo real entre os 9 PDVs da própria loja quando
a internet cai"** — se o único servidor estiver na nuvem, uma queda de internet mata toda
visibilidade entre terminais simultaneamente (reserva de estoque entre caixas, abertura de caixa
visível ao admin, etc.), mesmo que cada PDV continue vendendo isoladamente.

A 3ª camada (servidor da loja, na LAN) resolve exatamente essa lacuna: os 9 PDVs + 1 admin têm
tempo real entre si **na própria loja**, sem depender do ISP, porque o servidor que orquestra
isso está fisicamente ali. A nuvem deixa de ser "o único ponto de verdade" e vira "o
consolidador" — ela só precisa estar de pé para BI/multi-loja/backup remoto, não para a loja
operar em tempo real no dia a dia.

### 2.2 O que cada camada é dona de quê

| Camada | `CamadaExecucao` | Dono de | Sobrevive a |
|---|---|---|---|
| PDV | `Pdv` | Hardware (impressora, balança, TEF, gaveta, scanner), UI do operador, SQLite local próprio | Queda da LAN E da internet — persiste local, sincroniza quando volta |
| Servidor da loja | `ServidorDeLoja` | Fonte da verdade da loja, fanout em tempo real pros PDVs (WebSocket), fila de sync loja↔nuvem | Queda de internet — LAN continua de pé |
| Nuvem | `Nuvem` | Consolidação multi-loja, relatórios/BI cross-loja, multi-tenant, backup remoto | — (é o topo; se cair, cada loja continua operando sozinha) |

Um módulo pode se registrar **diferente** em cada camada — é para isso que `IModule.Registrar`
recebe `IModuleContext.Camada` (ver `src/Modules/Abstractions/SistemaX.Modules.Abstractions/IModule.cs`).
Exemplo: o vertical Assistência só liga handler de impressão de orçamento no PDV; a nuvem não
precisa disso.

### 2.3 O mesmo motor de sync se repete em cada salto (não reinventar)

Princípio extraído de `docs/robustez/robustez-hardware-licoes.md` §3-4: outbox transacional →
push em lote → pull com cursor → resolução de conflito por política explícita por entidade
(dado financeiro/transacional = terminal vence; cadastro/preço = servidor vence; contador de
estoque = soma de delta). Isso roda **duas vezes**, com o mesmo desenho: uma vez entre
PDV ↔ Servidor da Loja, outra entre Servidor da Loja ↔ Nuvem (aí o servidor da loja é visto pela
nuvem como "mais um terminal lógico"). Detalhes de implementação (triggers de outbox, formato de
idempotency key, backoff) ficam em `src/Infrastructure/SistemaX.Infrastructure.Sync/` — fora da
partição deste documento, mas o princípio de "por que 2 saltos, não 1" é arquitetural e vive
aqui.

### 2.4 Crash-safety é uma responsabilidade de camada, não de módulo

Regra herdada de `docs/robustez/robustez-hardware-licoes.md` §1-2: o fechamento de um fato
financeiro real (venda, faturamento de OS) é **uma única transação do motor de banco local**
(SQLite/WAL no PDV), nunca uma sequência de chamadas de aplicação independentes. Rascunho/auto-save
é UX (evitar redigitar), não é a garantia de integridade — os dois são camadas independentes.
Isso é responsabilidade de `Infrastructure.Local`, não de `Modules.*.Domain` — Domain só expõe o
método que, quando chamado dentro de uma transação, deixa o agregado num estado válido ou não
muda nada.

---

## 3. Como um módulo/vertical se pluga — `IModule`

Contrato completo em `src/Modules/Abstractions/SistemaX.Modules.Abstractions/IModule.cs`:

```csharp
public interface IModule
{
    string Codigo { get; }                 // "financeiro", "vendas", "assistencia", "posto"...
    string Nome { get; }
    IReadOnlyCollection<string> DependeDe => Array.Empty<string>();
    void Registrar(IServiceCollection services, IModuleContext contexto);
}
```

**Regra de ouro (repetida de propósito, porque é a mais fácil de violar sob pressão de prazo):
nada de `if (vertical == "posto")` em lugar nenhum do Core.** Se você sentir vontade de escrever
esse `if`, o que falta é uma abstração no lugar certo — geralmente um evento de integração novo
(módulo emite, quem precisa assina) ou uma interface nova em `Modules.Abstractions` que o módulo
implementa e o Core resolve via DI.

`DependeDe` existe para módulos que **de fato chamam serviço de outro diretamente** (raro — a
via preferida é sempre evento de integração, que não cria dependência de registro nenhuma).
`ModuleRegistry` valida esse grafo no boot: dependência ausente ou ciclo é erro de configuração
fatal, detectado ao subir o processo — nunca em runtime, nunca silenciosamente.

Guias completos com exemplo trabalhado:
- `docs/arquitetura/COMO-CRIAR-UM-MODULO.md` (módulo core, exemplo: Vendas)
- `docs/arquitetura/COMO-CRIAR-UM-VERTICAL.md` (vertical, exemplo: Assistência)

---

## 4. "Financeiro é o coração" — o fluxo por eventos de integração

### 4.1 O porquê

Todo módulo (Vendas, Compras, Ordem de Serviço, Agenda, Pedidos, Folha) **existe também para
alimentar o Financeiro** — é o que dá ao dono de negócio leigo a visão de um consultor
financeiro sênior sem ele precisar entender contabilidade. Mas nenhum módulo escreve direto nas
entidades financeiras (`ContaAReceber`, `ContaAPagar`, `MovimentoFinanceiro` — ver
`docs/financeiro/financeiro-datamodel.md`). O único canal de entrada é o **evento de
integração**.

### 4.2 Exemplo de sequência: `venda.concluida` → Financeiro cria recebível/receita

```
┌──────────┐          ┌──────────────────┐          ┌──────────────────┐          ┌────────────┐
│  Operador │          │  Venda (Domain)   │          │  Vendas.Application │          │ Financeiro │
│  do PDV   │          │  (Vendas.Domain)  │          │  (futuro)          │          │  (assinante)│
└────┬─────┘          └─────────┬────────┘          └─────────┬─────────┘          └──────┬─────┘
     │  Concluir(formaPagamento)     │                          │                           │
     ├───────────────────────────────►                          │                           │
     │                               │ valida FSM (Aberta→Concluida)                        │
     │                               │ acumula VendaConcluidaDomainEvent                      │
     │                               │ em AggregateRoot.DomainEvents (NADA publicado ainda)   │
     │                               ◄───────────────────────────┤                           │
     │                                                           │ persiste Venda             │
     │                                                           │ (COMMIT da transação local) │
     │                                                           │                            │
     │                                                           │ SÓ APÓS commit confirmado:  │
     │                                                           │ para cada DomainEvent:      │
     │                                                           │  evt.ParaEventoDeIntegracao()│
     │                                                           │  → VendaConcluida            │
     │                                                           │  bus.PublishAsync(evento)    │
     │                                                           ├──────────────────────────────►
     │                                                           │                            │ HandleAsync(VendaConcluida)
     │                                                           │                            │  1. consulta sourceRef=vendaId
     │                                                           │                            │     (idempotência — se já
     │                                                           │                            │      existe, NO-OP, não duplica)
     │                                                           │                            │  2. cria ContaAReceber
     │                                                           │                            │     (dataCompetencia=agora,
     │                                                           │                            │      categoria conforme item)
     │                                                           │                            │  3. se pago à vista: cria
     │                                                           │                            │     MovimentoFinanceiro
     │                                                           │                            │     ATOMICAMENTE junto
     │                                                           │                            │  4. gera LancamentoContabil
     │                                                           │                            │     (double-entry, invisível)
     │                                                           │ venda.ClearDomainEvents()   │
     │                                                           ◄──────────────────────────────┤
```

O passo "SÓ APÓS commit confirmado" é o ponto mais importante do diagrama: **nunca publique um
evento de integração de dentro da mesma transação que ainda pode dar rollback.** Se a transação
falhar depois de publicar, o Financeiro já reagiu a um fato que nunca aconteceu de verdade.

Modelo de dados completo do lado Financeiro (o que `ContaAReceber`/`Parcela`/
`MovimentoFinanceiro`/`LancamentoContabil` significam, por que é single-entry na superfície e
double-entry gerado por baixo) está em `docs/financeiro/financeiro-datamodel.md` — não duplicado
aqui.

### 4.3 Catálogo de eventos de integração (hoje)

Vive em `src/Modules/Abstractions/SistemaX.Modules.Abstractions/IntegrationEvents.cs`:

| Evento | Emissor | Efeito no Financeiro |
|---|---|---|
| `VendaConcluida` | Vendas | Cria `ContaAReceber` (+ `MovimentoFinanceiro` se à vista) |
| `VendaEstornada` | Vendas | Lança reversão — nunca apaga o fato original |
| `CompraRecebida` | Compras (futuro) | Cria `ContaAPagar` (custo) |
| `OsFaturada` | Assistência (e qualquer vertical de Ordem de Serviço) | Cria `ContaAReceber` de serviço + peças |
| `PedidoPago` | Pedidos (futuro) | Cria `ContaAReceber` já quitada + `MovimentoFinanceiro` |
| `ParcelaVencida` | Cron financeiro | Transição de FSM (`aberto→atrasado`), sem novo fato de dinheiro |
| `FolhaLancada` | RH/Folha (futuro) | Cria `ContaAPagar` de despesa com pessoal |

Cada `IIntegrationEvent` carrega uma `ChaveIdempotencia` estável, **derivada do id do fato de
origem, nunca de timestamp** — a lição do bug de granularidade de 1 segundo documentada em
`docs/robustez/robustez-hardware-licoes.md` §3 (triggers de outbox que geravam chave por
`strftime('%s','now')` e colidiam sob updates na mesma transação). Reprocessar o mesmo evento
(retry de rede, replay de fila) precisa ser NO-OP no assinante.

---

## 5. Evento de domínio vs evento de integração — a distinção que sustenta tudo

| | Evento de DOMÍNIO (`IDomainEvent`, SharedKernel) | Evento de INTEGRAÇÃO (`IIntegrationEvent`, Modules.Abstractions) |
|---|---|---|
| Onde nasce | Dentro de um agregado (`AggregateRoot.Raise()`) | Traduzido a partir de um evento de domínio, fora do agregado |
| Quem conhece o tipo | Só o módulo dono do agregado (privado) | Qualquer módulo que queira assinar (público, contrato estável) |
| Quando é publicado | Nunca "publicado" sozinho — só acumulado em `AggregateRoot.DomainEvents` | Depois do commit da transação de origem, via `IIntegrationEventBus.PublishAsync` |
| Estabilidade do shape | Pode mudar livremente com o agregado (é interno) | É um CONTRATO — mudar quebra assinantes; versione, não quebre |
| Exemplo | `VendaConcluidaDomainEvent` (Vendas.Domain, privado) | `VendaConcluida` (Modules.Abstractions, público) |
| Garantia de entrega | Nenhuma — é só uma lista em memória até `ClearDomainEvents()` | Ao menos uma vez, com idempotência por `ChaveIdempotencia` no assinante |

O par `Venda`/`VendaConcluida` (em `src/Modules/Vendas/SistemaX.Modules.Vendas.Domain/`) e o par
`OrdemDeServico`/`OsFaturada` (em `src/Verticals/Assistencia/SistemaX.Verticals.Assistencia/`)
são os exemplos trabalhados — leia `VendaDomainEvents.cs` e
`OrdemDeServicoDomainEvents.cs` para ver a tradução (`ParaEventoDeIntegracao()`) lado a lado com
o comentário de quem chama isso e quando.

**Por que não levantar `IIntegrationEvent` direto do agregado?** Porque o agregado não deveria
saber que existe um `IIntegrationEventBus`, nem que sua mudança de estado é "cross-módulo
relevante" — essa é uma decisão de Application/Infrastructure (o quê publicar, quando, com que
garantia de entrega), não de Domain (o quê aconteceu). Manter os dois tipos separados também
permite que o shape interno do agregado evolua sem quebrar o contrato que outros módulos
consomem.

---

## 6. Regras de camada (dependency rule)

```
                    ┌─────────────────────────────────────────┐
                    │              SharedKernel                │  (Money, AggregateRoot,
                    │  (não depende de nada do resto do repo)  │   DomainEvent, Result)
                    └───────────────────▲───────────────────────┘
                                        │ referenciado por
                    ┌───────────────────┴───────────────────────┐
                    │           Modules.Abstractions              │  (IModule, IIntegrationEvent,
                    │  (só depende de SharedKernel + abstrações   │   IIntegrationEventBus,
                    │   de DI/Configuration da BCL)                │   ModuleRegistry, Fsm<T>)
                    └───────────────────▲───────────────────────┘
                                        │ referenciado por
        ┌───────────────────────────────┼───────────────────────────────┐
        │                               │                               │
┌───────┴────────┐             ┌────────┴─────────┐            ┌────────┴─────────┐
│  Modules.X.     │             │ Verticals.Y      │            │  (futuro módulo)  │
│  Domain         │             │ (Domain)         │            │  Domain           │
│                 │             │                  │            │                   │
│ SÓ referencia:   │             │ SÓ referencia:    │            │                   │
│ SharedKernel +   │             │ SharedKernel +    │            │                   │
│ Modules.         │             │ Modules.          │            │                   │
│ Abstractions     │             │ Abstractions      │            │                   │
└───────┬────────┘             └────────┬─────────┘            └───────────────────┘
        │ referenciado por (Application conhece Domain, nunca o contrário)
┌───────┴────────┐
│ Modules.X.      │
│ Application     │   orquestra o agregado, PUBLICA eventos de integração após commit
└───────┬────────┘
        │
┌───────┴────────┐
│ Modules.X.      │   EF/Dapper/SQLite, HTTP, hardware — tudo que é "detalhe técnico"
│ Infrastructure  │
└─────────────────┘
```

**Regra dura: `Domain` não referencia `Infrastructure`, nem pacotes de banco/HTTP/hardware.**
`Modules.Vendas.Domain` e `Verticals.Assistencia` (Domain) hoje só têm `ProjectReference` para
`SharedKernel` e `Modules.Abstractions` — confira o `.csproj` de cada um. Se um dia um agregado
"precisar" de `Microsoft.Data.Sqlite` ou de uma chamada HTTP, é sinal de que a lógica não é
Domain — é Application ou Infrastructure vazando pra dentro. `Modules.Abstractions` é uma
exceção aceitável: é kernel compartilhado (contratos, sem implementação), não infraestrutura.

Um `Domain` pode ter dependência em pacotes puramente estruturais (ex.: `Ulid` para gerar Id) —
a régua é "isso é uma biblioteca de tipo/valor, sem I/O, sem estado externo?". `Microsoft.Data.Sqlite`
falha esse teste (I/O real); `Ulid` passa (função pura `byte[] → string`).

---

## 7. Stack

| Camada | Tecnologia |
|---|---|
| Engine/host (Windows) | **.NET 10** (C#) |
| UI | **React + Tailwind + Framer Motion** em **WebView2** (design herdado do saas-erp) |
| DB local (cada máquina) | **SQLite** (WAL, `synchronous=NORMAL`) |
| Servidor de loja (LAN) | .NET — `Store.Server` |
| Nuvem | **ASP.NET Core + PostgreSQL** — `Cloud.Api` |
| Sync | outbox transacional + idempotência ULID + cursor monotônico do servidor + conflito por-entidade |
| Testes | xUnit |

Ver `README.md` da raiz para o comando de build/test e para o mapa de pastas resumido; a versão
detalhada do mapa está na próxima seção.

---

## 8. Mapa de pastas

```
src/
  SharedKernel/                          primitivos puros, sem dependência de mais nada no repo
    SistemaX.SharedKernel/
      Money.cs                           dinheiro em centavos-inteiros — nunca double/float
      Entity.cs                          Entity<TId> + AggregateRoot<TId> (Raise/DomainEvents)
      DomainEvents.cs                    IDomainEvent, DomainEvent (base record)
      Result.cs                          Result/Result<T>/Error — erro esperado vs exceção

  Modules/
    Abstractions/
      SistemaX.Modules.Abstractions/
        IModule.cs                       contrato do plugin + IModuleContext + CamadaExecucao
        IntegrationEvents.cs             IIntegrationEvent + catálogo (VendaConcluida, OsFaturada...)
        IIntegrationEventHandler.cs      IIntegrationEventHandler<T> + IIntegrationEventBus
        ModuleRegistry.cs                descoberta/boot: valida grafo, ordena, chama Registrar()
        Fsm.cs                           Fsm<TStatus>.ValidarTransicao — guarda de FSM reutilizável

    Financeiro/                          ❤️ o coração — dono de outros agentes, não tocar aqui
      SistemaX.Modules.Financeiro.Domain/
      SistemaX.Modules.Financeiro.Application/
      SistemaX.Modules.Financeiro.Infrastructure/

    Vendas/                              módulo core — o exemplo EMISSOR deste documento
      SistemaX.Modules.Vendas.Domain/
        Venda.cs                         agregado raiz, FSM Aberta→Concluida→Estornada
        ItemDeVenda.cs                   objeto de valor (linha de venda)
        StatusVenda.cs                   enum da FSM
        VendaDomainEvents.cs             VendaConcluidaDomainEvent/VendaEstornadaDomainEvent
                                          + ParaEventoDeIntegracao() (tradução domínio→integração)
        (futuro: .Application/.Infrastructure)

  Verticals/
    Assistencia/                         1º vertical / MVP — o exemplo de PLUGAGEM deste documento
      SistemaX.Verticals.Assistencia/
        OrdemDeServico.cs                agregado raiz, FSM com 9 estados e ramificações
        Equipamento.cs / PecaAplicada.cs objetos de valor
        StatusOrdemServico.cs            enum da FSM (com diagrama ASCII no XML doc)
        OrdemDeServicoDomainEvents.cs    OsFaturadaDomainEvent + ParaEventoDeIntegracao()
        AssistenciaModule.cs             IModule — o ponto de plugagem do vertical
        (futuro: .Application/.Infrastructure)

  Infrastructure/                        dono de outros agentes, não tocar aqui
    SistemaX.Infrastructure.Local/       SQLite, transação atômica, backup/recovery
    SistemaX.Infrastructure.Sync/        motor de sync 3 camadas, outbox, conflito por-entidade
    SistemaX.Infrastructure.Hardware/    impressora, balança, TEF, gaveta, scanner

  Hosts/                                 dono de outros agentes, não tocar aqui
    SistemaX.Host.Desktop/               composition root do PDV (WebView2)
    SistemaX.Store.Server/               servidor de loja (LAN)
    SistemaX.Cloud.Api/                  ASP.NET Core + Postgres

web/                                     app React (UI)
tests/                                   xUnit — Financeiro primeiro
docs/
  arquitetura/                           este documento + guias de módulo/vertical
  financeiro/                            modelo de dados, features, UX do coração financeiro
  robustez/                              lições de crash-safety/durabilidade/sync/hardware
```

---

## 9. Para onde ir a partir daqui

- Vai criar um **módulo core** novo (ex.: Estoque, Compras)? →
  `docs/arquitetura/COMO-CRIAR-UM-MODULO.md`.
- Vai criar um **vertical** novo (ex.: Posto, Mercado)? →
  `docs/arquitetura/COMO-CRIAR-UM-VERTICAL.md`.
- Regras duras não-negociáveis (o que todo PR precisa respeitar)? → `CLAUDE.md` na raiz.
- Modelo de dados do Financeiro, catálogo completo de eventos, regime caixa vs competência? →
  `docs/financeiro/financeiro-datamodel.md`.
- Crash-safety, sync multi-terminal, drivers de hardware? → `docs/robustez/robustez-hardware-licoes.md`.
- Como o Host.Desktop sobe o bridge HTTP local (Kestrel/auth/`IModuleEndpoints`/janela Photino),
  como rodar em dev e verificar com `curl`? → `docs/arquitetura/bridge-http-local.md`.
