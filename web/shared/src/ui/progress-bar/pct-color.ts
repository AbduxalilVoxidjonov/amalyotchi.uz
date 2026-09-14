/** SPEC-TOKENS 1.4 — foizga qarab rang (CSS token). */
export function pctColor(pct: number): string {
  return pct >= 85
    ? 'var(--color-ok-strong)'
    : pct >= 70
      ? 'var(--color-late-strong)'
      : 'var(--color-bad-strong)';
}

export function pctKind(pct: number): 'ok' | 'late' | 'bad' {
  return pct >= 85 ? 'ok' : pct >= 70 ? 'late' : 'bad';
}
