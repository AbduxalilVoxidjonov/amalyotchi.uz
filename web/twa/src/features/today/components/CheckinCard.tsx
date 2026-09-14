import { Badge, Button, Card, Eyebrow, FactGrid, type FactItem } from '@/shared/ui';
import { formatDate, formatMeters, formatTime } from '@/shared/lib/format';
import { ATTENDANCE_STATUS, isCheckedIn, isFinished, type TodayDto } from '../types';
import styles from './CheckinCard.module.css';

export interface CheckinCardProps {
  today: TodayDto;
  /** Joylashuv aniqlanmoqda / server javobi kutilmoqda. */
  pending: boolean;
  /** Oxirgi urinish xatosi (GeoError yoki ApiError matni). */
  error: string | null;
  onToggle: () => void;
}

/** Sarlavha + izoh — holat (v2 enum) va oynadan hisoblanadi; server `note` bo'lsa u ustun. */
function describe(today: TodayDto): { title: string; note: string } {
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

/** SPEC-SCREENS §8 chap section — check-in (presentation). */
export function CheckinCard({ today, pending, error, onToggle }: CheckinCardProps) {
  const { checkin, window: win } = today;
  const checkedIn = isCheckedIn(checkin);
  const finished = isFinished(checkin);
  const canAct = checkedIn || checkin.status === 'pending';
  const { title, note } = describe(today);
  const status = ATTENDANCE_STATUS[checkin.status];

  const facts: FactItem[] = [
    { k: 'Holat', v: status.label, tone: status.tone },
    { k: 'Masofa', v: `${formatMeters(checkin.distanceM)} / ${formatMeters(checkin.radiusM)}` },
    { k: 'GPS aniqligi', v: formatMeters(checkin.gpsAccuracyM) },
    { k: 'Check-out oynasi', v: win.checkoutAt },
  ];

  return (
    <Card padded="lg" aria-labelledby="checkin-title">
      <Eyebrow as="div" spacing="wide" margin="none">
        Bugun · {formatDate(today.date)}
      </Eyebrow>
      <h2 id="checkin-title" className={styles.title}>
        {title}
      </h2>
      <p className={styles.note}>{note}</p>

      {finished ? (
        <div className={styles.done}>
          <Badge status={checkin.autoClosed && !checkin.checkOutAt ? 'late' : 'ok'} size="lg">
            {checkin.autoClosed && !checkin.checkOutAt ? 'Avtomatik yopildi' : 'Yakunlandi'}
          </Badge>
        </div>
      ) : canAct ? (
        <Button
          variant="checkin"
          tone={checkedIn ? 'dark' : 'accent'}
          className={styles.button}
          onClick={onToggle}
          disabled={pending || !win.isOpen}
          aria-busy={pending || undefined}
        >
          {pending ? 'Joylashuv aniqlanmoqda…' : checkedIn ? 'KETDIM' : 'KELDIM'}
        </Button>
      ) : null}

      {error && (
        <p className={styles.error} role="alert">
          {error}
        </p>
      )}

      <FactGrid items={facts} columns={2} className={styles.facts} />

      {checkin.suspicious && (
        <p className={styles.error} role="status">
          Belgilanish shubhali deb belgilandi — tyutor tekshiradi.
        </p>
      )}

      <p className={styles.hint}>
        Belgilanish faqat korxona radiusi ichidan qabul qilinadi. Har bir urinish — muvaffaqiyatsizi
        ham — tyutorga ko'rinadi.
      </p>
    </Card>
  );
}
