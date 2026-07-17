# ADR-0004 — Instalador Windows via Velopack: publish self-contained + `Setup.exe` + auto-update por canal, dado de instalação sobrevive fora da pasta versionada do app

**Status:** Parcialmente implementado (ver "Estado atual no repo" — código installer-ready +
pipeline CI Windows produzindo `Setup.exe`; artefato final/assinatura/teste de instalação real
requerem runner Windows) · **Data:** 2026-07-17 · **Contexto do produto:** `Host.Desktop`
(`src/Hosts/SistemaX.Host.Desktop/`) é hoje um app .NET 10 rodado via `dotnet run`/`dotnet build`
— Generic Host + Kestrel embutido (loopback) + janela Photino/WebView2 (ver
`docs/arquitetura/bridge-http-local.md`). Este ADR decide como ele vira um **app Windows
instalável de verdade** (duplo-clique num `Setup.exe`, sem `dotnet` nem projeto no PC do
lojista) com **atualização automática** — sem exigir elevação de administrador, sem depender de
loja de apps (Microsoft Store/MSIX), e compatível com o modelo local-first do ADR-0001 (o app
troca de versão; o dado local — `sistemax.db`, `config.json`, logs — não pode.)

## Pergunta que este ADR responde
> "Como o `Host.Desktop` vira um instalador Windows com auto-update, sem reescrever a
> distribuição a cada nova versão, sem exigir admin na máquina do lojista, e sem arriscar o
> `sistemax.db`/`config.json` de uma loja a cada atualização?"

**Resposta curta:** **Velopack** empacota um `dotnet publish` self-contained (`win-x64`,
multi-arquivo, nunca `PublishSingleFile`) num `Setup.exe` com auto-update por delta, instalado
**por usuário** (sem UAC). O ganho real sobre WiX/MSI/MSIX/ClickOnce não é o instalador em si — é
o **auto-update de fábrica com patch incremental e canal (stable/beta)**, que nenhum dos outros
dá de graça para um app fora da Microsoft Store. Em troca, isso empurra uma obrigação para o
código do host que HOJE não existe: **o diretório de dados (`SISTEMAX_DATA_DIR`) tem que sair da
pasta do executável e virar `%ProgramData%\SistemaX` fixo**, porque Velopack troca a pasta
versionada do app a cada update — se o dado morar ali junto, uma atualização automática apaga o
banco da loja. Este ADR também fixa assinatura de código como não-opcional para produção (sem
ela, `Setup.exe` some/assusta no SmartScreen e updates não convencem o Windows de que vieram do
mesmo publisher).

## Decisão

1. **Empacotador = Velopack** (`vpk` CLI + pacote NuGet `Velopack` no app). Produz, a partir de
   uma pasta de publish comum: `Setup.exe` (instalador + primeira versão embutida), um `.zip`
   portátil, os pacotes `.nupkg` (full e delta) e um manifesto de release por canal. Tudo
   hospedável em qualquer storage estático (S3, Azure Blob, Nginx, GitHub Releases) — não exige
   um serviço de update dedicado.

2. **Publish é sempre self-contained, `win-x64`, multi-arquivo — nunca `PublishSingleFile`.**
   Delta-patch do Velopack funciona arquivo-a-arquivo dentro do pacote; um bundle single-file
   vira um blob monolítico e qualquer mudança de uma linha de código força o cliente a baixar o
   binário inteiro de novo, matando o motivo de usar Velopack. `PublishTrimmed` fica de fora por
   padrão (não validado contra WebView2/Photino/reflection deste projeto).

3. **`web/dist` entra no pacote pelo `CopyWebDist` que já existe no `.csproj`** (`AfterTargets=
   "Build;Publish"`), mas com uma ressalva que o pipeline de empacotamento precisa cobrir
   explicitamente (não o `.csproj`): esse target copia para `$(OutDir)wwwroot`, que **não é**
   necessariamente a mesma pasta que `dotnet publish -o` produz para uma publicação
   self-contained com RID (ver `docs/build/empacotamento.md` §5 para o porquê e o comando de
   verificação/cópia). O pipeline de empacotamento (não o `.csproj`) garante que `wwwroot/` está
   dentro da pasta que o `vpk pack` vai efetivamente empacotar — sem isso, o app instalado sobe
   sem SPA.

4. **Versionamento é SemVer estrito (`X.Y.Z[-tag]`), uma única fonte da verdade por release**,
   usada tanto em `dotnet publish -p:Version=` quanto em `vpk pack -v`. Nunca dois números
   diferentes para o mesmo binário. Velopack exige monotonicidade dentro de um canal — não dá
   para publicar `1.2.0` depois de `1.3.0` no mesmo canal.

