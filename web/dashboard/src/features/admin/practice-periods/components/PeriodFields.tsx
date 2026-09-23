import { Link } from 'react-router-dom';
import { Input } from '@/shared/ui';
import { calendarDays } from '../dates';
import type { PeriodFormErrors, PeriodFormValues } from '../validation';
import styles from './PeriodFields.module.css';

export interface PeriodFieldsProps {
  idPrefix: string;
  values: PeriodFormValues;
  onChange: (next: PeriodFormValues) => void;
  /** Ko'rsatiladigan xatolar (touched/submit bo'yicha filtrlangan). */
  errors: PeriodFormErrors;
  onBlurField?: (field: keyof PeriodFormValues) => void;
  disabled?: boolean;
  /** Faol davrda boshlanish sanasi o'zgarmaydi. */
  startLocked?: boolean;
  /** "Global sozlamalardan olinadi" eslatmasi (yaratishda). */
  showSettingsNote?: boolean;
}

/** Davr nomi + boshlanish/tugash sanalari + hisoblangan kalendar kunlar. */
export function PeriodFields({
  idPrefix,
  values,
  onChange,
  errors,
  onBlurField,
  disabled = false,
  startLocked = false,
  showSettingsNote = false,
}: PeriodFieldsProps) {
  const days = calendarDays(values.startDate, values.endDate);
  const set = (key: keyof PeriodFormValues) => (e: { target: { value: string } }) =>
    onChange({ ...values, [key]: e.target.value });

  return (
    <div className={styles.fields}>
      <Input
        id={`${idPrefix}-name`}
        label="Nomi"
        variant="form"
        autoComplete="off"
        maxLength={200}
        placeholder="Masalan: 3-kurs kuzgi amaliyot 2026"
        required
        value={values.name}
        onChange={set('name')}
        onBlur={() => onBlurField?.('name')}
        error={errors.name}
        disabled={disabled}
      />
      <div className={styles.dates}>
        <Input
          id={`${idPrefix}-start`}
          label="Boshlanish sanasi"
          type="date"
          variant="form"
          mono
          required
          value={values.startDate}
          max={values.endDate || undefined}
          onChange={set('startDate')}
          onBlur={() => onBlurField?.('startDate')}
          error={errors.startDate}
          hint={startLocked ? "Faol davrda o'zgartirib bo'lmaydi." : undefined}
          disabled={disabled || startLocked}
        />
        <Input
          id={`${idPrefix}-end`}
          label="Tugash sanasi"
          type="date"
          variant="form"
          mono
          required
          value={values.endDate}
          min={values.startDate || undefined}
          onChange={set('endDate')}
          onBlur={() => onBlurField?.('endDate')}
          error={errors.endDate}
          disabled={disabled}
        />
      </div>
      <p className={styles.days} aria-live="polite">
        {days === null ? 'Sanalarni tanlang' : `≈ ${days} kalendar kun`}
      </p>
      {showSettingsNote && (
        <p className={styles.note}>
          Kunlik vaqt, ish kunlari va hisobot talablari global sozlamalardan olinadi.{' '}
          <Link to="/admin/settings">Sozlamalarni ochish</Link>
        </p>
      )}
    </div>
  );
}
