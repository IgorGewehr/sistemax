import { useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import { Modal } from '@/components/ui/Modal';
import { ApiError } from '@/lib/api/client';
import { estoqueApi, UNIDADES, type ProdutoDto } from '@/lib/api/estoque';

const inputClass =
  'w-full rounded-xl border border-border bg-background px-3 py-2 text-sm text-foreground outline-none placeholder:text-muted-foreground/60 focus:border-primary-400 focus:ring-2 focus:ring-primary-100 dark:focus:ring-primary-500/20';
const labelClass = 'mb-1 block text-xs font-semibold text-muted-foreground';

interface NovoProdutoState {
  nome: string;
  sku: string;
  categoria: string;
  unidade: string;
  precoVenda: string;
  controlaEstoque: boolean;
}

const ESTADO_INICIAL: NovoProdutoState = {
  nome: '',
  sku: '',
  categoria: '',
  unidade: 'UN',
  precoVenda: '',
  controlaEstoque: true,
};

interface NovoProdutoModalProps {
  open: boolean;
  onClose: () => void;
  onCriado: (produto: ProdutoDto) => void;
}

/** Modal de cadastro — wired 1:1 a `estoqueApi.criarProduto()` (`CriarProdutoRequest`), a mesma
 * ação real da página anterior. */
export function NovoProdutoModal({ open, onClose, onCriado }: NovoProdutoModalProps) {
  return (
    <Modal open={open} onClose={onClose} title="Novo produto">
      <NovoProdutoForm onCriado={onCriado} onCancelar={onClose} />
    </Modal>
  );
}

function NovoProdutoForm({ onCriado, onCancelar }: { onCriado: (produto: ProdutoDto) => void; onCancelar: () => void }) {
  const [form, setForm] = useState<NovoProdutoState>(ESTADO_INICIAL);
  const [salvando, setSalvando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!form.nome.trim()) {
      setErro('Nome é obrigatório.');
      return;
    }

    setSalvando(true);
    setErro(null);
    try {
      const precoReais = form.precoVenda.replace(',', '.').trim();
      const precoCentavos = precoReais ? Math.round(parseFloat(precoReais) * 100) : 0;

      const produto = await estoqueApi.criarProduto({
        nome: form.nome.trim(),
        unidade: form.unidade,
        sku: form.sku.trim() || null,
        precoVendaCentavos: Number.isFinite(precoCentavos) ? precoCentavos : 0,
        categoria: form.categoria.trim() || null,
        controlaEstoque: form.controlaEstoque,
      });
      onCriado(produto);
      setForm(ESTADO_INICIAL);
    } catch (e2) {
      setErro(e2 instanceof ApiError ? e2.message : 'Não foi possível cadastrar o produto.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-3.5">
      <div>
        <label className={labelClass} htmlFor="nome">
          Nome *
        </label>
        <input
          id="nome"
          className={inputClass}
          placeholder="Ex.: Refrigerante Lata 350ml"
          value={form.nome}
          onChange={(e) => setForm({ ...form, nome: e.target.value })}
          autoFocus
        />
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className={labelClass} htmlFor="sku">
            SKU (opcional)
          </label>
          <input
            id="sku"
            className={`${inputClass} num`}
            placeholder="gerado automaticamente"
            value={form.sku}
            onChange={(e) => setForm({ ...form, sku: e.target.value })}
          />
        </div>
        <div>
          <label className={labelClass} htmlFor="unidade">
            Unidade
          </label>
          <select
            id="unidade"
            className={inputClass}
            value={form.unidade}
            onChange={(e) => setForm({ ...form, unidade: e.target.value })}
          >
            {UNIDADES.map((u) => (
              <option key={u} value={u}>
                {u}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className={labelClass} htmlFor="categoria">
            Categoria (opcional)
          </label>
          <input
            id="categoria"
            className={inputClass}
            placeholder="Ex.: Bebidas"
            value={form.categoria}
            onChange={(e) => setForm({ ...form, categoria: e.target.value })}
          />
        </div>
        <div>
          <label className={labelClass} htmlFor="preco">
            Preço de venda (R$)
          </label>
          <input
            id="preco"
            className={`${inputClass} num`}
            inputMode="decimal"
            placeholder="0,00"
            value={form.precoVenda}
            onChange={(e) => setForm({ ...form, precoVenda: e.target.value })}
          />
        </div>
      </div>

      <label className="flex items-center gap-2 text-sm text-foreground">
        <input
          type="checkbox"
          checked={form.controlaEstoque}
          onChange={(e) => setForm({ ...form, controlaEstoque: e.target.checked })}
          className="h-4 w-4 rounded border-border text-primary-600 focus:ring-primary-400"
        />
        Controla estoque
      </label>

      {erro && <p className="text-sm font-medium text-red-600 dark:text-red-400">{erro}</p>}

      <div className="mt-1 flex justify-end gap-2">
        <Button type="button" variant="ghost" onClick={onCancelar} disabled={salvando}>
          Cancelar
        </Button>
        <Button type="submit" variant="primary" disabled={salvando}>
          {salvando ? 'Salvando…' : 'Cadastrar'}
        </Button>
      </div>
    </form>
  );
}
