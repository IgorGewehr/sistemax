# SistemaX

ERP de bancada **offline-first, modular e multi-vertical** para o pequeno empreendedor
brasileiro. Instala na máquina, funciona **sem internet**, sincroniza quando ela volta, e
roda numa loja com vários PCs compartilhando dados em tempo real. O **coração é o financeiro**:
cada módulo existe também para alimentá-lo, de modo que até um leigo tenha a visão de um
consultor financeiro sênior e não quebre.

> **Comece por `docs/arquitetura/ARCHITECTURE.md`** (a constituição do projeto) e por
> `CLAUDE.md` (as regras duras). Qualquer dev deve conseguir bater o olho e começar.

> **⚙️ Status honesto (2026-07) — o que roda HOJE vs. staged.**
> **Roda:** o `Host.Desktop` (Bridge HTTP local + SQLite, offline real numa máquina), com
> **PDV → venda → estoque persistidos de verdade**. Financeiro, Compras, OS e Fiscal têm domínio
> .NET testado (o Fiscal já calcula tributo dos 3 regimes).
> **Presente no código mas AINDA NÃO ligado (staged):** o motor de **Sync 3 camadas**
> (`Infrastructure.Sync`), o **Hardware** (impressora/balança/TEF — `Infrastructure.Hardware`), o
> **`Store.Server`** (LAN) e a **`Cloud.Api`** (esqueleto). A topologia de 3 camadas abaixo é o
> **alvo arquitetural**, não o estado atual — cada peça é registrada no composition root quando
> entra em produção. Boa parte das telas ainda consome mock tipado (seam pronto pra API).
> **Instalador (ADR-0004):** `Host.Desktop` está *installer-ready* — `Velopack` referenciado,
> `VelopackApp.Build().Run()` na primeira linha de `Program.cs` (confirmado NO-OP em dev/testes),
> serviço de auto-update opcional (desligado sem feed configurado) e `/api/health` reportando
> versão. O pipeline que **produz** o `Setup.exe` existe e roda em CI Windows
> (`.github/workflows/release-windows.yml` + `build/pack-windows.ps1`). O que **não** dá pra
> produzir/verificar de um Mac de dev — só num runner/máquina Windows: o `Setup.exe` em si, a
> assinatura Authenticode (secret de CI é placeholder, sem certificado provisionado) e um teste
> real de instalar/atualizar. Ver "Estado atual no repo" em
> `docs/arquitetura/adr/0004-instalador-velopack.md`.

## Stack

| Camada | Tecnologia |
|---|---|
| Engine/host (Windows) | **.NET 10** (C#) — dono de hardware, DB local, sync, robustez |
| UI | **React + Tailwind + Framer Motion** (design herdado do saas-erp) em **WebView2** |
| DB local (cada máquina) | **SQLite** (WAL, ACID, crash-safe) |
| Servidor de loja (LAN) | .NET — fonte da verdade local, sobrevive a queda de internet |
| Nuvem | **ASP.NET Core + PostgreSQL** — multi-loja, BI, multi-tenant |
| Sync | motor bidirecional 3 camadas (PDV ↔ loja ↔ nuvem), outbox, idempotência ULID |

## Topologia (o porquê de 3 camadas)

```
9 PDVs ─┐
        ├─► SERVIDOR DA LOJA (LAN) ─► NUVEM
1 Admin ─┘   sobrevive à internet cair    consolida / BI / multi-tenant
```

Cai a internet no meio de uma venda? A loja continua na LAN; cada PDV persiste local (WAL) e
recupera no boot. Nada corrompe o banco. Ver `docs/robustez/`.

## Estrutura da solução

```
src/
  SharedKernel/            primitivos: Money (centavos), DomainEvent, AggregateRoot, Result
  Modules/
    Abstractions/          IModule (o contrato do plugin) + catálogo de eventos de integração
    Financeiro/            ❤️ o coração — Domain / Application / Infrastructure
    Vendas/                módulo base (emite eventos que o Financeiro consome)
  Verticals/
    Assistencia/           1º vertical / MVP (Ordem de Serviço, peças, NFS-e)
  Infrastructure/
    Local/                 SQLite, transação atômica de venda, backup/recovery
    Sync/                  motor de sync 3 camadas, outbox, conflito por-entidade
    Hardware/              impressora, balança, TEF, gaveta, scanner (adapter + Null Object)
  Hosts/
    Host.Desktop/          app Windows (WebView2 + Photino) — composition root do PDV
    Store.Server/          servidor de loja (LAN)
    Cloud.Api/             ASP.NET Core + Postgres
web/                       app React (UI)
tests/                     testes (financeiro primeiro)
docs/                      arquitetura + design do financeiro + lições de robustez
```

## Rodar

```bash
dotnet restore
dotnet build
dotnet test
```

### Subir o app (Host.Desktop — bridge HTTP local + janela)

```bash
dotnet build   # compila .NET + copia web/dist -> wwwroot, se existir
SISTEMAX_PORT=5090 dotnet run --project src/Hosts/SistemaX.Host.Desktop/SistemaX.Host.Desktop.csproj
```

O host loga a URL da API e da janela (com o boot-token) ao subir. Variáveis de ambiente
(`SISTEMAX_UI_URL`, `SISTEMAX_HEADLESS`, `SISTEMAX_DATA_DIR`, `SISTEMAX_PORT`), endpoints
disponíveis e como logar via `curl` → **`docs/arquitetura/bridge-http-local.md`**.

(UI e hosts em construção — Host.Desktop já sobe o bridge real (F1a); Store.Server/Cloud.Api
seguem esqueleto.)
