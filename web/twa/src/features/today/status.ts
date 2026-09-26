import { formatMeters, formatTime } from '@/shared/lib/format';
import { periodPhase } from '@/features/period/types';
import { isCheckedIn, isFinished, type TodayDto } from './types';

/**
 * Bugungi holat sarlavhasi + izohi — holat (v2 enum) va oynadan hisoblanadi; server `note` bo'lsa u ustun.
 * Bosh ekrandagi qisqa holat (`TodaySummary`) va QR sahifasidagi bo'sh holatlar shu matnlardan.
 */
export function describeToday(today: TodayDto): { title: string; note: string } {
  const { checkin, window: win } = today;
  if (isFinished(checkin)) {
    return checkin.checkOutAt
      ? {
          title: `Kun yakunlandi · ${formatTime(checkin.checkOutAt)}`,
          note: 'Check-out qayd etildi. Kundalik yuborilgan bo‘lsa, kun to‘liq hisoblanadi.',
        }
      : {
          title: 'Kun avtomatik yakunlandi',
          note: 'Check-out qilinmagan — server kunni avtomatik yopdi. Bu tyutorga ko‘rinadi.',
        };
  }
  if (isCheckedIn(checkin)) {
    return {
      title: `Belgilandingiz · ${formatTime(checkin.checkInAt)}`,
      note:
        checkin.note ??
        `Korxonadan ${formatMeters(checkin.distanceM)} masofada qayd etildi. Kun yakunlanishi uchun kundalik va check-out (${win.checkoutAt} dan) kerak.`,
    };
  }
  switch (checkin.status) {
    case 'absent':
      return {
        title: 'Bugun belgilanmadingiz',
        note: checkin.note ?? `Belgilanish oynasi (${win.start}–${win.closesAt}) yopilgan.`,
      };
    case 'excused':
      return { title: 'Bugun sababli', note: checkin.note ?? 'Tasdiqlangan ruxsat kuni.' };
    case 'dayOff':
      return {
        title: 'Bugun dam olish kuni',
        note: checkin.note ?? 'Belgilanish talab qilinmaydi.',
      };
    default:
      return win.isOpen
        ? {
            title: 'Belgilanish oynasi ochiq',
            note: `Oyna ${win.start}–${win.closesAt}. ${win.end} dan keyingi belgilanish "Kech keldi" bo'ladi.`,
          }
        : {
            title: 'Belgilanish oynasi yopiq',
            note:
              checkin.note ??
              `Oyna ${win.start}–${win.closesAt}. Belgilanish faqat oyna ochiq paytda qabul qilinadi.`,
          };
  }
}

/**
 * QR sahifasi nimani ko'rsatadi (TodayDto'dan):
 *  - `finished` — kelgan va ketgan (yoki avtomatik yopilgan): yakuniy holat;
 *  - `checkout` — korxonadasiz: oyna ochiq bo'lsa "Ketganini belgilash" oqimi, aks holda kutish;
 *  - `no-period` — davr biriktirilmagan · `gap` — davr hali boshlanmagan / tugagan (`PeriodGapCard`);
 *  - `no-place` — korxona (tasdiqlangan ariza) yo'q;
 *  - `checkin` — "Kelganini belgilash" oqimi (oyna ochiq);
 *  - `closed` — dam olish / bayram / sababli / kelmadi / oyna yopiq yoki hali ochilmagan.
 */
export type QrView =
  | { kind: 'finished' }
  | { kind: 'checkout'; open: boolean }
  | { kind: 'no-period' }
  | { kind: 'gap'; phase: 'upcoming' | 'ended' }
  | { kind: 'no-place' }
  | { kind: 'checkin' }
  | { kind: 'closed' };

export function qrView(today: TodayDto): QrView {
  const { checkin, window: win, period } = today;
  if (isFinished(checkin)) return { kind: 'finished' };
  if (isCheckedIn(checkin)) return { kind: 'checkout', open: win.isOpen };
  if (!period) return { kind: 'no-period' };
  const phase = periodPhase(period, today.date);
  if (phase !== 'ongoing') return { kind: 'gap', phase };
  if (!today.place) return { kind: 'no-place' };
  if (checkin.status === 'pending' && win.isOpen) return { kind: 'checkin' };
  return { kind: 'closed' };
}

/** Bosh ekrandan QR sahifasiga o'tish mumkinmi (belgilanish yoki ketishni belgilash hozir ochiq). */
export function canMarkNow(today: TodayDto): boolean {
  const view = qrView(today);
  return view.kind === 'checkin' || (view.kind === 'checkout' && view.open);
}
