import { describe, expect, it } from 'vitest';
import {
  defaultPeriodMonth,
  fmtPeriodDates,
  periodPhase,
  plannedPeriodText,
  stepMonthWithin,
} from './periods';

const P = { startDate: '2026-08-31', endDate: '2026-10-14' };

describe('periods', () => {
  it('"Tugagan" — active va endDate < bugun; qolganlari statusdan', () => {
    expect(periodPhase({ status: 'active', endDate: '2026-10-14' }, '2026-10-15')).toBe('ended');
    expect(periodPhase({ status: 'active', endDate: '2026-10-14' }, '2026-10-14')).toBe('active');
    expect(periodPhase({ status: 'closed', endDate: '2026-10-14' }, '2026-12-01')).toBe('closed');
    expect(periodPhase({ status: 'planned', endDate: '2027-04-30' }, '2026-09-23')).toBe('planned');
  });

  it('sanalar: bir yil — "31.08 — 14.10.2026", yillar farqli — to\'liq', () => {
    expect(fmtPeriodDates(P)).toBe('31.08 — 14.10.2026');
    expect(fmtPeriodDates({ startDate: '2026-12-01', endDate: '2027-01-20' })).toBe(
      '01.12.2026 — 20.01.2027',
    );
    expect(plannedPeriodText({ startDate: '2027-02-01' })).toBe(
      'Bu davr 01.02.2027 dan boshlanadi',
    );
  });

  it('sukut oy: faol → joriy, tugagan → oxirgi, rejada → birinchi oy', () => {
    expect(defaultPeriodMonth(P, '2026-09-23')).toEqual({ year: 2026, month: 9 });
    expect(defaultPeriodMonth(P, '2027-01-05')).toEqual({ year: 2026, month: 10 });
    expect(defaultPeriodMonth(P, '2026-05-01')).toEqual({ year: 2026, month: 8 });
  });

  it('oy navigatsiyasi davr chegarasidan chiqmaydi', () => {
    expect(stepMonthWithin({ year: 2026, month: 8 }, -1, P)).toEqual({ year: 2026, month: 8 });
    expect(stepMonthWithin({ year: 2026, month: 10 }, 1, P)).toEqual({ year: 2026, month: 10 });
    expect(stepMonthWithin({ year: 2026, month: 9 }, 1, P)).toEqual({ year: 2026, month: 10 });
    expect(stepMonthWithin({ year: 2026, month: 12 }, 1, null)).toEqual({ year: 2027, month: 1 });
  });
});
