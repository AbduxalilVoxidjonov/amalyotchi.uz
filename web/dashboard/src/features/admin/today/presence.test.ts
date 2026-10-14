import { fmtAgoShort, matchesPresenceFilter, presenceChip } from './presence';
import { mockTodayRows } from './mocks';
import type { AttendanceRow, Presence } from './types';

const base: Presence = {
  state: 'inside',
  lastPingAt: '2026-10-14T05:15:00Z', // 10:15 Toshkent
  distanceM: 42,
  outsideMinutes: 18,
  mocked: false,
};

describe('presenceChip', () => {
  it.each([
    ['inside', 'Joyida', 'ok'],
    ['outside', 'Tashqarida · 18 daq', 'bad'],
    ['inaccurate', 'Aniqlik past', 'warn'],
    ['signalLost', "Signal yo'q · 10:15 dan", 'lost'],
    ['stopped', 'Ketgan', 'neutral'],
    ['noConsent', "Rozilik yo'q", 'neutral'],
  ] as const)('%s → "%s" (%s)', (state, label, tone) => {
    expect(presenceChip({ ...base, state })).toEqual({ label, tone });
  });

  it("signalLost, ping bo'lmasa — vaqtsiz", () => {
    expect(presenceChip({ ...base, state: 'signalLost', lastPingAt: null }).label).toBe(
      "Signal yo'q",
    );
  });
});

describe('fmtAgoShort', () => {
  const now = Date.parse('2026-10-14T05:18:30Z');
  it('nisbiy vaqt', () => {
    expect(fmtAgoShort('2026-10-14T05:18:10Z', now)).toBe('hozirgina');
    expect(fmtAgoShort('2026-10-14T05:15:00Z', now)).toBe('3 daq oldin');
    expect(fmtAgoShort('2026-10-14T02:10:00Z', now)).toBe('3 soat oldin');
    expect(fmtAgoShort('bad', now)).toBe('');
  });
});

describe('matchesPresenceFilter', () => {
  const row = (presence: Presence | null): AttendanceRow => ({ ...mockTodayRows[0]!, presence });
  it("Tashqarida / Signal yo'q bo'yicha", () => {
    expect(matchesPresenceFilter(row({ ...base, state: 'outside' }), 'outside')).toBe(true);
    expect(matchesPresenceFilter(row({ ...base, state: 'inside' }), 'outside')).toBe(false);
    expect(matchesPresenceFilter(row({ ...base, state: 'signalLost' }), 'signalLost')).toBe(true);
    expect(matchesPresenceFilter(row(null), 'signalLost')).toBe(false);
    expect(matchesPresenceFilter(row(null), 'all')).toBe(true);
  });
});
