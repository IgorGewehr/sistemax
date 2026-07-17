using System.Net;
using System.Net.Http.Json;

namespace SistemaX.Modules.Abstractions.Tests.Autorizacao;

/// <summary>
/// Prova de ponta a ponta (HTTP real, rotas de produção) do achado de auditoria: ANTES de
/// <c>.RequerPermissao(...)</c>, qualquer papel autenticado chamava qualquer rota. Estes testes
/// batem contra os MESMOS <c>*EndpointsModule</c> que o Bridge real mapeia — se algum dia alguém
/// remover um <c>.RequerPermissao(...)</c> de uma rota coberta aqui, um destes testes falha.
/// </summary>
public sealed class RequerPermissaoHttpTests : IClassFixture<PermissaoHttpFixture>
{
    private readonly PermissaoHttpFixture _fixture;

    public RequerPermissaoHttpTests(PermissaoHttpFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PostVendas_ComoViewer_Recebe403_SemPermissaoDeEditar()
    {
        using var request = _fixture.Requisicao(HttpMethod.Post, "/api/vendas", "viewer");
        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<ErroDeFio>();
        Assert.Equal("auth.sem_permissao", corpo!.Codigo);
    }

    [Fact]
    public async Task PostVendas_ComoOperator_Passa_TemPermissaoDeEditar()
    {
        using var request = _fixture.Requisicao(HttpMethod.Post, "/api/vendas", "operator");
        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task PostEstoqueProdutos_ComoViewer_Recebe403_SemPermissaoDeEditar()
    {
        using var request = _fixture.Requisicao(HttpMethod.Post, "/api/estoque/produtos", "viewer");
        request.Content = JsonContent.Create(new { nome = "Parafuso", unidade = "UN" });

        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<ErroDeFio>();
        Assert.Equal("auth.sem_permissao", corpo!.Codigo);
    }

    [Fact]
    public async Task PostEstoqueProdutos_ComoManager_Passa_TemPermissaoDeEditar()
    {
        using var request = _fixture.Requisicao(HttpMethod.Post, "/api/estoque/produtos", "manager");
        request.Content = JsonContent.Create(new { nome = "Parafuso", unidade = "UN" });

        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task GetEstoqueSaldos_ComoViewer_Passa_TodoPapelTemVerDeEstoque()
    {
        using var request = _fixture.Requisicao(HttpMethod.Get, "/api/estoque/saldos", "viewer");
        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task GetFinanceiroReceitaRecorrente_ComoOperator_Recebe403_OperatorNaoTemFinanceiro()
    {
        using var request = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/receita-recorrente", "operator");
        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<ErroDeFio>();
        Assert.Equal("auth.sem_permissao", corpo!.Codigo);
    }

    [Fact]
    public async Task GetFinanceiroReceitaRecorrente_ComoManager_Passa_TemFinanceiroVer()
    {
        using var request = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/receita-recorrente", "manager");
        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task GetFinanceiroConsultor_ComoOperator_Recebe403_OperatorNaoTemFinanceiro()
    {
        using var request = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/consultor", "operator");
        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<ErroDeFio>();
        Assert.Equal("auth.sem_permissao", corpo!.Codigo);
    }

    [Fact]
    public async Task GetFinanceiroConsultor_ComoManager_Passa_TemFinanceiroVer()
    {
        using var request = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/consultor", "manager");
        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task GetFinanceiroContasBancarias_ComoOperator_Recebe403_OperatorNaoTemFinanceiro()
    {
        using var request = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/contas-bancarias", "operator");
        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<ErroDeFio>();
        Assert.Equal("auth.sem_permissao", corpo!.Codigo);
    }

    [Fact]
    public async Task GetFinanceiroContasBancarias_ComoManager_Passa_TemFinanceiroVer()
    {
        using var request = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/contas-bancarias", "manager");
        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task GetFinanceiroFormasPagamento_ComoOperator_Recebe403_OperatorNaoTemFinanceiro()
    {
        using var request = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/formas-pagamento", "operator");
        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<ErroDeFio>();
        Assert.Equal("auth.sem_permissao", corpo!.Codigo);
    }

    [Fact]
    public async Task GetFinanceiroFormasPagamento_ComoManager_Passa_TemFinanceiroVer()
    {
        using var request = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/formas-pagamento", "manager");
        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task GetEstoqueSaldos_SemPapelNaSessao_Recebe403_FalhaFechado()
    {
        using var request = _fixture.Requisicao(HttpMethod.Get, "/api/estoque/saldos", papel: null);
        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<ErroDeFio>();
        Assert.Equal("auth.papel_desconhecido", corpo!.Codigo);
    }

    private sealed record ErroDeFio(string Codigo, string Mensagem);
}
