import { Eye, EyeOff } from 'lucide-react';
import { useState, type ButtonHTMLAttributes, type ReactNode } from 'react';

import { MoneyValue } from '@/components/shared';
import { Button } from '@/components/ui/Button';
import { formatDate, formatDateShort } from '@/lib/format';
import { formatCentavos } from '@/lib/money';
import { cn } from '@/lib/utils';

import { diaSemana, orcamentoVencido, totalExecucaoAtual, totalOrcamento } from './calc';
import type { CanalResposta, FormaPagamento, OrdemServico } from './types';
import type { UseOrdemServico } from './useOrdemServico';

/**
 * Corpos do passo "atual" da linha do tempo (`corpoAberta`…`corpoEntrega` do mockup) — um por
 * status. Cada um chama a ação equivalente do hook e mostra o erro inline exatamente com o texto
 * (e o código do invariante) que o agregado `OrdemDeServico` devolveria.
 */

// ── Primitivos de formulário (mesma receita visual em todos os corpos) ──────

function FieldRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <label className="text-xs font-semibold text-muted-foreground">{label}</label>
      {children}
    </div>
  );
}

const inputClass =
  'w-full rounded-[9px] border border-border bg-card px-[11px] py-2 text-sm text-foreground outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100 dark:focus:ring-primary-500/20';

function FieldTwo({ children }: { children: ReactNode }) {
  return <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">{children}</div>;
}
function FieldThree({ children }: { children: ReactNode }) {
  return <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-[2fr_1fr_1fr]">{children}</div>;
}
function InlineErr({ children }: { children: ReactNode }) {
  return <div className="text-xs font-semibold text-crit">{children}</div>;
}
/** CTA primário de cada corpo (`.btn-primary` do mockup) — o mesmo `<Button variant="primary">` do design system. */
function PrimaryButton({ children, ...rest }: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <Button type="button" variant="primary" {...rest}>
      {children}
    </Button>
  );
}
function ChipBtn({ selected, ...rest }: ButtonHTMLAttributes<HTMLButtonElement> & { selected: boolean }) {
  return (
    <button
      type="button"
      className={cn(
        'rounded-[10px] border px-3.5 py-2 text-sm font-semibold transition-colors active:brightness-95',
        selected ? 'border-primary-600 bg-primary-600 text-white' : 'border-border bg-card text-foreground hover:bg-surface-2',
      )}
      {...rest}
    />
  );
}
function TotalRow({ label, valorCentavos }: { label: string; valorCentavos: number }) {
  return (
    <div className="flex items-baseline justify-between border-t border-dashed border-border pt-1.5">
      <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{label}</span>
      <span className="num text-[22px] font-bold">
        <MoneyValue centavos={valorCentavos} />
      </span>
    </div>
  );
}

/** Senha de acesso do equipamento — oculta por padrão, nunca sai impressa (`renderSenha` do mockup). */
function SenhaField({ senha }: { senha: string }) {
  const [mostrando, setMostrando] = useState(false);
  return (
    <span className="inline-flex items-center gap-1.5">
      <span className="num">{mostrando ? senha : '••••••'}</span>
      <button
        type="button"
        onClick={() => setMostrando((v) => !v)}
        title="Mostrar/ocultar (nunca sai impressa)"
        className="text-muted-foreground hover:text-primary-600"
      >
        {mostrando ? <EyeOff className="h-3.5 w-3.5" /> : <Eye className="h-3.5 w-3.5" />}
      </button>
    </span>
  );
}

// ── Aberta → registrar diagnóstico ──────────────────────────────────────────

