/** Helpers de data puros para o motor de projeção de caixa — sempre ISO `yyyy-mm-dd`. */

export function todayIso(): string {
  return toIso(new Date());
}

export function toIso(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

export function addDays(iso: string, days: number): string {
  const d = new Date(`${iso}T00:00:00`);
  d.setDate(d.getDate() + days);
  return toIso(d);
}

export function diffDays(fromIso: string, toIsoDate: string): number {
  const a = new Date(`${fromIso}T00:00:00`).getTime();
  const b = new Date(`${toIsoDate}T00:00:00`).getTime();
  return Math.round((b - a) / 86_400_000);
}

/** "2026-07-16" → "2026-07-01" — primeiro dia do mês daquela data. */
export function startOfMonthIso(iso: string): string {
  return `${iso.slice(0, 7)}-01`;
}

/** "2026-07-16" → "2026-07-31" — último dia do mês daquela data (calendário real, sem hardcode
 * de 28/30/31). */
export function endOfMonthIso(iso: string): string {
  const [ano, mes] = iso.split('-');
  const primeiroDoProximoMes = new Date(Number(ano), Number(mes), 1);
  return addDays(toIso(primeiroDoProximoMes), -1);
}
