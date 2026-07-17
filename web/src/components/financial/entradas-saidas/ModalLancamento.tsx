import { useEffect, useId, useState } from 'react';

import { Button } from '@/components/ui/Button';
import { cn } from '@/lib/utils';

import { ModalShell } from './ModalShell';
import type { CategoriasLancamentoRapido, NovoLancamentoInput, TipoLancamento } from './types';

interface ModalLancamentoProps {
  open: boolean;
  categorias: CategoriasLancamentoRapido;
  vencimentoPadrao: string;
  onClose: () => void;
  onSalvar: (input: NovoLancamentoInput) => void;
}

const TIPOS: { value: TipoLancamento; label: string }[] = [
  { value: 'entrada', label: 'Entrada' },
  { value: 'saida', label: 'Saída' },
];

/** Modal "Lançamento rápido" (FAB) — registra uma entrada ou saída avulsa direto na Linha do tempo. */
export function ModalLancamento({ open, categorias, vencimentoPadrao, onClose, onSalvar }: ModalLancamentoProps) {
  const [tipo, setTipo] = useState<TipoLancamento>('entrada');
  const [descricao, setDescricao] = useState('');
  const [categoriaLabel, setCategoriaLabel] = useState(categorias.entrada[0]);
  const [valor, setValor] = useState('');
  const [vencimento, setVencimento] = useState(vencimentoPadrao);
  const [recorrente, setRecorrente] = useState(false);
  const [anexado, setAnexado] = useState(false);
  const descId = useId();

  // Reabrir sempre reseta o formulário — mesmo comportamento do `abrirLancar()` do mockup.
  useEffect(() => {
    if (!open) return;
    setTipo('entrada');
    setDescricao('');
    setCategoriaLabel(categorias.entrada[0]);
    setValor('');
    setVencimento(vencimentoPadrao);
    setRecorrente(false);
    setAnexado(false);
  }, [open, vencimentoPadrao, categorias.entrada]);

  const opcoesCategoria = tipo === 'entrada' ? categorias.entrada : categorias.saida;

  function handleTipo(novoTipo: TipoLancamento) {
    setTipo(novoTipo);
    setCategoriaLabel((novoTipo === 'entrada' ? categorias.entrada : categorias.saida)[0]);
  }

  return (
    <ModalShell
      open={open}
      onClose={onClose}
      eyebrow="Lançamento rápido"
      title="Novo lançamento"
      description="Registre uma entrada ou saída. Dá pra ajustar tudo depois."
      footer={
        <>
          <Button variant="secondary" size="sm" onClick={onClose}>
            Cancelar
          </Button>
          <Button
            variant="primary"
            size="sm"
            onClick={() => onSalvar({ tipo, descricao, categoriaLabel, valorReais: Number(valor), vencimento, recorrente })}
          >
            Salvar lançamento
          </Button>
        </>
      }
    >
      <div className="inline-flex self-start gap-0.5 rounded-[11px] border border-border bg-surface-2 p-[3px]">
        {TIPOS.map((t) => (
          <button
            key={t.value}
            type="button"
            onClick={() => handleTipo(t.value)}
            className={cn(
              'rounded-lg px-3.5 py-1.5 text-[13px] font-semibold transition-colors',
              tipo === t.value ? 'bg-card text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {t.label}
          </button>
        ))}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor={descId} className="text-xs font-semibold text-muted-foreground">
          Descrição
        </label>
        <input
          id={descId}
          type="text"
          value={descricao}
          onChange={(e) => setDescricao(e.target.value)}
          placeholder="Ex.: Recebimento Padaria Central"
          className="rounded-[10px] border border-border bg-surface-2 px-3 py-2.5 font-sans text-sm text-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring"
        />
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div className="flex flex-col gap-1.5">
          <label className="text-xs font-semibold text-muted-foreground">Categoria</label>
          <select
            value={categoriaLabel}
            onChange={(e) => setCategoriaLabel(e.target.value)}
            className="rounded-[10px] border border-border bg-surface-2 px-3 py-2.5 font-sans text-sm text-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            {opcoesCategoria.map((op) => (
              <option key={op}>{op}</option>
            ))}
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <label className="text-xs font-semibold text-muted-foreground">Valor</label>
          <input
            type="number"
            min={0}
            step={1}
            value={valor}
            onChange={(e) => setValor(e.target.value)}
            placeholder="0"
            className="num rounded-[10px] border border-border bg-surface-2 px-3 py-2.5 text-sm text-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <label className="text-xs font-semibold text-muted-foreground">Vencimento</label>
        <input
          type="date"
          value={vencimento}
          onChange={(e) => setVencimento(e.target.value)}
          className="num rounded-[10px] border border-border bg-surface-2 px-3 py-2.5 text-sm text-foreground outline-none focus-visible:ring-2 focus-visible:ring-ring"
        />
      </div>

      <label className="flex cursor-pointer items-start gap-2.5 rounded-[10px] bg-surface-2 px-3 py-2.5 text-[12.5px] text-muted-foreground">
        <input type="checkbox" checked={recorrente} onChange={(e) => setRecorrente(e.target.checked)} className="mt-0.5" />
        <span>
          🔁 <b className="font-bold text-foreground">Isso se repete todo mês</b> — depois de salvar, dá pra configurar a recorrência
          completa em Recorrentes.
        </span>
      </label>

      <button
        type="button"
        onClick={() => setAnexado(true)}
        className="self-start rounded-[10px] border border-border bg-card px-2.5 py-2 text-[13px] font-medium text-foreground transition-colors hover:bg-surface-2 active:brightness-95"
      >
        {anexado ? '📎 Nota anexada ✓' : '📎 Anexar nota/foto'}
      </button>
    </ModalShell>
  );
}
