import { Fragment } from 'react';
import type { GradeDistribution } from '../types';
import styles from './PeriodMetrics.module.css';

type GradeKey = keyof GradeDistribution;

const GRADES: readonly { key: GradeKey; short: string; name: string }[] = [
  { key: 'excellent', short: '5', name: "5 (a'lo)" },
  { key: 'good', short: '4', name: '4 (yaxshi)' },
  { key: 'satisfactory', short: '3', name: '3 (qoniqarli)' },
  { key: 'unsatisfactory', short: '2', name: '2 (qoniqarsiz)' },
  { key: 'retake', short: 'Qayta', name: 'qayta topshiradi' },
];

function gradesTotal(g: GradeDistribution): number {
  return g.excellent + g.good + g.satisfactory + g.unsatisfactory + g.retake;
}

/** Ekran o'quvchi va tooltip uchun: "Baholar: 5 — 8 ta, 4 — 6 ta, …". */
function gradesLabel(g: GradeDistribution): string {
  return `Baholar: ${GRADES.map((x) => `${x.name} — ${g[x.key]} ta`).join(', ')}`;
}

export interface GradeDistributionViewProps {
  grades: GradeDistribution;
  /** `compact` — jadval katagi ("8·6·2·0·4"); `full` — segmentli chiziq + izoh. */
  variant?: 'compact' | 'full';
}

/** Baholar taqsimoti: 5 / 4 / 3 / 2 / qayta topshiradi. */
export function GradeDistributionView({ grades, variant = 'compact' }: GradeDistributionViewProps) {
  const label = gradesLabel(grades);

  if (variant === 'compact') {
    return (
      <span className={styles.gradesCompact} role="img" aria-label={label} title={label}>
        {GRADES.map((x, i) => (
          <Fragment key={x.key}>
            {i > 0 && (
              <span className={styles.sep} aria-hidden>
                ·
              </span>
            )}
            <span data-grade={x.key} data-zero={grades[x.key] === 0 || undefined} aria-hidden>
              {grades[x.key]}
            </span>
          </Fragment>
        ))}
      </span>
    );
  }

  const total = gradesTotal(grades);
  if (total === 0) return <span className={styles.noGrades}>Hali baho yo'q</span>;
  return (
    <div>
      <div className={styles.gradeBar} role="img" aria-label={label}>
        {GRADES.filter((x) => grades[x.key] > 0).map((x) => (
          <span
            key={x.key}
            className={styles.gradeSeg}
            data-grade={x.key}
            style={{ width: `${(grades[x.key] / total) * 100}%` }}
          />
        ))}
      </div>
      <ul className={styles.gradeLegend} aria-hidden>
        {GRADES.map((x) => (
          <li key={x.key} className={styles.gradeLegendItem} data-grade={x.key}>
            <span className={styles.gradeSwatch} />
            {x.short} <b>{grades[x.key]}</b>
          </li>
        ))}
      </ul>
    </div>
  );
}
