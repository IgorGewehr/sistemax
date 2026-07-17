namespace SistemaX.Modules.Abstractions.Autorizacao;

/// <summary>
/// RBAC do SERVIDOR — espelha <c>web/src/lib/permissions.ts</c> (a fonte de verdade original,
/// hoje só consumida pela UI). Até este arquivo existir, o Bridge não tinha NENHUMA checagem de
/// autorização por rota: qualquer sessão Bearer válida chamava qualquer endpoint de qualquer
/// módulo (achado de auditoria ALTA). Este arquivo + <see cref="PermissaoEndpointExtensions"/>
/// fecham essa lacuna.
///
/// SINCRONIA COM O FRONT — mantida por CONVENÇÃO, não por geração de código (mesma decisão já
/// registrada no ADR-0003 §4 para <c>PermissaoOverride</c>: duplicar é aceitável quando o custo de
/// gerar automaticamente > o custo de manter os dois em sincronia manualmente):
///   - <see cref="Papel"/>            ↔ <c>Papel</c>              (mesmos 5 valores, mesma hierarquia numérica)
///   - <see cref="Modulo"/>           ↔ <c>Modulo</c>              (mesmos 11 módulos)
///   - <see cref="Acao"/>             ↔ <c>Acao</c>                (mesmas 5 ações)
///   - <see cref="ModuloAcoes"/>      ↔ <c>MODULO_ACOES</c>        (mesmo grid de ações por módulo)
///   - <see cref="PermissoesPadraoPorPapel"/> ↔ <c>PAPEL_PERMISSOES_PADRAO</c> (mesmo mapa papel→permissões)
///
/// Ao adicionar um módulo/ação/mudar o padrão de um papel em <c>lib/permissions.ts</c>, replique
/// AQUI na mesma volta de PR — os dois lados descrevem a MESMA regra de negócio, só em runtimes
/// diferentes (servidor autoriza de verdade; front só decide o que mostrar).
///
/// O QUE ESTE ARQUIVO NÃO FAZ (fora de escopo, nomeado): overrides por usuário
/// (<c>PermissaoOverride</c>/<c>permissoesEfetivas</c> do front) — o servidor hoje não tem sessão
/// por PESSOA (ADR-0003 ainda não implementado, ver seu "Estado atual no repo"), só por
/// INSTALAÇÃO/papel. Quando `Identidade` existir, este arquivo ganha um `PermissoesEfetivas(Papel,
/// overrides)` espelhando a função pura do front; até lá, autorização é só o padrão do papel.
/// </summary>
public enum Papel
{
    Founder,
    Admin,
    Manager,
    Operator,
    Viewer,
}

/// <summary>Hierarquia numérica — maior número = mais permissão. Mesma convenção do
/// <c>ROLE_HIERARCHY</c>/<c>PapelHierarquia</c> do ecossistema (saas-erp, front deste repo). Só
/// resolve "quem administra quem" — acesso a módulo é SEMPRE por <see cref="PermissoesPadraoPorPapel"/>,
/// nunca por comparação de hierarquia (mesmo erro comum documentado no CLAUDE.md irmão).</summary>
public static class PapelHierarquia
{
    public static readonly IReadOnlyDictionary<Papel, int> Valores = new Dictionary<Papel, int>
    {
        [Papel.Founder] = 100,
        [Papel.Admin] = 80,
        [Papel.Manager] = 60,
        [Papel.Operator] = 40,
        [Papel.Viewer] = 20,
    };

    /// <summary>Só founder/admin administram usuários — regra de hierarquia, não permissão
    /// granular (espelha <c>podeAdministrarUsuarios</c> do front).</summary>
    public static bool PodeAdministrarUsuarios(Papel papel) => Valores[papel] >= Valores[Papel.Admin];
}

public enum Modulo
{
    Dashboard,
    Vendas,
    Pdv,
    Financeiro,
    Estoque,
    Compras,
    Ordens,
    Clientes,
    Agenda,
    Fiscal,
    Configuracoes,
}

public enum Acao
{
    Ver,
    Editar,
    OperarCaixa,
    EmitirFiscal,
    GerenciarUsuarios,
}

/// <summary>Par (Módulo, Ação) — o átomo de permissão do sistema (espelha o tipo <c>Permissao =
/// `${Modulo}:${Acao}`</c> do front; aqui é um struct em vez de string interpolada porque C# tem
/// enum, não union de string literal).</summary>
public readonly record struct Permissao(Modulo Modulo, Acao Acao)
{
    public override string ToString() => $"{Modulo}:{Acao}";
}

