using SistemaX.SharedKernel;
using SistemaX.Verticals.Assistencia;

namespace SistemaX.Verticals.Assistencia.Tests;

/// <summary>Atalhos comuns aos testes de <see cref="OrdemDeServico"/> — evita repetir os ~7
/// passos da FSM (abrir → diagnóstico → orçamento → aprovação → execução → pronta → entrega)
/// em toda classe de teste. Cada método avança um instante de tempo fixo e determinístico
/// (nunca <c>DateTimeOffset.UtcNow</c>) para manter os testes reprodutíveis.</summary>
internal static class OrdemDeServicoTestBuilder
{
    public const string TenantId = "tenant-1";
    public static readonly DateTimeOffset Abertura = new(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);

    public static ClienteRef Cliente(string id = "cliente-1", string nome = "Pedro Lima", string? telefone = "(11) 98877-1234")
        => new(id, nome, telefone);

    public static Equipamento Equipamento(string? senhaAcesso = "1234", string? numeroSerie = "X1234")
        => new("Console", "Sony", "PS5 CFI-1214A", numeroSerie, senhaAcesso, "1 controle", "risco na tampa superior");

    public static OrdemDeServico AbrirNova(
        string numero = "OS-0001", DateTimeOffset? previsaoEntrega = null, string? osOrigemId = null)
        => OrdemDeServico.Abrir(
            TenantId, numero, Cliente(), Equipamento(), "desliga sozinho após 10 min de jogo",
            Abertura, previsaoEntrega, osOrigemId);

    public static OrdemDeServico AteEmDiagnostico(
        string diagnostico = "Pasta térmica ressecada + fonte com capacitor estufado.",
        string tecnicoId = "tecnico-1", string tecnicoNome = "Igor")
    {
        var os = AbrirNova();
        var atribuicao = os.AtribuirTecnico(tecnicoId, tecnicoNome);
        FalharSeErro(atribuicao);

        var resultado = os.RegistrarDiagnostico(diagnostico, Abertura.AddDays(1));
        FalharSeErro(resultado);
        return os;
    }

    public static readonly Money PrecoPecaOrcada = Money.DeReais(390);
    public static readonly Money MaoDeObraOrcada = Money.DeReais(120);
    public const string ProdutoIdPecaOrcada = "produto-fonte-1";
    public const string DescricaoPecaOrcada = "Fonte ADP-400DR";

    public static OrdemDeServico AteAguardandoAprovacao(int validadeDias = 10)
    {
        var os = AteEmDiagnostico();
        var pecas = new List<PecaOrcada> { PecaOrcada.Nova(ProdutoIdPecaOrcada, DescricaoPecaOrcada, 1, PrecoPecaOrcada) };

        var resultado = os.EnviarOrcamento(pecas, MaoDeObraOrcada, validadeDias, Abertura.AddDays(2));
        FalharSeErro(resultado);
        return os;
    }

    public static OrdemDeServico AteAprovada(CanalAprovacao canal = CanalAprovacao.WhatsApp)
    {
        var os = AteAguardandoAprovacao();
        var resultado = os.RegistrarAprovacao(canal, Abertura.AddDays(2).AddHours(3), "cliente-1", "Pedro Lima");
        FalharSeErro(resultado);
        return os;
    }

    public static OrdemDeServico AteEmExecucao()
    {
        var os = AteAprovada();
        var resultado = os.IniciarExecucao(Abertura.AddDays(3));
        FalharSeErro(resultado);
        return os;
    }

    /// <summary>Aplica a única peça orçada e conclui a execução — chega em
    /// <see cref="StatusOrdemServico.Pronta"/> com o orçamento inteiro consumido.</summary>
    public static OrdemDeServico AtePronta()
    {
        var os = AteEmExecucao();
        var linhaId = os.Orcamento!.Pecas.Single().LinhaId;

        FalharSeErro(os.AplicarPeca(linhaId, Abertura.AddDays(3).AddHours(1)));
        FalharSeErro(os.ConcluirExecucao(Abertura.AddDays(4)));
        return os;
    }

    public static OrdemDeServico AteEntregue(
        FormaPagamento formaPagamento = FormaPagamento.Pix, Money? desconto = null, int garantiaDias = 90)
    {
        var os = AtePronta();
        var resultado = os.Entregar(formaPagamento, desconto ?? Money.Zero, garantiaDias, Abertura.AddDays(5));
        FalharSeErro(resultado);
        return os;
    }

    private static void FalharSeErro(Result resultado)
    {
        if (resultado.Falha)
            throw new InvalidOperationException($"Falha ao montar cenário de teste: {resultado.Erro.Codigo} — {resultado.Erro.Mensagem}");
    }
}
