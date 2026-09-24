import { Link } from 'react-router-dom';
import { Badge, Button, Card, Eyebrow } from '@/shared/ui';
import { isApiError } from '@/shared/api/client';
import { daysBetween, formatDate, formatPeriod } from '@/shared/lib/format';
import type { StudentPeriodOption } from '@/features/period/types';
import { usePlaceQuery } from '@/features/place/hooks';
import { APPLICATION_STATUS } from '@/features/place/types';
import type { TodayDto } from '../types';
import styles from './PeriodGapCard.module.css';

export interface PeriodGapCardProps {
  today: TodayDto;
  period: StudentPeriodOption;
  /** `upcoming` — keyingi davr hali boshlanmagan · `ended` — faqat tugagan davr (keyingisi yo'q). */
  phase: 'upcoming' | 'ended';
}

function daysLeftLabel(days: number): string {
  if (days <= 0) return 'Bugun boshlanadi';
  if (days === 1) return 'Ertaga boshlanadi';
  return `${days} kun qoldi`;
}

/**
 * Kelgusi davr uchun amaliyot joyi arizasi holati (§4.6: tanaffusda GET place — bahorgi ariza,
 * yo'q bo'lsa 404). Ariza yo'q → "Amaliyot joyini yuborish" (STIR formasi `/joyim` da).
 */
function UpcomingPlaceAction() {
  const place = usePlaceQuery();
  if (place.isPending) return null;
  if (place.isError) {
    if (!isApiError(place.error) || place.error.kind !== 'not-found') return null;
    return (
      <div className={styles.action}>
        <p className={styles.hint}>
          Yangi davr uchun amaliyot joyi arizasi hali yuborilmagan — davr boshlanishidan oldin
          yuboring.
        </p>
        <Button asChild variant="primary" radius="md2" block>
          <Link to="/joyim">Amaliyot joyini yuborish</Link>
        </Button>
      </div>
    );
  }
  const status = APPLICATION_STATUS[place.data.status];
  return (
    <div className={styles.placeRow}>
      <span className={styles.placeText}>
        Amaliyot joyi: <Link to="/joyim">{place.data.company}</Link>
      </span>
      <Badge status={status.kind}>{status.label}</Badge>
    </div>
  );
}

/**
 * Ikki davr oralig'i (kontrakt v3.5 §4.6): belgilanish yopiq — check-in tugmasi o'rniga davr holati.
 * Server `checkin.note` (`"Amaliyot davri hali boshlanmagan: …"`) shu ma'lumotdan tuzilgan.
 */
export function PeriodGapCard({ today, period, phase }: PeriodGapCardProps) {
  if (phase === 'upcoming') {
    const days = daysBetween(today.date, period.startDate);
    return (
      <Card padded="lg" aria-labelledby="period-gap-title">
        <Eyebrow as="div" spacing="wide" margin="none">
          Bugun · {formatDate(today.date)} · Amaliyot davri hali boshlanmagan
        </Eyebrow>
        <h2 id="period-gap-title" className={styles.title}>
          {period.name}
        </h2>
        <div className={styles.meta}>
          <span className={styles.starts}>{formatDate(period.startDate)} dan boshlanadi</span>
          <Badge status="info" size="lg">
            {daysLeftLabel(days)}
          </Badge>
        </div>
        <p className={styles.note}>
          Davr {formatPeriod(period.startDate, period.endDate)}. Belgilanish (KELDIM) davr
          boshlangan kundan ochiladi.
        </p>
        <UpcomingPlaceAction />
      </Card>
    );
  }

  return (
    <Card padded="lg" aria-labelledby="period-gap-title">
      <Eyebrow as="div" spacing="wide" margin="none">
        Bugun · {formatDate(today.date)}
      </Eyebrow>
      <h2 id="period-gap-title" className={styles.title}>
        Amaliyot davri tugagan: {period.name}
      </h2>
      <p className={styles.note}>
        Davr {formatPeriod(period.startDate, period.endDate)}. Yangi davr biriktirilgach belgilanish
        qayta ochiladi. Natijalaringiz quyidagi portfolioda saqlanadi.
      </p>
    </Card>
  );
}
