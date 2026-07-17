using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Fiscal.Application.CasosDeUso;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.Modules.Fiscal.Infrastructure.InMemory;
using SistemaX.Modules.Fiscal.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Tests;

/// <summary>
/// Prova o gap fechado por <see cref="RetransmitirDocumentosPendentesUseCase"/> — o "job de
/// retransmissão" que docs/fiscal/emissao-mapping.md §7.2/§9 nomeava como necessário e ainda não
/// existia: documentos presos em <see cref="StatusDocumentoFiscal.NumeroAlocado"/> por falha de
/// infraestrutura numa tentativa anterior são retentados, nunca deixados "pairando" para sempre.
/// </summary>
public sealed class RetransmitirDocumentosPendentesUseCaseTests
{
    private const string TenantId = "tenant-1";

    private static DocumentoFiscal DocumentoNumeroAlocado(string vendaId)
    {
        var documento = DocumentoFiscal.Abrir(TenantId, TipoDocumentoFiscal.NFCe, new SourceRef("vendas", vendaId));
        var item = new ItemDocumentoFiscal(
            "produto-1", "Produto 1", "12345678", null, OrigemMercadoria.Nacional, "5102",
            Quantidade.DeInteiro(1), Money.DeReais(10), Money.Zero,
            [new TributoResolvidoItem(TipoTributo.Icms, "102", Money.DeReais(10), Percentual.Zero, Money.Zero)]);

        documento.AdicionarItemResolvido(item);
        documento.AlocarNumero("1", 1);
        documento.ClearDomainEvents();
        return documento;
    }

    /// <summary>Mesmo documento de <see cref="DocumentoNumeroAlocado"/>, mas "nascido" há
    /// <paramref name="idade"/> — via <c>Reconstituir</c> (reidratação, não valida, não levanta
    /// evento), o único jeito de simular um documento antigo sem depender de relógio de parede
    /// mockável no agregado (que não tem — <c>CriadoEm</c> é <c>DateTimeOffset.UtcNow</c> real em
    /// <c>Abrir</c>, de propósito, mesmo racional de auditoria imutável do resto do domínio).</summary>
    private static DocumentoFiscal DocumentoNumeroAlocadoComIdade(string vendaId, TimeSpan idade)
    {
        var recente = DocumentoNumeroAlocado(vendaId);
        return DocumentoFiscal.Reconstituir(
            recente.Id, recente.TenantId, recente.Tipo, recente.Origem, recente.Status,
            recente.Serie, recente.Numero, recente.ChaveDeAcesso, recente.Protocolo,
            recente.MotivoBloqueioOuRejeicaoOuDenegacao, DateTimeOffset.UtcNow - idade, recente.Itens);
    }

    [Fact]
    public async Task ExecutarAsync_FalhaAlemDoTetoDeIdade_DesisteDoNumero()
    {
        var documentos = new InMemoryDocumentoFiscalRepository();
        var bus = new FakeIntegrationEventBus();
        var doc = DocumentoNumeroAlocadoComIdade("venda-3", TimeSpan.FromHours(3));
        await documentos.SalvarAsync(doc);

        var gateway = FakeGatewayEmissaoSefaz.FalhandoInfra();
        var transmissor = new TransmitirDocumentoFiscalUseCase(documentos, bus, gateway);
        var useCase = new RetransmitirDocumentosPendentesUseCase(documentos, transmissor, new DesistirDeNumeroUseCase(documentos, bus));

        var resolvidos = await useCase.ExecutarAsync(TenantId, TimeSpan.Zero, idadeMaximaAntesDeDesistir: TimeSpan.FromHours(2));

        Assert.Equal(0, resolvidos); // não conta como "resolvido por retransmissão" — foi desistência
        var relido = await documentos.ObterPorIdAsync(doc.Id);
        Assert.Equal(StatusDocumentoFiscal.Inutilizado, relido!.Status);
        Assert.Contains(bus.Publicados, e => e is NumeroFiscalInutilizado);
    }

    [Fact]
    public async Task ExecutarAsync_FalhaAindaDentroDoTetoDeIdade_NaoDesisteEContinuaRetentando()
    {
        var documentos = new InMemoryDocumentoFiscalRepository();
        var bus = new FakeIntegrationEventBus();
        var doc = DocumentoNumeroAlocadoComIdade("venda-4", TimeSpan.FromMinutes(30));
        await documentos.SalvarAsync(doc);

        var gateway = FakeGatewayEmissaoSefaz.FalhandoInfra();
        var transmissor = new TransmitirDocumentoFiscalUseCase(documentos, bus, gateway);
        var useCase = new RetransmitirDocumentosPendentesUseCase(documentos, transmissor, new DesistirDeNumeroUseCase(documentos, bus));

        var resolvidos = await useCase.ExecutarAsync(TenantId, TimeSpan.Zero, idadeMaximaAntesDeDesistir: TimeSpan.FromHours(2));

        Assert.Equal(0, resolvidos);
        var relido = await documentos.ObterPorIdAsync(doc.Id);
        Assert.Equal(StatusDocumentoFiscal.NumeroAlocado, relido!.Status); // ainda dentro do teto — retentável na próxima rodada
        Assert.DoesNotContain(bus.Publicados, e => e is NumeroFiscalInutilizado);
    }

