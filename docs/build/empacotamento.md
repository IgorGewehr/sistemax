# Empacotamento do `Host.Desktop` — publish, Velopack, assinatura, canal de update

> Como o `Host.Desktop` (`src/Hosts/SistemaX.Host.Desktop/`) sai de "`dotnet run` numa pasta de
> dev" para "`Setup.exe` assinado, instalável sem admin, com auto-update" — comandos exatos,
> reprodutíveis, do zero até o pacote publicado. Decisão e o porquê de cada escolha estão em
> `docs/arquitetura/adr/0004-instalador-velopack.md`; este documento é só o "como fazer".
>
> **Atualização — as mudanças de código listadas no §9 original FORAM feitas** (referência ao
> NuGet `Velopack`, `VelopackApp.Build().Run()` como primeira instrução de `Program.cs`, default
> de produção de `SISTEMAX_DATA_DIR` via `OnFirstRun`, checagem de update em runtime). `dotnet
> build`/`dotnet test` (836 testes) seguem verdes com essas mudanças aplicadas. Também existem
> agora `build/pack-windows.ps1` (implementa o checklist §11 abaixo) e
> `.github/workflows/release-windows.yml` (roda esse script em `windows-latest`).
>
> **O que continua NÃO executado/verificável de um Mac de dev:** `vpk pack` de verdade (não há
> `vpk` instalado nesta máquina de dev, e mesmo que houvesse, o `Setup.exe` resultante só abre no
> Windows), a assinatura Authenticode (nenhum certificado provisionado — o secret de CI é
> placeholder documentado), e qualquer teste de instalação/auto-update real. Isso só roda/valida
> no runner Windows do workflow ou numa máquina Windows real — ver "Estado atual no repo" do
> ADR-0004.

> **Script real:** os passos 2–5 abaixo são implementados por `build/pack-windows.ps1`
> (parametrizado por `-Channel`/`-Version`/`-SignParams`/`-AzureTrustedSignFile`) e executados em
> CI por `.github/workflows/release-windows.yml` (`windows-latest`, dispara em tag `v*` ou
> manualmente via `workflow_dispatch`). Este documento continua sendo a referência de "como/por
> quê" de cada passo; o script é a automação.

## 0. Visão geral do pipeline

```
web/ (pnpm build)                         src/Hosts/.../SistemaX.Host.Desktop.csproj
      │                                                    │
      │  web/dist/**                                       │  dotnet publish -r win-x64
      ▼                                                    ▼      --self-contained true
  ┌────────────────────────────────────────────────────────────────┐
  │  Target CopyWebDist (AfterTargets="Build;Publish", já existe    │
  │  no .csproj) copia web/dist → $(OutDir)wwwroot                  │
  └───────────────────────────────┬──────────────────────────────┘
                                   │  ⚠ $(OutDir) ≠ pasta de publish com RID — §5 explica e dá
                                   │    o comando de verificação/cópia que fecha o gap
                                   ▼
                     ./artifacts/publish/win-x64/   (pasta final que o vpk empacota)
                                   │
                                   │  vpk pack (self-contained + assinatura)
                                   ▼
     Setup.exe · <PackId>-Portable.zip · <PackId>-<versão>-full.nupkg (+ delta) · manifesto
                                   │
                                   │  upload pro host estático do feed (S3/Blob/Nginx)
                                   ▼
                     https://updates.sistemax.com.br/win/{stable|beta}/
                                   │
                                   │  UpdateManager.CheckForUpdatesAsync() (dentro do app)
                                   ▼
                          PC da loja recebe e aplica o update
```

## 1. Pré-requisitos (uma vez por máquina de empacotamento)

