import { useId } from 'react';
import { Link } from 'react-router-dom';
import { Button, Eyebrow } from '@/shared/ui';
import { ErrorState, LoadingState } from '../../components/QueryState';
import { fmtTime, todayInTashkent } from '../../shared/fmt';
import { fmtMinutes } from '../derive';
import { useLocationPingsQuery } from '../hooks';
import { locationTrackHref } from '../paths';
import { sortByTime } from '../route';
import type { LocationPingsResponse } from '../types';
import { TrackMap } from './TrackMap';
import styles from './DayRoute.module.css';

/** Modal ichida — telefonda ham sig'adigan balandlik. */
const ROUTE_MAP_HEIGHT = 300;
const ROUTE_MAX_ARROWS = 10;

export interface DayRouteProps {
  studentId: string;
  /** Iz sahifasi sarlavhasi uchun (javobda ism yo'q). */
  studentName?: string | undefined;
  /** DateOnly — "2026-10-14". */
  date: string;
}

/** Bir qatorli xulosa: nuqtalar soni, birinchi → oxirgi vaqt, tashqarida vaqt. */
function RouteMeta({ data }: { data: LocationPingsResponse }) {
  const ordered = sortByTime(data.pings);
  const first = ordered[0]!;
  const last = ordered[ordered.length - 1]!;
  const parts = [
    `${ordered.length} ta nuqta`,
    ordered.length > 1
      ? `${fmtTime(first.occurredAt)} → ${fmtTime(last.occurredAt)}`
      : fmtTime(first.occurredAt),
  ];
  if (data.summary.outsideMinutes > 0)
    parts.push(`tashqarida ${fmtMinutes(data.summary.outsideMinutes)}`);
  return <p className={styles.meta}>{parts.join(' · ')}</p>;
}

/**
 * Talaba profili · kun oynasi: o'sha kundagi harakat yo'li (jonli joylashuv ping'lari) — vaqt
 * tartibidagi chiziq, yo'nalish strelkalari, boshlanish (A) / tugash (B) belgilari, korxona radiusi.
 * Ma'lumot iz sahifasi bilan bir xil so'rovdan (`useLocationPingsQuery`) — kesh umumiy.
 */
export function DayRoute({ studentId, studentName, date }: DayRouteProps) {
  const headingId = useId();
  const isToday = date === todayInTashkent();
  const query = useLocationPingsQuery(studentId, date, isToday);
  const href = locationTrackHref(studentId, { name: studentName, date, from: 'student' });

  let body;
  if (query.status === 'pending') {
    body = <LoadingState label="Joylashuv izi yuklanmoqda…" />;
  } else if (query.status === 'error') {
    body = <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  } else if (query.data.pings.length === 0) {
    body = (
      <div className={styles.empty} role="status">
        <p className={styles.emptyTitle}>Bu kun uchun joylashuv izi yo&apos;q</p>
        {!query.data.consentGiven && (
          <p className={styles.emptyNote}>Talaba fonda kuzatuvga rozilik bermagan.</p>
        )}
      </div>
    );
  } else {
    const d = query.data;
    body = (
      <>
        <RouteMeta data={d} />
        {d.pings.length === 1 && (
          <p className={styles.note}>
            Faqat bitta nuqta qayd etilgan — harakat yo&apos;nalishi chizilmaydi.
          </p>
        )}
        {!d.place && <p className={styles.note}>Korxona joylashuvi kiritilmagan.</p>}
        <TrackMap
          place={d.place}
          pings={d.pings}
          checkInAt={d.checkInAt}
          checkOutAt={d.checkOutAt}
          height={ROUTE_MAP_HEIGHT}
          maxArrows={ROUTE_MAX_ARROWS}
          legendBelow
        />
      </>
    );
  }

  return (
    <section className={styles.route} aria-labelledby={headingId}>
      <div className={styles.head}>
        <Eyebrow margin="none" id={headingId}>
          Harakat yo&apos;li
        </Eyebrow>
        <Button asChild size="xs">
          <Link to={href}>To&apos;liq izni ochish</Link>
        </Button>
      </div>
      {body}
    </section>
  );
}
