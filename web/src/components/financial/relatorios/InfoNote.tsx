import { Info } from 'lucide-react';

/** `.info-note` do mockup — explica competência x caixa antes dos cards de documento. */
export function InfoNote() {
  return (
    <div className="mb-5 flex items-start gap-2 text-[12.5px] leading-relaxed text-muted-foreground">
      <Info className="mt-0.5 h-3.5 w-3.5 flex-none text-primary-600" />
      <span>
        No regime de <b className="font-bold text-foreground">competência</b>, conta quando foi vendido/gasto; no de{' '}
        <b className="font-bold text-foreground">caixa</b>, quando o dinheiro mudou de mão. Seu contador normalmente
        pede competência.
      </span>
    </div>
  );
}