/// <summary>Quais ações existem em cada módulo — espelha <c>MODULO_ACOES</c>. A maioria só tem
/// Ver/Editar; ações extras só onde uma operação real e sensível já checa por elas.</summary>
public static class ModuloAcoes
{
    public static readonly IReadOnlyDictionary<Modulo, IReadOnlyList<Acao>> Mapa = new Dictionary<Modulo, IReadOnlyList<Acao>>
    {
        [Modulo.Dashboard] = [Acao.Ver],
        [Modulo.Vendas] = [Acao.Ver, Acao.Editar],
        [Modulo.Pdv] = [Acao.Ver, Acao.Editar, Acao.OperarCaixa],
        [Modulo.Financeiro] = [Acao.Ver, Acao.Editar],
        [Modulo.Estoque] = [Acao.Ver, Acao.Editar],
        [Modulo.Compras] = [Acao.Ver, Acao.Editar],
        [Modulo.Ordens] = [Acao.Ver, Acao.Editar],
        [Modulo.Clientes] = [Acao.Ver, Acao.Editar],
        [Modulo.Agenda] = [Acao.Ver, Acao.Editar],
        [Modulo.Fiscal] = [Acao.Ver, Acao.Editar, Acao.EmitirFiscal],
        [Modulo.Configuracoes] = [Acao.Ver, Acao.Editar, Acao.GerenciarUsuarios],
    };

    /// <summary>Todas as combinações válidas do sistema — base do conjunto founder/admin (espelha
    /// <c>todasPermissoes()</c>).</summary>
    public static IEnumerable<Permissao> Todas() =>
        Mapa.SelectMany(par => par.Value.Select(acao => new Permissao(par.Key, acao)));
}

/// <summary>
/// Conjunto PADRÃO de permissões por papel — espelha <c>PAPEL_PERMISSOES_PADRAO</c> literalmente
/// linha por linha. É a checagem GROSSA (sem overrides por pessoa, ver nota no topo do arquivo)
/// mas é a que fecha o buraco de autorização: sem isto, todo endpoint autenticado era acessível a
/// qualquer papel.
/// </summary>
public static class PermissoesPadraoPorPapel
{
    private static IReadOnlySet<Permissao> DePares(IEnumerable<(Modulo Modulo, Acao[] Acoes)> pares) =>
        pares.SelectMany(par => par.Acoes.Select(acao => new Permissao(par.Modulo, acao))).ToHashSet();

    public static readonly IReadOnlyDictionary<Papel, IReadOnlySet<Permissao>> Mapa = new Dictionary<Papel, IReadOnlySet<Permissao>>
    {
        [Papel.Founder] = ModuloAcoes.Todas().ToHashSet(),
        [Papel.Admin] = ModuloAcoes.Todas().ToHashSet(),

        [Papel.Manager] = DePares([
            (Modulo.Dashboard, [Acao.Ver]),
            (Modulo.Vendas, [Acao.Ver, Acao.Editar]),
            (Modulo.Pdv, [Acao.Ver, Acao.Editar, Acao.OperarCaixa]),
            (Modulo.Financeiro, [Acao.Ver]),
            (Modulo.Estoque, [Acao.Ver, Acao.Editar]),
            (Modulo.Compras, [Acao.Ver, Acao.Editar]),
            (Modulo.Ordens, [Acao.Ver, Acao.Editar]),
            (Modulo.Clientes, [Acao.Ver, Acao.Editar]),
            (Modulo.Agenda, [Acao.Ver, Acao.Editar]),
            (Modulo.Fiscal, [Acao.Ver]),
            (Modulo.Configuracoes, [Acao.Ver]),
        ]),

        [Papel.Operator] = DePares([
            (Modulo.Dashboard, [Acao.Ver]),
            (Modulo.Vendas, [Acao.Ver, Acao.Editar]),
            (Modulo.Pdv, [Acao.Ver, Acao.Editar, Acao.OperarCaixa]),
            (Modulo.Estoque, [Acao.Ver]),
            (Modulo.Ordens, [Acao.Ver, Acao.Editar]),
            (Modulo.Clientes, [Acao.Ver, Acao.Editar]),
            (Modulo.Agenda, [Acao.Ver, Acao.Editar]),
        ]),

        [Papel.Viewer] = DePares([
            (Modulo.Dashboard, [Acao.Ver]),
            (Modulo.Vendas, [Acao.Ver]),
            (Modulo.Estoque, [Acao.Ver]),
            (Modulo.Ordens, [Acao.Ver]),
            (Modulo.Clientes, [Acao.Ver]),
            (Modulo.Agenda, [Acao.Ver]),
        ]),
    };

    /// <summary><c>papel</c> tem a permissão módulo+ação, pelo padrão do papel (sem overrides —
    /// ver nota de escopo no topo do arquivo). Espelha <c>usuarioPode</c> do front.</summary>
    public static bool Tem(Papel papel, Modulo modulo, Acao acao) => Mapa[papel].Contains(new Permissao(modulo, acao));
}
