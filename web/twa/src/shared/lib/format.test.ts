import { formatDate, formatPeriod, formatPhone, formatTime } from './format';

describe('format (kontrakt v2 → UI)', () => {
  it('DateTimeOffset → Toshkent soati; DateOnly → KK.OO.YYYY', () => {
    expect(formatTime('2026-09-14T03:51:00+00:00')).toBe('08:51');
    expect(formatTime('2026-10-12T04:02:00Z')).toBe('09:02');
    expect(formatTime(null)).toBe('');
    expect(formatDate('2026-09-14')).toBe('14.09.2026');
    expect(formatDate('2026-09-14T17:37:22.786142+00:00')).toBe('14.09.2026');
  });

  it('telefon 2-3-2-2 va davr qisqa yozuvi', () => {
    expect(formatPhone('+998901112233')).toBe('+998 90 111 22 33');
    expect(formatPhone('+1 555 0100')).toBe('+1 555 0100');
    expect(formatPhone(null)).toBe('');
    expect(formatPeriod('2026-08-31', '2026-10-14')).toBe('31.08–14.10.2026');
    expect(formatPeriod('2026-12-20', '2027-01-10')).toBe('20.12.2026–10.01.2027');
  });
});
