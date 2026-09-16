import { useRef } from 'react';
import { Badge, Button, Card, Eyebrow, FactGrid, type FactItem } from '@/shared/ui';
import { formatDate, formatMeters, formatTime } from '@/shared/lib/format';
import type { CheckinFlow } from '../hooks';
import { PHOTO_ACCEPT } from '../photo';
import { ATTENDANCE_STATUS, isCheckedIn, isFinished, type TodayDto } from '../types';
import { SelfieCapture } from './SelfieCapture';
import styles from './CheckinCard.module.css';

export interface CheckinCardProps {
  today: TodayDto;
  /** Selfie oqimi (`useCheckinFlow`) — xato/yuklanish holatlari shu yerda. */
  flow: CheckinFlow;
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

/** SPEC-SCREENS §8 chap section — check-in + selfie (presentation). */
export function CheckinCard({ today, flow }: CheckinCardProps) {
  const { checkin, window: win } = today;
  const cameraRef = useRef<HTMLInputElement>(null);
  const checkedIn = isCheckedIn(checkin);
  const finished = isFinished(checkin);
  const canAct = checkedIn || checkin.status === 'pending';
  const { title, note } = describe(today);
  const status = ATTENDANCE_STATUS[checkin.status];

  /**
   * Kamera SHU foydalanuvchi harakatida ochiladi (`input.click()`) — `await` dan keyin
   * chaqirilsa iOS/Telegram WebView bloklaydi. Joylashuv so'rovi `flow.start` da parallel boshlanadi.
   */
  function openCamera() {
    const el = cameraRef.current;
    if (!el) return;
    el.value = '';
    el.click();
  }

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

      <input
        ref={cameraRef}
        type="file"
        hidden
        accept={PHOTO_ACCEPT}
        capture="user"
        aria-label="Selfie olish"
        onChange={(e) => flow.selectPhoto(e.target.files?.[0])}
      />

      {finished ? (
        <div className={styles.done}>
          <Badge status={checkin.autoClosed && !checkin.checkOutAt ? 'late' : 'ok'} size="lg">
            {checkin.autoClosed && !checkin.checkOutAt ? 'Avtomatik yopildi' : 'Yakunlandi'}
          </Badge>
        </div>
      ) : canAct && flow.phase === 'idle' ? (
        <Button
          variant="checkin"
          tone={checkedIn ? 'dark' : 'accent'}
          className={styles.button}
          onClick={() => {
            flow.start(checkedIn ? 'checkout' : 'checkin');
            openCamera();
          }}
          disabled={!win.isOpen}
        >
          {checkedIn ? 'KETDIM' : 'KELDIM'}
        </Button>
      ) : null}

      {canAct && flow.phase !== 'idle' && <SelfieCapture flow={flow} onOpenCamera={openCamera} />}

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
