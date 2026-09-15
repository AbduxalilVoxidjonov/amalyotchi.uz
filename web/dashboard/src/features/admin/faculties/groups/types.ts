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

/**
 * `GET /directions/{id}/groups` qatori (hierarchy-contract.md — mavjud `GroupRow` + `isActive`).
 * `direction`/`faculty`/`facultyCode` maydonlari API kontraktida qoladi (global `/admin/groups`
 * bilan bir xil shakl), lekin bu jadval breadcrumb ichida ko'rsatilgani uchun alohida ustun
 * qilib chiqarilmaydi.
 */
export interface GroupRow {
  id: string;
  /** Guruh nomi, masalan "412-22". */
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
  isActive: boolean;
}

/** `GET/POST/PUT/PATCH /groups/{id}` javobi. */
export interface GroupDto {
  id: string;
  directionId: string;
  name: string;
  course: number;
  isActive: boolean;
  /** Faol `AcademicYear` — "2026-2027". */
  academicYear: string;
}

/** `POST/PUT /groups` body'si. */
export interface GroupInput {
  name: string;
  course: number;
}
