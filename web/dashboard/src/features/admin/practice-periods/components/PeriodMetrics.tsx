import { pctKind, StatGrid, StatTile } from '@/shared/ui';
import { fmtDecimal } from '@/features/tutor/format';
import { formatCount, formatPct } from '../../shared/format';
import type { GroupMetrics } from '../types';
import { GradeDistributionView } from './GradeDistributionView';
import { MetricSkeleton } from './MetricSkeleton';
import styles from './PeriodMetrics.module.css';

export interface PeriodMetricsProps {
  metrics: GroupMetrics;
  elapsedWorkDays: number;
  /** Davrning majburiy ish kunlari (berilsa "22 / 39" ko'rinishida). */
  requiredDays?: number | undefined;
  /** Bo'lim sarlavhasi va a11y nomi. */
  title?: string;
}

/**
 * Davr (yoki davr ichidagi guruh) ko'rsatkichlari: ish kunlari, talabalar, davomat, korxona,
 * kundaliklar, o'rtacha ball, shubhali kunlar va baholar taqsimoti. Davr detail va guruh
 * sahifalarida qayta ishlatiladi.
 */
export function PeriodMetrics({
  metrics: m,
  elapsedWorkDays,
  requiredDays,
  title = "Davr ko'rsatkichlari",
}: PeriodMetricsProps) {
  const lowTone = m.lowAttendanceCount > 0 ? 'bad' : 'ok';
  return (
    <section className={styles.section} aria-label={title}>
      <h2 className={styles.sectionTitle}>{title}</h2>
      <StatGrid min={170}>
        <StatTile
          label="O'tgan ish kunlari"
          value={
            <>
              {elapsedWorkDays}
              {requiredDays !== undefined && (
                <span className={styles.suffix}> / {requiredDays}</span>
              )}
            </>
          }
          note={requiredDays !== undefined ? 'majburiy kunlardan' : 'ish kuni'}
        />
        <StatTile
          label="Talabalar"
          value={formatCount(m.studentsCount)}
          note={`${formatCount(m.finalizedCount)} tasi yakunlangan`}
        />
        <StatTile
          label="O'rtacha davomat"
          dot={pctKind(m.attendancePct)}
          value={
            <span className={styles.value} data-kind={pctKind(m.attendancePct)}>
              {formatPct(m.attendancePct)}
            </span>
          }
          note={
            m.lowAttendanceCount > 0
              ? `${formatCount(m.lowAttendanceCount)} talaba 70% dan past`
              : "70% dan past talaba yo'q"
          }
          noteTone={lowTone}
        />
        <StatTile
          label="Korxonaga biriktirilgan"
          value={
            <>
              {formatCount(m.withCompanyCount)}
              <span className={styles.suffix}> / {formatCount(m.studentsCount)}</span>
            </>
          }
          note={
            m.pendingApplicationsCount > 0
              ? `${formatCount(m.pendingApplicationsCount)} ariza ko'rib chiqilmoqda`
              : "Kutilayotgan ariza yo'q"
          }
          {...(m.pendingApplicationsCount > 0 ? { noteTone: 'late' as const } : {})}
        />
        <StatTile
          label="Kundalik o'rtacha bali"
          value={
            m.diaryCount > 0 ? (
              <>
                {fmtDecimal(m.diaryAvgScore)}
                <span className={styles.suffix}> / 5</span>
              </>
            ) : (
              '—'
            )
          }
          note={`${formatCount(m.diaryApprovedCount)} / ${formatCount(m.diaryCount)} tasdiqlangan`}
        />
        <StatTile
          label="O'rtacha ball"
          value={
            m.avgTotal === null ? (
              '—'
            ) : (
              <>
                {fmtDecimal(m.avgTotal)}
                <span className={styles.suffix}> / 100</span>
              </>
            )
          }
          note={`Qayta topshiradi: ${formatCount(m.grades.retake)}`}
          {...(m.grades.retake > 0 ? { noteTone: 'bad' as const } : {})}
        />
        <StatTile
          label="Shubhali kunlar"
          value={formatCount(m.suspiciousDays)}
          note={m.suspiciousDays > 0 ? 'Tekshirish kerak' : 'Aniqlanmagan'}
          noteTone={m.suspiciousDays > 0 ? 'bad' : 'ok'}
        />
        <StatTile
          className={styles.wide}
          label="Baholar taqsimoti"
          value={
            <div className={styles.gradesValue}>
              <GradeDistributionView grades={m.grades} variant="full" />
            </div>
          }
        />
      </StatGrid>
    </section>
  );
}

/** Ko'rsatkichlar yuklanayotganda — kartalar joyi saqlanadi (sakrash bo'lmasin). */
export function PeriodMetricsSkeleton({ title = "Davr ko'rsatkichlari" }: { title?: string }) {
  return (
    <section className={styles.section} aria-label={title} aria-busy>
      <h2 className={styles.sectionTitle}>{title}</h2>
      <StatGrid min={170}>
        {Array.from({ length: 7 }, (_, i) => (
          <StatTile
            key={i}
            label={<MetricSkeleton width={90} />}
            value={<MetricSkeleton width={64} height={26} />}
          />
        ))}
        <StatTile
          className={styles.wide}
          label={<MetricSkeleton width={110} />}
          value={<MetricSkeleton width={200} height={10} />}
        />
      </StatGrid>
    </section>
  );
}