5. **Canal de update = `stable` (produção) e `beta` (pré-release/piloto de loja), via `vpk pack
   --channel`.** O cliente decide qual canal segue (`UpdateOptions.ExplicitChannel` no
   `UpdateManager`) — permite rodar uma atualização arriscada só nas lojas-piloto antes de
   promover para `stable`, sem infraestrutura extra (é o mesmo `Setup.exe`/pacote, só muda o
   manifesto que ele consulta).

6. **Assinatura de código (Authenticode) é obrigatória antes de qualquer distribuição fora da
   máquina de dev — não é um "nice to have" de v2.** `Setup.exe`, o executável principal e o
   stub/`Update.exe` do Velopack são assinados no mesmo passo do `vpk pack`
   (`--signParams`/`--signTemplate`, que por baixo chama `signtool.exe` ou um assinante
   compatível — ver `docs/build/empacotamento.md` §8). Sem assinatura: (a) o Windows
   SmartScreen bloqueia/assusta no primeiro download de cada versão nova; (b) o Velopack usa a
   identidade do certificado para decidir se um update "confia" no binário anterior — sem
   assinatura estável, cada update pode ser tratado como potencialmente hostil pelo SO. Carimbo
   de tempo (`timestamp`/RFC3161) é obrigatório: sem ele, a assinatura expira junto com o
   certificado e todas as versões antigas instaladas passam a parecer não-assinadas.

7. **Dado de instalação sai da pasta do app.** Hoje `HostConfigLoader.CarregarOuCriar()`
   (`Bridge/HostConfig.cs`) usa `SISTEMAX_DATA_DIR ?? AppContext.BaseDirectory` — e
   `AppContext.BaseDirectory`, sob Velopack, é a pasta **versionada** do app (trocada/substituída
   a cada auto-update). Para produção, `SISTEMAX_DATA_DIR` **tem** que apontar para
   `%ProgramData%\SistemaX` (caminho já antecipado no comentário de `HostConfig.cs`, nunca antes
   fixado como decisão). Isto é uma mudança de comportamento do host (default de produção
   diferente do default de dev). **Implementado** via `OnFirstRun` (opção (a) abaixo) — ver
   "Estado atual no repo": `Updates.PrimeiraInstalacaoVelopack.ConfigurarDiretorioDeDadosDeProducao()`,
   chamado só quando o Velopack detecta instalação real, nunca em dev. Só é exercitável de verdade
   num Windows instalado via Velopack — não testado neste PR (macOS).

8. **`VelopackApp.Build().Run()` precisa ser a primeira instrução executada no processo** — antes
   de `HostConfigLoader.CarregarOuCriar()`, antes de qualquer bind de Kestrel, antes de abrir a
   janela Photino. É assim que o Velopack intercepta as invocações especiais que ele mesmo faz no
   executável durante instalar/desinstalar/atualizar (criar/remover atalho, etc.) — se qualquer
   código do host rodar antes disso, uma instalação silenciosa pode abrir janela, escrever
   `config.json` fora de hora, ou travar o instalador. **Implementado** — é literalmente a primeira
   linha executável de `Program.cs` (ver "Estado atual no repo"); confirmado NO-OP em
   dev/`dotnet run`/testes via smoke test manual (`SISTEMAX_HEADLESS=1`).

9. **WebView2 Runtime é pré-requisito de execução, não responsabilidade do Velopack.** Windows 11
   já traz o runtime in-box; Windows 10 exige o Evergreen Bootstrapper. O empacotamento não
   resolve isso sozinho — `docs/build/empacotamento.md` §10 documenta a checagem/point de
   instalação do pré-requisito como parte do checklist de release, não como código do pacote.

## Por que Velopack e não as alternativas óbvias

- **MSIX** — dá sandboxing e loja (Microsoft Store), que aqui atrapalha mais do que ajuda: o
  `Host.Desktop` precisa de acesso irrestrito a hardware (impressora térmica, balança, TEF,
  gaveta — `Infrastructure.Hardware`), a um Kestrel fazendo bind de porta e a um `sistemax.db`
  em caminho estável — tudo isso é mais fricção sob o sandbox do MSIX do que sob um instalador
  clássico. Auto-update fora da Store também exige reimplementar boa parte do que o Velopack já
  resolve.
- **WiX/MSI tradicional** — instalador maduro, mas **sem auto-update embutido**: seria preciso
  escrever um checker de versão + reinvocar `msiexec` a cada release, e ainda assim sem delta
  patch. MSI per-machine tipicamente pede elevação (UAC) para instalar — fricção real numa loja
  onde o operador de PDV pode não ter conta de admin na máquina.
  Este é o desenho que WiX resolveria bem (é o motivo de citar), mas reconstruir "auto-update com
  canal e delta" por cima dele é reinventar o que o Velopack já entrega pronto.
