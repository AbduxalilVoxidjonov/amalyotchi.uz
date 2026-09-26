import { useId } from 'react';
import { Link } from 'react-router-dom';
import { Input, Pill, cn } from '@/shared/ui';
import { countWorkDays, parseWeekdays, toggleWeekday, WEEKDAYS } from '../../shared/weekdays';
import { calendarDays } from '../dates';
import type { PeriodFormErrors, PeriodFormField, PeriodFormValues } from '../validation';
import styles from './PeriodFields.module.css';

export interface PeriodFieldsProps {
  idPrefix: string;
  values: PeriodFormValues;
  onChange: (next: PeriodFormValues) => void;
  /** Ko'rsatiladigan xatolar (touched/submit bo'yicha filtrlangan). */
  errors: PeriodFormErrors;
  onBlurField?: (field: PeriodFormField) => void;
  disabled?: boolean;
  /** Faol davrda boshlanish sanasi o'zgarmaydi. */
  startLocked?: boolean;
  /** "Standart qiymatlar sozlamalardan olinadi" eslatmasi (yaratishda). */
  showSettingsNote?: boolean;
  /** Ish kunlari ostidagi ogohlantirish (faol davrda — o'zgarish butun davrni qayta hisoblaydi). */
  workDaysWarning?: string | undefined;
  /** Ish vaqti ostidagi izoh (faol davrda — yangi vaqt bugundan kuchga kiradi). */
  dailyTimeHint?: string | undefined;
}

/**
 * Davr nomi + boshlanish/tugash sanalari + ish kunlari (7 ta chip) + kunlik ish vaqti.
 * Ostida — taxminiy kalendar va ish kunlari soni (bayramlar hisobga olinmaydi).
 */
export function PeriodFields({
  idPrefix,
  values,
  onChange,
  errors,
  onBlurField,
  disabled = false,
  startLocked = false,
  showSettingsNote = false,
  workDaysWarning,
  dailyTimeHint,
}: PeriodFieldsProps) {
  const uid = useId();
  const days = calendarDays(values.startDate, values.endDate);
  const workDayCount = countWorkDays(values.startDate, values.endDate, values.workDays);
  const selectedDays = parseWeekdays(values.workDays);
  const set = (key: PeriodFormField) => (e: { target: { value: string } }) =>
    onChange({ ...values, [key]: e.target.value });

  const workDaysErrorId = `${uid}-workdays-error`;
  const workDaysWarningId = `${uid}-workdays-warning`;
  const dailyTimeHintId = `${uid}-daily-time-hint`;

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
      <div className={styles.pair}>
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

      <fieldset
        className={styles.group}
        aria-describedby={
          cn(errors.workDays ? workDaysErrorId : '', workDaysWarning ? workDaysWarningId : '') ||
          undefined
        }
        disabled={disabled}
      >
        <legend className={styles.legend}>Ish kunlari</legend>
        <div className={styles.weekdays}>
          {WEEKDAYS.map((d) => (
            <Pill
              key={d.day}
              shape="square"
              className={styles.dayChip}
              active={selectedDays.has(d.day)}
              aria-label={d.name}
              title={d.name}
              onClick={() =>
                onChange({ ...values, workDays: toggleWeekday(values.workDays, d.day) })
              }
              onBlur={() => onBlurField?.('workDays')}
            >
              {d.short}
            </Pill>
          ))}
        </div>
        {errors.workDays && (
          <p id={workDaysErrorId} className={styles.error} role="alert">
            {errors.workDays}
          </p>
        )}
        {workDaysWarning && (
          <p id={workDaysWarningId} className={styles.warning}>
            {workDaysWarning}
          </p>
        )}
      </fieldset>

      <fieldset
        className={styles.group}
        aria-describedby={dailyTimeHint ? dailyTimeHintId : undefined}
        disabled={disabled}
      >
        <legend className={styles.legend}>Ish vaqti</legend>
        <div className={styles.pair}>
          <Input
            id={`${idPrefix}-daily-start`}
            label="Boshlanishi"
            type="time"
            variant="form"
            mono
            required
            value={values.dailyStart}
            onChange={set('dailyStart')}
            onBlur={() => onBlurField?.('dailyStart')}
            error={errors.dailyStart}
          />
          <Input
            id={`${idPrefix}-daily-end`}
            label="Tugashi"
            type="time"
            variant="form"
            mono
            required
            value={values.dailyEnd}
            onChange={set('dailyEnd')}
            onBlur={() => onBlurField?.('dailyEnd')}
            error={errors.dailyEnd}
          />
        </div>
        {dailyTimeHint && (
          <p id={dailyTimeHintId} className={styles.hint}>
            {dailyTimeHint}
          </p>
        )}
      </fieldset>

      <p className={styles.days} aria-live="polite">
        {days === null ? (
          'Sanalarni tanlang'
        ) : (
          <>
            <span>{`≈ ${days} kalendar kun`}</span>
            {workDayCount !== null && workDayCount > 0 && (
              <span
                className={styles.workDays}
                title="Bayram kunlari hisobga olinmagan — taxminiy qiymat"
              >{`≈ ${workDayCount} ish kuni`}</span>
            )}
          </>
        )}
      </p>
      {showSettingsNote && (
        <p className={styles.note}>
          Ish kunlari va ish vaqtining standart qiymatlari global sozlamalardan olinadi; hisobot
          talablari ham shu yerdan. <Link to="/admin/settings">Sozlamalarni ochish</Link>
        </p>
      )}
    </div>
  );
}
