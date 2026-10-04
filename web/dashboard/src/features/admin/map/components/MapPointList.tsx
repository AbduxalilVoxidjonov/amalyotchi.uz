import { Card } from '@/shared/ui';
import { fmtDistance } from '../../format';
import type { MapPoint } from '../types';
import styles from './MapPointList.module.css';

export interface MapPointListProps {
  title: string;
  points: readonly MapPoint[];
}

function formatPointDistance(p: MapPoint): string {
  return p.rejected
    ? `${fmtDistance(p.distanceM)} — rad etildi`
    : `${fmtDistance(p.distanceM)} / ${fmtDistance(p.radiusM)}`;
}

/** SPEC-SCREENS §11 — o'ng panel: bugungi nuqtalar ro'yxati. */
export function MapPointList({ title, points }: MapPointListProps) {
  return (
    <Card aria-label={title}>
      <div className={styles.head}>{title}</div>
      {points.length === 0 ? (
        <div className={styles.empty}>Bugun belgilanishlar yo'q</div>
      ) : (
        <ul className={styles.list}>
          {points.map((p) => (
            <li key={p.studentId} className={styles.item}>
              <span className={styles.dot} data-kind={p.kind} aria-hidden />
              <div className={styles.body}>
                <div className={styles.name}>{p.name}</div>
                <div className={styles.company}>{p.company}</div>
                <div className={styles.dist} data-kind={p.kind}>
                  {formatPointDistance(p)}
                </div>
              </div>
              <span className={styles.time}>{p.time}</span>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
