using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.Modules.Fiscal.Domain.Regimes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sefaz.Mapeamento;

/// <summary>
/// Insumos externos ao agregado fiscal necessários para montar o payload — cada um é um GAP
/// nomeado em docs/fiscal/emissao-mapping.md §11 (cadastral do emitente, cliente, pagamento,
/// certificado); nenhum é lido de <c>TributacaoProduto</c>/<c>PerfilFiscalNCM</c>/
/// <c>RegraFiscalPorOperacao</c> (§1 — o mapper é projeção pura, nunca resolução).
/// </summary>
internal sealed record InsumosMapeamento(
    CadastroFiscalEmitente Emitente,
    RegimeTributario Regime,
    string UfOrigem,
    string AmbienteSefaz,
    DestinatarioDocumentoFiscal? Destinatario,
    IReadOnlyList<FormaPagamentoParaEmitir> Pagamentos,
    CertificadoDigital? Certificado,
    string? Csc = null,
    string? RefNFeDevolucao = null,
    string? InformacoesAdicionais = null,
    /// <summary>Gap #6 (§11) — GTIN/unidade comercial por produto, chaveado por
    /// <c>ItemDocumentoFiscal.ProdutoId</c>. Ausência de entrada (ou campo nulo dentro dela) cai
    /// no fallback "SEM GTIN"/"UN" em <see cref="DocumentoFiscalPayloadMapper.MontarItens"/> —
    /// nunca lido de tabela de resolução tributária (§1), só da cópia cadastral local.</summary>
    IReadOnlyDictionary<string, DadosFiscaisProdutoCache>? DadosProdutoPorId = null);

/// <summary>
/// Projeção PURA <see cref="DocumentoFiscal"/> → payload JSON do gateway de emissão — nunca lê
/// tabela de resolução tributária (§1 de emissao-mapping.md). Todo campo cujo valor seria
/// null/zero-não-aplicável é omitido via <c>JsonIgnoreCondition.WhenWritingNull</c> na
/// serialização (não aqui) — este mapper só decide QUAL valor (ou null), nunca serializa.
///
/// GAP não numerado no mapeamento original: <see cref="DocumentoFiscal"/> não retém
/// <c>OperacaoFiscal.Tipo</c> (natureza da operação) após a resolução tributária — só
/// <c>ItemDocumentoFiscal.Cfop</c> sobrevive por item. <c>naturezaOperacao</c>/<c>tipoOperacao</c>/
/// <c>finalidade</c> abaixo assumem o caminho hoje realmente cablado (venda de balcão via
/// <c>VendaItensMovimentadosHandler</c>, sempre <c>VendaMercadoria</c>) — documentado aqui como
/// extensão necessária do agregado (ou parâmetro adicional de transmissão) no dia em que
/// devolução/transferência/comodato precisarem emitir de verdade.
/// </summary>
internal static class DocumentoFiscalPayloadMapper
{
    public static Result<NFePayload> MontarNFe(DocumentoFiscal documento, InsumosMapeamento insumos)
    {
        var itensResult = MontarItens(documento, insumos);
        if (itensResult.Falha) return Result.Falhar<NFePayload>(itensResult.Erro);

        var pagamento = MontarPagamento(insumos.Pagamentos);
        var payload = new NFePayload(
            Emitente: MontarEmitente(insumos.Emitente, insumos.Regime),
            Numero: documento.Numero!.Value,
            Serie: documento.Serie!,
            UfEmitente: insumos.UfOrigem,
            Ambiente: insumos.AmbienteSefaz,
            NaturezaOperacao: "VENDA DE MERCADORIA",
            TipoOperacao: "1",
            Finalidade: "1",
            ConsumidorFinal: "1",
            PresencaComprador: "9",
            Destinatario: MontarDestinatario(insumos.Destinatario),
            Produtos: itensResult.Valor,
            Pagamento: pagamento,
            Transporte: new TransportePayload(ModFrete: "9"),
            Referencias: insumos.RefNFeDevolucao is not null ? new ReferenciasPayload(insumos.RefNFeDevolucao) : null,
            InformacoesAdicionais: insumos.InformacoesAdicionais,
            Certificado: MontarCertificado(insumos.Certificado));

        return Result.Ok(payload);
    }

