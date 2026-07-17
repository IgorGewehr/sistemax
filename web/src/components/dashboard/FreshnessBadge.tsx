/**
 * Selo "Atualizado agora" do header (`actions` do `PageHeader`) — puramente informativo. O
 * Dashboard não tem período pra trocar (é sempre "agora"), então, ao contrário do `PeriodoTrigger`
 * do Financeiro, este selo não é um botão: não há o que clicar até existir atualização em tempo
 * real de verdade.
 */
export function FreshnessBadge() {
  return (
    <span className="inline-flex items-center gap-1.5 rounded-xl border border-border bg-card px-3 py-2 text-[12.5px] font-semibold text-muted-foreground">
      <span className="h-1.5 w-1.5 rounded-full bg-pos" />
      Atualizado agora
    </span>
  );
}
