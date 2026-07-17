namespace SistemaX.Verticals.Assistencia;

/// <summary>
/// Referência ao dono do equipamento, com snapshot de nome/telefone (mesmo padrão de
/// audit-field do resto do projeto: grava o nome junto do id para não fazer lookup na listagem
/// nem na impressão). Endereço, CPF e e-mail NÃO vivem aqui — ficam no cadastro do cliente;
/// a OS só referencia (regra de corte §3 do plano: "campo que não muda decisão não entra").
/// </summary>
public sealed record ClienteRef(string ClienteId, string Nome, string? Telefone = null);
