import { Badge, Card, CardHeader, EmptyState } from '@/shared/ui';
import { formatDate } from '@/shared/lib/format';
import { LEAVE_STATUS, type LeaveRequestDto } from '../types';
import styles from './LeaveRequestList.module.css';

/** SPEC-SCREENS §15 "Mening so'rovlarim" ro'yxati. */
export function LeaveRequestList({ items }: { items: LeaveRequestDto[] }) {
  return (
    <Card aria-label="Mening so'rovlarim">
      <CardHeader title="Mening so'rovlarim" />
      {items.length === 0 ? (
        <div className={styles.emptyWrap}>
          <EmptyState
            title="Hali so'rov yo'q"
            description="Yuborilgan so'rovlar shu yerda ko'rinadi."
          />
        </div>
      ) : (
        <ul className={styles.list}>
          {items.map((r) => {
            const status = LEAVE_STATUS[r.status];
            return (
              <li key={r.id} className={styles.row}>
                <div className={styles.text}>
                  <div className={styles.date}>
                    {r.dateFrom === r.dateTo
                      ? formatDate(r.dateFrom)
                      : `${formatDate(r.dateFrom)} – ${formatDate(r.dateTo)}`}
                  </div>
                  <div className={styles.reason}>{r.reason}</div>
                  {(r.document || r.comment) && (
                    <div className={styles.meta}>
                      {r.document &&
                        (r.document.url ? (
                          <a href={r.document.url} target="_blank" rel="noopener noreferrer">
                            {r.document.name}
                          </a>
                        ) : (
                          <span>{r.document.name}</span>
                        ))}
                      {r.document && r.comment && ' · '}
                      {r.comment && <span>Tyutor: {r.comment}</span>}
                    </div>
                  )}
                </div>
                <Badge status={status.kind}>{status.label}</Badge>
              </li>
            );
          })}
        </ul>
      )}
    </Card>
  );
}
