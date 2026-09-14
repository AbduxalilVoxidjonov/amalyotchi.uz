/** SPEC-TOKENS 1.3 — semantik status juftliklari (CSS token nomlari bilan). */
export const STATUS = {
  ok: { bg: 'var(--color-ok-bg)', fg: 'var(--color-ok-fg)', strong: 'var(--color-ok-strong)' },
  late: {
    bg: 'var(--color-late-bg)',
    fg: 'var(--color-late-fg)',
    strong: 'var(--color-late-strong)',
  },
  bad: { bg: 'var(--color-bad-bg)', fg: 'var(--color-bad-fg)', strong: 'var(--color-bad-strong)' },
  neu: { bg: 'var(--color-neu-bg)', fg: 'var(--color-neu-fg)', strong: 'var(--color-neu-fg)' },
  info: {
    bg: 'var(--color-info-bg)',
    fg: 'var(--color-info-fg)',
    strong: 'var(--color-info-fg)',
  },
} as const;

export type StatusKind = keyof typeof STATUS;

export const STATUS_KINDS = Object.keys(STATUS) as StatusKind[];