    public static Result<NFCePayload> MontarNFCe(DocumentoFiscal documento, InsumosMapeamento insumos)
    {
        var itensResult = MontarItens(documento, insumos);
        if (itensResult.Falha) return Result.Falhar<NFCePayload>(itensResult.Erro);

        ConsumidorNfcePayload? consumidor = insumos.Destinatario is { Cpf: { Length: > 0 } cpf }
            ? new ConsumidorNfcePayload(cpf, insumos.Destinatario.Nome)
            : null;

        var payload = new NFCePayload(
            Emitente: MontarEmitente(insumos.Emitente, insumos.Regime),
            Numero: documento.Numero!.Value,
            Serie: documento.Serie!,
            UfEmitente: insumos.UfOrigem,
            Ambiente: insumos.AmbienteSefaz,
            NaturezaOperacao: "VENDA AO CONSUMIDOR FINAL",
            TipoOperacao: "1",
            Finalidade: "1",
            ConsumidorFinal: "1",
            PresencaComprador: "1",
            Consumidor: consumidor,
            Produtos: itensResult.Valor,
            Pagamento: MontarPagamento(insumos.Pagamentos),
            Transporte: new TransportePayload(ModFrete: "9"),
            Csc: insumos.Csc,
            InformacoesAdicionais: insumos.InformacoesAdicionais,
            Certificado: MontarCertificado(insumos.Certificado));

        return Result.Ok(payload);
    }

    /// <summary>Rascunho — ISS fora de escopo hoje (nenhum motor de cálculo semeado, §5 de
    /// emissao-mapping.md). Monta o envelope com o que já existe no domínio; blocos que dependem
    /// do motor de ISS (ainda inexistente) usam zero/omissão explícita, nunca valor
    /// inventado.</summary>
    public static Result<NFSePayload> MontarNFSe(DocumentoFiscal documento, InsumosMapeamento insumos)
    {
        var iss = documento.Itens
            .SelectMany(i => i.Tributos)
            .FirstOrDefault(t => t.Tipo == TipoTributo.Iss);

        var payload = new NFSePayload(
            NumeroDPS: documento.Numero!.Value,
            Serie: documento.Serie!,
            CodigoMunicipioEmissao: insumos.Emitente.CodigoMunicipio,
            Prestador: new PrestadorPayload(
                Cnpj: SomenteDigitos(insumos.Emitente.Cnpj),
                InscricaoMunicipal: insumos.Emitente.InscricaoMunicipal,
                Nome: insumos.Emitente.RazaoSocial,
                NomeFantasia: insumos.Emitente.NomeFantasia,
                SimplesNacional: insumos.Regime.UsaCsosn() ? "1" : "2"),
            Tomador: MontarTomador(insumos.Destinatario),
            Servico: new ServicoPayload(
                CodigoTributacaoNacional: "0",
                CodigoTributacaoMunicipal: null,
                Discriminacao: string.Join("; ", documento.Itens.Select(i => i.Descricao)),
                LocalPrestacao: new LocalPrestacaoPayload(insumos.Emitente.CodigoMunicipio),
                Nbs: null,
                Cnae: null),
            Valores: new ValoresPayload(
                ValorServicos: documento.Total.EmReais,
                ValorDeducoes: null, ValorDescontoCondicionado: null, ValorDescontoIncondicionado: null),
            Issqn: new IssqnPayload(
                TipoRetencaoISSQN: "1",
                BaseCalculo: iss?.Base.EmReais ?? documento.Total.EmReais,
                Aliquota: iss is not null ? iss.Aliquota.EmFracao * 100m : 0m,
                ValorISS: iss?.Valor.EmReais ?? 0m,
                ValorISSRetido: null),
            InformacoesComplementares: insumos.InformacoesAdicionais,
            Ambiente: insumos.AmbienteSefaz,
            Certificado: MontarCertificado(insumos.Certificado));

        return Result.Ok(payload);
    }

    // ------------------------------------------------------------------ envelope / emitente

