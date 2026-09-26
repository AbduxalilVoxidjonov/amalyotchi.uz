import type { PeriodDay, PeriodDayStatus } from './types';
import { isPeriodEnded, summarizePeriodDays } from './summary';

const TODAY = '2026-09-24';

describe('isPeriodEnded', () => {
  it('yopilgan (closed) — sanasi hali tugamagan bo‘lsa ham tugagan', () => {
    expect(isPeriodEnded({ status: 'closed', endDate: '2026-10-14' }, TODAY)).toBe(true);
    expect(isPeriodEnded({ status: 'closed', endDate: '2026-07-11' }, TODAY)).toBe(true);
  });

  it('faol, lekin endDate bugundan oldin — tugagan', () => {
    expect(isPeriodEnded({ status: 'active', endDate: '2026-09-23' }, TODAY)).toBe(true);
  });

  it('faol va endDate bugun yoki keyin — tugamagan', () => {
    expect(isPeriodEnded({ status: 'active', endDate: TODAY }, TODAY)).toBe(false);
    expect(isPeriodEnded({ status: 'active', endDate: '2026-10-14' }, TODAY)).toBe(false);
  });

  it('rejadagi davr — tugamagan', () => {
    expect(isPeriodEnded({ status: 'planned', endDate: '2027-03-15' }, TODAY)).toBe(false);
  });
});

const day = (status: PeriodDayStatus, i: number): PeriodDay => ({
  date: `2026-09-${String(i + 1).padStart(2, '0')}`,
  weekday: (i % 7) + 1,
  isWorkDay: status !== 'dayOff',
  holiday: null,
  status,
  checkInAt: null,
  checkOutAt: null,
  autoClosed: false,
  suspicious: false,
  manual: false,
  diary: null,
});

const days = (list: PeriodDayStatus[]) => list.map(day);

describe('summarizePeriodDays', () => {
  it('keldi = present + late; future/pending/dayOff hisobga kirmaydi', () => {
    const s = summarizePeriodDays(
      days([
        'present',
        'present',
        'present',
        'late',
        'late',
        'absent',
        'excused',
        'dayOff',
        'pending',
        'future',
        'future',
      ]),
    );
    expect(s).toMatchObject({ present: 3, late: 2, attended: 5, absent: 1, excused: 1 });
    // 5 / (5 + 1) — sababli maxrajdan chiqariladi.
    expect(s.attendancePct).toBeCloseTo(83.333, 2);
  });

  it('hamma kun keldi → 100%; sababli ta’sir qilmaydi', () => {
    expect(summarizePeriodDays(days(['present', 'late', 'excused'])).attendancePct).toBe(100);
  });

  it('hisoblanadigan kun yo‘q → foiz null', () => {
    const s = summarizePeriodDays(days(['excused', 'dayOff', 'future']));
    expect(s).toMatchObject({ attended: 0, absent: 0, excused: 1, attendancePct: null });
    expect(summarizePeriodDays([]).attendancePct).toBeNull();
  });
});
