import type { PeriodGroupStudent } from './types';

/** Davr ichidagi guruh talabalari jadvali: client tomonda qidiruv va saralash. */
export type StudentSortKey =
  'fullName' | 'attendancePct' | 'suspiciousDays' | 'diaryCount' | 'total';
export interface StudentSort {
  key: StudentSortKey;
  dir: 'asc' | 'desc';
}

export const DEFAULT_STUDENT_SORT: StudentSort = { key: 'fullName', dir: 'asc' };

const collator = new Intl.Collator('uz', { sensitivity: 'base' });

export function sortStudents(
  rows: readonly PeriodGroupStudent[],
  sort: StudentSort,
): PeriodGroupStudent[] {
  const sign = sort.dir === 'asc' ? 1 : -1;
  return [...rows].sort((a, b) => {
    const primary =
      sort.key === 'fullName'
        ? collator.compare(a.fullName, b.fullName)
        : a[sort.key] - b[sort.key];
    // Teng qiymatlarda FISH bo'yicha — tartib barqaror.
    return primary !== 0 ? primary * sign : collator.compare(a.fullName, b.fullName);
  });
}

function normalize(s: string): string {
  return s
    .toLocaleLowerCase('uz')
    .replace(/[‘’ʻʼ`']/g, "'")
    .trim();
}

export function filterStudents(
  rows: readonly PeriodGroupStudent[],
  search: string,
): readonly PeriodGroupStudent[] {
  const q = normalize(search);
  if (!q) return rows;
  return rows.filter((r) => normalize(r.fullName).includes(q) || r.hemisId.includes(q));
}