    private static EmitentePayload MontarEmitente(CadastroFiscalEmitente emitente, RegimeTributario regime) =>
        new(
            Cnpj: SomenteDigitos(emitente.Cnpj),
            RazaoSocial: emitente.RazaoSocial,
            NomeFantasia: emitente.NomeFantasia,
            InscricaoEstadual: emitente.InscricaoEstadual,
            InscricaoMunicipal: emitente.InscricaoMunicipal,
            Crt: regime.Crt(),
            Endereco: new EnderecoPayload(
                emitente.Logradouro, emitente.Numero, emitente.Complemento, emitente.Bairro,
                emitente.CodigoMunicipio, emitente.Municipio, emitente.Cep),
            Telefone: emitente.Telefone);

    private static DestinatarioPayload? MontarDestinatario(DestinatarioDocumentoFiscal? destinatario)
    {
        if (destinatario is null) return null;

        var cnpj = destinatario.Cnpj is { Length: 14 } ? SomenteDigitos(destinatario.Cnpj) : null;
        var cpf = cnpj is null && destinatario.Cpf is { Length: 11 } ? SomenteDigitos(destinatario.Cpf) : null;

        return new DestinatarioPayload(
            Cnpj: cnpj, Cpf: cpf, Nome: destinatario.Nome, Email: destinatario.Email,
            InscricaoEstadual: destinatario.InscricaoEstadual,
            IndicadorIE: string.IsNullOrWhiteSpace(destinatario.InscricaoEstadual) ? "9" : "1",
            Endereco: destinatario.Endereco is { } e
                ? new EnderecoPayload(e.Logradouro, e.Numero, e.Complemento, e.Bairro, e.CodigoMunicipio, e.Municipio, e.Cep, e.Uf)
                : null);
    }

    private static TomadorPayload? MontarTomador(DestinatarioDocumentoFiscal? destinatario)
    {
        if (destinatario is null) return null;
        return new TomadorPayload(
            Nome: destinatario.Nome,
            Cpf: destinatario.Cpf, Cnpj: destinatario.Cnpj, Email: destinatario.Email, Telefone: null,
            Endereco: destinatario.Endereco is { } e
                ? new TomadorEnderecoPayload(e.Logradouro, e.Numero, e.Complemento, e.Bairro, e.Municipio, e.CodigoMunicipio, e.Uf, e.Cep)
                : null);
    }

    private static CertificadoPayload MontarCertificado(CertificadoDigital? certificado) =>
        new(certificado?.PfxBase64 ?? string.Empty, certificado?.Senha ?? string.Empty);

    // ------------------------------------------------------------------ itens (§4.3/§4.4)

    private const string SemGtinFallback = "SEM GTIN";
    private const string UnidadeComercialFallback = "UN";

    private static Result<IReadOnlyList<ItemPayload>> MontarItens(DocumentoFiscal documento, InsumosMapeamento insumos)
    {
        var itens = new List<ItemPayload>(documento.Itens.Count);
        for (var i = 0; i < documento.Itens.Count; i++)
        {
            var item = documento.Itens[i];
            var impostoResult = MontarImposto(item, insumos.Regime);
            if (impostoResult.Falha) return Result.Falhar<IReadOnlyList<ItemPayload>>(impostoResult.Erro);

            var valorTotalBruto = Money.DeReais(item.PrecoUnitario.EmReais * item.Quantidade.EmDecimal);

            // Gap #6 (§11) — GTIN/unidade comercial vêm da cópia cadastral local do produto
            // (DadosFiscaisProdutoCache), nunca de tabela de resolução tributária (§1). Cai no
            // fallback "SEM GTIN"/"UN" quando o produto não tem cadastro comercial ainda (mesmo
            // default do saas-erp).
            var cache = insumos.DadosProdutoPorId?.GetValueOrDefault(item.ProdutoId);
            var cean = string.IsNullOrWhiteSpace(cache?.Gtin) ? SemGtinFallback : cache.Gtin;
            var unidade = string.IsNullOrWhiteSpace(cache?.UnidadeComercial) ? UnidadeComercialFallback : cache.UnidadeComercial;

            itens.Add(new ItemPayload(
                Numero: i + 1,
                Codigo: item.ProdutoId,
                CEAN: cean,
                Descricao: item.Descricao,
                Ncm: item.Ncm,
                Cest: item.Cest,
                Cfop: item.Cfop,
                Unidade: unidade,
                Quantidade: item.Quantidade.EmDecimal,
                ValorUnitario: item.PrecoUnitario.EmReais,
                ValorTotal: valorTotalBruto.EmReais,
                ValorDesconto: item.Desconto.EhZero ? null : item.Desconto.EmReais,
                UnidadeTrib: unidade,
                QuantidadeTrib: item.Quantidade.EmDecimal,
                ValorUnitarioTrib: item.PrecoUnitario.EmReais,
                CEANTrib: cean,
                IndTot: "1",
                Imposto: impostoResult.Valor));
        }

        return Result.Ok<IReadOnlyList<ItemPayload>>(itens);
    }

