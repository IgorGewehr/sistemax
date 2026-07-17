namespace SistemaX.Modules.Fiscal.Infrastructure.Sefaz.Mapeamento;

// Payloads JSON do gateway de emissão (não o XML da NF-e) — schema-alvo confirmado em
// docs/fiscal/emissao-mapping.md §4/§5, espelhando saas-erp/lib/services/sefaz-gateway.ts.
// Nomes de propriedade em PascalCase (idiomático em C#) — a serialização usa
// JsonNamingPolicy.CamelCase (ver SefazApiGateway.JsonOpts) para virar `cnpj`/`razaoSocial`/etc
// no wire, exatamente como o gateway espera. Serializados com
// JsonIgnoreCondition.WhenWritingNull — nenhuma chave aqui precisa de lógica manual de omissão,
// satisfaz o invariante §10 "nunca enviar null literal" automaticamente.

internal sealed record EnderecoPayload(
    string Logradouro, string Numero, string? Complemento, string Bairro,
    string CodigoMunicipio, string Municipio, string Cep, string? Uf = null);

internal sealed record EmitentePayload(
    string Cnpj, string RazaoSocial, string? NomeFantasia, string InscricaoEstadual,
    string? InscricaoMunicipal, string Crt, EnderecoPayload Endereco, string? Telefone);

internal sealed record DestinatarioPayload(
    string? Cnpj, string? Cpf, string Nome, string? Email, string? InscricaoEstadual,
    string IndicadorIE, EnderecoPayload? Endereco);

internal sealed record ConsumidorNfcePayload(string Cpf, string? Nome);

internal sealed record IcmsPayload(
    string Orig, string? Csosn, string? Cst, decimal? ModBC, decimal? ValorBC,
    decimal? Aliquota, decimal? Valor, decimal? PercentualReducaoBC, decimal? Mva);

internal sealed record IcmsStPayload(decimal BaseCalculoST, decimal AliquotaST, decimal ValorST);

internal sealed record IcmsUfDestPayload(
    decimal VBCUFDest, decimal? PFCPUFDest, decimal? VFCPUFDest, decimal PICMSUFDest,
    decimal PICMSInter, decimal VICMSUFDest, decimal VICMSUFRemet);

internal sealed record IpiPayload(string Cst, decimal? BaseCalculo, decimal? Aliquota, decimal? Valor, string CEnq);

internal sealed record PisCofinsPayload(string Cst, decimal? ValorBC, decimal? Aliquota, decimal? Valor);

internal sealed record ImpostoPayload(
    IcmsPayload Icms, IcmsStPayload? IcmsSt, IcmsUfDestPayload? IcmsUFDest,
    IpiPayload? Ipi, PisCofinsPayload? Pis, PisCofinsPayload? Cofins);

internal sealed record ItemPayload(
    int Numero, string Codigo, string CEAN, string Descricao, string Ncm, string? Cest,
    string Cfop, string Unidade, decimal Quantidade, decimal ValorUnitario, decimal ValorTotal,
    decimal? ValorDesconto, string UnidadeTrib, decimal QuantidadeTrib, decimal ValorUnitarioTrib,
    string CEANTrib, string IndTot, ImpostoPayload Imposto);

internal sealed record CartaoPayload(string TipoIntegracao);

internal sealed record FormaPagamentoPayload(string TPag, decimal Valor, CartaoPayload? Cartao, string? Descricao);

internal sealed record PagamentoPayload(IReadOnlyList<FormaPagamentoPayload> Formas, string IndicadorPagamento);

internal sealed record TransportePayload(string ModFrete);

internal sealed record ReferenciasPayload(string? RefNFe);

internal sealed record CertificadoPayload(string PfxBase64, string Password);

internal sealed record NFePayload(
    EmitentePayload Emitente, long Numero, string Serie, string UfEmitente, string Ambiente,
    string NaturezaOperacao, string TipoOperacao, string Finalidade, string ConsumidorFinal,
    string PresencaComprador, DestinatarioPayload? Destinatario, IReadOnlyList<ItemPayload> Produtos,
    PagamentoPayload? Pagamento, TransportePayload Transporte, ReferenciasPayload? Referencias,
    string? InformacoesAdicionais, CertificadoPayload Certificado);

internal sealed record NFCePayload(
    EmitentePayload Emitente, long Numero, string Serie, string UfEmitente, string Ambiente,
    string NaturezaOperacao, string TipoOperacao, string Finalidade, string ConsumidorFinal,
    string PresencaComprador, ConsumidorNfcePayload? Consumidor, IReadOnlyList<ItemPayload> Produtos,
    PagamentoPayload? Pagamento, TransportePayload Transporte, string? Csc, string? InformacoesAdicionais,
    CertificadoPayload Certificado);

internal sealed record PrestadorPayload(
    string Cnpj, string? InscricaoMunicipal, string Nome, string? NomeFantasia, string SimplesNacional);

internal sealed record TomadorEnderecoPayload(
    string? Logradouro, string? Numero, string? Complemento, string? Bairro,
    string? Municipio, string? CodigoMunicipio, string? Uf, string? Cep);

internal sealed record TomadorPayload(
    string Nome, string? Cpf, string? Cnpj, string? Email, string? Telefone, TomadorEnderecoPayload? Endereco);

internal sealed record LocalPrestacaoPayload(string CodigoMunicipio);

internal sealed record ServicoPayload(
    string CodigoTributacaoNacional, string? CodigoTributacaoMunicipal, string Discriminacao,
    LocalPrestacaoPayload? LocalPrestacao, string? Nbs, string? Cnae);

internal sealed record ValoresPayload(
    decimal ValorServicos, decimal? ValorDeducoes, decimal? ValorDescontoCondicionado, decimal? ValorDescontoIncondicionado);

internal sealed record IssqnPayload(
    string TipoRetencaoISSQN, decimal BaseCalculo, decimal Aliquota, decimal ValorISS, decimal? ValorISSRetido);

internal sealed record NFSePayload(
    long NumeroDPS, string Serie, string CodigoMunicipioEmissao, PrestadorPayload Prestador,
    TomadorPayload? Tomador, ServicoPayload Servico, ValoresPayload Valores, IssqnPayload Issqn,
    string? InformacoesComplementares, string Ambiente, CertificadoPayload Certificado);
