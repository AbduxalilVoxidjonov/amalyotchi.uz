import { useMemo } from 'react';
import { useParams, useSearchParams } from 'react-router-dom';
import { Badge, Breadcrumb, Card, CardBody, Input, type BreadcrumbItem } from '@/shared/ui';
import { QueryState } from '../components/QueryState';
import { fmtDateOnly, fmtDistance, fmtTime, todayInTashkent } from '../shared/fmt';
import { TrackMap } from './components/TrackMap';
import { TrackSummary } from './components/TrackSummary';
import { TrackTimeline } from './components/TrackTimeline';
import { buildTimeline } from './derive';
import { useLocationPingsQuery } from './hooks';
import styles from './LocationTrackPage.module.css';

const DATE_RE = /^\d{4}-\d{2}-\d{2}$/;

/**
 * Admin · talabaning kunlik joylashuv izi (`/admin/today/students/:studentId/location?date=&name=`).
 * Xarita (korxona radiusi + ping'lar, vaqt bo'yicha chiziq) · kun xulosasi · vaqt chizig'i. Sana sukut
 * bo'yicha — bugun (Toshkent). `?from=student` — talaba profilidan kelingan (breadcrumb profilga qaytaradi).
 */
export function LocationTrackPage() {
  const { studentId = '' } = useParams<{ studentId: string }>();
  const [params, setParams] = useSearchParams();
  const today = todayInTashkent();
  const dateParam = params.get('date');
  const date = dateParam && DATE_RE.test(dateParam) && dateParam <= today ? dateParam : today;
  const isToday = date === today;
  const name = params.get('name')?.trim() || 'Talaba';
  const fromStudent = params.get('from') === 'student';
  const breadcrumb: BreadcrumbItem[] = fromStudent
    ? [
        { label: 'Talabalar', to: '/admin/students' },
        { label: name, to: `/admin/students/${encodeURIComponent(studentId)}` },
        { label: 'Joylashuv izi' },
      ]
    : [{ label: 'Bugun', to: '/admin/today' }, { label: name }];

  const query = useLocationPingsQuery(studentId, date, isToday);
  const data = query.data;
  // `now` faqat bugun uchun (oxirgi ping'dan hozirgacha — "signal yo'q" oralig'i ko'rinsin).
  const timeline = useMemo(
    () => (data ? buildTimeline(data, isToday ? Date.now() : null) : null),
    [data, isToday],
  );

  const setDate = (next: string) => {
    const sp = new URLSearchParams(params);
    if (!next || next === today) sp.delete('date');
    else sp.set('date', next);
    setParams(sp, { replace: true });
  };

  return (
    <div className={styles.page}>
      <Breadcrumb items={breadcrumb} />
      <Card as="section" aria-labelledby="track-title">
        <CardBody className={styles.head}>
          <div className={styles.titleBlock}>
            <h2 id="track-title" className={styles.title}>
              {name} — joylashuv izi
            </h2>
            <p className={styles.sub}>
              {fmtDateOnly(date)}
              {data?.place
                ? ` · ${data.place.name} · radius ${fmtDistance(data.place.radiusM)}`
                : ''}
              {data?.checkInAt ? ` · check-in ${fmtTime(data.checkInAt)}` : ''}
              {data?.checkOutAt ? ` · check-out ${fmtTime(data.checkOutAt)}` : ''}
            </p>
          </div>
          <Input
            type="date"
            label="Sana"
            variant="form"
            mono
            value={date}
            max={today}
            onChange={(e) => setDate(e.target.value)}
            wrapperClassName={styles.date}
          />
        </CardBody>
      </Card>

      <QueryState status={query.status} data={data} error={query.error} refetch={query.refetch}>
        {(d) => {
          const mockedCount = d.pings.filter((p) => p.isMocked).length;
          return (
            <>
              {(!d.consentGiven || mockedCount > 0 || !d.place) && (
                <div className={styles.flags}>
                  {!d.consentGiven && (
                    <Badge status="neu">Talaba fonda kuzatuvga rozilik bermagan</Badge>
                  )}
                  {mockedCount > 0 && (
                    <Badge status="bad">Soxta joylashuv aniqlangan · {mockedCount} ta ping</Badge>
                  )}
                  {!d.place && <Badge status="late">Korxona joylashuvi kiritilmagan</Badge>}
                </div>
              )}
              <div className={styles.grid}>
                <TrackMap place={d.place} pings={d.pings} />
                <TrackSummary summary={d.summary} mockedCount={mockedCount} />
              </div>
              <TrackTimeline
                timeline={timeline}
                checkInAt={d.checkInAt}
                checkOutAt={d.checkOutAt}
              />
            </>
          );
        }}
      </QueryState>
    </div>
  );
}

export default LocationTrackPage;
