import { ArrowLeft } from 'lucide-react';

/** Botão "←" dos cabeçalhos de drill (`.back` do mockup) — volta pra visão geral do card. */
export function DrillBackButton({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label="Voltar"
      className="grid h-[26px] w-[26px] flex-none place-items-center rounded-lg bg-surface-2 text-foreground transition-colors hover:bg-primary-soft hover:text-primary-600 active:brightness-95"
    >
      <ArrowLeft className="h-3.5 w-3.5" />
    </button>
  );
}
