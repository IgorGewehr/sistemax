namespace SistemaX.Modules.Identidade.Domain.Usuarios;

/// <summary>Status do usuário — mesma convenção FSM de <c>StatusFornecedor</c>. Um usuário
/// <c>Inativo</c> nunca é candidato de <c>AutenticarPorPinUseCase</c> (só busca ativos) nem passa
/// por <see cref="BearerSessionMiddleware"/> caso o token tenha sido emitido antes da
/// desativação — ver decisão #3 do ADR-0003.</summary>
public enum StatusUsuario
{
    Ativo,
    Inativo,
}
