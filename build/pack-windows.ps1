#requires -version 7
<#
.SYNOPSIS
    Empacota o Host.Desktop num instalador Velopack (Setup.exe) Windows.

.DESCRIPTION
    Implementa o checklist de release do ADR-0004 (docs/arquitetura/adr/0004-instalador-velopack.md)
    e docs/build/empacotamento.md §11: build do front-end -> dotnet publish self-contained win-x64
    -> garante wwwroot na pasta de publish -> vpk pack (+ assinatura opcional).

    HONESTIDADE: este script PRODUZ o Setup.exe de verdade só quando `vpk` está instalado e o
    passo de `dotnet publish -r win-x64` roda numa máquina/CI com o runtime pack win-x64
    disponível (cross-publish funciona de qualquer SO, mas o Setup.exe RESULTANTE só é
    executável/instalável/testável no Windows — ver README/ADR-0004 "Estado atual"). Rodar este
    script num Mac de dev produz o mesmo Setup.exe (o `vpk`/dotnet publish são cross-platform),
    mas ninguém consegue abri-lo ali para validar — só um runner/máquina Windows faz isso.

    Assinatura (Authenticode) é OPCIONAL e CONDICIONAL neste script: sem -SignParams nem
    -AzureTrustedSignFile, o pacote sai SEM ASSINATURA (serve para teste interno, nunca para uma
    loja real — ver docs/build/empacotamento.md §8). Este script nunca finge assinar.

.PARAMETER Channel
    Canal Velopack (stable|beta). Default: stable.

.PARAMETER Version
    SemVer da release. Default: conteúdo de deploy/velopack/VERSION.