export function CorpoAberta({ os, vm }: { os: OrdemServico; vm: UseOrdemServico }) {
  const [tecnico, setTecnico] = useState(os.tecnico ?? '');
  const [previsao, setPrevisao] = useState(os.prazo ? formatDate(os.prazo) : '');
  const [diagnostico, setDiagnostico] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  function submit() {
    const e = vm.registrarDiagnostico(os.numero, { tecnico, diagnostico, previsaoTxt: previsao });
    setErro(e);
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-[13px] leading-relaxed text-muted-foreground">
        {os.marca} {os.modelo} · série {os.serie} · acessórios: {os.acessorios}
        <br />
        Estado de entrada: {os.estadoEntrada}
        <br />
        {os.senha ? (
          <>
            Senha de acesso: <SenhaField senha={os.senha} />
          </>
        ) : (
          'Sem senha registrada'
        )}
        <br />
        Defeito relatado: &quot;<b className="font-semibold text-foreground">{os.defeito}</b>&quot;
      </p>
      <FieldTwo>
        <FieldRow label="Técnico responsável">
          <input className={inputClass} value={tecnico} onChange={(e) => setTecnico(e.target.value)} placeholder="Nome do técnico" />
        </FieldRow>
        <FieldRow label="Previsão de entrega">
          <input className={cn(inputClass, 'num')} value={previsao} onChange={(e) => setPrevisao(e.target.value)} placeholder="dd/mm/aaaa" />
        </FieldRow>
      </FieldTwo>
      <FieldRow label="Diagnóstico">
        <textarea
          className={cn(inputClass, 'min-h-[54px] resize-y')}
          value={diagnostico}
          onChange={(e) => setDiagnostico(e.target.value)}
          placeholder="O que o técnico encontrou ao investigar…"
        />
      </FieldRow>
      {erro && <InlineErr>{erro}</InlineErr>}
      <div>
        <PrimaryButton onClick={submit}>Registrar diagnóstico</PrimaryButton>
      </div>
    </div>
  );
}

// ── EmDiagnostico → enviar orçamento ─────────────────────────────────────────

export function CorpoDiagnostico({ os, vm }: { os: OrdemServico; vm: UseOrdemServico }) {
  const [desc, setDesc] = useState('');
  const [qtd, setQtd] = useState('1');
  const [preco, setPreco] = useState('');
  const [mao, setMao] = useState('');
  const [validade, setValidade] = useState('10');
  const [erro, setErro] = useState<string | null>(null);

  function submit() {
    const e = vm.enviarOrcamento(os.numero, { desc, qtdTxt: qtd, precoTxt: preco, maoTxt: mao, validadeTxt: validade });
    setErro(e);
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-[13px] leading-relaxed text-muted-foreground">
        <b className="font-semibold text-foreground">Diagnóstico:</b> {os.diagnostico}
      </p>
      <FieldRow label="Peça prevista (catálogo ou sob encomenda)">
        <FieldThree>
          <input className={inputClass} value={desc} onChange={(e) => setDesc(e.target.value)} placeholder="Descrição da peça" />
          <input className={cn(inputClass, 'num')} type="number" min={1} value={qtd} onChange={(e) => setQtd(e.target.value)} placeholder="Qtd" />
          <input
            className={cn(inputClass, 'num')}
            type="number"
            step="0.01"
            value={preco}
            onChange={(e) => setPreco(e.target.value)}
            placeholder="Preço (R$)"
          />
        </FieldThree>
      </FieldRow>
      <FieldTwo>
        <FieldRow label="Mão de obra (R$)">
          <input className={cn(inputClass, 'num')} type="number" step="0.01" value={mao} onChange={(e) => setMao(e.target.value)} placeholder="120,00" />
        </FieldRow>
        <FieldRow label="Validade do orçamento (dias)">
          <input className={cn(inputClass, 'num')} type="number" min={1} value={validade} onChange={(e) => setValidade(e.target.value)} />
        </FieldRow>
      </FieldTwo>
      {erro && <InlineErr>{erro}</InlineErr>}
      <div>
        <PrimaryButton onClick={submit}>Enviar orçamento</PrimaryButton>
      </div>
    </div>
  );
}

// ── AguardandoAprovacao → decisão do cliente ─────────────────────────────────

