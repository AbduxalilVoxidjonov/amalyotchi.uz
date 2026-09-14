import {
  formatCount,
  formatDayMonth,
  formatHours,
  formatPhone,
  formatRelative,
  formatScope,
  formatShortDateTime,
  formatTin,
} from './format';

describe('admin/shared/format', () => {
  it('formatCount — thin space', () => {
    expect(formatCount(1284)).toBe('1 284');
    expect(formatCount(999)).toBe('999');
    expect(formatCount(1000000)).toBe('1 000 000');
  });

  it('formatShortDateTime — Toshkent (UTC+5)', () => {
    expect(formatShortDateTime('2026-10-12T04:31:00+00:00')).toBe('12.10 09:31');
    expect(formatShortDateTime('bad')).toBe('—');
  });

  it('formatDayMonth — DateOnly', () => {
    expect(formatDayMonth('2000-03-08')).toBe('08.03');
    expect(formatDayMonth('2027-03-20', true)).toBe('20.03.2027');
    expect(formatDayMonth('x')).toBe('—');
  });

  it('formatPhone — E.164 → +998 90 123-45-67', () => {
    expect(formatPhone('+998901234567')).toBe('+998 90 123-45-67');
    expect(formatPhone('+12025550123')).toBe('+12025550123');
    expect(formatPhone(null)).toBe('—');
  });

  it('formatTin — 305881204 → 305 881 204', () => {
    expect(formatTin('305881204')).toBe('305 881 204');
    expect(formatTin('12')).toBe('12');
  });

  it('formatHours — daqiqa / soat / kun', () => {
    expect(formatHours(null)).toBe('—');
    expect(formatHours(0.5)).toBe('30 daqiqa');
    expect(formatHours(1.44)).toBe('1.4 soat');
    expect(formatHours(33.4)).toBe('1.4 kun');
    expect(formatHours(48)).toBe('2 kun');
  });

  it('formatRelative — oldin', () => {
    const now = Date.parse('2026-09-14T12:00:00Z');
    expect(formatRelative('2026-09-14T11:59:40Z', now)).toBe('hozirgina');
    expect(formatRelative('2026-09-14T11:55:00Z', now)).toBe('5 daqiqa oldin');
    expect(formatRelative('2026-09-14T10:00:00Z', now)).toBe('2 soat oldin');
    expect(formatRelative('2026-09-11T12:00:00Z', now)).toBe('3 kun oldin');
    expect(formatRelative('2026-06-01T04:00:00Z', now)).toBe('01.06 09:00');
    expect(formatRelative(null, now)).toBe('—');
  });

  it('formatScope — fakultet kodi + guruhlar', () => {
    expect(formatScope('AT', ['412-22', '413-22'])).toBe('AT · 412-22, 413-22');
    expect(formatScope('IM', ['1', '2', '3', '4', '5'])).toBe('IM · 5 guruh');
    expect(formatScope(null, [])).toBe("guruh yo'q");
  });
});
