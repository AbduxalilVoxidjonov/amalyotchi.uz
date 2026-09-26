import { useId } from 'react';
import { formatDecimal, formatPercent, formatPeriod } from '@/shared/lib/format';
import { Badge, FactGrid, ProgressBar, type StatusKind } from '@/shared/ui';
import { PERIOD_STATUS_LABEL, type StudentProfilePracticeDto } from '../types';
import styles from './PracticePeriodCard.module.css';

const PERIOD_STATUS_KIND: Record<StudentProfilePracticeDto['period']['status'], StatusKind> = {
  planned: 'info',
  active: 'ok',
  closed: 'neu',
};

/**
 * Profildagi bitta amaliyot davri — ixcham karta: nom, sanalar, holat, korxona, davomat, ball/baho.
 * Faol davr aksent chegara bilan ajralib turadi va davomat progress-bar'i bilan ko'rsatiladi.
 */
export function PracticePeriodCard({ practice }: { practice: StudentProfilePracticeDto }) {
  const titleId = useId();
  const { period, company } = practice;
  const active = period.status === 'active';
  const planned = period.status === 'planned';

  return (
    <article className={styles.card} data-active={active || undefined} aria-labelledby={titleId}>
      <div className={styles.head}>
        <h3 id={titleId} className={styles.name}>
          {period.name}
        </h3>
        <div className={styles.badges}>
          {!planned && !practice.finalized && (
            <Badge
              status="info"
              size="sm"
              title="Davr yakunlanmagan — ko'rsatkichlar o'zgarib boradi"
            >
              Joriy hisob
            </Badge>
          )}
          <Badge status={PERIOD_STATUS_KIND[period.status]} size="sm">
            {PERIOD_STATUS_LABEL[period.status]}
          </Badge>
        </div>
      </div>
      <div className={`${styles.sub} ${styles.mono}`}>
        {formatPeriod(period.startDate, period.endDate)}
      </div>

      <div className={styles.company}>
        {company ? (
          <>
            <div className={styles.companyName}>{company.name}</div>
            {company.address && <div className={styles.sub}>{company.address}</div>}
          </>
        ) : (
          <div className={styles.sub}>Korxona biriktirilmagan</div>
        )}
      </div>

      {planned ? (
        <p className={styles.note}>Davr hali boshlanmagan.</p>
      ) : (
        <>
          {active && (
            <ProgressBar
              className={styles.progress}
              value={practice.attendancePct}
              label="Davomat"
            />
          )}
          <FactGrid
            className={styles.facts}
            columns={3}
            items={[
              { k: 'Davomat', v: formatPercent(practice.attendancePct) },
              // Kontrakt v3.8: sababli (ruxsat berilgan) kunlar ham shu songa kiradi.
              {
                k: <span title="Sababli kunlar bilan birga">Ish kunlari</span>,
                v: String(practice.elapsedWorkDays),
              },
              {
                k: practice.finalized ? 'Yakuniy ball' : 'Joriy ball',
                v: formatDecimal(practice.total),
              },
            ]}
          />
          <div className={styles.gradeLine}>
            {practice.grade !== null ? (
              <Badge
                status={practice.grade >= 4 ? 'ok' : practice.grade === 3 ? 'late' : 'bad'}
                size="sm"
              >
                Baho: {practice.grade}
                {practice.finalized ? '' : ' (joriy)'}
              </Badge>
            ) : (
              <Badge status="neu" size="sm">
                Baho hali yo'q
              </Badge>
            )}
            {practice.suspiciousDays > 0 && (
              <span className={styles.warn}>Shubhali kunlar: {practice.suspiciousDays}</span>
            )}
          </div>
        </>
      )}
    </article>
  );
}
