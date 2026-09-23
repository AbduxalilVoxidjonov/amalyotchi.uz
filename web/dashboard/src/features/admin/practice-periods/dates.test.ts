import { calendarDays, formatRange, rangesOverlap, todayIso } from './dates';

describe('practice-periods/dates', () => {
  it('kalendar kunlar (chegaralar kiradi), teskari oraliq — null', () => {
    expect(calendarDays('2026-09-01', '2026-10-30')).toBe(60);
    expect(calendarDays('2026-09-01', '2026-09-01')).toBe(1);
    expect(calendarDays('2026-09-02', '2026-09-01')).toBeNull();
    expect(calendarDays('', '2026-09-01')).toBeNull();
  });

  it('oraliq formati va kesishish', () => {
    expect(formatRange('2026-09-01', '2026-10-30')).toBe('01.09.2026 — 30.10.2026');
    expect(rangesOverlap('2026-09-01', '2026-09-30', '2026-09-30', '2026-10-10')).toBe(true);
    expect(rangesOverlap('2026-09-01', '2026-09-29', '2026-09-30', '2026-10-10')).toBe(false);
  });

  it('bugun — Toshkent vaqti bo‘yicha', () => {
    // 2026-09-22 20:00 UTC = 2026-09-23 01:00 Toshkent.
    expect(todayIso(new Date('2026-09-22T20:00:00Z'))).toBe('2026-09-23');
  });
});
