/**
 * DTO (.NET, `Money`/camelCase) → fatias do view-model de Entradas & Saídas
 * (`components/financial/entradas-saidas/types.ts`), ver docs/wiring/financeiro-telas-restantes.md
 * §1 (task #33). Funções puras, zero React — mesmo padrão de `adapters/financeiro/bancario.ts`.
 */
import type { ContaDisponivel, LancamentoRow } from '@/components/financial/entradas-saidas/types';
import type { ContaBancariaDto, ExtratoLinhaDto } from '@/lib/api/financeiro';

/** "2026-07-16T00:00:00-03:00" ou "2026-07-16" → "2026-07-16". Nunca `new Date(iso)` — extração
 * textual, determinística (mesma diretriz de `adapters/financeiro/bancario.ts`). */
function isoData(iso: string): string {
  return iso.split('T')[0] ?? iso;
}

export function deExtratoLinhas(dtos: ExtratoLinhaDto[]): LancamentoRow[] {
  return dtos.map((l) => ({
    id: l.id,
    data: isoData(l.data),
    desc: l.descricao,
    sub: null,
    categoria: l.categoriaId,
    tipo: l.tipo,
    status: l.status,
    valorCentavos: l.valor.centavos,
    conta: l.conta ?? undefined,
    origem: l.origem ?? undefined,
    diasAtraso: l.diasAtraso ?? undefined,
  }));
}

export interface KpisAbertoReal {
  aReceberAbertoCentavos: number;
  aReceberAtrasadoCentavos: number;
  aReceberParcelasAbertas: number;
  aPagarAbertoCentavos: number;
  aPagarLancamentosAbertos: number;
  aPagarMaiorLabel: string;
  aPagarMaiorData: string;
}

/** Deriva os KPIs "em aberto"/"atrasado" direto das LINHAS do extrato (previsto/atrasado é
 * exatamente o que ainda não foi pago) — mesmo dado-base de `ContasEmAbertoService`, sem round-trip
 * extra: um único `GET /financeiro/extrato` de horizonte largo já contém tudo. */
export function calcularKpisAberto(linhas: LancamentoRow[]): KpisAbertoReal {
  const aReceberAbertas = linhas.filter((l) => l.tipo === 'entrada' && l.status !== 'pago');
  const aPagarAbertas = linhas.filter((l) => l.tipo === 'saida' && l.status !== 'pago');
  const aReceberAtrasadas = aReceberAbertas.filter((l) => l.status === 'atrasado');

  const maior = [...aPagarAbertas].sort((a, b) => b.valorCentavos - a.valorCentavos)[0] ?? null;

  return {
    aReceberAbertoCentavos: aReceberAbertas.reduce((acc, l) => acc + l.valorCentavos, 0),
    aReceberAtrasadoCentavos: aReceberAtrasadas.reduce((acc, l) => acc + l.valorCentavos, 0),
    aReceberParcelasAbertas: aReceberAbertas.length,
    aPagarAbertoCentavos: aPagarAbertas.reduce((acc, l) => acc + l.valorCentavos, 0),
    aPagarLancamentosAbertos: aPagarAbertas.length,
    aPagarMaiorLabel: maior?.desc ?? '—',
    aPagarMaiorData: maior ? ddMm(maior.data) : '—',
  };
}

function ddMm(iso: string): string {
  const [, mes, dia] = iso.split('-');
  return dia && mes ? `${dia}/${mes}` : '—';
}

export interface ConsultorFornecedoresReal {
  deltaPct: number;
  mediaHistoricaCentavos: number;
  totalMesCentavos: number;
  qtdPagamentos: number;
}

/** Slug real de "Fornecedores" no catálogo do domínio (`CategoriaFinanceiraPadrao.CustoMercadoriaVendida`
 * — ver `src/Modules/Financeiro/.../Categorias/CategoriaFinanceiraPadrao.cs`), não o `'fornecedores'`
 * ilustrativo de `types.ts`/`exemplos.ts`. */
const CATEGORIA_FORNECEDORES_REAL = 'cmv-fornecedor';

/** Deriva o insight do Super Consultor de Fornecedores 100% das LINHAS já pagas do extrato (mesma
 * chamada de horizonte largo dos KPIs "em aberto" — nenhum round-trip extra): total do mês corrente
 * vs a média dos meses anteriores presentes no horizonte, ambos sobre pagamentos REAIS
 * (`status === 'pago'`), nunca uma cópia fixa de exemplo. */
export function calcularConsultorFornecedores(linhas: LancamentoRow[], mesAtualDeIso: string): ConsultorFornecedoresReal {
  const pagosFornecedores = linhas.filter((l) => l.categoria === CATEGORIA_FORNECEDORES_REAL && l.tipo === 'saida' && l.status === 'pago');
  const prefixoMesAtual = mesAtualDeIso.slice(0, 7);

  const doMes = pagosFornecedores.filter((l) => l.data.slice(0, 7) === prefixoMesAtual);
  const anteriores = pagosFornecedores.filter((l) => l.data.slice(0, 7) < prefixoMesAtual);

  const totalPorMes = new Map<string, number>();
  anteriores.forEach((l) => {
    const chave = l.data.slice(0, 7);
    totalPorMes.set(chave, (totalPorMes.get(chave) ?? 0) + l.valorCentavos);
  });
  const totaisMensais = [...totalPorMes.values()];
  const mediaHistoricaCentavos = totaisMensais.length ? Math.round(totaisMensais.reduce((a, b) => a + b, 0) / totaisMensais.length) : 0;
  const totalMesCentavos = doMes.reduce((a, l) => a + l.valorCentavos, 0);
  const deltaPct = mediaHistoricaCentavos > 0 ? Math.round(((totalMesCentavos - mediaHistoricaCentavos) / mediaHistoricaCentavos) * 100) : 0;

  return { deltaPct, mediaHistoricaCentavos, totalMesCentavos, qtdPagamentos: doMes.length };
}

/** Ponto de `pontos[]` de `GET /financeiro/fluxo` cuja `data` é a mais próxima (≤) do fim do
 * período — o "saldo acumulado" ali é exatamente o "Como fecha o mês" (projeção de caixa), ver
 * docs/wiring/financeiro-telas-restantes.md §1 (achado do `FluxoDeCaixaService` reusado, não
 * duplicado). Cai pro ÚLTIMO ponto disponível se nenhum bater (ex.: `ate` além do horizonte). */
export function saldoAcumuladoAte(pontos: { data: string; saldoAcumulado: { centavos: number } }[], ateIso: string): number {
  const ateData = isoData(ateIso);
  const candidatos = pontos.filter((p) => isoData(p.data) <= ateData);
  const escolhido = candidatos.length ? candidatos[candidatos.length - 1] : pontos[pontos.length - 1];
  return escolhido?.saldoAcumulado.centavos ?? 0;
}

/** Select de contas do modal "Dar baixa"/"Lançamento rápido" — reusa `GET /financeiro/contas-bancarias`
 * (já real na tela Bancário, ver docs/wiring/financeiro-telas-restantes.md §1: "repo/endpoint já
 * resolvidos, falta só o front consumir aqui"). `CaixaFisico` vira a tag "espécie" (mesma distinção
 * do mockup original entre "banco" e "Caixa da loja"); só contas ativas aparecem no select. */
export function deContasBancariasParaDisponiveis(dtos: ContaBancariaDto[]): ContaDisponivel[] {
  return dtos
    .filter((c) => c.ativa)
    .map((c) => ({ nome: c.nome, tag: c.tipo === 'CaixaFisico' ? 'espécie' : 'banco' }));
}