| Ferramenta | Para quê | Comando |
|---|---|---|
| .NET 10 SDK | `dotnet publish` (já é dependência do repo — `global.json` fixa `10.0.301`) | já instalado se o repo builda |
| pnpm | build do front-end (`web/`) | já é dependência do repo |
| `vpk` (Velopack CLI) | empacotar/assinar/gerar manifesto de update | `dotnet tool install --global vpk` |
| `signtool.exe` (só se assinar via certificado tradicional) | Authenticode | vem com o Windows SDK/Build Tools — **só existe em Windows** |
| Ferramenta de Azure Trusted Signing (alternativa recomendada, ver §8) | Authenticode sem precisar de Windows nem custodiar `.pfx` | `dotnet tool install --global sign` |

`vpk` roda em macOS/Linux/Windows e consegue empacotar um alvo Windows a partir de qualquer um
deles — **não é preciso uma VM Windows só para gerar o `Setup.exe`**. A única etapa que
tradicionalmente pede Windows é assinar com `signtool.exe`; se o caminho de assinatura escolhido
for Azure Trusted Signing (§8), o pipeline inteiro roda em macOS/Linux/CI Linux sem VM Windows
nenhuma.

Verifique a versão instalada antes de seguir os comandos abaixo — o CLI já teve mudança de nome
de flag entre versões 0.x:

```bash
vpk --version
vpk pack --help
```

## 2. Passo 1 — build do front-end

```bash
pnpm --dir web install   # primeira vez / lockfile mudou
pnpm --dir web build     # tsc -b && vite build → web/dist/
```

Precisa rodar **antes** do `dotnet publish` — é `web/dist/` que o `Target CopyWebDist` do
`.csproj` copia para dentro do output do .NET (condição `Exists('.../web/dist')`; se não existir,
o host builda mesmo assim, só sobe sem SPA).

## 3. Passo 2 — `dotnet publish` self-contained `win-x64`

```bash
VERSION=1.0.0   # ver §6 — fonte única do número, usada aqui e no vpk pack

dotnet publish src/Hosts/SistemaX.Host.Desktop/SistemaX.Host.Desktop.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:Version=$VERSION \
  -p:PublishSingleFile=false \
  -o ./artifacts/publish/win-x64
```

Notas:

- `--self-contained true` embute o runtime .NET — a máquina da loja não precisa ter .NET
  instalado. Roda cross-publish de macOS/Linux sem problema (o SDK baixa o runtime pack
  `win-x64` via NuGet).
