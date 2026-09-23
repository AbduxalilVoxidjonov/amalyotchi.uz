import type { StudentPeriodOption } from './types';

/** Demo seed (kontrakt §6.11): kuzgi davr + bahorgi "Bahorgi amaliyot 2027" (2027-02-01…2027-03-15). */
export const MOCK_AUTUMN_PERIOD: StudentPeriodOption = {
  id: 'cccccccc-0000-4000-8000-000000000001',
  name: 'Kuzgi amaliyot 2026',
  startDate: '2026-10-01',
  endDate: '2026-11-15',
  status: 'active',
  isDefault: true,
};

export const MOCK_SPRING_PERIOD: StudentPeriodOption = {
  id: 'cccccccc-0000-4000-8000-000000000002',
  name: 'Bahorgi amaliyot 2027',
  startDate: '2027-02-01',
  endDate: '2027-03-15',
  status: 'planned',
  isDefault: false,
};

/** Ikki davr oralig'idagi mock sana (kuzgi tugagan, bahorgi 43 kundan keyin). */
export const MOCK_GAP_DATE = '2026-12-20';
