import { RADIUS_MAX_M, RADIUS_MIN_M, RADIUS_STEP_M } from '../types';
import styles from './RadiusStepper.module.css';

export interface RadiusStepperProps {
  value: number;
  onChange: (next: number) => void;
  step?: number;
  min?: number;
  max?: number;
}

/** SPEC-TOKENS 4.1 `stepper` — [−][150 m][+], qadam 50 m (❓ dizaynda ko'rsatilmagan). */
export function RadiusStepper({
  value,
  onChange,
  step = RADIUS_STEP_M,
  min = RADIUS_MIN_M,
  max = RADIUS_MAX_M,
}: RadiusStepperProps) {
  return (
    <div className={styles.row}>
      <span className={styles.label}>Radius</span>
      <div className={styles.stepper} role="group" aria-label="Geofence radiusi">
        <button
          type="button"
          className={styles.btn}
          aria-label="Radiusni kamaytirish"
          disabled={value - step < min}
          onClick={() => onChange(Math.max(min, value - step))}
        >
          −
        </button>
        <output className={styles.value} aria-live="polite">
          {value} m
        </output>
        <button
          type="button"
          className={styles.btn}
          aria-label="Radiusni oshirish"
          disabled={value + step > max}
          onClick={() => onChange(Math.min(max, value + step))}
        >
          +
        </button>
      </div>
      <span className={styles.hint}>ofis uchun 100–150 m</span>
    </div>
  );
}
