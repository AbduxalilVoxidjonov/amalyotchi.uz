import { countWorkDays, normalizeWeekdays, parseWeekdays, toggleWeekday } from './weekdays';

describe('weekdays', () => {
  it('parse / normalize: noto‘g‘ri qismlar tashlanadi, tartiblanadi', () => {
    expect([...parseWeekdays('1, 3,x,8,0,3')]).toEqual([1, 3]);
    expect(normalizeWeekdays('6,1,3,1')).toBe('1,3,6');
    expect(normalizeWeekdays('')).toBe('');
  });

  it('toggle: qo‘shadi va olib tashlaydi, CSV tartibli', () => {
    expect(toggleWeekday('1,2,3', 7)).toBe('1,2,3,7');
    expect(toggleWeekday('1,2,3', 2)).toBe('1,3');
    expect(toggleWeekday('5', 5)).toBe('');
  });

  it('countWorkDays: hafta kunlari bo‘yicha (bayramlarsiz)', () => {
    // 2026-11-02 — dushanba; 2 hafta.
    expect(countWorkDays('2026-11-02', '2026-11-15', '1,2,3,4,5')).toBe(10);
    expect(countWorkDays('2026-11-02', '2026-11-15', '1,2,3,4,5,6')).toBe(12);
    expect(countWorkDays('2026-11-02', '2026-11-02', '7')).toBe(0);
    expect(countWorkDays('2026-11-10', '2026-11-01', '1')).toBeNull();
    expect(countWorkDays('', '2026-11-01', '1')).toBeNull();
  });
});
