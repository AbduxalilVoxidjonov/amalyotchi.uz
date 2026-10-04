/**
 * Kontrakt v2 `AdminSettings` (backend `AdminSettingsDto`). Qiymatlar xom matn (`"200"`, `"true"`,
 * `"1,2,3,4,5,6"`) — ko'rinish (`unit`, Ha/Yo'q) frontend'da. Kalitlar ro'yxati backend'niki (`SettingKeys.cs`).
 */
export type SettingType = 'int' | 'bool' | 'weekdays';

/**
 * Frontend biladigan kalitlar (backend `SettingKeys.cs`). `Setting.key` ataylab `string` —
 * backend yangi kalit qo'shsa UI uni "Boshqa" guruhida ko'rsatadi.
 */
export type SettingKey =
  | 'geofenceRadius'
  | 'lateTolerance'
  | 'minGpsAccuracy'
  | 'autoCheckout'
  | 'workDays'
  | 'dailyReportRequired'
  | 'minReportLength'
  | 'checkInWindow'
  | 'checkinPhotoRequired'
  | 'checkinQrRequired'
  | 'maxStudentsPerCompany'
  | 'diaryPdfRequired'
  | 'faceVerificationEnabled'
  | 'faceMatchThreshold';

export interface Setting {
  /** `SettingKey` yoki backend'ning yangi (frontend hali bilmaydigan) kaliti. */
  key: SettingKey | (string & {});
  label: string;
  value: string;
  type: SettingType;
  /** "m" · "min" · "chars" · null */
  unit: string | null;
  note: string;
  /** Faqat `int` turida. */
  min: number | null;
  max: number | null;
  updatedAt: string | null;
}

export interface Holiday {
  id: string;
  /** DateOnly; takrorlanuvchida yil shartli ("2000-03-08"). */
  date: string;
  name: string;
  isRecurring: boolean;
}

/** `DocumentTemplateKind.cs` — camelCase. */
export type DocTemplateKind = 'contract' | 'referral' | 'reference';

export interface DocTemplate {
  id: string;
  kind: DocTemplateKind;
  name: string;
  fileName: string;
  /** `GET /api/files/{id}` */
  url: string;
}

export interface AdminSettings {
  settings: Setting[];
  holidays: Holiday[];
  /** Backend yuboradi, lekin Sozlamalar UI'da ko'rsatilmaydi. */
  templates: DocTemplate[];
}

/** PUT so'rovi: faqat o'zgargan kalitlar. 400 → `errors{key:[...]}`. */
export interface SettingsUpdate {
  values: Record<string, string>;
}

/** Backend `unit` → ko'rinish. */
export const UNIT_LABEL: Record<string, string> = {
  m: 'm',
  min: 'daqiqa',
  chars: 'belgi',
  percent: '%',
  '%': '%',
};

export const DOC_TEMPLATE_KIND_LABEL: Record<DocTemplateKind, string> = {
  contract: 'Shartnoma',
  referral: "Yo'llanma",
  reference: 'Tavsifnoma',
};