    [Fact]
    public async Task ExecutarAsync_RodarDuasVezesSobreOMesmoDocumentoResolvido_NaoReprocessaNemDuplicaAutorizacao()
    {
        var documentos = new InMemoryDocumentoFiscalRepository();
        var bus = new FakeIntegrationEventBus();
        var doc = DocumentoNumeroAlocado("venda-5");
        await documentos.SalvarAsync(doc);

        var gateway = FakeGatewayEmissaoSefaz.Autorizando("35260112345678000195650010000000091000000091", "135260000000001");
        var transmissor = new TransmitirDocumentoFiscalUseCase(documentos, bus, gateway);
        var useCase = new RetransmitirDocumentosPendentesUseCase(documentos, transmissor, new DesistirDeNumeroUseCase(documentos, bus));

        var primeiraRodada = await useCase.ExecutarAsync(TenantId, TimeSpan.Zero);
        var segundaRodada = await useCase.ExecutarAsync(TenantId, TimeSpan.Zero); // idempotência: já Autorizado, sai da consulta de pendentes

        Assert.Equal(1, primeiraRodada);
        Assert.Equal(0, segundaRodada); // não é mais NumeroAlocado — a query de pendentes já não o devolve
        Assert.Equal(1, gateway.Chamadas); // gateway chamado uma única vez no total
    }

    [Fact]
    public async Task ExecutarAsync_DocumentoPresoPorFalhaDeInfra_RetransmiteEAutoriza()
    {
        var documentos = new InMemoryDocumentoFiscalRepository();
        var bus = new FakeIntegrationEventBus();
        var doc = DocumentoNumeroAlocado("venda-1");
        await documentos.SalvarAsync(doc);

        var gateway = FakeGatewayEmissaoSefaz.Autorizando("35260112345678000195650010000000091000000091", "135260000000001");
        var transmissor = new TransmitirDocumentoFiscalUseCase(documentos, bus, gateway);
        var useCase = new RetransmitirDocumentosPendentesUseCase(documentos, transmissor, new DesistirDeNumeroUseCase(documentos, bus));

        var resolvidos = await useCase.ExecutarAsync(TenantId, TimeSpan.Zero);

        Assert.Equal(1, resolvidos);
        Assert.Equal(1, gateway.Chamadas);
        var relido = await documentos.ObterPorIdAsync(doc.Id);
        Assert.Equal(StatusDocumentoFiscal.Autorizado, relido!.Status);
        Assert.Equal("135260000000001", relido.Protocolo);
    }

    [Fact]
    public async Task ExecutarAsync_GatewayContinuaFalhandoPorInfra_DocumentoPermaneceNumeroAlocado()
    {
        var documentos = new InMemoryDocumentoFiscalRepository();
        var bus = new FakeIntegrationEventBus();
        var doc = DocumentoNumeroAlocado("venda-2");
        await documentos.SalvarAsync(doc);

        var gateway = FakeGatewayEmissaoSefaz.FalhandoInfra();
        var transmissor = new TransmitirDocumentoFiscalUseCase(documentos, bus, gateway);
        var useCase = new RetransmitirDocumentosPendentesUseCase(documentos, transmissor, new DesistirDeNumeroUseCase(documentos, bus));

        var resolvidos = await useCase.ExecutarAsync(TenantId, TimeSpan.Zero);

        Assert.Equal(0, resolvidos);
        var relido = await documentos.ObterPorIdAsync(doc.Id);
        Assert.Equal(StatusDocumentoFiscal.NumeroAlocado, relido!.Status);
    }

    [Fact]
    public async Task ExecutarAsync_SemDocumentosPendentes_NaoChamaOGateway()
    {
        var documentos = new InMemoryDocumentoFiscalRepository();
        var bus = new FakeIntegrationEventBus();
        var gateway = FakeGatewayEmissaoSefaz.Autorizando();
        var transmissor = new TransmitirDocumentoFiscalUseCase(documentos, bus, gateway);
        var useCase = new RetransmitirDocumentosPendentesUseCase(documentos, transmissor, new DesistirDeNumeroUseCase(documentos, bus));

        var resolvidos = await useCase.ExecutarAsync(TenantId, TimeSpan.FromHours(1));

        Assert.Equal(0, resolvidos);
        Assert.Equal(0, gateway.Chamadas);
    }
}
