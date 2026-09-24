import type { Setting, SettingKey } from './types';

export type SettingGroupId = 'attendance' | 'report' | 'companies' | 'other';

export interface SettingGroupDef {
  id: SettingGroupId;
  title: string;
  /** Guruh ichidagi tartib. */
  keys: readonly SettingKey[];
}

/**
 * Sozlamalar guruhlari (UI). Backend ro'yxati data-driven — bu yerda yo'q kalit
 * "Boshqa" guruhiga tushadi (yangi kalit yo'qolib qolmaydi).
 */
export const SETTING_GROUPS: readonly SettingGroupDef[] = [
  {
    id: 'attendance',
    title: 'Davomat va geofence',
    keys: [
      'geofenceRadius',
      'lateTolerance',
      'minGpsAccuracy',
      'checkInWindow',
      'autoCheckout',
      'workDays',
    ],
  },
  {
    id: 'report',
    title: 'Talaba hisoboti — nimalar majburiy',
    keys: [
      'dailyReportRequired',
      'minReportLength',
      'checkinPhotoRequired',
      'checkinQrRequired',
      'diaryPdfRequired',
    ],
  },
  {
    id: 'companies',
    title: 'Korxonalar',
    keys: ['maxStudentsPerCompany'],
  },
];

export const OTHER_GROUP_TITLE = 'Boshqa';

export interface SettingGroup {
  id: SettingGroupId;
  title: string;
  settings: Setting[];
}

/** Sozlamalarni guruhlarga ajratadi; bo'sh guruhlar tashlab yuboriladi. Noma'lum kalitlar — server tartibida "Boshqa". */
export function groupSettings(settings: readonly Setting[]): SettingGroup[] {
  const byKey = new Map(settings.map((s) => [s.key, s]));
  const used = new Set<string>();
  const groups: SettingGroup[] = [];

  for (const def of SETTING_GROUPS) {
    const items: Setting[] = [];
    for (const key of def.keys) {
      const s = byKey.get(key);
      if (s) {
        items.push(s);
        used.add(key);
      }
    }
    if (items.length > 0) groups.push({ id: def.id, title: def.title, settings: items });
  }

  const rest = settings.filter((s) => !used.has(s.key));
  if (rest.length > 0) groups.push({ id: 'other', title: OTHER_GROUP_TITLE, settings: rest });
  return groups;
}
