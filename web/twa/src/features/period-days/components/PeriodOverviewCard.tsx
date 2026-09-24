import { Badge, Card, Eyebrow, type StatusKind } from '@/shared/ui';
import { formatDate } from '@/shared/lib/format';
import { periodPhase, type PeriodPhase } from '@/features/period/types';
import type { PeriodSummary } from '../types';
import styles from './PeriodOverviewCard.module.css';

const PHASE_BADGE: Record<PeriodPhase, { label: string; kind: StatusKind }> = {
  upcoming: { label: 'Rejalashtirilgan', kind: 'info' },
  ongoing: { label: 'Davom etmoqda', kind: 'ok' },
  ended: { label: 'Tugagan', kind: 'neu' },
};

export interface PeriodOverviewCardProps {
  period: PeriodSummary;
  /** Server sanasi — "tugagan" holati `endDate < bugun` dan ham chiqadi (`periodPhase`). */
  today: string;
}

/** Bosh ekran tepasi: davr nomi, sanalari, holati va o'tgan ish kunlari. */
export function PeriodOverviewCard({ period, today }: PeriodOverviewCardProps) {
  const phase = PHASE_BADGE[periodPhase(period, today)];
  return (
    <Card padded="lg" aria-labelledby="period-overview-title">
      <Eyebrow as="div" spacing="wide" margin="none">
        Amaliyot davri
      </Eyebrow>
      <div className={styles.head}>
        <h2 id="period-overview-title" className={styles.title}>
          {period.name}
        </h2>
        <Badge status={phase.kind} size="md">
          {phase.label}
        </Badge>
      </div>
      <p className={styles.dates}>
        {formatDate(period.startDate)} – {formatDate(period.endDate)}
      </p>
      <p className={styles.progress}>
        Ish kunlari:{' '}
        <span className={styles.count}>
          {period.elapsedWorkDays} / {period.requiredDays}
        </span>{' '}
        o'tdi
      </p>
    </Card>
  );
}
