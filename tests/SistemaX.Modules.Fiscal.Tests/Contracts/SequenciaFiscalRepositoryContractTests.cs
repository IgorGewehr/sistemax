using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

/// <summary>Contract test do port <see cref="ISequenciaFiscalRepository"/> — roda 2× (InMemory +
/// SQLite), mesmo molde de <c>DocumentoFiscalRepositoryContractTests</c>. PRIORIDADE MÁXIMA
/// (docs/fiscal/arquitetura.md §5/§7): numeração fiscal é um fato jurídico, nunca pode duplicar
/// nem pular sem justificativa — os testes de concorrência abaixo são os que realmente provam a
/// invariante "nunca dois documentos com o mesmo número".</summary>
public abstract class SequenciaFiscalRepositoryContractTests
{
    protected const string TenantA = "tenant-a";
    protected const string TenantB = "tenant-b";

    protected abstract ISequenciaFiscalRepository CriarRepositorio();

    [Fact]
    public async Task Primeira_alocacao_de_uma_chave_nova_retorna_1()
    {
        var repo = CriarRepositorio();

        var numero = await repo.AlocarProximoAsync(TenantA, "65", "1");

        Assert.True(numero.Sucesso);
        Assert.Equal(1, numero.Valor);
    }

    [Fact]
    public async Task Alocacoes_sucessivas_da_mesma_chave_incrementam_sequencialmente_sem_lacuna()
    {
        var repo = CriarRepositorio();

        var primeiro = await repo.AlocarProximoAsync(TenantA, "65", "1");
        var segundo = await repo.AlocarProximoAsync(TenantA, "65", "1");
        var terceiro = await repo.AlocarProximoAsync(TenantA, "65", "1");

        Assert.Equal(1, primeiro.Valor);
        Assert.Equal(2, segundo.Valor);
        Assert.Equal(3, terceiro.Valor);
    }

    [Fact]
    public async Task Chaves_tenant_modelo_serie_diferentes_tem_contadores_independentes()
    {
        var repo = CriarRepositorio();

        var nfceTerminal1 = await repo.AlocarProximoAsync(TenantA, "65", "1");
        var nfceTerminal2 = await repo.AlocarProximoAsync(TenantA, "65", "2");
        var nfeLoja = await repo.AlocarProximoAsync(TenantA, "55", "1");
        var outroTenant = await repo.AlocarProximoAsync(TenantB, "65", "1");

        Assert.Equal(1, nfceTerminal1.Valor);
        Assert.Equal(1, nfceTerminal2.Valor);
        Assert.Equal(1, nfeLoja.Valor);
        Assert.Equal(1, outroTenant.Valor);

        // Repetir a alocação da MESMA chave agora avança para 2 — prova que o contador da chave
        // (tenant, modelo, série) não foi afetado pelas outras chaves alocadas acima.
        var nfceTerminal1DeNovo = await repo.AlocarProximoAsync(TenantA, "65", "1");
        Assert.Equal(2, nfceTerminal1DeNovo.Valor);
    }

    [Fact]
    public async Task Concorrencia_N_alocacoes_simultaneas_da_mesma_chave_produz_numeros_unicos_sequenciais_sem_lacuna()
    {
        const int quantidade = 30;
        var repo = CriarRepositorio();

        var tarefas = Enumerable.Range(0, quantidade)
            .Select(_ => repo.AlocarProximoAsync(TenantA, "65", "1"))
            .ToArray();
        var resultados = await Task.WhenAll(tarefas);

        Assert.All(resultados, r => Assert.True(r.Sucesso));

        var numeros = resultados.Select(r => r.Valor).ToArray();

        // Nunca duas alocações concorrentes retornam o MESMO número (nunca duplicata — é a
        // invariante legal: um número de NF-e/NFC-e não pode ser reaproveitado por dois
        // documentos).
        Assert.Equal(quantidade, numeros.Distinct().Count());

        // Gapless: o conjunto de números alocados é exatamente {1..quantidade}, nunca um "buraco"
        // criado por alocação perdida sob concorrência.
        var esperado = Enumerable.Range(1, quantidade).Select(i => (long)i).OrderBy(n => n);
        Assert.Equal(esperado, numeros.OrderBy(n => n));
    }

    [Fact]
    public async Task Concorrencia_de_chaves_diferentes_nao_interfere_uma_na_outra()
    {
        const int quantidade = 20;
        var repo = CriarRepositorio();

        var tarefasSerie1 = Enumerable.Range(0, quantidade).Select(_ => repo.AlocarProximoAsync(TenantA, "65", "1"));
        var tarefasSerie2 = Enumerable.Range(0, quantidade).Select(_ => repo.AlocarProximoAsync(TenantA, "65", "2"));

        var resultados = await Task.WhenAll(tarefasSerie1.Concat(tarefasSerie2));

        Assert.All(resultados, r => Assert.True(r.Sucesso));

        var numerosSerie1 = resultados.Take(quantidade).Select(r => r.Valor).OrderBy(n => n);
        var numerosSerie2 = resultados.Skip(quantidade).Select(r => r.Valor).OrderBy(n => n);
        var esperado = Enumerable.Range(1, quantidade).Select(i => (long)i);

        Assert.Equal(esperado, numerosSerie1);
        Assert.Equal(esperado, numerosSerie2);
    }
}
