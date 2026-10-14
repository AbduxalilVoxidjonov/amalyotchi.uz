import { fmtTime } from '../shared/fmt';
import type { LocationPing, LocationPingsResponse, PingStatus } from './types';

export type SegmentKind = PingStatus | 'gap';

export interface TimelineSegment {
  kind: SegmentKind;
  /** epoch ms */
  from: number;
  to: number;
}

export interface Timeline {
  start: number;
  end: number;
  segments: TimelineSegment[];
  /** Shu daqiqadan uzun ping'siz oraliq — "uzilish" (signal yo'q). */
  gapThresholdMin: number;
}

const MIN = 60_000;
/** Ping'lar 2 tadan kam bo'lsa — kontrakt sukuti (intervalSec 300) × 2. */
const DEFAULT_GAP_MIN = 10;

function median(values: number[]): number {
  const sorted = [...values].sort((a, b) => a - b);
  const mid = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[mid]! : (sorted[mid - 1]! + sorted[mid]!) / 2;
}

/**
 * Uzilish chegarasi: javobda `intervalSec` yo'q — ping'lar orasidagi median oraliqdan × 2
 * (server `outsideMinutes` ni ham `2 × intervalSec` da kesadi).
 */
export function gapThresholdMinutes(pings: readonly LocationPing[]): number {
  if (pings.length < 2) return DEFAULT_GAP_MIN;
  const diffs: number[] = [];
  for (let i = 1; i < pings.length; i++) {
    const d = (Date.parse(pings[i]!.occurredAt) - Date.parse(pings[i - 1]!.occurredAt)) / MIN;
    if (d > 0) diffs.push(d);
  }
  if (diffs.length === 0) return DEFAULT_GAP_MIN;
  return Math.max(2, median(diffs) * 2);
}

function push(segments: TimelineSegment[], seg: TimelineSegment) {
  if (seg.to <= seg.from) return;
  const last = segments[segments.length - 1];
  if (last && last.kind === seg.kind && last.to === seg.from) last.to = seg.to;
  else segments.push({ ...seg });
}

/**
 * Vaqt chizig'i: check-in → ping'lar → check-out (yoki bugun bo'lsa — hozir). Har oraliq oldingi
 * ping holatida bo'yaladi; chegaradan uzun oraliq — `gap`.
 */
export function buildTimeline(
  data: Pick<LocationPingsResponse, 'pings' | 'checkInAt' | 'checkOutAt'>,
  now: number | null,
): Timeline | null {
  const pings = [...data.pings].sort((a, b) => Date.parse(a.occurredAt) - Date.parse(b.occurredAt));
  const checkIn = data.checkInAt ? Date.parse(data.checkInAt) : null;
  const checkOut = data.checkOutAt ? Date.parse(data.checkOutAt) : null;
  const first = pings[0];
  const last = pings[pings.length - 1];
  const start = checkIn ?? (first ? Date.parse(first.occurredAt) : null);
  if (start === null) return null;
  const lastTs = last ? Date.parse(last.occurredAt) : start;
  const end = Math.max(checkOut ?? (now !== null ? Math.max(now, lastTs) : lastTs), lastTs, start);

  const thresholdMin = gapThresholdMinutes(pings);
  const threshold = thresholdMin * MIN;
  const segments: TimelineSegment[] = [];

  if (!first) {
    push(segments, { kind: 'gap', from: start, to: end });
    return { start, end, segments, gapThresholdMin: thresholdMin };
  }

  const firstTs = Date.parse(first.occurredAt);
  if (firstTs > start) {
    push(segments, {
      kind: firstTs - start > threshold ? 'gap' : first.status,
      from: start,
      to: firstTs,
    });
  }
  for (let i = 0; i < pings.length - 1; i++) {
    const a = pings[i]!;
    const from = Date.parse(a.occurredAt);
    const to = Date.parse(pings[i + 1]!.occurredAt);
    push(segments, { kind: to - from > threshold ? 'gap' : a.status, from, to });
  }
  if (end > lastTs) {
    push(segments, {
      kind: end - lastTs > threshold ? 'gap' : last!.status,
      from: lastTs,
      to: end,
    });
  }
  return { start, end, segments, gapThresholdMin: thresholdMin };
}

export interface OutsideInterval {
  from: number;
  to: number;
  minutes: number;
  label: string;
}

/** Tashqarida bo'lgan oraliqlar: "10:20–10:45". */
export function outsideIntervals(timeline: Timeline | null): OutsideInterval[] {
  if (!timeline) return [];
  return timeline.segments
    .filter((s) => s.kind === 'outside')
    .map((s) => ({
      from: s.from,
      to: s.to,
      minutes: Math.round((s.to - s.from) / MIN),
      label: `${fmtTime(new Date(s.from).toISOString())}–${fmtTime(new Date(s.to).toISOString())}`,
    }));
}

/** 75 → "1 soat 15 daq", 18 → "18 daq". */
export function fmtMinutes(min: number): string {
  const m = Math.max(0, Math.round(min));
  if (m < 60) return `${m} daq`;
  const h = Math.floor(m / 60);
  const rest = m % 60;
  return rest ? `${h} soat ${rest} daq` : `${h} soat`;
}
