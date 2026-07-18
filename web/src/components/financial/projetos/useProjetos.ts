import { useCallback, useEffect, useState } from 'react';

import { ApiError } from '@/lib/api/client';
import { financeiroApi, type ConfiguracaoFinanceiraDto, type PainelDoProjetoDto, type ProjetoDto } from '@/lib/api/financeiro';

export interface Recurso<T> {
  dado: T | null;
  erro: string | null;
  carregando: boolean;
}

function inicial<T>(): Recurso<T> {
  return { dado: null, erro: null, carregando: true };
}

function mensagemDeErro(e: unknown): string {
  return e instanceof ApiError ? e.message : 'Não foi possível carregar.';
}

/**
 * Financeiro › Projetos — dado REAL, 1:1 com `docs/ui/mockups/projeto.html`:
 * `GET /financeiro/configuracoes` decide o estado opt-in (`analisePorProjetoAtiva`);
 * `GET /financeiro/projetos` alimenta o seletor; `GET /financeiro/projetos/{id}/painel` é buscado
 * para CADA projeto listado (o seletor do mockup mostra MRR/assinaturas por card — não dá pra
 * saber isso sem o painel de cada um). N tende a ser pequeno (feature opt-in, poucos projetos por
 * tenant) — o N+1 aqui é aceitável pelo mesmo racional de `useReceitaRecorrente`/`useBancario`
 * rodarem sobre SQLite local, sem rede.
 *
 * Toggle desligado ⇒ `projetos` volta `[]` (nunca erro, design §2.2) — a tela distingue "análise
 * desligada" de "nenhum projeto cadastrado ainda" lendo `configuracao.dado.analisePorProjetoAtiva`.
 */
export function useProjetos() {
  const [configuracao, setConfiguracao] = useState<Recurso<ConfiguracaoFinanceiraDto>>(inicial);
  const [projetos, setProjetos] = useState<Recurso<ProjetoDto[]>>(inicial);
  const [selecionadoId, setSelecionadoId] = useState<string | null>(null);
  const [paineis, setPaineis] = useState<Record<string, Recurso<PainelDoProjetoDto>>>({});

  const carregar = useCallback(() => {
    setConfiguracao(inicial());
    setProjetos(inicial());
    setSelecionadoId(null);
    setPaineis({});

    financeiroApi
      .configuracoes()
      .then((dto) => setConfiguracao({ dado: dto, erro: null, carregando: false }))
      .catch((e) => setConfiguracao({ dado: null, erro: mensagemDeErro(e), carregando: false }));

    financeiroApi
      .projetos()
      .then((dto) => setProjetos({ dado: dto, erro: null, carregando: false }))
      .catch((e) => setProjetos({ dado: null, erro: mensagemDeErro(e), carregando: false }));
  }, []);

  useEffect(() => {
    carregar();
  }, [carregar]);

  // Seleciona o primeiro projeto assim que a lista chega.
  useEffect(() => {
    if (selecionadoId || !projetos.dado || projetos.dado.length === 0) return;
    setSelecionadoId(projetos.dado[0].id);
  }, [projetos.dado, selecionadoId]);

  // Busca o painel de CADA projeto — alimenta tanto os cards do seletor quanto o painel ativo.
  useEffect(() => {
    if (!projetos.dado) return;
    for (const p of projetos.dado) {
      setPaineis((prev) => (prev[p.id] ? prev : { ...prev, [p.id]: inicial() }));
      financeiroApi
        .projetoPainel(p.id)
        .then((dto) => setPaineis((prev) => ({ ...prev, [p.id]: { dado: dto, erro: null, carregando: false } })))
        .catch((e) => setPaineis((prev) => ({ ...prev, [p.id]: { dado: null, erro: mensagemDeErro(e), carregando: false } })));
    }
  }, [projetos.dado]);

  return {
    configuracao,
    projetos,
    selecionadoId,
    selecionar: setSelecionadoId,
    paineis,
    painelAtivo: selecionadoId ? (paineis[selecionadoId] ?? inicial<PainelDoProjetoDto>()) : inicial<PainelDoProjetoDto>(),
    recarregar: carregar,
  };
}

export type ProjetosVm = ReturnType<typeof useProjetos>;
