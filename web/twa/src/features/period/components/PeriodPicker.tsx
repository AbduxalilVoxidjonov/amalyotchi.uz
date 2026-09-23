import { formatPeriod } from '@/shared/lib/format';
import type { StudentPeriodOption } from '../types';
import styles from './PeriodPicker.module.css';

export interface PeriodPickerProps {
  periods: readonly StudentPeriodOption[];
  selectedId: string;
  onSelect: (periodId: string) => void;
  /** Guruh nomi (a11y). */
  label?: string;
  disabled?: boolean;
}

/**
 * Davr tanlagichi (chip/segment) — v3.5 `periods` (startDate kamayish tartibida).
 * Tanlangan chip — `aria-pressed="true"`; har chipda davr sanalari ikkinchi qator bo'lib turadi.
 */
export function PeriodPicker({
  periods,
  selectedId,
  onSelect,
  label = 'Amaliyot davri',
  disabled = false,
}: PeriodPickerProps) {
  return (
    <div className={styles.row} role="group" aria-label={label}>
      {periods.map((p) => {
        const active = p.id === selectedId;
        return (
          <button
            key={p.id}
            type="button"
            className={styles.chip}
            aria-pressed={active}
            disabled={disabled && !active}
            onClick={() => !active && onSelect(p.id)}
          >
            <span className={styles.name}>{p.name}</span>
            <span className={styles.dates}>{formatPeriod(p.startDate, p.endDate)}</span>
          </button>
        );
      })}
    </div>
  );
}
