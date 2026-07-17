# Bridge HTTP local (Host.Desktop) — F1a

> Fatia implementada: o `Host.Desktop` deixou de ser o demo de console do F0 e virou o app real —
> Generic Host (`WebApplication`) com Kestrel embutido em `127.0.0.1`, autenticação por PIN,
> contrato `IModuleEndpoints` e a janela Photino. Ver `scratchpad`/decisão original em
> `sistemax-production-plano.md §2` para o raciocínio completo (por que HTTP local, por que
> Photino). Este documento é o "como rodar e o que existe hoje".

## 1. O que existe agora

```
SistemaX.Host.Desktop (net10.0, Sdk.Web)
 ├─ Program.cs           WebApplication: config.json → Kestrel (loopback) → módulos → rotas → janela
 ├─ Composition/
 │   SistemaXHost.cs      composition root — RegistrarModulos(services, camada, config) devolve o
 │                        ModuleRegistry (mesmos módulos do F0 + 2 novos "*.Endpoints")
 └─ Bridge/
     HostConfig.cs         config.json (porta, businessId, PIN hash, log level, persistência)
     PinHasher.cs          PBKDF2-SHA256 210k iterações
     SessionStore.cs       sessões Bearer em memória (TTL 12h deslizante) + rate-limit de login
     BearerSessionMiddleware.cs  exige Bearer em /api/*, injeta businessId em HttpContext.Items
     BridgeEndpoints.cs    /api/health e /api/auth/login (endpoints do HOST, não de módulo)
     DemoSeeder.cs         semente idempotente (assinaturas + produtos) — TEMPORÁRIO, ver §5
     PhotinoWindowLauncher.cs  abre a janela; non-fatal se não houver display
```

Na camada de módulo (Application), dois novos `IModule` só para HTTP:

- `SistemaX.Modules.Financeiro.Application.Endpoints.FinanceiroEndpointsModule`
- `SistemaX.Modules.Estoque.Application.Endpoints.EstoqueEndpointsModule`

Ambos implementam `IModuleEndpoints` (`SistemaX.Modules.Abstractions`) — o Host enumera
`registry.ModulosAdicionados.OfType<IModuleEndpoints>()` e chama `MapearEndpoints(api)` uma vez
por módulo. Zero `if` no Host sobre qual módulo é qual; adicionar um terceiro módulo com endpoints
é só implementar a interface e registrar o módulo em `SistemaXHost.RegistrarModulos`.

## 2. Endpoints expostos nesta fatia

| Rota | Auth | O que faz |
|---|---|---|
| `GET /api/health` | Anônima | Sonda de vida — `instalacaoId`, `businessId`, uptime |
| `POST /api/auth/login` | Boot-token (header `X-Boot-Token`) | PIN → sessão Bearer (12h, TTL deslizante) |
| `GET /api/financeiro/receita-recorrente` | Bearer | MRR/ARR/churn — `ReceitaRecorrenteService` sobre `IAssinaturaRepository` |
| `GET /api/estoque/produtos` | Bearer | Catálogo — `IProdutoRepository.ListarAsync` |

Todo dinheiro no wire é `{ centavos, moeda }` (`Money` do SharedKernel, com as propriedades
computadas — `EmReais`, `EhPositivo`, ... — marcadas `[JsonIgnore]` de propósito). Erros seguem
`{ codigo, mensagem }` em `camelCase`.

**R1 no bridge**: nenhum endpoint de módulo lê `businessId` de query string/corpo — só de
`HttpContext.ObterBusinessId()` (extensão em `SistemaX.Modules.Abstractions`), que lê o que o
`BearerSessionMiddleware` gravou depois de validar a sessão.

**Fora do escopo desta fatia** (fica pro resto da F1): `GET /api/instalacao`, SSE
`/api/eventos`, endpoints de escrita (criar produto, iniciar venda, lançar conta), RBAC por papel
além do único papel "gerente" hoje emitido no login.

## 3. Rodar em dev (macOS/Linux/Windows, sem instalador)

