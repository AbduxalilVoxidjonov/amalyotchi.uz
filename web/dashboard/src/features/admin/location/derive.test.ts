import { buildTimeline, fmtMinutes, gapThresholdMinutes, outsideIntervals } from './derive';
import type { LocationPing, PingStatus } from './types';

/** "10:20" (Toshkent) → 2026-10-14 UTC ISO. */
const t = (hhmm: string) => new Date(`2026-10-14T${hhmm}:00+05:00`).toISOString();

function ping(hhmm: string, status: PingStatus = 'inside'): LocationPing {
  return {
    occurredAt: t(hhmm),
    lat: 41.31,
    lng: 69.28,
    accuracyM: 10,
    distanceM: status === 'outside' ? 400 : 40,
    status,
    isMocked: false,
    source: 'background',
  };
}

describe('buildTimeline', () => {
  it("tashqarida oraliq, uzilish va check-out bilan bo'laklar", () => {
    const pings = [
      ping('09:05'),
      ping('09:10'),
      ping('09:15', 'outside'),
      ping('09:20', 'outside'),
      ping('09:25'),
      // 09:25 → 10:00 — 35 daq ping yo'q (chegara 10 daq) → uzilish
      ping('10:00'),
    ];
    const tl = buildTimeline({ pings, checkInAt: t('09:02'), checkOutAt: t('10:05') }, null)!;
    expect(gapThresholdMinutes(pings)).toBe(10);
    expect(tl.start).toBe(Date.parse(t('09:02')));
    expect(tl.end).toBe(Date.parse(t('10:05')));
    expect(tl.segments.map((s) => s.kind)).toEqual(['inside', 'outside', 'gap', 'inside']);

    const out = outsideIntervals(tl);
    expect(out).toHaveLength(1);
    expect(out[0]).toMatchObject({ label: '09:15–09:25', minutes: 10 });
  });

  it("bugun: oxirgi ping'dan hozirgacha uzoq — signal yo'q bo'lagi", () => {
    const pings = [ping('09:05'), ping('09:10'), ping('09:15')];
    const tl = buildTimeline(
      { pings, checkInAt: t('09:00'), checkOutAt: null },
      Date.parse(t('11:00')),
    )!;
    expect(tl.segments.at(-1)).toMatchObject({ kind: 'gap', to: Date.parse(t('11:00')) });
  });

  it("ping'lar 2 tadan kam → sukut chegarasi 2 × 120 s = 4 daq", () => {
    expect(gapThresholdMinutes([])).toBe(4);
    expect(gapThresholdMinutes([ping('09:10')])).toBe(4);
  });

  it("check-in ham ping ham yo'q → null", () => {
    expect(buildTimeline({ pings: [], checkInAt: null, checkOutAt: null }, null)).toBeNull();
  });
});

describe('fmtMinutes', () => {
  it('daqiqa / soat', () => {
    expect(fmtMinutes(18)).toBe('18 daq');
    expect(fmtMinutes(60)).toBe('1 soat');
    expect(fmtMinutes(75)).toBe('1 soat 15 daq');
  });
});
