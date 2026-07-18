using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v33 do módulo "financeiro" — Análise por Projeto (docs/financeiro/
/// design-analise-por-projeto.md §3.2/§7), fato_receita_diaria: coluna <c>projeto_id</c> NULLABLE
/// (ALTER simples, FORA da chave primária).
///
/// DECISÃO DESTA FATIA (Parte A) — o design (§3.2/§7, migrações V31/V32 indicativas) desenha a
/// versão FINAL como DROP+CREATE com <c>projeto_id</c> ENTRANDO NA CHAVE primária junto de
/// <c>corrente</c> (espelho byte-a-byte de V19/V20: rebuild + replay do <c>ProjectionRunner</c>) —
/// mas isso só faz sentido quando os FOLDS (<c>FatoReceitaDiariaProjection</c>) souberem escrever
/// um <c>ProjetoId</c> real, o que exige a dimensão trafegar pelos EVENTOS DE INTEGRAÇÃO que
/// alimentam a projeção (<c>VendaConcluida</c>/<c>OsFaturada</c>/<c>CobrancaDeAssinaturaGerada</c>/
/// ...) — plumbing que o design classifica explicitamente como fatia TARDIA (P5, "Projeto nas
/// fact tables"), não a fundação (P1) nem o painel v1 (P2) que esta Parte A cobre. Painel v1 lê
/// direto de <c>ContaAReceber</c>/<c>ContaAPagar</c>/<c>Assinatura</c> (mesma fonte do DRE), não
/// destas fact tables — ver <c>PainelDoProjetoService</c>.
///
/// Esta coluna nasce aqui só para não exigir OUTRA migração quando os folds ganharem o
/// plumbing: hoje ela é gravada como <c>NULL</c> por todo caminho de escrita existente (nenhum
/// código lê nem escreve nela ainda) — puramente aditiva, zero efeito no fold/replay atual.
/// </summary>
public sealed class FinanceiroSchemaMigrationV33 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 33;

    protected override string Sql =>
        """
        ALTER TABLE fato_receita_diaria ADD COLUMN projeto_id TEXT NULL;
        """;
}