- **ClickOnce** — legado, pensado para WinForms/WPF publicado a partir do Visual Studio; combina
  mal com um app que já é ASP.NET Core self-hosted (Kestrel) + janela nativa (Photino); controle
  fino de local de instalação e de conteúdo do pacote é mais limitado.
- **Squirrel.Windows** — o predecessor direto do Velopack, mesmo modelo (per-user, delta,
  `Setup.exe`), mas hoje com manutenção reduzida frente ao Velopack — que é o fork/reescrita
  ativa da mesma linhagem (Clowd.Squirrel → Velopack), com o mesmo desenho de instalação e um
  CLI (`vpk`) que já roda cross-platform (dá para empacotar um `Setup.exe` de Windows a partir de
  uma máquina macOS/Linux de dev, sem precisar de uma VM Windows só para empacotar — só a
  assinatura de código com `signtool.exe` exige um passo em ambiente Windows, ver
  `docs/build/empacotamento.md` §8).

Nenhuma das alternativas muda a decisão de topologia do ADR-0001 (PDV com SQLite local,
offline-first) — este ADR é só sobre **como o binário chega e se atualiza na máquina do
lojista**, não sobre onde o dado mora ou como ele sincroniza.

## Consequências

- **(+)** Instalação e atualização **sem UAC/admin** — compatível com máquinas de loja onde o
  operador não tem conta elevada.
- **(+)** Auto-update com **delta patch**: releases pequenas (só o que mudou) baixam rápido numa
  loja com internet ruim — coerente com a premissa do ADR-0001 de que a rede é "otimização, não
  requisito", mas quando ela existe, o update deve ser leve.
- **(+)** Canal `beta`/`stable` permite piloto controlado numa loja antes de promover para todas
  — sem infraestrutura além de uma segunda pasta/URL de manifesto.
- **(+)** `vpk` roda no mesmo macOS/Linux onde o time já desenvolve — não força uma máquina
  Windows dedicada só para gerar o pacote (só para assinar, se `signtool.exe` for o caminho
  escolhido).