    private static Result<ImpostoPayload> MontarImposto(ItemDocumentoFiscal item, RegimeTributario regime)
    {
        var porTipo = item.Tributos.ToDictionary(t => t.Tipo);

        if (!porTipo.TryGetValue(TipoTributo.Icms, out var icmsResolvido))
            return Result.Falhar<ImpostoPayload>(new Error(
                "fiscal.emissao.icms_ausente", $"Item '{item.ProdutoId}' sem ICMS resolvido — não deveria chegar em NumeroAlocado (invariante de AdicionarItemResolvido)."));

        var icms = MontarIcms(icmsResolvido, item.Origem, regime);
        var icmsSt = porTipo.TryGetValue(TipoTributo.IcmsSt, out var st) ? MontarIcmsSt(st) : null;
        var icmsUfDest = MontarIcmsUfDest(porTipo, icmsResolvido);
        var ipi = porTipo.TryGetValue(TipoTributo.Ipi, out var ipiResolvido) ? MontarIpi(ipiResolvido) : null;
        var pis = porTipo.TryGetValue(TipoTributo.Pis, out var pisResolvido) ? MontarPisCofins(pisResolvido) : null;
        var cofins = porTipo.TryGetValue(TipoTributo.Cofins, out var cofinsResolvido) ? MontarPisCofins(cofinsResolvido) : null;

        return Result.Ok(new ImpostoPayload(icms, icmsSt, icmsUfDest, ipi, pis, cofins));
    }

    /// <summary>csosn/cst nunca decide pela FORMA do valor de <see cref="TributoResolvidoItem.SituacaoTributaria"/>
    /// — a chave JSON usada é sempre a de <see cref="RegimeTributarioExtensions.UsaCsosn"/> (§4.4,
    /// checklist §10), nunca uma inspeção do próprio código.</summary>
    private static IcmsPayload MontarIcms(TributoResolvidoItem icms, OrigemMercadoria origem, RegimeTributario regime)
    {
        var usaCsosn = regime.UsaCsosn();
        return new IcmsPayload(
            Orig: ((int)origem).ToString(),
            Csosn: usaCsosn ? icms.SituacaoTributaria : null,
            Cst: usaCsosn ? null : icms.SituacaoTributaria,
            ModBC: usaCsosn ? null : icms.Base.EmReais,
            ValorBC: usaCsosn ? null : icms.Base.EmReais,
            Aliquota: usaCsosn ? null : icms.Aliquota.EmFracao * 100m,
            Valor: usaCsosn ? null : icms.Valor.EmReais,
            PercentualReducaoBC: icms.ReducaoBaseCalculo?.EmFracao * 100m,
            Mva: icms.Mva?.EmFracao * 100m);
    }

    private static IcmsStPayload MontarIcmsSt(TributoResolvidoItem st) =>
        new(st.Base.EmReais, st.Aliquota.EmFracao * 100m, st.Valor.EmReais);

