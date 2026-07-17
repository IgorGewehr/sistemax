import type { PosicaoDeItemDto, ProdutoDto } from '@/lib/api/estoque';

/**
 * View-model de "Estoque" (SDD) — espelha `docs/ui/mockups/estoque.html`, mas só modela o que o
 * Bridge (`lib/api/estoque.ts`) realmente expõe hoje: `ProdutoDto` (catálogo) + `PosicaoDeItemDto`
 * (saldo). O mockup também tem razão de movimentações, inventários e analytics de consumo
 * histórico (giro, cobertura, curva ABC, kardex) — nenhuma dessas tem contrato de API ainda, então
 * não são "implementadas com mock disfarçado de real": as abas/relatórios correspondentes mostram
 * um estado vazio explicando exatamente o que falta (ver README.md).
 */

export type EstoqueTab = 'geral' | 'produtos' | 'mov' | 'inv' | 'rel';

/** Estado do item — mesmo vocabulário do mockup, sem "bom" (BOM/composto): `ProdutoDto` não tem
 * ficha técnica nesta API, então esse estado não existe no domínio real ainda. */
export type EstadoItemCode = 'ok' | 'baixo' | 'zerado' | 'servico';

export interface EstadoItem {
  code: EstadoItemCode;
  label: string;
}

/** Produto do catálogo + a linha de saldo correspondente (join por `produtoId`, feito em
 * `calc.ts`). `saldo` é `null` quando o produto não controla estoque OU quando controla mas ainda
 * não tem uma linha em `listarSaldos()` (produto recém-criado, sem nenhum movimento ainda). */
export interface ProdutoComSaldo {
  produto: ProdutoDto;
  saldo: PosicaoDeItemDto | null;
  estado: EstadoItem;
}

export interface CategoriaResumo {
  nome: string;
  itens: ProdutoComSaldo[];
  valorCentavos: number;
}

export interface EstoqueKpis {
  valorEmEstoqueCentavos: number;
  /** Produtos com `controlaEstoque === true` — equivalente ao `produtosComSaldo()` do mockup. */
  itensComSaldo: number;
  abaixoDoMinimo: number;
  zerados: number;
  produtosCadastrados: number;
}

export interface ProdutosFiltro {
  busca: string;
  /** `'todas'` ou o nome exato da categoria. */
  categoria: string;
  estado: 'todos' | EstadoItemCode;
  soProblema: boolean;
}
