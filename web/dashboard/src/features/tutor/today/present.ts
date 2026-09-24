import type { StatusKind } from '@/shared/ui';
import { fmtDistance } from '../format';
import type { TodayAlert, TodayStats } from './types';

export interface StatTileModel {
  key: 'present' | 'late' | 'absent' | 'excused' | 'reports';
  label: string;
  value: string;
  note: string;
  dot: StatusKind;
}

/** SPEC-SCREENS §3 `todayStats` — 5 ta tile; qiymatlar backend `stats` dan, izohlar dizayndagi kabi. */
export function buildStatTiles(stats: TodayStats): StatTileModel[] {
  return [
    { key: 'present', label: 'Keldi', value: String(stats.present), note: 'radius ichida', dot: 'ok' },
    { key: 'late', label: 'Kech keldi', value: String(stats.late), note: '09:15 dan keyin', dot: 'late' },
    {
      key: 'absent',
      label: 'Kelmadi',
      value: String(stats.absent),
      note: stats.pending > 0 ? `${stats.pending} ta hali belgilanmadi` : '10:30 da belgilandi',
      dot: 'bad',
    },
    {
      key: 'excused',
      label: 'Sababli',
      value: String(stats.excused),
      note: 'ruxsat tasdiqlangan',
      dot: 'info',
    },
    {
      key: 'reports',
      label: 'Hisobot',
      value: `${stats.diaries}/${stats.total}`,
      note: 'bugun yozilgan',
      dot: 'neu',
    },
  ];
}

export interface AlertModel {
  id: string;
  text: string;
  action: string;
  href: string;
}

/** `alerts[].kind` → o'zbekcha matn + tugma (SPEC-SCREENS §17 `showAlerts`). */
export function presentAlert(alert: TodayAlert): AlertModel {
  const n = alert.count;
  switch (alert.kind) {
    case 'outOfRadius': {
      const far =
        alert.maxDistanceM != null ? ` (eng uzog'i ${fmtDistance(alert.maxDistanceM)})` : '';
      return {
        id: alert.kind,
        text: `${n} ta talaba radius tashqarisidan belgilanishga urindi${far}`,
        action: "Ko'rish",
        href: alert.href,
      };
    }
    case 'notCheckedIn':
      return {
        id: alert.kind,
        text: `${n} ta talaba bugun belgilanmadi — Kelmadi deb qayd etildi`,
        action: "Ko'rish",
        href: alert.href,
      };
    case 'newApplications':
      return {
        id: alert.kind,
        text: `${n} ta yangi ariza tekshirishni kutmoqda`,
        action: "Ko'rib chiqish",
        href: alert.href,
      };
  }
}
