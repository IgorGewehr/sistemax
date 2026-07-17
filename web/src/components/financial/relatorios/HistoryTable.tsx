import { motion } from 'framer-motion';

import { SectionCard, StatusChip } from '@/components/shared';

import type { HistoryRow } from './types';

const COLUMNS = ['Data', 'Documento', 'Formato', 'Gerado por', 'Envio'];

/** Auditoria — sempre o último bloco da tela (quem gerou/enviou o quê e quando). */
export function HistoryTable({ rows }: { rows: HistoryRow[] }) {
  return (
    <SectionCard title="Histórico de exports" hint="auditoria — quem gerou e quando" bodyClassName="overflow-x-auto pb-1">
      <table className="w-full min-w-[620px] border-collapse text-left">
        <thead>
          <tr>
            {COLUMNS.map((col) => (
              <th
                key={col}
                className="border-b border-border px-4 py-3 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground"
              >
                {col}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <motion.tr
              key={row.id}
              initial={row.isNew ? { backgroundColor: 'hsl(var(--pos-soft))' } : false}
              animate={{ backgroundColor: 'rgba(0,0,0,0)' }}
              transition={{ duration: 1.8, ease: 'easeOut' }}
              className="border-b border-border/60 text-[13.5px] last:border-0 hover:bg-surface-2/60"
            >
              <td className="px-4 py-[13px]">{row.date}</td>
              <td className="px-4 py-[13px]">{row.document}</td>
              <td className="px-4 py-[13px]">{row.format}</td>
              <td className="px-4 py-[13px]">{row.generatedBy}</td>
              <td className="px-4 py-[13px]">
                {row.channel === 'email' && <StatusChip tone="sobra">✓ E-mail</StatusChip>}
                {row.channel === 'whatsapp' && <StatusChip tone="sobra">✓ WhatsApp</StatusChip>}
                {row.channel === null && <StatusChip tone="neutro">— Não enviado</StatusChip>}
              </td>
            </motion.tr>
          ))}
        </tbody>
      </table>
    </SectionCard>
  );
}
