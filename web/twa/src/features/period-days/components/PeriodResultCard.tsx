import { Card, ProgressBar, cn } from '@/shared/ui';
import tone from '@/features/calendar/components/DayStatus.module.css';
import { DAY_STATUS, type CalendarDayStatus } from '@/features/calendar/types';
import { formatPercent } from '@/shared/lib/format';
import type { PeriodDay, PeriodSummary } from '../types';
import { summarizePeriodDays } from '../summary';
import styles from './PeriodResultCard.module.css';

export interface PeriodResultCardProps {
  period: PeriodSummary;
  days: readonly PeriodDay[];
}

/**
 * Tugagan davr yig'indisi (bosh ekranda kunlar ro'yxati o'rniga): Keldi (vaqtida + kech),
 * Kech qoldi, Kelmadi — katta raqamlar; Sababli (0 bo'lmasa), talab qilingan ish kunlari va
 * davomat foizi. Ranglar — kalendar kun holati tokenlari (`DayStatus.module.css`).
 */
export function PeriodResultCard({ period, days }: PeriodResultCardProps) {
  const s = summarizePeriodDays(days);
  const stats: { key: string; status: CalendarDayStatus; label: string; value: number }[] = [
    { key: 'attended', status: 'present', label: 'Keldi', value: s.attended },
    { key: 'late', status: 'late', label: 'Kech qoldi', value: s.late },
    { key: 'absent', status: 'absent', label: 'Kelmadi', value: s.absent },
  ];

  return (
    <Card padded="lg" aria-labelledby="period-result-title">
      <h2 id="period-result-title" className={styles.title}>
        Davr yakuni
      </h2>
      <p className={styles.lead}>
        <span className={styles.count}>{period.requiredDays}</span> ish kunidan{' '}
        <span className={styles.count}>{s.attended}</span> kun keldi
      </p>

      <dl className={styles.grid}>
        {stats.map((st) => (
          <div
            key={st.key}
            className={cn(tone.tone, styles.stat)}
            data-status={st.status}
            data-stat={st.key}
          >
            <dt className={styles.label}>{st.label}</dt>
            <dd className={styles.value}>{st.value}</dd>
          </div>
        ))}
      </dl>
      <p className={styles.hint}>«Keldi» ga kech qolgan kunlar ham kiradi.</p>

      {s.excused > 0 && (
        <p className={styles.extra} data-stat="excused">
          <span className={cn(tone.tone, styles.dot)} data-status="excused" aria-hidden="true" />
          {DAY_STATUS.excused.label}: <span className={styles.count}>{s.excused}</span> kun
        </p>
      )}

      <div className={styles.pct}>
        <span className={styles.pctLabel}>Davomat</span>
        {s.attendancePct === null ? (
          <span className={styles.count}>—</span>
        ) : (
          <ProgressBar
            className={styles.bar}
            value={s.attendancePct}
            label={`Davomat ${formatPercent(s.attendancePct)}`}
          />
        )}
      </div>
    </Card>
  );
}