export function CorpoAguardandoAprovacao({ os, vm }: { os: OrdemServico; vm: UseOrdemServico }) {
  const orc = os.orcamento!;
  const vence = new Date(orc.enviadoEm);
  vence.setDate(vence.getDate() + orc.validadeDias);
  const vencido = orcamentoVencido(os);

  const [canal, setCanal] = useState<CanalResposta | null>(null);
  const [motivo, setMotivo] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  function decidir(decisao: 'Aprovada' | 'Reprovada') {
    const e = vm.decidir(os.numero, decisao, canal, motivo);
    setErro(e);
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-[13px] leading-relaxed text-muted-foreground">
        Enviado em {formatDateShort(orc.enviadoEm)} · vence {diaSemana(vence)}, {formatDateShort(vence)}
        {vencido && <b className="text-crit"> — vencido</b>}
      </p>

      <div className="flex flex-col gap-2">
        {orc.pecas.length === 0 ? (
          <div className="rounded-[10px] bg-card px-2.5 py-2.5 text-[13px] text-muted-foreground">Sem peças previstas — só mão de obra.</div>
        ) : (
          orc.pecas.map((p, i) => (
            <div key={i} className="flex items-center gap-2.5 rounded-[10px] bg-card px-2.5 py-2.5">
              <div className="flex-1 text-[13px]">{p.desc}</div>
              <div className="num whitespace-nowrap text-sm font-semibold">
                {p.qtd} × {formatCentavos(p.preco)}
              </div>
            </div>
          ))
        )}
      </div>

      <TotalRow label="Total do orçamento" valorCentavos={totalOrcamento(orc)} />

      <FieldRow label="Canal de resposta do cliente">
        <div className="flex flex-wrap gap-2">
          {(['Presencial', 'WhatsApp', 'Telefone'] as CanalResposta[]).map((c) => (
            <ChipBtn key={c} selected={canal === c} onClick={() => setCanal(c)}>
              {c}
            </ChipBtn>
          ))}
        </div>
      </FieldRow>

      {canal && (
        <div className="flex flex-col gap-3">
          <FieldRow label="Motivo da reprovação (opcional — só relevante se reprovar)">
            <textarea className={cn(inputClass, 'min-h-[54px] resize-y')} value={motivo} onChange={(e) => setMotivo(e.target.value)} />
          </FieldRow>
          {erro && <InlineErr>{erro}</InlineErr>}
          <div className="flex gap-2.5">
            <button
              type="button"
              onClick={() => decidir('Aprovada')}
              className="inline-flex items-center gap-1.5 rounded-[10px] bg-pos px-[15px] py-2.5 text-[13.5px] font-semibold text-white transition-all hover:brightness-105 active:brightness-95"
            >
              ✓ Aprovado
            </button>
            <button
              type="button"
              onClick={() => decidir('Reprovada')}
              className="inline-flex items-center gap-1.5 rounded-[10px] bg-crit px-[15px] py-2.5 text-[13.5px] font-semibold text-white transition-all hover:brightness-105 active:brightness-95"
            >
              ✕ Reprovado
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

// ── Aprovada → iniciar execução ──────────────────────────────────────────────

export function CorpoAprovada({ os, vm }: { os: OrdemServico; vm: UseOrdemServico }) {
  return (
    <div className="flex flex-col gap-3">
      <p className="text-[13px] leading-relaxed text-muted-foreground">
        Aprovado via {os.aprovacao!.canal} em {formatDateShort(os.aprovacao!.em)}. Peças com produto de catálogo já reservadas no
        estoque.
      </p>
      <div>
        <PrimaryButton onClick={() => vm.iniciarExecucao(os.numero)}>Iniciar execução</PrimaryButton>
      </div>
    </div>
  );
}

// ── EmExecucao → aplicar peças, mão de obra final, concluir ─────────────────

export function CorpoExecucao({ os, vm }: { os: OrdemServico; vm: UseOrdemServico }) {
  const pecas = os.pecasExecucao ?? [];
  const total = totalExecucaoAtual(os);
  const maoOrcada = os.orcamento!.maoDeObra;

  const [mostrarExtra, setMostrarExtra] = useState(false);
  const [extraDesc, setExtraDesc] = useState('');
  const [extraQtd, setExtraQtd] = useState('1');
  const [extraPreco, setExtraPreco] = useState('');
  const [extraAvisado, setExtraAvisado] = useState(false);
  const [erroExtra, setErroExtra] = useState<string | null>(null);

  const [mdoValor, setMdoValor] = useState(((os.maoDeObraFinal ?? maoOrcada) / 100).toFixed(2));
  const [mdoAvisado, setMdoAvisado] = useState(false);
  const [erroMdo, setErroMdo] = useState<string | null>(null);

  function submitExtra() {
    const e = vm.adicionarPecaExtra(os.numero, { desc: extraDesc, qtdTxt: extraQtd, precoTxt: extraPreco, avisado: extraAvisado });
    setErroExtra(e);
    if (!e) {
      setMostrarExtra(false);
      setExtraDesc('');
      setExtraQtd('1');
      setExtraPreco('');
      setExtraAvisado(false);
    }
  }

  function submitMdo() {
    const e = vm.ajustarMaoDeObra(os.numero, { valorTxt: mdoValor, avisado: mdoAvisado });
    setErroMdo(e);
    if (!e) setMdoValor((Number(mdoValor) || 0).toFixed(2));
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-[13px] leading-relaxed text-muted-foreground">
        Peças do orçamento — reservadas no estoque quando o produto veio do catálogo.
      </p>

      <div className="flex flex-col gap-2">
        {pecas.map((p) => (
          <div key={p.linhaId} className={cn('flex items-center gap-2.5 rounded-[10px] bg-card px-2.5 py-2.5', p.aplicada && 'opacity-70')}>
            <div className="flex-1 text-[13px]">
              {p.desc}
              <div className="text-xs text-muted-foreground">
                {p.qtd} × {formatCentavos(p.preco)} · {p.origem === 'extra' ? 'peça extra' : 'orçada'}
              </div>
            </div>
            <div className="num whitespace-nowrap text-sm font-semibold">{formatCentavos(p.preco * p.qtd)}</div>
            <button
              type="button"
              disabled={p.aplicada}
              onClick={() => vm.aplicarPeca(os.numero, p.linhaId)}
              className={cn(
                'whitespace-nowrap rounded-lg border px-2.5 py-1.5 text-xs font-semibold',
                p.aplicada ? 'cursor-default border-transparent bg-pos-soft text-pos' : 'border-border bg-card text-primary-600 hover:bg-primary-soft',
              )}
            >
              {p.aplicada ? '✔ Aplicada' : 'Apliquei'}
            </button>
          </div>
        ))}
      </div>

      <div>
        <button
          type="button"
          onClick={() => setMostrarExtra((v) => !v)}
          className="rounded-[10px] border border-border bg-card px-2.5 py-1.5 text-xs font-semibold text-foreground hover:bg-surface-2"
        >
          ＋ peça extra
        </button>
      </div>

      {mostrarExtra && (
        <div className="flex flex-col gap-2.5">
          <FieldThree>
            <input className={inputClass} value={extraDesc} onChange={(e) => setExtraDesc(e.target.value)} placeholder="Descrição da peça extra" />
            <input className={cn(inputClass, 'num')} type="number" min={1} value={extraQtd} onChange={(e) => setExtraQtd(e.target.value)} placeholder="Qtd" />
            <input
              className={cn(inputClass, 'num')}
              type="number"
              step="0.01"
              value={extraPreco}
              onChange={(e) => setExtraPreco(e.target.value)}
              placeholder="Preço (R$)"
            />
          </FieldThree>
          <label className="inline-flex items-center gap-2 text-[12.5px] text-muted-foreground">
            <input type="checkbox" checked={extraAvisado} onChange={(e) => setExtraAvisado(e.target.checked)} /> Cliente avisado do
            novo valor
          </label>
          {erroExtra && <InlineErr>{erroExtra}</InlineErr>}
          <div>
            <button
              type="button"
              onClick={submitExtra}
              className="rounded-[10px] border border-border bg-card px-2.5 py-1.5 text-xs font-semibold text-foreground hover:bg-surface-2"
            >
              Adicionar peça extra
            </button>
          </div>
        </div>
      )}

      <div className="max-w-[240px]">
        <FieldRow label={`Mão de obra (orçado ${formatCentavos(maoOrcada)})`}>
          <input className={cn(inputClass, 'num')} type="number" step="0.01" value={mdoValor} onChange={(e) => setMdoValor(e.target.value)} />
        </FieldRow>
      </div>
      <label className="inline-flex items-center gap-2 text-[12.5px] text-muted-foreground">
        <input type="checkbox" checked={mdoAvisado} onChange={(e) => setMdoAvisado(e.target.checked)} /> Cliente avisado do aumento
        (se for além do orçado)
      </label>
      {erroMdo && <InlineErr>{erroMdo}</InlineErr>}
      <div>
        <button
          type="button"
          onClick={submitMdo}
          className="rounded-[10px] border border-border bg-card px-2.5 py-1.5 text-xs font-semibold text-foreground hover:bg-surface-2"
        >
          Aplicar novo valor de mão de obra
        </button>
      </div>

      <TotalRow label="Total até agora" valorCentavos={total} />

      <div>
        <PrimaryButton onClick={() => vm.concluirExecucao(os.numero)}>✓ Concluir execução</PrimaryButton>
      </div>
    </div>
  );
}

// ── Pronta → receber e entregar (`corpoEntrega` do mockup) ──────────────────

const FORMAS_PAGAMENTO: { key: FormaPagamento; label: string }[] = [
  { key: 'Dinheiro', label: 'Dinheiro' },
  { key: 'Pix', label: 'Pix' },
  { key: 'CartaoDebito', label: 'Débito' },
  { key: 'CartaoCredito', label: 'Crédito' },
];

export function CorpoPronta({ os, vm }: { os: OrdemServico; vm: UseOrdemServico }) {
  const total = totalExecucaoAtual(os);
  const prontaDesde = os.historico.find((h) => h.para === 'Pronta')?.em ?? os.abertaEm;

  const [forma, setForma] = useState<FormaPagamento | null>(null);
  const [desconto, setDesconto] = useState('0');
  const [garantia, setGarantia] = useState('90');
  const [erro, setErro] = useState<string | null>(null);

  function submit() {
    if (!forma) return;
    const e = vm.entregar(os.numero, { forma, descontoTxt: desconto, garantiaTxt: garantia });
    setErro(e);
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-[13px] leading-relaxed text-muted-foreground">
        Pronta desde {formatDateShort(prontaDesde)}. Avisar o cliente por WhatsApp antes da retirada.
      </p>

      <TotalRow label="Total a receber" valorCentavos={total} />

      <FieldRow label="Forma de pagamento">
        <div className="flex flex-wrap gap-2">
          {FORMAS_PAGAMENTO.map((f) => (
            <ChipBtn key={f.key} selected={forma === f.key} onClick={() => setForma(f.key)}>
              {f.label}
            </ChipBtn>
          ))}
        </div>
      </FieldRow>

      <FieldTwo>
        <FieldRow label="Desconto (R$)">
          <input className={cn(inputClass, 'num')} type="number" step="0.01" value={desconto} onChange={(e) => setDesconto(e.target.value)} />
        </FieldRow>
        <FieldRow label="Garantia (dias)">
          <input className={cn(inputClass, 'num')} type="number" min={0} value={garantia} onChange={(e) => setGarantia(e.target.value)} />
        </FieldRow>
      </FieldTwo>

      {erro && <InlineErr>{erro}</InlineErr>}
      <div>
        <PrimaryButton disabled={!forma} onClick={submit}>
          {forma ? 'Receber e entregar' : 'Selecione a forma de pagamento'}
        </PrimaryButton>
      </div>
    </div>
  );
}
