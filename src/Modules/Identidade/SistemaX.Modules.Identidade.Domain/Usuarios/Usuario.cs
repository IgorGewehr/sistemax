using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Identidade.Domain.Usuarios;

/// <summary>
/// Agregado raiz de usuário (ADR-0003) — a pessoa real que loga por PIN e opera sob um papel do
/// RBAC (<see cref="Papel"/>, de <c>Abstractions.Autorizacao</c> — não redeclarado aqui, mesma
/// hierarquia usada por <c>PermissoesPadraoPorPapel</c>/<c>RequerPermissao</c>). PIN nunca é
/// guardado em texto puro — só <see cref="PinHash"/>/<see cref="PinSalt"/> (PBKDF2, ver
/// <see cref="PinHasher"/>).
/// </summary>
public sealed class Usuario : AggregateRoot<string>
{
    private const int PinTamanhoMinimo = 4;
    private const int PinTamanhoMaximo = 8;

    public string BusinessId { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public Papel Papel { get; private set; }
    public StatusUsuario Status { get; private set; }
    public string PinHash { get; private set; } = string.Empty;
    public string PinSalt { get; private set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset? UltimoAcessoEm { get; private set; }

    /// <summary>Marca que este PIN foi atribuído pelo sistema (seed do founder, <c>PIN "1234"</c>
    /// de <c>IdentidadeBootstrapSeeder</c>) e ainda não foi escolhido pelo dono da instalação —
    /// o wizard de 1º-boot lê isto via <c>deveTrocarPin</c> no <c>POST /api/auth/login</c> para
    /// forçar a troca. Zerado por <see cref="RedefinirPin"/> — qualquer troca de PIN, própria
    /// (<c>POST /api/auth/trocar-pin</c>) ou administrativa (<c>PATCH /usuarios/{id}</c>), é
    /// considerada uma escolha real e encerra o estado provisório.</summary>
    public bool PinProvisorio { get; private set; }

    private Usuario()
    {
    }

    public static Result<Usuario> Criar(
        string businessId, string nome, string email, string pin, Papel papel, bool pinProvisorio = false)
    {
        if (string.IsNullOrWhiteSpace(businessId))
            return Result.Falhar<Usuario>(new Error("identidade.usuario.business_id_invalido", "BusinessId é obrigatório."));

        if (string.IsNullOrWhiteSpace(nome))
            return Result.Falhar<Usuario>(new Error("identidade.usuario.nome_invalido", "Nome é obrigatório."));

        if (string.IsNullOrWhiteSpace(email))
            return Result.Falhar<Usuario>(new Error("identidade.usuario.email_invalido", "E-mail/login é obrigatório."));

        var validacaoPin = ValidarFormatoPin(pin);
        if (validacaoPin.Falha) return Result.Falhar<Usuario>(validacaoPin.Erro);

        var (hash, salt) = PinHasher.Hash(pin);

        return Result.Ok(new Usuario
        {
            Id = IdGenerator.NovoId(),
            BusinessId = businessId,
            Nome = nome,
            Email = email,
            Papel = papel,
            Status = StatusUsuario.Ativo,
            PinHash = hash,
            PinSalt = salt,
            CriadoEm = DateTimeOffset.UtcNow,
            UltimoAcessoEm = null,
            PinProvisorio = pinProvisorio,
        });
    }

    /// <summary>
    /// REIDRATAÇÃO a partir do banco — usada só pela camada de persistência. Não valida nem
    /// levanta evento: reconstrói o estado exato que foi persistido (mesmo padrão de
    /// <c>Fornecedor.Reconstituir</c>/<c>Assinatura.Reconstituir</c>).
    /// </summary>
    public static Usuario Reconstituir(
        string id, string businessId, string nome, string email, Papel papel, StatusUsuario status,
        string pinHash, string pinSalt, DateTimeOffset criadoEm, DateTimeOffset? ultimoAcessoEm,
        bool pinProvisorio)
        => new()
        {
            Id = id,
            BusinessId = businessId,
            Nome = nome,
            Email = email,
            Papel = papel,
            Status = status,
            PinHash = pinHash,
            PinSalt = pinSalt,
            CriadoEm = criadoEm,
            UltimoAcessoEm = ultimoAcessoEm,
            PinProvisorio = pinProvisorio,
        };

    /// <summary>Verifica o PIN em texto puro contra o hash guardado (tempo constante, ver
    /// <see cref="PinHasher"/>). Nunca loga/lança em caso de PIN errado — só devolve <c>false</c>,
    /// deixando quem chama (<c>AutenticarPorPinUseCase</c>) decidir a mensagem genérica de erro.</summary>
    public bool VerificarPin(string pin) => PinHasher.Verificar(pin, PinHash, PinSalt);

    public void RegistrarAcesso(DateTimeOffset agora) => UltimoAcessoEm = agora;

    public Result TrocarPapel(Papel novoPapel)
    {
        Papel = novoPapel;
        return Result.Ok();
    }

    public Result RedefinirPin(string novoPin)
    {
        var validacao = ValidarFormatoPin(novoPin);
        if (validacao.Falha) return validacao;

        (PinHash, PinSalt) = PinHasher.Hash(novoPin);

        // Qualquer troca de PIN — própria ou administrativa — encerra o estado provisório do
        // seed (ver PinProvisorio). Não há caminho de "troca de PIN que não conta".
        PinProvisorio = false;
        return Result.Ok();
    }

    public Result Ativar() => Transicionar(StatusUsuario.Ativo);

    public Result Desativar() => Transicionar(StatusUsuario.Inativo);

    /// <summary>Só founder/admin administram usuários — espelha <c>podeAdministrarUsuarios</c> do
    /// front e <c>PapelHierarquia.PodeAdministrarUsuarios</c> do servidor (mesma regra, exposta
    /// aqui como método de instância por conveniência do caso de uso).</summary>
    public bool PodeAdministrarUsuarios() => PapelHierarquia.PodeAdministrarUsuarios(Papel);

    /// <summary>Founder nunca é rebaixado/desativado sem substituto — ver
    /// <c>AlterarUsuarioUseCase</c>, que consulta este método antes de aplicar qualquer mudança
    /// que reduziria a contagem de founders ativos a zero.</summary>
    public bool EhIntocavel() => Papel == Papel.Founder;

    private Result Transicionar(StatusUsuario destino)
    {
        var transicao = Fsm<StatusUsuario>.ValidarTransicao(Status, destino, TransicoesPermitidas);
        if (transicao.Falha) return transicao;

        Status = destino;
        return Result.Ok();
    }

    private static Result ValidarFormatoPin(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin) || pin.Length is < PinTamanhoMinimo or > PinTamanhoMaximo || !pin.All(char.IsDigit))
        {
            return Result.Falhar(new Error(
                "identidade.usuario.pin_invalido",
                $"PIN deve ter entre {PinTamanhoMinimo} e {PinTamanhoMaximo} dígitos numéricos."));
        }

        return Result.Ok();
    }

    private static readonly IReadOnlyDictionary<StatusUsuario, StatusUsuario[]> TransicoesPermitidas =
        new Dictionary<StatusUsuario, StatusUsuario[]>
        {
            [StatusUsuario.Ativo] = [StatusUsuario.Inativo],
            [StatusUsuario.Inativo] = [StatusUsuario.Ativo],
        };
}
