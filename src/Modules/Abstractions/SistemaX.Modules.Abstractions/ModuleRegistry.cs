using Microsoft.Extensions.DependencyInjection;

namespace SistemaX.Modules.Abstractions;

/// <summary>
/// Descoberta e boot de módulos. É a ÚNICA peça do Core que enumera <see cref="IModule"/> — e
/// mesmo ela nunca decide COMO um módulo se comporta, só ORDEM de registro.
///
/// Um host (Host.Desktop, Store.Server, Cloud.Api) monta a lista de módulos habilitados nesta
/// instalação (tipicamente lida de configuração — "quais verticais este cliente comprou") e
/// entrega para <see cref="RegistrarTodos"/>. Daí em diante o Core só fala com <see cref="IModule"/>:
/// não existe, em lugar nenhum do Core, um `switch` ou `if` sobre o código de um módulo
/// específico. Isso é a regra de ouro da arquitetura (ver docs/arquitetura/ARCHITECTURE.md §3).
///
/// Responsabilidades desta classe:
///  1. Impedir dois módulos com o mesmo <see cref="IModule.Codigo"/>.
///  2. Validar que toda dependência declarada em <see cref="IModule.DependeDe"/> está presente
///     entre os módulos adicionados — falha no BOOT (erro de configuração), nunca em runtime.
///  3. Ordenar topologicamente por dependência (Kahn/DFS) e chamar <see cref="IModule.Registrar"/>
///     nessa ordem, para que um módulo dependente nunca registre antes de quem ele depende.
/// </summary>
public sealed class ModuleRegistry
{
    private readonly List<IModule> _modulos = new();

    public IReadOnlyList<IModule> ModulosAdicionados => _modulos.AsReadOnly();

    /// <summary>Registra um módulo candidato. Não chama <see cref="IModule.Registrar"/> ainda —
    /// isso só acontece em <see cref="RegistrarTodos"/>, depois de validar o grafo inteiro.</summary>
    public ModuleRegistry Adicionar(IModule modulo)
    {
        ArgumentNullException.ThrowIfNull(modulo);

        if (_modulos.Any(m => m.Codigo == modulo.Codigo))
            throw new InvalidOperationException(
                $"Módulo duplicado: já existe um IModule registrado com Codigo='{modulo.Codigo}'.");

        _modulos.Add(modulo);
        return this;
    }

    /// <summary>
    /// Valida o grafo de dependências, ordena topologicamente e chama
    /// <see cref="IModule.Registrar"/> de cada módulo, na camada de execução do <paramref name="contexto"/>.
    /// Lança <see cref="InvalidOperationException"/> se houver dependência ausente ou ciclo —
    /// de propósito: um vertical mal configurado deve impedir o boot do processo, não degradar
    /// silenciosamente em runtime.
    /// </summary>
    public void RegistrarTodos(IServiceCollection services, IModuleContext contexto)
    {
        foreach (var modulo in OrdenarPorDependencia())
            modulo.Registrar(services, contexto);
    }

    private List<IModule> OrdenarPorDependencia()
    {
        var porCodigo = _modulos.ToDictionary(m => m.Codigo);

        foreach (var modulo in _modulos)
            foreach (var dependencia in modulo.DependeDe)
                if (!porCodigo.ContainsKey(dependencia))
                    throw new InvalidOperationException(
                        $"Módulo '{modulo.Codigo}' depende de '{dependencia}', que não está " +
                        "registrado nesta instalação. Habilite o módulo dependente ou remova a " +
                        "dependência.");

        var resolvidos = new List<IModule>(_modulos.Count);
        var concluidos = new HashSet<string>();
        var emVisita = new HashSet<string>();

        foreach (var modulo in _modulos)
            Visitar(modulo, porCodigo, concluidos, emVisita, resolvidos);

        return resolvidos;
    }

    private static void Visitar(
        IModule modulo,
        IReadOnlyDictionary<string, IModule> porCodigo,
        HashSet<string> concluidos,
        HashSet<string> emVisita,
        List<IModule> resolvidos)
    {
        if (concluidos.Contains(modulo.Codigo))
            return;

        if (!emVisita.Add(modulo.Codigo))
            throw new InvalidOperationException(
                $"Dependência cíclica entre módulos envolvendo '{modulo.Codigo}'.");

        foreach (var dependencia in modulo.DependeDe)
            Visitar(porCodigo[dependencia], porCodigo, concluidos, emVisita, resolvidos);

        emVisita.Remove(modulo.Codigo);
        concluidos.Add(modulo.Codigo);
        resolvidos.Add(modulo);
    }
}