    /// <summary>Fecha o gap #7 de emissao-mapping.md §4.4/§11 — <c>pICMSInter</c> é a alíquota
    /// interestadual EFETIVAMENTE usada no ICMS de origem, que já está gravada no próprio
    /// <paramref name="icms"/> resolvido (<c>MotorDeCalculoTributario</c> já decidiu 4%/7%/12%
    /// conforme <c>OrigemMercadoria</c>/UF antes do agregado existir — §1: mapper nunca decide,
    /// só projeta). Nunca um valor hardcoded — reaproveita o MESMO <see cref="TributoResolvidoItem"/>
    /// que já alimenta o bloco <c>imposto.icms</c>.</summary>
    private static IcmsUfDestPayload? MontarIcmsUfDest(IReadOnlyDictionary<TipoTributo, TributoResolvidoItem> porTipo, TributoResolvidoItem icms)
    {
        if (!porTipo.TryGetValue(TipoTributo.IcmsDifal, out var difal)) return null;
        porTipo.TryGetValue(TipoTributo.Fcp, out var fcp);

        return new IcmsUfDestPayload(
            VBCUFDest: difal.Base.EmReais,
            PFCPUFDest: fcp is not null ? fcp.Aliquota.EmFracao * 100m : null,
            VFCPUFDest: fcp is not null ? fcp.Valor.EmReais : null,
            PICMSUFDest: difal.Aliquota.EmFracao * 100m,
            PICMSInter: icms.Aliquota.EmFracao * 100m,
            VICMSUFDest: difal.Valor.EmReais,
            VICMSUFRemet: 0m); // partilha do remetente é 0% desde 2019 (Convênio 93/2015)
    }

    private static IpiPayload MontarIpi(TributoResolvidoItem ipi) =>
        new(
            Cst: ipi.SituacaoTributaria ?? "99",
            BaseCalculo: ipi.Aliquota.EmFracao > 0 ? ipi.Base.EmReais : null,
            Aliquota: ipi.Aliquota.EmFracao > 0 ? ipi.Aliquota.EmFracao * 100m : null,
            Valor: ipi.Aliquota.EmFracao > 0 ? ipi.Valor.EmReais : null,
            CEnq: "999"); // gap — código de enquadramento IPI não existe em PerfilFiscalNCM hoje

    private static PisCofinsPayload MontarPisCofins(TributoResolvidoItem tributo) =>
        new(
            Cst: tributo.SituacaoTributaria ?? "99",
            ValorBC: tributo.Aliquota.EmFracao > 0 ? tributo.Base.EmReais : null,
            Aliquota: tributo.Aliquota.EmFracao > 0 ? tributo.Aliquota.EmFracao * 100m : null,
            Valor: tributo.Aliquota.EmFracao > 0 ? tributo.Valor.EmReais : null);

    // ------------------------------------------------------------------ pagamento (§4.5)

    private static PagamentoPayload? MontarPagamento(IReadOnlyList<FormaPagamentoParaEmitir> pagamentos)
    {
        if (pagamentos.Count == 0) return null; // gap #3 — sem forma vinculada ainda

        var formas = pagamentos.Select(p =>
        {
            var codigo = CodigoSefazDoMetodo(p.Metodo);
            var precisaCartao = codigo is "03" or "04" or "17";
            return new FormaPagamentoPayload(
                TPag: codigo,
                Valor: p.Valor.EmReais,
                Cartao: precisaCartao ? new CartaoPayload(TipoIntegracao: "2") : null,
                Descricao: codigo == "99" ? p.Metodo : null);
        }).ToList();

        return new PagamentoPayload(Formas: formas, IndicadorPagamento: "0");
    }

    /// <summary>Tabela `tPag` — reaproveitada 1:1 de `lib/fiscal/number-sequence.ts:getPaymentCode`
    /// do saas-erp (já testada em produção), emissao-mapping.md §4.5.</summary>
    private static string CodigoSefazDoMetodo(string metodo) => metodo.Trim().ToLowerInvariant() switch
    {
        "dinheiro" => "01",
        "cheque" => "02",
        "credito" or "cartao_credito" => "03",
        "debito" or "cartao_debito" => "04",
        "credito_loja" or "fiado" => "05",
        "vale_alimentacao" => "10",
        "vale_refeicao" => "11",
        "vale_presente" or "gift_card" => "12",
        "vale_combustivel" => "13",
        "boleto" => "15",
        "deposito" => "16",
        "pix" => "17",
        "transferencia" => "18",
        "fidelidade" or "pontos" => "19",
        "sem_pagamento" => "90",
        _ => "99",
    };

    private static string SomenteDigitos(string valor) => new(valor.Where(char.IsDigit).ToArray());
}
