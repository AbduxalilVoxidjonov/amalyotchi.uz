import { fmtTime } from '../shared/fmt';
import type { AttendanceRow, Presence, PresenceFilter, PresenceState } from './types';

/** Chip rangi (PresenceCell.module.css `data-tone`). */
export type PresenceTone = 'ok' | 'bad' | 'warn' | 'lost' | 'neutral';

export interface PresenceChipModel {
  label: string;
  tone: PresenceTone;
}

const TONE: Record<PresenceState, PresenceTone> = {
  inside: 'ok',
  outside: 'bad',
  inaccurate: 'warn',
  signalLost: 'lost',
  stopped: 'neutral',
  noConsent: 'neutral',
};

/** `presence.state` → chip matni va rangi (LIVE-JOYLASHUV.md · Admin API). */
export function presenceChip(p: Presence): PresenceChipModel {
  const tone = TONE[p.state];
  switch (p.state) {
    case 'inside':
      return { label: 'Joyida', tone };
    case 'outside':
      return { label: `Tashqarida · ${Math.max(0, Math.round(p.outsideMinutes))} daq`, tone };
    case 'inaccurate':
      return { label: 'Aniqlik past', tone };
    case 'signalLost': {
      const since = p.lastPingAt ? fmtTime(p.lastPingAt) : '';
      return { label: since ? `Signal yo'q · ${since} dan` : "Signal yo'q", tone };
    }
    case 'stopped':
      return { label: 'Ketgan', tone };
    case 'noConsent':
      return { label: "Rozilik yo'q", tone };
  }
}

/** ISO → "hozirgina" · "3 daq oldin" · "2 soat oldin" (qisqa, jadval katagi uchun). */
export function fmtAgoShort(iso: string, now: number = Date.now()): string {
  const t = new Date(iso).getTime();
  if (Number.isNaN(t)) return '';
  const diffMin = Math.max(0, Math.floor((now - t) / 60_000));
  if (diffMin < 1) return 'hozirgina';
  if (diffMin < 60) return `${diffMin} daq oldin`;
  const hours = Math.floor(diffMin / 60);
  if (hours < 24) return `${hours} soat oldin`;
  return `${Math.floor(hours / 24)} kun oldin`;
}

/** "Joyida" filtri: Tashqarida / Signal yo'q. */
export function matchesPresenceFilter(row: AttendanceRow, filter: PresenceFilter): boolean {
  if (filter === 'all') return true;
  return row.presence?.state === filter;
}
