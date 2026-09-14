/** Kontrakt v2 `Company` (backend `CompanyRow`). Belgi: null (—) · largeRadius (late) · suspicious (bad). */
export type CompanyFlag = 'largeRadius' | 'suspicious';

export interface Company {
  id: string;
  name: string;
  /** STIR xom 9 raqam "305881204" (UI: "305 881 204"). */
  tin: string;
  activity: string;
  address: string;
  radiusM: number;
  /** Arizasi tasdiqlangan talabalar. */
  students: number;
  /** Shu korxonadagi talabalarning shubhali davomat kunlari. */
  suspiciousDays: number;
  isActive: boolean;
  flag: CompanyFlag | null;
}

export const COMPANY_FLAG_LABEL: Record<CompanyFlag, { label: string; kind: 'late' | 'bad' }> = {
  largeRadius: { label: 'Katta radius', kind: 'late' },
  suspicious: { label: "Shubhali to'planish", kind: 'bad' },
};
