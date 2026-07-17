import { AnimatePresence, motion } from 'framer-motion';
import { useState } from 'react';

import { SectionCard, StatusChip } from '@/components/shared';

import { BancarioMoneyValue } from './BancarioMoneyValue';
import { DrillBackButton } from './DrillBackButton';
import type { ItemBatidoAmostra, ItemConciliacaoPendente } from './types';

type BucketId = 'ok' | 'banco' | 'sistema';

interface ConciliacaoCardProps {
  bateuCertinhoTotal: number;
  bateuCertinhoAmostra: ItemBatidoAmostra[];
  sobrouNoBanco: ItemConciliacaoPendente[];
  sobrouNoSistema: ItemConciliacaoPendente[];
  /** Resolve um item pendente — "sim" também soma em "Bateu certinho", "nao" só descarta. */
  onResolve: (kind: 'banco' | 'sistema', itemId: string, action: 'sim' | 'nao') => void;
}

/**
 * Card "Conciliação" — os 3 baldes (bateu certinho / sobrou no banco / sobrou no sistema) ⇄ drill
 * com os itens de cada balde e ação de 1 clique. As sugestões de match são heurística de
 * conciliação (não é o Super Consultor) — resolver é sempre o usuário, nunca automático.
 */
export function ConciliacaoCard({
  bateuCertinhoTotal,
  bateuCertinhoAmostra,
  sobrouNoBanco,
  sobrouNoSistema,
  onResolve,
}: ConciliacaoCardProps) {
  const [bucket, setBucket] = useState<BucketId | null>(null);

  const rows: { id: BucketId; label: string; count: number; sev: 'pos' | 'warn'; desc: string }[] = [
    {
      id: 'ok',
      label: 'Bateu certinho',
      count: bateuCertinhoTotal,
      sev: 'pos',
      desc: `${bateuCertinhoTotal} itens conciliados automaticamente por valor e data — nada a fazer.`,
    },
    {
      id: 'banco',
      label: 'Sobrou no banco',
      count: sobrouNoBanco.length,
      sev: sobrouNoBanco.length ? 'warn' : 'pos',
      desc: 'Aparece no extrato, mas não tem lançamento no sistema.',
    },
    {
      id: 'sistema',
      label: 'Sobrou no sistema',
      count: sobrouNoSistema.length,
      sev: sobrouNoSistema.length ? 'warn' : 'pos',
      desc: 'Lançado no sistema, mas o banco ainda não confirma.',
    },
  ];

  const pendentes = bucket === 'banco' ? sobrouNoBanco : bucket === 'sistema' ? sobrouNoSistema : [];

  return (
    <SectionCard
      className="min-h-[300px]"
      title={
        bucket ? (
          <span className="inline-flex items-center gap-2">
            <DrillBackButton onClick={() => setBucket(null)} />
            {bucket === 'ok' ? 'Bateu certinho' : bucket === 'banco' ? 'Sobrou no banco' : 'Sobrou no sistema'}
          </span>
        ) : (
          'Conciliação'
        )
      }
      hint={
        bucket === 'ok'
          ? `${bateuCertinhoTotal} itens · amostra`
          : bucket
            ? `${pendentes.length} pendente${pendentes.length === 1 ? '' : 's'}`
            : 'clique num grupo p/ ver os itens →'
      }
    >
      <AnimatePresence mode="wait">
        <motion.div
          key={bucket ?? 'baldes'}
          initial={{ opacity: 0, y: 5 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.22, ease: 'easeOut' }}
        >
          {!bucket && (
            <div className="flex flex-col gap-2.5 px-3.5 pb-4 pt-3">
              {rows.map((row) => (
                <button
                  key={row.id}
                  type="button"
                  onClick={() => setBucket(row.id)}
                  className="flex w-full items-start gap-3 rounded-xl bg-surface-2 p-3 text-left transition-[filter] hover:brightness-[0.97] active:brightness-95 dark:hover:brightness-125"
                >
                  <span className={`w-2 flex-none self-stretch rounded-md ${row.sev === 'pos' ? 'bg-pos' : 'bg-warn'}`} />
                  <span className="min-w-0 flex-1">
                    <span className="text-[13px] font-semibold text-foreground">
                      {row.label} <span className="num">· {row.count}</span>
                    </span>
                    <span className="mt-0.5 block text-[12.5px] text-muted-foreground">{row.desc}</span>
                  </span>
                </button>
              ))}
            </div>
          )}

          {bucket === 'ok' && (
            <>
              <div className="flex flex-col px-[18px] pb-4">
                {bateuCertinhoAmostra.map((item) => (
                  <div
                    key={`${item.data}-${item.descricao}`}
                    className="flex items-center gap-2.5 border-b border-border/50 py-2 text-[12.5px] last:border-b-0"
                  >
                    <span className="num w-[34px] flex-none text-muted-foreground">{item.data}</span>
                    <span className="min-w-0 flex-1 truncate">{item.descricao}</span>
                    <StatusChip tone="sobra" dot={false}>✔ auto</StatusChip>
                  </div>
                ))}
              </div>
              <div className="px-[18px] pb-[18px] text-xs text-muted-foreground">
                Valor e data batem automaticamente — nenhuma ação necessária.
              </div>
            </>
          )}

          {(bucket === 'banco' || bucket === 'sistema') && (
            <div className="flex max-h-[372px] flex-col gap-2.5 overflow-y-auto px-3.5 pb-4 pt-2.5">
              {pendentes.length === 0 && (
                <div className="px-[18px] py-[22px] text-center text-[13px] text-muted-foreground">
                  Tudo resolvido por aqui. 🎉
                </div>
              )}
              {pendentes.map((item) => (
                <div key={item.id} className="rounded-xl bg-surface-2 p-3">
                  <div className="flex items-center justify-between gap-2.5 text-[13px] font-semibold text-foreground">
                    <span>
                      {item.data} · {item.descricao}
                    </span>
                    <BancarioMoneyValue centavos={item.valorCentavos} signed tone="auto" />
                  </div>
                  <p className="mt-1.5 text-xs leading-relaxed text-muted-foreground">{item.sugestao}</p>
                  {/* Sem `idSugerido` não há par movimento↔extrato pra confirmar/ignorar de verdade
                      (o backend exige os dois lados) — mostra só a explicação, sem botão que falharia. */}
                  {item.idSugerido !== null && (
                    <div className="mt-2.5 flex gap-2">
                      <button
                        type="button"
                        onClick={() => onResolve(bucket, item.id, 'sim')}
                        className="rounded-lg bg-pos px-3 py-1.5 text-xs font-semibold text-white transition-[filter] hover:brightness-105 active:brightness-95"
                      >
                        {item.rotuloAcaoPrimaria}
                      </button>
                      <button
                        type="button"
                        onClick={() => onResolve(bucket, item.id, 'nao')}
                        className="rounded-lg border border-border bg-card px-3 py-1.5 text-xs font-semibold text-foreground transition-colors hover:bg-surface-2 active:brightness-95"
                      >
                        {item.rotuloAcaoSecundaria}
                      </button>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </motion.div>
      </AnimatePresence>
    </SectionCard>
  );
}
