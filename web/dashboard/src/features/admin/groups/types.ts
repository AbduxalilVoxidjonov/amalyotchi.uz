/** `PracticePeriodStatus.cs` — camelCase. */
export type PeriodStatus = 'planned' | 'active' | 'closed';

export interface GroupPeriod {
  id: string;
  name: string;
  status: PeriodStatus;
  /** DateOnly "2026-08-31". */
  startDate: string;
  endDate: string;
}

/** Kontrakt v2 `Group` (backend `GroupRow`). */
export interface Group {
  id: string;
  /** "412-22" */
  code: string;
  course: number;
  direction: string;
  faculty: string;
  facultyCode: string;
  tutorId: string | null;
  /** Tyutor FISH; biriktirilmagan — null. */
  tutor: string | null;
  students: number;
  /** Faol davr boshidan kechagacha. */
  attendancePct: number;
  /** Faol amaliyot davri, yo'q bo'lsa null. */
  period: GroupPeriod | null;
}
