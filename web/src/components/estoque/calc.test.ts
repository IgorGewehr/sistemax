import { describe, it, expect } from 'vitest';

import type { ProdutoDto, PosicaoDeItemDto } from '@/lib/api/estoque';

import { kpisDe, categoriasDe, semCustoMedioDe } from './calc';
import type { EstadoItemCode, ProdutoComSaldo } from './types';

/**
 * Fixture única monta um `ProdutoComSaldo` completo (produto + saldo + estado) — os três campos
 * consistentes entre si, mesmo corte de `estadoDe()`: sem `controlaEstoque`, saldo é sempre `null`
 * e o estado é sempre `'servico'` (não dá pra "faltar custo médio" de um item que não controla
 * estoque).
 */
function mkItem(
  opts: {
    id?: string;
    categoria?: string | null;
    controlaEstoque?: boolean;
    saldoCategoria?: string | null;
    valorTotalCentavos?: number;
    custoMedioCentavos?: number;
    estadoCode?: EstadoItemCode;
    semSaldo?: boolean;
  } = {},
): ProdutoComSaldo {
  const {
    id = 'p1',
    categoria = 'Bebidas',
    controlaEstoque = true,
    saldoCategoria,
    valorTotalCentavos = 5_000,
    custoMedioCentavos = 500,
    estadoCode = 'ok',
    semSaldo = false,
  } = opts;

  const produto: ProdutoDto = {
    id,
    sku: `SKU-${id}`,
    nome: `Produto ${id}`,
    categoria,
    unidade: 'UN',
    precoVenda: { centavos: 1000, moeda: 'BRL' },
    controlaEstoque,
    ativo: true,
  };

  const saldo: PosicaoDeItemDto | null =
    !controlaEstoque || semSaldo
      ? null
      : {
          produtoId: id,
          produtoNome: produto.nome,
          categoria: saldoCategoria ?? categoria,
          sku: produto.sku,
          fisico: { milesimos: 10_000 },
          reservado: { milesimos: 0 },
          disponivel: { milesimos: 10_000 },
          custoMedio: { centavos: custoMedioCentavos, moeda: 'BRL' },
          valorTotal: { centavos: valorTotalCentavos, moeda: 'BRL' },
          abaixoDoMinimo: false,
          zerado: false,
        };

  return {
    produto,
    saldo,
    estado: { code: controlaEstoque ? estadoCode : 'servico', label: '' },
  };
}

describe('kpisDe', () => {
  it('soma valorTotal.centavos só dos itens que controlam estoque', () => {
    const itens = [
      mkItem({ id: '1', valorTotalCentavos: 10_000 }),
      mkItem({ id: '2', valorTotalCentavos: 5_000 }),
      mkItem({ id: '3', controlaEstoque: false }), // serviço — não entra na soma
    ];
    const kpis = kpisDe(itens);
    expect(kpis.valorEmEstoqueCentavos).toBe(15_000);
    expect(kpis.itensComSaldo).toBe(2);
    expect(kpis.produtosCadastrados).toBe(3);
  });

  it('item controlado sem saldo (produto novo, nenhum movimento) conta 0 no valor, sem lançar', () => {
    const itens = [mkItem({ id: '1', semSaldo: true, estadoCode: 'zerado' })];
    const kpis = kpisDe(itens);
    expect(kpis.valorEmEstoqueCentavos).toBe(0);
  });

  it('conta abaixoDoMinimo e zerados só entre os itens controlados', () => {
    const itens = [
      mkItem({ id: '1', estadoCode: 'baixo' }),
      mkItem({ id: '2', estadoCode: 'zerado' }),
      mkItem({ id: '3', estadoCode: 'ok' }),
      mkItem({ id: '4', controlaEstoque: false }), // fora do universo de baixo/zerado
    ];
    const kpis = kpisDe(itens);
    expect(kpis.abaixoDoMinimo).toBe(1);
    expect(kpis.zerados).toBe(1);
  });

  it('lista vazia produz kpis zerados, nunca NaN', () => {
    const kpis = kpisDe([]);
    expect(kpis).toEqual({
      valorEmEstoqueCentavos: 0,
      itensComSaldo: 0,
      abaixoDoMinimo: 0,
      zerados: 0,
      produtosCadastrados: 0,
    });
  });
});

describe('categoriasDe', () => {
  it('agrupa por categoria e soma valorTotal, ordenado desc por valor', () => {
    const itens = [
      mkItem({ id: '1', categoria: 'Bebidas', valorTotalCentavos: 3_000 }),
      mkItem({ id: '2', categoria: 'Bebidas', valorTotalCentavos: 2_000 }),
      mkItem({ id: '3', categoria: 'Limpeza', valorTotalCentavos: 10_000 }),
    ];
    const cats = categoriasDe(itens);
    expect(cats.map((c) => c.nome)).toEqual(['Limpeza', 'Bebidas']);
    expect(cats[0].valorCentavos).toBe(10_000);
    expect(cats[1].valorCentavos).toBe(5_000);
    expect(cats[1].itens).toHaveLength(2);
  });

  it('produtos sem categoria caem no bucket "Sem categoria"', () => {
    const itens = [mkItem({ id: '1', categoria: null, valorTotalCentavos: 1_000 })];
    expect(categoriasDe(itens).map((c) => c.nome)).toEqual(['Sem categoria']);
  });

  it('exclui itens que não controlam estoque (serviços não entram em nenhuma categoria de valor)', () => {
    const itens = [mkItem({ id: '1', controlaEstoque: false, categoria: 'Serviços' })];
    expect(categoriasDe(itens)).toEqual([]);
  });

  it('invariante: soma de valorCentavos de todas as categorias === kpisDe(...).valorEmEstoqueCentavos — agrupar não perde nem duplica centavo', () => {
    const itens = [
      mkItem({ id: '1', categoria: 'A', valorTotalCentavos: 1_234 }),
      mkItem({ id: '2', categoria: 'B', valorTotalCentavos: 5_678 }),
      mkItem({ id: '3', categoria: 'A', valorTotalCentavos: 999 }),
      mkItem({ id: '4', controlaEstoque: false }), // fora do universo dos dois lados, consistente
    ];
    const somaCategorias = categoriasDe(itens).reduce((acc, c) => acc + c.valorCentavos, 0);
    expect(somaCategorias).toBe(kpisDe(itens).valorEmEstoqueCentavos);
  });
});

describe('semCustoMedioDe', () => {
  it('conta itens controlados com saldo mas custo médio zerado (sinal de qualidade de dado)', () => {
    const itens = [mkItem({ id: '1', custoMedioCentavos: 0 }), mkItem({ id: '2', custoMedioCentavos: 500 })];
    expect(semCustoMedioDe(itens)).toBe(1);
  });

  it('ignora itens sem saldo — não há custo médio pra avaliar ainda', () => {
    const itens = [mkItem({ id: '1', semSaldo: true })];
    expect(semCustoMedioDe(itens)).toBe(0);
  });

  it('ignora serviços (não controlam estoque, não têm custo médio de estoque)', () => {
    const itens = [mkItem({ id: '1', controlaEstoque: false })];
    expect(semCustoMedioDe(itens)).toBe(0);
  });

  it('lista vazia retorna 0', () => {
    expect(semCustoMedioDe([])).toBe(0);
  });
});
