import type { ReactNode } from 'react';

import { ConsultorInsight, MoneyValue } from '@/components/shared';

import type { ConsultorResumo } from './calc';

interface ConsultorCardProps {
  resumo: ConsultorResumo;
  onVerProdutosComProblema: () => void;
  onVerTodosProdutos: () => void;
}

/**
 * Super Consultor da Visão Geral. Só afirma o que dá pra provar com `listarSaldos()` real — sem a
 * estimativa de "perda de venda por semana" do mockup (precisa de consumo histórico + OS
 * reservadas, nenhuma das duas APIs existe aqui). Read-only (Lei 2): observa, nunca "aplica".
 */
export function ConsultorCard({ resumo, onVerProdutosComProblema, onVerTodosProdutos }: ConsultorCardProps) {
  const temProblema = resumo.zerados.length > 0 || resumo.baixos.length > 0;
  return (
    <ConsultorInsight
      className="mb-4"
      action={
        temProblema
          ? { label: 'Ver produtos com problema →', onClick: onVerProdutosComProblema }
          : { label: 'Ver produtos →', onClick: onVerTodosProdutos }
      }
    >
      {mensagemDe(resumo)}
    </ConsultorInsight>
  );
}

function mensagemDe({ zerados, baixos, foco }: ConsultorResumo): ReactNode {
  if (foco) {
    const valor = foco.saldo?.valorTotal.centavos ?? 0;
    return (
      <>
        <b>{foco.produto.nome}</b> está zerado
        {valor > 0 && (
          <>
            {' '}
            (o saldo registrado vale <MoneyValue centavos={valor} /> hoje)
          </>
        )}
        . {zerados.length > 1 && <>Mais {zerados.length - 1} produto(s) na mesma situação. </>}
        {baixos.length > 0 && <>{baixos.length} item(ns) abaixo do mínimo também merecem revisão.</>}
      </>
    );
  }
  if (baixos.length > 0) {
    return (
      <>
        <b>{baixos.length} item(ns)</b> estão abaixo do mínimo configurado. Nenhum zerou ainda — bom momento para
        repor antes que vire ruptura.
      </>
    );
  }
  return <>Nenhum item abaixo do mínimo agora — estoque sob controle.</>;
}
