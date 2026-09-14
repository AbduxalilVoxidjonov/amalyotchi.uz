/** SPEC-SCREENS §12 `isHisobot` (tyutor + admin, bir xil blok) — backend `ReportsCatalog`. */
export type ReportFormat = 'pdf' | 'xlsx';

export interface ReportCard {
  id: string;
  name: string;
  /** "pdf" / "xlsx" */
  formats: ReportFormat[];
  desc: string;
  /** Fayl generatsiyasi hozircha yo'q (M14) — false, sabab `note` da. */
  available: boolean;
  note: string | null;
}

export interface ReportFilter {
  /** Faol davr sanalari (DateOnly) — faol davr bo'lmasa null. */
  dateFrom: string | null;
  dateTo: string | null;
  /** Ko'lam yorlig'i: tyutor — "412-22, 413-22", admin — "Barcha fakultetlar". */
  scope: string;
  /** Tyutor guruhlari (admin uchun bo'sh). */
  groups: string[];
  studentCount: number;
}

/** GET /api/reports */
export interface ReportsCatalog {
  filter: ReportFilter;
  reports: ReportCard[];
}

const FORMAT_LABEL: Record<ReportFormat, string> = { pdf: 'PDF', xlsx: 'Excel' };

/** ["pdf","xlsx"] → "PDF / Excel". */
export function formatLabel(formats: readonly ReportFormat[]): string {
  return formats.map((f) => FORMAT_LABEL[f] ?? f.toUpperCase()).join(' / ');
}
