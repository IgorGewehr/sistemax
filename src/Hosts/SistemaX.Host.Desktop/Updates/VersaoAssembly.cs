using System.Reflection;

namespace SistemaX.Host.Desktop.Updates;

/// <summary>
/// Fonte ÚNICA da versão exposta em runtime (endpoint <c>/api/health</c>, item 3 do ADR-0004) —
/// lê o <c>AssemblyInformationalVersionAttribute</c>, que o SDK popula a partir da MESMA
/// propriedade MSBuild <c>-p:Version=</c> usada em <c>dotnet publish</c> (ver
/// docs/build/empacotamento.md §3/§6) e replicada em <c>vpk pack --packVersion</c> pelo script de
/// empacotamento — um único número, dois lugares que precisam bater (publish e pacote), nunca
/// hardcoded aqui.
/// </summary>
public static class VersaoAssembly
{
    public static string Atual { get; } = ObterVersao();

    private static string ObterVersao()
    {
        var versao = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        // "1.0.0" é o default do SDK quando NENHUM `-p:Version=` foi passado (dev/`dotnet run`,
        // testes) — reportar isso como "0.0.0-dev" no `/api/health` deixa óbvio que este boot não
        // veio de um publish versionado pelo pipeline de release (docs/build/empacotamento.md §6).
        if (string.IsNullOrWhiteSpace(versao) || versao == "1.0.0")
        {
            return "0.0.0-dev";
        }

        return versao;
    }
}