- `-p:PublishSingleFile=false` é **explícito e deliberado** (decisão #2 do ADR-0004): Velopack
  faz delta patch arquivo-a-arquivo; um bundle single-file é um blob único e mata o delta.
- `-o ./artifacts/publish/win-x64` fixa a pasta de saída explicitamente — não depender do
  caminho implícito `bin/Release/net10.0/win-x64/publish/` deixa o restante do pipeline (e o
  passo de verificação do §5) reproduzível independente de versão do SDK.
- `PublishTrimmed`/`PublishReadyToRun` ficam de fora deste comando por padrão — não validados
  contra WebView2/Photino/reflection deste projeto. Se algum dia forem adotados, revalidar o
  passo 5 (wwwroot) e o boot completo antes de promover a produção.

## 4. Passo 3 — garantir que `wwwroot/` está na pasta de publish

**O gotcha:** o `Target CopyWebDist` (já existente no `.csproj`, não tocado aqui) copia
`web/dist` para `$(OutDir)wwwroot`. Para uma publicação com `-r win-x64 --self-contained`,
`$(OutDir)` (a pasta de *build*, ex.: `src/Hosts/SistemaX.Host.Desktop/bin/Release/net10.0/
win-x64/`) **não é** a mesma pasta que `-o ./artifacts/publish/win-x64` produz como saída de
*publish* — são dois estágios MSBuild distintos, e o `wwwroot` colado dinamicamente em `$(OutDir)`
não é necessariamente arrastado para a pasta final de publish (diferente de um `wwwroot/` que
existisse como pasta do PROJETO desde antes do build, que o SDK Web trata como conteúdo publicável
por padrão — aqui não é o caso: não existe `wwwroot/` versionado ao lado do `.csproj`, só o que o
target cria em tempo de build).

Depois de cada `dotnet publish`, confirme e, se faltar, copie:

```bash
BUILD_WWWROOT="src/Hosts/SistemaX.Host.Desktop/bin/Release/net10.0/win-x64/wwwroot"
PUBLISH_DIR="./artifacts/publish/win-x64"

if [ -d "$BUILD_WWWROOT" ] && [ ! -d "$PUBLISH_DIR/wwwroot" ]; then
  echo "wwwroot não veio no publish — copiando do build output (gotcha do CopyWebDist, ver ADR-0004 §3)"
  cp -R "$BUILD_WWWROOT" "$PUBLISH_DIR/wwwroot"
fi

# Prova real: o pacote tem que ter pelo menos o index.html da SPA.
test -f "$PUBLISH_DIR/wwwroot/index.html" && echo "OK: wwwroot presente no publish" \
  || { echo "FALTA wwwroot/index.html em $PUBLISH_DIR — não empacote assim"; exit 1; }
```

Equivalente Windows/PowerShell (CI):

```powershell
$buildWwwroot = "src/Hosts/SistemaX.Host.Desktop/bin/Release/net10.0/win-x64/wwwroot"
$publishDir   = "./artifacts/publish/win-x64"

if ((Test-Path $buildWwwroot) -and -not (Test-Path "$publishDir/wwwroot")) {
    Copy-Item -Recurse $buildWwwroot "$publishDir/wwwroot"
}

if (-not (Test-Path "$publishDir/wwwroot/index.html")) {
    throw "FALTA wwwroot/index.html em $publishDir — não empacote assim"
}
```

Isso é um passo do **pipeline de empacotamento** (script/CI), não uma mudança no `.csproj`. Se
quem mantém o `.csproj` mais adiante quiser resolver isso na origem (ex.: `CopyWebDist` também
mirar `$(PublishDir)`, ou declarar `wwwroot` como `<Content Include CopyToPublishDirectory=...>`),
fica registrado como melhoria possível — fora do escopo deste documento.

## 5. Passo 4 — `vpk pack`

```bash
vpk pack \
  --packId SistemaX \
  --packVersion $VERSION \
  --packDir ./artifacts/publish/win-x64 \
  --mainExe SistemaX.Host.Desktop.exe \
  --packTitle "SistemaX" \
  --packAuthors "SistemaX" \
  --channel stable \
  --outputDir ./artifacts/releases/stable \
  --signParams "/n \"NOME-DO-SIGNATARIO-NO-CERTIFICADO\" /fd sha256 /tr http://timestamp.digicert.com /td sha256"
```

Flags:

| Flag | Significado |
|---|---|
| `--packId` | Id estável do app (`SistemaX`) — usado no caminho de instalação (`%LocalAppData%\SistemaX\...`) e como chave do canal de update. **Nunca muda entre releases.** |
| `--packVersion` | SemVer desta release — mesma string do `-p:Version=` do passo 2 (§6). |
| `--packDir` | A pasta de publish validada no passo 3 (com `wwwroot/` dentro). |
| `--mainExe` | Nome do `.exe` que o instalador vai atalhar/lançar. |
| `--channel` | `stable` ou `beta` (§7) — cada canal é uma trilha de update independente. |
| `--outputDir` | Onde ficam `Setup.exe`, `.nupkg` (full/delta) e o manifesto — organizar por canal (`./artifacts/releases/<channel>/`) evita misturar manifestos de canais diferentes. |
| `--signParams` | String repassada para `signtool.exe sign <isso> <arquivo>`, aplicada pelo `vpk` a `Setup.exe`, ao `.exe` principal e ao stub `Update.exe` embutido. Ver §8 antes de rodar isso de verdade — sem certificado configurado na máquina, omita esta flag para gerar um pacote **não assinado** (só para teste interno, nunca para uma loja real). |

Não use `--icon` ainda — não existe hoje um `.ico` no repo (ver `web/` sem `public/favicon`
dedicado). Enquanto isso, `vpk pack` usa o ícone padrão do Velopack. Quando houver um ícone
oficial, adicione `--icon caminho/para/sistemax.ico` (arquivo `.ico` multi-resolução, não PNG).

**Delta automático:** se `--outputDir` já contiver o `.nupkg` full da versão anterior do mesmo
canal (ex.: baixado do host do feed antes de rodar este comando — ver §7), o `vpk pack` gera
também um pacote delta (`<packId>-<versão>-delta.nupkg`) — é isso que faz o update do cliente ser
pequeno. Sem o `.nupkg` anterior presente localmente, só o pacote full é gerado (update funciona
igual, só que o cliente baixa o binário inteiro daquela versão).

`vpk pack --help` mostra a lista completa e atualizada de flags para a versão instalada — confirme
nomes antes de rodar em produção, especialmente as de assinatura (§8), que variam mais entre
versões do CLI.

## 6. Versionamento

Convenção: **SemVer estrito `MAJOR.MINOR.PATCH[-prerelease]`** (ex.: `1.0.0`, `1.1.0`,
`1.1.1-beta.1`). Velopack exige monotonicidade dentro de um canal — não é possível publicar uma
versão menor que a última já publicada naquele canal.

Fonte única do número, usada nos dois comandos (`dotnet publish -p:Version=` e `vpk pack
--packVersion`) — proposta de convenção (arquivo ainda não existe, é trabalho de quem
implementar o pipeline):

```bash
# deploy/velopack/VERSION — uma linha, só o número
echo "1.0.0" > deploy/velopack/VERSION
VERSION=$(cat deploy/velopack/VERSION)
```

Bump manual a cada release (`echo "1.1.0" > deploy/velopack/VERSION`) antes de rodar os passos 2
e 5. Se mais adiante quiserem automatizar (tag do git, `MinVer`/`Nerdbank.GitVersioning`), isso é
mudança de `.csproj`/`Directory.Build.props` — fora do escopo deste documento.

## 7. Canais — `stable` e `beta`

Cada canal é uma trilha de update independente, hospedada em um sub-caminho próprio do mesmo
storage estático:

```
https://updates.sistemax.com.br/win/stable/    ← produção, todas as lojas
https://updates.sistemax.com.br/win/beta/      ← loja(s)-piloto antes de promover pra stable
```

Fluxo de release:

1. `vpk pack --channel beta --outputDir ./artifacts/releases/beta ...` → sobe para
   `.../win/beta/`.
2. Loja(s)-piloto (apontadas para o canal `beta` — ver `UpdateOptions.ExplicitChannel` no cliente,
   §9) recebem o update primeiro.
3. Validado, repita o pack com `--channel stable --outputDir ./artifacts/releases/stable` (mesma
   `VERSION`, mesmo binário) e suba para `.../win/stable/` — é isso que promove a versão para
   todo o parque instalado.

## 8. Assinatura de código

**Não opcional para qualquer pacote que saia da máquina de dev** (decisão #6 do ADR-0004). Sem
assinatura: SmartScreen bloqueia/assusta o instalador a cada versão nova, e o Windows não tem
como confirmar que um update veio do mesmo publisher que a versão anterior.

### O que precisa existir

1. **Um certificado de assinatura de código (Authenticode)** — desde a mudança de política do
   CA/Browser Forum (jun/2023), certificados novos (OV ou EV) exigem a chave privada em hardware
   (HSM ou token USB) — não é mais possível emitir um `.pfx` solto exportável para um certificado
   novo. Duas rotas:
   - **Azure Trusted Signing** (recomendada para começar): certificado + HSM gerenciados pela
     Microsoft, ~US$10/mês, sem custódia de token físico. Hoje exige verificação de organização
     elegível (histórico de CNPJ/negócio verificável — checar elegibilidade atual antes de
     assumir este caminho). Assinatura roda via `dotnet tool install --global sign` (cross-
     plataforma, sem precisar de Windows).
   - **Certificado OV/EV tradicional** (Sectigo, DigiCert, SSL.com, GlobalSign, ...) com a chave
     num token/HSM suportado — assinatura via `signtool.exe`, que só roda em Windows.
2. **Timestamp (RFC 3161) em toda assinatura** — `/tr <url> /td sha256` no `signtool`, ou
   equivalente no `sign` da Azure. Sem isso, a assinatura **expira junto com o certificado**: no
   dia em que o certificado vencer, todas as versões antigas instaladas passam a parecer
   não-assinadas, mesmo sem terem sido alteradas.
3. **Reputação do SmartScreen não é automática** com certificado OV: um certificado EV ganha
   reputação instantânea (benefício da Microsoft); OV/Trusted Signing constrói reputação aos
   poucos, por telemetria de downloads/instalações daquele binário+certificado específico —
   esperar aviso do SmartScreen nas primeiras versões é normal, não é sinal de configuração
   errada.

### Caminho A — `signtool.exe` (precisa de Windows)

```powershell
vpk pack `
  --packId SistemaX --packVersion $env:VERSION --packDir .\artifacts\publish\win-x64 `
  --mainExe SistemaX.Host.Desktop.exe --channel stable --outputDir .\artifacts\releases\stable `
  --signParams '/n "SistemaX Ltda" /fd sha256 /tr http://timestamp.digicert.com /td sha256'
```

(`/n` seleciona o certificado pelo nome do assinante já importado no cert store do Windows —
evita apontar para um `.pfx`/senha em disco, útil quando a chave vive num token/HSM.)

### Caminho B — Azure Trusted Signing (cross-plataforma, sem VM Windows)

```bash
dotnet tool install --global sign

# arquivo de config gerado conforme a doc do Azure Trusted Signing — nomes de campo a confirmar
# na versão atual do serviço/CLI antes de assumir como definitivos:
cat > ./trusted-signing.json <<'EOF'
{
  "Endpoint": "https://<região>.codesigning.azure.net/",
  "CodeSigningAccountName": "<nome-da-conta>",
  "CertificateProfileName": "<nome-do-perfil>"
}
EOF

vpk pack \
  --packId SistemaX --packVersion "$VERSION" --packDir ./artifacts/publish/win-x64 \
  --mainExe SistemaX.Host.Desktop.exe --channel stable --outputDir ./artifacts/releases/stable \
  --azureTrustedSignFile ./trusted-signing.json
```

Se `vpk` (na versão instalada) não expuser `--azureTrustedSignFile`, o caminho equivalente é
`--signTemplate` apontando para o `sign` CLI da Azure em vez de `signtool.exe` — `vpk pack --help`
mostra o token de substituição de arquivo que a versão instalada espera nesse template.

### Verificação pós-assinatura

```bash
# Windows — confirma que a assinatura é válida e tem timestamp:
signtool verify /pa /v .\artifacts\releases\stable\Setup.exe
```

## 9. Mudanças de código necessárias — **aplicadas** (ver nota no topo do documento)

As quatro mudanças abaixo (propostas originalmente aqui como pendentes) foram implementadas em
`src/Hosts/SistemaX.Host.Desktop/`. Os exemplos de código abaixo são o desenho original desta
seção — a implementação real está em `Program.cs`, `Bridge/HostConfig.cs` e na pasta nova
`Updates/` (`IServicoDeAtualizacao`, `ServicoDeAtualizacaoVelopack`, `PrimeiraInstalacaoVelopack`,
`VersaoAssembly`), com pequenas diferenças de nomenclatura (métodos em português, seguindo a
convenção do repo) e de assinatura da API real do pacote `Velopack` 1.2.0:

1. **Referência ao pacote NuGet `Velopack`** — feita em `SistemaX.Host.Desktop.csproj`
   (`Version="1.2.0"`, última estável em nuget.org no momento desta implementação).

2. **`VelopackApp.Build().Run()` como a PRIMEIRA instrução de `Program.cs`** — feita, antes até
   da linha `var (hostConfig, configPath, dataDir) = HostConfigLoader.CarregarOuCriar();`. Forma
   real (com o hook de primeira instalação encadeado, decisão #7):

   ```csharp
   using Velopack;

   VelopackApp.Build()
       .OnFirstRun(_ => PrimeiraInstalacaoVelopack.ConfigurarDiretorioDeDadosDeProducao())
       .Run();

   // ... resto do Program.cs atual, sem mudança ...
   ```

3. **Default de produção de `SISTEMAX_DATA_DIR` fora da pasta versionada do app** — feita pela
   opção (a) descrita originalmente aqui: `Updates/PrimeiraInstalacaoVelopack.cs` fixa
   `SISTEMAX_DATA_DIR` (variável de **usuário** — não de máquina, para não exigir admin/UAC — e
   também de processo, para valer já neste boot) para `%ProgramData%\SistemaX`, chamado pelo
   `OnFirstRun` acima. Só dispara quando o Velopack detecta uma instalação real — nunca em
   dev/`dotnet run`/testes (`OperatingSystem.IsWindows()` como guard extra). **Ressalva honesta:**
   isto não foi exercitado contra uma instalação Velopack real (só compilado e raciocinado) — só
   um Windows com o `Setup.exe` instalado de verdade prova que o hook dispara como esperado.

4. **Checagem/aplicação de update em runtime** — feita em `Updates/ServicoDeAtualizacaoVelopack.cs`
   (`IServicoDeAtualizacao`), chamado fire-and-forget logo após `DemoSeeder.SemearAsync` em
   `Program.cs`. Implementação real:

   ```csharp
   public async Task VerificarEAplicarAsync(CancellationToken cancellationToken)
   {
       if (!Habilitado) // sem HostConfig.AtualizacaoFeedUrl/SISTEMAX_UPDATE_FEED_URL
       {
           logger.LogInformation("Atualização automática desabilitada (sem feed configurado).");
           return;
       }

       try
       {
           var opcoes = string.IsNullOrWhiteSpace(config.AtualizacaoCanal)
               ? null : new UpdateOptions { ExplicitChannel = config.AtualizacaoCanal };
           var gerenciador = new UpdateManager(config.AtualizacaoFeedUrl!, opcoes);

           if (!gerenciador.IsInstalled) return; // dev/dotnet run — nunca tenta update

           var atualizacao = await gerenciador.CheckForUpdatesAsync();
           if (atualizacao is null) return;

           await gerenciador.DownloadUpdatesAsync(atualizacao, cancelToken: cancellationToken);
           gerenciador.WaitExitThenApplyUpdates(atualizacao.TargetFullRelease, silent: true, restart: true);
       }
       catch (Exception ex)
       {
           logger.LogWarning(ex, "Checagem/aplicação de atualização falhou — app segue normalmente sem ela.");
       }
   }
   ```

   `Habilitado`/`AtualizacaoFeedUrl` também aparecem em `GET /api/health` como
   `atualizacaoAutomaticaHabilitada` — é assim que dá para confirmar de fora se uma instalação tem
   update ligado, sem olhar `config.json` na máquina da loja.

## 10. Pré-requisito de runtime — WebView2

`PhotinoWindowLauncher` usa WebView2 no Windows (`Bridge/PhotinoWindowLauncher.cs`). Windows 11
já traz o WebView2 Runtime in-box; Windows 10 pode não ter. O empacotamento Velopack **não**
resolve isso — não é um componente que o `vpk pack` embute. Duas opções, a decidir antes do
piloto em Windows 10:

- Checar a presença do runtime no primeiro boot (registry key
  `SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}`) e, se ausente,
  baixar/rodar o Evergreen Bootstrapper (`MicrosoftEdgeWebview2Setup.exe`, redistribuível oficial
  da Microsoft) antes de abrir a janela Photino.
- Ou aceitar como pré-requisito documentado de instalação (checklist manual do implantador),
  adiando a checagem automática para uma iteração futura.

Fica registrado como item do checklist de release (§11), não como parte do pacote em si.

## 11. Checklist de release — fim a fim

```bash
# 0. Bump de versão
echo "1.1.0" > deploy/velopack/VERSION
VERSION=$(cat deploy/velopack/VERSION)

# 1. Front-end
pnpm --dir web install
pnpm --dir web build

# 2. Publish self-contained
dotnet publish src/Hosts/SistemaX.Host.Desktop/SistemaX.Host.Desktop.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:Version=$VERSION -p:PublishSingleFile=false \
  -o ./artifacts/publish/win-x64

# 3. Garantir wwwroot no publish (ver §5)
BUILD_WWWROOT="src/Hosts/SistemaX.Host.Desktop/bin/Release/net10.0/win-x64/wwwroot"
[ -d "$BUILD_WWWROOT" ] && [ ! -d "./artifacts/publish/win-x64/wwwroot" ] && \
  cp -R "$BUILD_WWWROOT" "./artifacts/publish/win-x64/wwwroot"
test -f "./artifacts/publish/win-x64/wwwroot/index.html" || { echo "FALTA wwwroot"; exit 1; }

# 4. (opcional, delta) baixar o .nupkg full da versão anterior do canal alvo para
#    ./artifacts/releases/<channel>/ antes do passo 5, se quiser delta patch nesta release.

# 5. Empacotar + assinar (beta primeiro)
vpk pack \
  --packId SistemaX --packVersion $VERSION \
  --packDir ./artifacts/publish/win-x64 --mainExe SistemaX.Host.Desktop.exe \
  --packTitle "SistemaX" --packAuthors "SistemaX" \
  --channel beta --outputDir ./artifacts/releases/beta \
  --azureTrustedSignFile ./trusted-signing.json   # ou --signParams, ver §8

# 6. Verificar assinatura
signtool verify /pa /v ./artifacts/releases/beta/Setup.exe   # roda em Windows

# 7. Subir ./artifacts/releases/beta/* inteiro (Setup.exe, .nupkg full+delta, manifesto) para
#    https://updates.sistemax.com.br/win/beta/ — mantendo os artefatos de versões anteriores
#    daquele canal ali hospedados (não sobrescrever/remover — são a base do delta futuro e do
#    catálogo de rollback do manifesto).

# 8. Validar na(s) loja(s)-piloto (canal beta) — boot limpo, auto-update de uma versão anterior,
#    impressão térmica, PDV completo (ver docs/build de smoke test do host, se existir).

# 9. Promover: repetir o pack com --channel stable --outputDir ./artifacts/releases/stable
#    (mesma VERSION) e subir para https://updates.sistemax.com.br/win/stable/.
```

## 12. Troubleshooting

| Sintoma | Causa provável | Ver |
|---|---|---|
| App instalado sobe sem SPA (tela em branco) | `wwwroot/` não chegou na pasta de publish | §5 |
| SmartScreen bloqueia o `Setup.exe` | Pacote não assinado, ou certificado ainda sem reputação acumulada | §8 |
| Update baixa mas nunca aplica sozinho | Só `DownloadUpdatesAsync` foi chamado, faltou `ApplyUpdatesAndRestart`/`WaitExitThenApplyUpdates` — ou o código do passo §9.4 nem foi implementado ainda | §9 |
| Auto-update "sumiu" com `sistemax.db`/`config.json` da loja | `SISTEMAX_DATA_DIR` não fixado fora da pasta versionada — Velopack trocou a pasta do app numa atualização | §9.3 — risco mais grave documentado, resolver ANTES de habilitar update em produção |
| `vpk pack` não gera pacote delta | `.nupkg` full da versão anterior do mesmo canal não estava presente em `--outputDir` no momento do pack | §5 |
| Assinatura "válida" hoje, binário antigo mostra "não confiável" daqui a alguns anos | Faltou timestamp (`/tr`/`/td`) na assinatura — expira junto com o certificado | §8 |