.PARAMETER SignParams
    String de assinatura via signtool.exe (ex.: '/n "SistemaX Ltda" /fd sha256 /tr
    http://timestamp.digicert.com /td sha256'). Requer Windows. Ver §8 do doc de empacotamento.

.PARAMETER AzureTrustedSignFile
    Caminho para o JSON de config do Azure Trusted Signing (alternativa cross-plataforma ao
    signtool.exe — ver §8). Se informado, tem prioridade sobre -SignParams.

.PARAMETER SkipWebBuild
    Pula `pnpm build` do front-end (útil em CI que já buildou web/dist num job anterior).

.EXAMPLE
    pwsh ./build/pack-windows.ps1 -Channel stable

.EXAMPLE
    pwsh ./build/pack-windows.ps1 -Channel beta -AzureTrustedSignFile ./trusted-signing.json
#>
param(
    [ValidateSet('stable', 'beta')]
    [string]$Channel = 'stable',

    [string]$Version = (Get-Content -Raw (Join-Path $PSScriptRoot '../deploy/velopack/VERSION')).Trim(),

    [string]$SignParams = '',

    [string]$AzureTrustedSignFile = '',

    [switch]$SkipWebBuild
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

function Write-Step($mensagem) {
    Write-Host "`n=== $mensagem ===" -ForegroundColor Cyan
}

Write-Host "SistemaX — pack Windows (Velopack)" -ForegroundColor Green
Write-Host "  Versão: $Version"
Write-Host "  Canal:  $Channel"
Write-Host "  SO deste runner: $([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)"
if (-not $IsWindows) {
    Write-Host "  AVISO: rodando fora do Windows — o Setup.exe é gerado (vpk/dotnet publish são" -ForegroundColor Yellow
    Write-Host "  cross-platform), mas NÃO pode ser instalado/executado/verificado aqui. Use um" -ForegroundColor Yellow
    Write-Host "  runner Windows (ver .github/workflows/release-windows.yml) antes de distribuir." -ForegroundColor Yellow
}

# 1. Front-end ----------------------------------------------------------------------------------
if (-not $SkipWebBuild) {
    Write-Step "1/5 — build do front-end (web/dist)"
    pnpm --dir (Join-Path $RepoRoot 'web') install
    if ($LASTEXITCODE -ne 0) { throw "pnpm install falhou" }
    pnpm --dir (Join-Path $RepoRoot 'web') build
    if ($LASTEXITCODE -ne 0) { throw "pnpm build falhou" }
} else {
    Write-Step "1/5 — build do front-end PULADO (-SkipWebBuild)"
}

# 2. dotnet publish self-contained win-x64 -------------------------------------------------------
Write-Step "2/5 — dotnet publish (win-x64, self-contained, PublishSingleFile=false — ADR-0004 decisão #2)"
$publishDir = Join-Path $RepoRoot 'artifacts/publish/win-x64'
$hostCsproj = Join-Path $RepoRoot 'src/Hosts/SistemaX.Host.Desktop/SistemaX.Host.Desktop.csproj'

dotnet publish $hostCsproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:PublishSingleFile=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou" }

# 3. Garantir wwwroot na pasta de publish (gotcha do CopyWebDist — ver ADR-0004 §3 / doc §5) ------
Write-Step "3/5 — validar wwwroot dentro da pasta de publish"
$buildWwwroot = Join-Path $RepoRoot 'src/Hosts/SistemaX.Host.Desktop/bin/Release/net10.0/win-x64/wwwroot'
$publishWwwroot = Join-Path $publishDir 'wwwroot'

if ((Test-Path $buildWwwroot) -and -not (Test-Path $publishWwwroot)) {
    Write-Host "wwwroot não veio no publish — copiando do build output (gotcha do CopyWebDist, ver ADR-0004 §3)"
    Copy-Item -Recurse $buildWwwroot $publishWwwroot
}

if (-not (Test-Path (Join-Path $publishWwwroot 'index.html'))) {
    throw "FALTA wwwroot/index.html em $publishDir — não empacote assim (web/dist não foi buildado?)"
}
Write-Host "OK: wwwroot presente no publish."

# 4. vpk pack -------------------------------------------------------------------------------------
Write-Step "4/5 — vpk pack (canal $Channel)"

$vpk = Get-Command vpk -ErrorAction SilentlyContinue
if (-not $vpk) {
    throw "vpk não encontrado no PATH — instale com 'dotnet tool install --global vpk' (ver docs/build/empacotamento.md §1)"
}

$outputDir = Join-Path $RepoRoot "artifacts/releases/$Channel"

$packArgs = @(
    'pack'
    '--packId', 'SistemaX'
    '--packVersion', $Version
    '--packDir', $publishDir
    '--mainExe', 'SistemaX.Host.Desktop.exe'
    '--packTitle', 'SistemaX'
    '--packAuthors', 'SistemaX'
    '--channel', $Channel
    '--outputDir', $outputDir
)

# Assinatura — CONDICIONAL a um dos dois parâmetros. Sem nenhum, o pacote sai não-assinado e o
# script avisa explicitamente (nunca finge assinar — ver docs/build/empacotamento.md §8).
if ($AzureTrustedSignFile) {
    Write-Host "Assinando via Azure Trusted Signing ($AzureTrustedSignFile)"
    $packArgs += @('--azureTrustedSignFile', $AzureTrustedSignFile)
} elseif ($SignParams) {
    Write-Host "Assinando via signtool.exe (--signParams)"
    $packArgs += @('--signParams', $SignParams)
} else {
    Write-Host "AVISO: nenhum parâmetro de assinatura informado — Setup.exe sairá NÃO ASSINADO." -ForegroundColor Yellow
    Write-Host "        Válido para teste interno; NUNCA distribua um Setup.exe não assinado a uma loja real." -ForegroundColor Yellow
}

& vpk @packArgs
if ($LASTEXITCODE -ne 0) { throw "vpk pack falhou" }

# 5. Resumo -----------------------------------------------------------------------------------
Write-Step "5/5 — pronto"
Write-Host "Artefatos em: $outputDir"
Get-ChildItem $outputDir | ForEach-Object { Write-Host "  - $($_.Name)" }

if ($AzureTrustedSignFile -or $SignParams) {
    Write-Host "`nPara verificar a assinatura (só funciona no Windows):"
    Write-Host "  signtool verify /pa /v `"$outputDir\Setup.exe`""
}

Write-Host "`nPróximo passo (manual, fora deste script): subir o conteúdo de $outputDir inteiro"
Write-Host "para o host estático do feed de update (ex.: https://updates.sistemax.com.br/win/$Channel/)."
Write-Host "Ver docs/build/empacotamento.md §7/§11."