```bash
# 1. Build (compila .NET + copia web/dist -> wwwroot, ver Target CopyWebDist no .csproj)
#    Se web/dist não existir ainda, rode `pnpm --dir web build` antes (ou ignore — o host sobe
#    do mesmo jeito, só sem SPA estática).
dotnet build

# 2. Rodar o host. Variáveis de ambiente úteis:
#    SISTEMAX_DATA_DIR   onde ficam config.json, sistemax.db, logs/ (default: AppContext.BaseDirectory)
#    SISTEMAX_PORT       porta fixa do Kestrel (default: efêmera, porta 0)
#    SISTEMAX_UI_URL     aponta a janela pro Vite dev server em vez do bundle estático
#    SISTEMAX_HEADLESS=1 pula a abertura da janela Photino (útil p/ verificação via curl/CI)
SISTEMAX_PORT=5090 dotnet run --project src/Hosts/SistemaX.Host.Desktop/SistemaX.Host.Desktop.csproj

# 3. (Opcional) Vite com HMR na janela, apontando /api pro Kestrel:
SISTEMAX_UI_URL=http://localhost:5173 SISTEMAX_PORT=5090 dotnet run --project src/Hosts/SistemaX.Host.Desktop
pnpm --dir web dev   # noutro terminal — configure vite.config.ts server.proxy['/api'] -> :5090 se ainda não configurado
```

No primeiro boot, se `config.json` não existir, ele é criado com PIN padrão de DEV **1234**
(hash PBKDF2, nunca em texto puro) e `businessId: "loja-demo"`. Um wizard de primeiro-boot real
(nome da loja, PIN escolhido pelo usuário) é trabalho do resto da F1.

## 4. Verificar com `curl` (o boot-token vem do log do processo)

```bash
# O host loga a URL completa da janela (com o boot-token) ao subir:
#   SistemaX no ar. API: http://127.0.0.1:5090/api — janela: http://127.0.0.1:5090/?boot=<TOKEN>
BOOT=<TOKEN-do-log>

curl http://127.0.0.1:5090/api/health

curl -X POST http://127.0.0.1:5090/api/auth/login \
  -H "Content-Type: application/json" -H "X-Boot-Token: $BOOT" \
  -d '{"pin":"1234"}'
# -> { "token": "...", "businessId": "loja-demo", "papel": "gerente", "expiraEm": "..." }

TOKEN=<token-da-resposta-acima>
curl http://127.0.0.1:5090/api/financeiro/receita-recorrente -H "Authorization: Bearer $TOKEN"
curl http://127.0.0.1:5090/api/estoque/produtos -H "Authorization: Bearer $TOKEN"
```

## 5. `DemoSeeder` — por que existe e quando cai

Os dois endpoints reais desta fatia (`receita-recorrente`, `produtos`) precisavam de dado de
verdade fluindo ponta-a-ponta pra provar o caminho use-case/read-model → HTTP. Como `Estoque` e
`Financeiro/Assinaturas` ainda não têm tela de cadastro nem porte SQLite nesta fatia,
`Bridge/DemoSeeder.cs` semeia idempotentemente (só se a coleção estiver vazia) as mesmas
assinaturas/produtos que o demo de console do F0 imprimia no stdout — agora populando os
repositórios reais do host a cada boot (eles são in-memory: reinicia o processo, reseeda do
zero). **Cai** quando a UI ganhar os formulários de cadastro reais e/ou esses ports ganharem
persistência SQLite (ver `docs/persistencia/persistencia-sqlite.md` e o roadmap F1 restante).

## 6. Janela Photino — o que esperar

`PhotinoWindowLauncher` tenta abrir a janela (WebView2 no Windows, WKWebView no macOS) navegando
para `{url}/?boot={token}` (ou `SISTEMAX_UI_URL` em dev). Se o ambiente não tiver display/sessão
gráfica (comum em CI, terminais remotos sem GUI, ou `SISTEMAX_HEADLESS=1`), a tentativa falha ou é
pulada, é logada como aviso, e **o servidor `/api` continua no ar normalmente** — é o essencial
desta fatia. Testado em macOS de dev; validação em Windows real (WebView2 Evergreen) fica para
quando houver máquina Windows disponível.

## 7. Config.json — campos

```json
{
  "InstalacaoId": "guid sem hífen, gerado no primeiro boot",
  "BusinessId": "tenant desta instalação (R1 — todo dado gravado carrega isto)",
  "NomeLoja": "nome de exibição",
  "Porta": 0,
  "LogLevel": "Information",
  "Persistencia": "sqlite",
  "PinAdminHash": "base64 — PBKDF2-SHA256, nunca o PIN em texto puro",
  "PinAdminSalt": "base64",
  "UiUrl": null
}
```

`SISTEMAX_PORT`/`SISTEMAX_UI_URL` sobrescrevem `Porta`/`UiUrl` em runtime sem tocar o arquivo
(são da EXECUÇÃO, não da instalação).

## 8. Logging (Serilog)

Console (sempre) + arquivo rolling diário em `{SISTEMAX_DATA_DIR}/logs/sistemax-YYYYMMDD.log`
(retém 14 dias). Nível vem de `config.json` (`LogLevel`). Enriquecido com `instalacaoId`.