- **(−)** Introduz uma obrigação de código nova e **não-opcional para produção** que não existe
  hoje: `SISTEMAX_DATA_DIR` fixo fora da pasta versionada do app (decisão #7). Enquanto isso não
  for implementado, **auto-update em produção é uma ameaça real ao dado da loja** — este ADR
  documenta a decisão; a implementação é trabalho separado (ver gap em
  `docs/build/empacotamento.md` §9).
- **(−)** Assinatura de código deixa de ser dispensável: builds não assinados servem para teste
  interno, nunca para uma loja real — isso é custo recorrente (certificado + processo de
  assinatura em CI), não uma taxa única.
- **(−)** Mais uma peça de infraestrutura para manter no ar: o host estático do feed de update
  (mesmo que seja um bucket S3/Blob simples) precisa existir e ficar disponível para todo
  cliente instalado conseguir checar/baixar update — se cair, o app **continua funcionando**
  (checagem de update falha graciosamente, é só um `CheckForUpdatesAsync` que não acha nada novo),
  mas ninguém recebe correções até ele voltar.

## Estado atual no repo (atualizado — implementação parcial aplicada)

**Implementado neste repo, verificável de um Mac de dev (build + 836 testes verdes):**

- `Velopack` (NuGet) referenciado em `SistemaX.Host.Desktop.csproj` — compila cross-platform, não
  exige Windows para o dev buildar.
- `VelopackApp.Build().OnFirstRun(...).Run()` é literalmente a primeira instrução de
  `Program.cs` (decisão #8) — confirmado por smoke test manual (`SISTEMAX_HEADLESS=1`, boot
  normal, `/api/health` responde) que isto é NO-OP em dev/`dotnet run`/testes.
- `OnFirstRun` chama `Updates.PrimeiraInstalacaoVelopack.ConfigurarDiretorioDeDadosDeProducao()`,
  que resolve a decisão #7 pela opção (a) do gap original: fixa `SISTEMAX_DATA_DIR` para
  `%ProgramData%\SistemaX` (variável de **usuário**, não de máquina — instalação Velopack é
  per-user/sem UAC, decisão #1) só quando o Velopack detecta uma instalação real (nunca dispara em
  dev). **Isto só é exercitável de verdade num Windows instalado via Velopack** — em macOS o hook
  nunca roda (guard `OperatingSystem.IsWindows()`), então esta parte permanece não testada fora de
  um runner/máquina Windows.
- `Updates.IServicoDeAtualizacao` / `ServicoDeAtualizacaoVelopack` — serviço opcional e honesto:
  sem `HostConfig.AtualizacaoFeedUrl` (config.json) nem `SISTEMAX_UPDATE_FEED_URL` configurados,
  fica desligado e só loga "atualização automática desabilitada (sem feed)" — nunca inventa um
  feed. Chamado fire-and-forget depois de `app.StartAsync()`.
- `GET /api/health` ganhou `versao` (fonte única: `Updates.VersaoAssembly`, lida do
  `AssemblyInformationalVersionAttribute` que o MSBuild popula a partir do MESMO `-p:Version=`
  usado no publish) e `atualizacaoAutomaticaHabilitada`.
- `build/pack-windows.ps1` — script PowerShell que implementa o checklist §11 de
  `docs/build/empacotamento.md` (pnpm build → dotnet publish win-x64 self-contained → valida
  wwwroot → `vpk pack`, com assinatura condicional a `-SignParams`/`-AzureTrustedSignFile`).
- `.github/workflows/release-windows.yml` — roda em `windows-latest`, builda+testa antes de
  empacotar, chama `pack-windows.ps1`, publica `Setup.exe`/`.nupkg` como artifact do workflow.
  Assinatura é condicional ao secret `WINDOWS_CODESIGN_SIGNTOOL_PARAMS` — sem ele, o job **ainda
  assim produz** um `Setup.exe` não assinado e avisa explicitamente (nunca falha silenciosamente
  fingindo que assinou).
- `deploy/velopack/VERSION` — fonte única de versão (`0.1.0` inicial), lida tanto pelo script de
  pack quanto (implicitamente, via `-p:Version=`) pelo `dotnet publish`.

**Explicitamente FORA do alcance de um Mac de dev — requer runner/máquina Windows:**

- Rodar `dotnet publish -r win-x64 --self-contained` e obter um `.exe` **executável** (o publish
  cross-compila o binário, mas não há como abrir/rodar um `.exe` Windows num Mac para confirmar
  que a janela Photino/WebView2 sobe).
- Rodar `vpk pack` de verdade e obter um `Setup.exe` **instalável** — o comando roda cross-platform
  e provavelmente gera bytes válidos, mas instalar, abrir, forçar um auto-update de uma versão
  anterior e confirmar que `SISTEMAX_DATA_DIR`/`OnFirstRun` funcionam **só é verificável clicando
  no instalador num Windows de verdade**.
- Qualquer assinatura Authenticode real (`signtool.exe` só existe no Windows; Azure Trusted
  Signing seria cross-platform, mas nenhum certificado/conta foi provisionado neste PR — o secret
  `WINDOWS_CODESIGN_SIGNTOOL_PARAMS` é um placeholder documentado, não configurado).
- `signtool verify` pós-assinatura.
- WebView2 Runtime presente/ausente em uma loja Windows 10 real (§10 do doc de empacotamento —
  ainda não implementado, nem neste PR).

Ou seja: o app está **installer-ready** e o **pipeline que produz o `Setup.exe` existe e roda em
CI Windows** — mas o artefato final, a assinatura, e o teste de instalação/auto-update de verdade
continuam sendo, por natureza, coisas que só um runner Windows prova. Ver também a seção
"Status honesto" do `README.md`.

Pontos de entrada no código (para quem for além desta fatia):

- `src/Hosts/SistemaX.Host.Desktop/SistemaX.Host.Desktop.csproj` — `Target CopyWebDist` (linha
  ~44) continua copiando `web/dist` para `$(OutDir)wwwroot`, sem alteração; `pack-windows.ps1` §3
  cobre o gap descrito na decisão #3 deste ADR.
- `src/Hosts/SistemaX.Host.Desktop/Program.cs` — `VelopackApp.Build()...Run()` na primeira linha;
  registro de `IServicoDeAtualizacao` em DI; chamada fire-and-forget após `app.StartAsync()`.
- `src/Hosts/SistemaX.Host.Desktop/Updates/` — `PrimeiraInstalacaoVelopack`,
  `IServicoDeAtualizacao`, `ServicoDeAtualizacaoVelopack`, `VersaoAssembly` (pasta nova).
- `src/Hosts/SistemaX.Host.Desktop/Bridge/HostConfig.cs` — `AtualizacaoFeedUrl`/`AtualizacaoCanal`
  adicionados ao record (opcionais, default `null` — retrocompatíveis com `config.json`
  existentes) + overrides `SISTEMAX_UPDATE_FEED_URL`/`SISTEMAX_UPDATE_CHANNEL`.
- `docs/arquitetura/bridge-http-local.md` — documento irmão deste, "como rodar em dev sem
  instalador"; continua válido para desenvolvimento sem alteração.
