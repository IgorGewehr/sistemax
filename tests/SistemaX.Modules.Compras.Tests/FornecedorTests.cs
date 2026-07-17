using SistemaX.Modules.Compras.Application.CasosDeUso;
using SistemaX.Modules.Compras.Domain.Fornecedores;
using SistemaX.Modules.Compras.Infrastructure.InMemory;

namespace SistemaX.Modules.Compras.Tests;

public class FornecedorTests
{
    [Fact]
    public void Cadastrar_SemRazaoSocial_Falha()
    {
        var resultado = Fornecedor.Cadastrar(ComprasTestBuilder.TenantId, string.Empty);

        Assert.True(resultado.Falha);
        Assert.Equal("compras.fornecedor.razao_social_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void Cadastrar_ComDocumentoVazio_PermiteMultiplosFornecedoresSemDocumento()
    {
        // Lição real corrigida do gestao-raiz: dois fornecedores DISTINTOS sem CNPJ (produtor
        // rural) nunca podem ser fundidos só porque documento == "".
        var a = Fornecedor.Cadastrar(ComprasTestBuilder.TenantId, "Produtor Rural A").Valor;
        var b = Fornecedor.Cadastrar(ComprasTestBuilder.TenantId, "Produtor Rural B").Valor;

        Assert.NotEqual(a.Id, b.Id);
        Assert.Null(a.Documento);
        Assert.Null(b.Documento);
    }

    [Fact]
    public async Task CadastrarFornecedorUseCase_ComDocumentoJaExistente_RetornaOMesmoFornecedor()
    {
        var repositorio = new InMemoryFornecedorRepository();
        var cadastrar = new CadastrarFornecedorUseCase(repositorio);

        var primeiro = await cadastrar.ExecutarAsync(ComprasTestBuilder.TenantId, "Pescados Sul LTDA", "04312887000190");
        var segundo = await cadastrar.ExecutarAsync(ComprasTestBuilder.TenantId, "Pescados Sul LTDA (nome digitado diferente)", "04312887000190");

        Assert.Equal(primeiro.Valor.Id, segundo.Valor.Id);
    }

    [Fact]
    public async Task CadastrarFornecedorUseCase_ComDocumentoVazioDuasVezes_CriaDoisFornecedores()
    {
        var repositorio = new InMemoryFornecedorRepository();
        var cadastrar = new CadastrarFornecedorUseCase(repositorio);

        var primeiro = await cadastrar.ExecutarAsync(ComprasTestBuilder.TenantId, "Produtor A");
        var segundo = await cadastrar.ExecutarAsync(ComprasTestBuilder.TenantId, "Produtor B");

        Assert.NotEqual(primeiro.Valor.Id, segundo.Valor.Id);
    }

    [Fact]
    public void Bloquear_DeAtivo_TransicionaComSucesso()
    {
        var fornecedor = Fornecedor.Cadastrar(ComprasTestBuilder.TenantId, "Fornecedor Teste").Valor;

        var resultado = fornecedor.Bloquear();

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusFornecedor.Bloqueado, fornecedor.Status);
    }

    [Fact]
    public void Reativar_DeBloqueado_TransicionaComSucesso()
    {
        var fornecedor = Fornecedor.Cadastrar(ComprasTestBuilder.TenantId, "Fornecedor Teste").Valor;
        fornecedor.Bloquear();

        var resultado = fornecedor.Reativar();

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusFornecedor.Ativo, fornecedor.Status);
    }

    [Fact]
    public void Bloquear_DeInativo_Falha()
    {
        var fornecedor = Fornecedor.Cadastrar(ComprasTestBuilder.TenantId, "Fornecedor Teste").Valor;
        fornecedor.Inativar();

        var resultado = fornecedor.Bloquear();

        Assert.True(resultado.Falha);
        Assert.Equal("fsm.transicao_invalida", resultado.Erro.Codigo);
        Assert.Equal(StatusFornecedor.Inativo, fornecedor.Status); // não mudou
    }

    [Fact]
    public async Task GerenciarFornecedorUseCase_FornecedorInexistente_Falha()
    {
        var repositorio = new InMemoryFornecedorRepository();
        var gerenciar = new GerenciarFornecedorUseCase(repositorio);

        var resultado = await gerenciar.BloquearAsync("fornecedor-que-nao-existe");

        Assert.True(resultado.Falha);
        Assert.Equal("compras.fornecedor.nao_encontrado", resultado.Erro.Codigo);
    }
}
