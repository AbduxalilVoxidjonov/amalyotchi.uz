import { cn } from '@/shared/ui';
import { DAY_STATUS, type CalendarDayStatus } from '../types';
import styles from './DayStatus.module.css';

export interface DayStatusChipProps {
  status: CalendarDayStatus;
  className?: string;
}

/** Kun holati chip'i — yorliq/glyph `DAY_STATUS`, rang oy gridi katagi bilan bir xil. */
export function DayStatusChip({ status, className }: DayStatusChipProps) {
  const meta = DAY_STATUS[status];
  return (
    <span className={cn(styles.tone, styles.chip, className)} data-status={status}>
      {meta.glyph && (
        <span className={styles.glyph} aria-hidden="true">
          {meta.glyph}
        </span>
      )}
      {meta.label}
    </span>
  );
}
