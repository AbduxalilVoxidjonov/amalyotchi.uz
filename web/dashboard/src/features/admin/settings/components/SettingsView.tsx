import { Button, Card, CardHeader, Input, Pill, cn } from '@/shared/ui';
import { ErrorState } from '../../components/PageStatus';
import { formatDayMonth } from '../../shared/format';
import { groupSettings } from '../groups';
import { UNIT_LABEL, type AdminSettings, type Setting } from '../types';
import styles from './SettingsView.module.css';

/** ISO hafta kunlari: 1 — Dushanba … 7 — Yakshanba (backend `WorkDays`). */
const WEEKDAYS = [
  { day: 1, short: 'Du', name: 'Dushanba' },
  { day: 2, short: 'Se', name: 'Seshanba' },
  { day: 3, short: 'Ch', name: 'Chorshanba' },
  { day: 4, short: 'Pa', name: 'Payshanba' },
  { day: 5, short: 'Ju', name: 'Juma' },
  { day: 6, short: 'Sh', name: 'Shanba' },
  { day: 7, short: 'Ya', name: 'Yakshanba' },
] as const;

function parseWeekdays(csv: string): Set<number> {
  return new Set(
    csv
      .split(',')
      .map((p) => Number(p.trim()))
      .filter((n) => Number.isInteger(n) && n >= 1 && n <= 7),
  );
}

/** Kunni qo'shadi/olib tashlaydi → tartiblangan CSV ("1,2,3,4,5,6"). */
function toggleWeekday(csv: string, day: number): string {
  const days = parseWeekdays(csv);
  if (days.has(day)) days.delete(day);
  else days.add(day);
  return [...days].sort((a, b) => a - b).join(',');
}

export interface SettingsViewProps {
  data: AdminSettings;
  /** Joriy (draft yoki server) xom qiymatlar: key → "200". */
  values: Record<string, string>;
  /** PUT 400 `errors{key:[...]}`. */
  fieldErrors: Record<string, string[]>;
  isDirty: boolean;
  isSaving: boolean;
  saveError: unknown;
  saved: boolean;
  onChange: (key: string, value: string) => void;
  onSave: () => void;
  onCancel: () => void;
  onAddHoliday: () => void;
}

interface SettingControlProps {
  setting: Setting;
  id: string;
  labelId: string;
  noteId: string | undefined;
  errorId: string;
  value: string;
  error: string | undefined;
  disabled: boolean;
  onChange: (value: string) => void;
}

/** Tur bo'yicha boshqaruv: bool — switch, int — raqam + birlik, weekdays — 7 ta kun chip'i. */
function SettingControl({
  setting,
  id,
  labelId,
  noteId,
  errorId,
  value,
  error,
  disabled,
  onChange,
}: SettingControlProps) {
  const describedBy = cn(noteId, error ? errorId : '') || undefined;

  if (setting.type === 'bool') {
    const on = value === 'true';
    return (
      <button
        id={id}
        type="button"
        role="switch"
        aria-checked={on}
        aria-describedby={describedBy}
        aria-invalid={error ? true : undefined}
        className={styles.switch}
        disabled={disabled}
        onClick={() => onChange(on ? 'false' : 'true')}
      >
        <span className={styles.switchThumb} aria-hidden />
      </button>
    );
  }

  if (setting.type === 'weekdays') {
    const days = parseWeekdays(value);
    return (
      <div
        role="group"
        aria-labelledby={labelId}
        aria-describedby={describedBy}
        className={styles.weekdays}
      >
        {WEEKDAYS.map((d) => (
          <Pill
            key={d.day}
            shape="square"
            className={styles.dayChip}
            active={days.has(d.day)}
            aria-label={d.name}
            title={d.name}
            disabled={disabled}
            onClick={() => onChange(toggleWeekday(value, d.day))}
          >
            {d.short}
          </Pill>
        ))}
      </div>
    );
  }

  const unit = setting.unit ? (UNIT_LABEL[setting.unit] ?? setting.unit) : null;
  return (
    <div className={styles.intField}>
      <Input
        id={id}
        variant="readonly"
        mono
        type="number"
        inputMode="numeric"
        className={styles.intInput ?? ''}
        wrapperClassName={styles.intWrap}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={disabled}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        {...(setting.min != null && { min: setting.min })}
        {...(setting.max != null && { max: setting.max })}
      />
      <span className={styles.unit} aria-hidden>
        {unit}
      </span>
    </div>
  );
}

interface SettingRowProps {
  setting: Setting;
  value: string;
  error: string | undefined;
  disabled: boolean;
  onChange: (value: string) => void;
}

/** Ixcham qator: chapda label + note, o'ngda boshqaruv; xato — qator ostida. */
function SettingRow({ setting, value, error, disabled, onChange }: SettingRowProps) {
  const id = `setting-${setting.key}`;
  const labelId = `${id}-label`;
  const noteId = `${id}-note`;
  const errorId = `${id}-error`;
  const isGroup = setting.type === 'weekdays';
  return (
    <div className={styles.settingRow} data-type={setting.type}>
      <div className={styles.settingMain}>
        <div className={styles.settingText}>
          {isGroup ? (
            <span id={labelId} className={styles.settingName}>
              {setting.label}
            </span>
          ) : (
            <label id={labelId} htmlFor={id} className={styles.settingName}>
              {setting.label}
            </label>
          )}
          {setting.note && (
            <div id={noteId} className={styles.settingNote}>
              {setting.note}
            </div>
          )}
        </div>
        <SettingControl
          setting={setting}
          id={id}
          labelId={labelId}
          noteId={setting.note ? noteId : undefined}
          errorId={errorId}
          value={value}
          error={error}
          disabled={disabled}
          onChange={onChange}
        />
      </div>
      {error && (
        <p id={errorId} className={styles.fieldError} role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

/** SPEC-SCREENS §13 — guruhlangan qoidalar (2 ustun) · Bayramlar · pastda sticky Saqlash paneli. */
export function SettingsView({
  data,
  values,
  fieldErrors,
  isDirty,
  isSaving,
  saveError,
  saved,
  onChange,
  onSave,
  onCancel,
  onAddHoliday,
}: SettingsViewProps) {
  const groups = groupSettings(data.settings);

  return (
    <form
      className={styles.root}
      // Chegaralar backend'da tekshiriladi (400 errors{key}) — brauzer tooltip'i submit'ni to'smasin.
      noValidate
      onSubmit={(e) => {
        e.preventDefault();
        onSave();
      }}
    >
      <div className={styles.grid}>
        {groups.map((g) => {
          const titleId = `settings-group-${g.id}`;
          return (
            <Card key={g.id} as="section" aria-labelledby={titleId} className={styles.card}>
              <CardHeader title={<span id={titleId}>{g.title}</span>} />
              <div className={styles.rows}>
                {g.settings.map((s) => (
                  <SettingRow
                    key={s.key}
                    setting={s}
                    value={values[s.key] ?? s.value}
                    error={fieldErrors[s.key]?.join(' ')}
                    disabled={isSaving}
                    onChange={(v) => onChange(s.key, v)}
                  />
                ))}
              </div>
            </Card>
          );
        })}

        <Card as="section" aria-labelledby="settings-holidays" className={styles.card}>
          <CardHeader
            title={<span id="settings-holidays">Bayram va dam olish kunlari</span>}
            actions={
              <Button size="sm" variant="dashed" onClick={onAddHoliday}>
                Kun qo'shish
              </Button>
            }
          />
          <ul className={styles.holidays}>
            {data.holidays.length === 0 && (
              <li className={styles.holidayEmpty}>Bayram kunlari kiritilmagan.</li>
            )}
            {data.holidays.map((h) => (
              <li key={h.id} className={styles.holidayRow}>
                <span className={styles.holidayDate}>{formatDayMonth(h.date, !h.isRecurring)}</span>
                <span className={styles.holidayName}>{h.name}</span>
              </li>
            ))}
          </ul>
        </Card>
      </div>

      <div className={styles.saveBar}>
        {saveError ? (
          <div className={styles.saveError}>
            <ErrorState inline error={saveError} onRetry={onSave} />
          </div>
        ) : null}
        <div className={styles.saveActions}>
          <div className={styles.saveStatus}>
            {isDirty && <span className={styles.saveHint}>Saqlanmagan o'zgarishlar bor</span>}
            {saved && !isDirty && (
              <span className={styles.saved} role="status">
                Saqlandi
              </span>
            )}
          </div>
          <Button type="button" onClick={onCancel} disabled={isSaving || !isDirty}>
            Bekor qilish
          </Button>
          <Button type="submit" variant="primary" disabled={isSaving || !isDirty}>
            {isSaving ? 'Saqlanmoqda…' : 'Saqlash'}
          </Button>
        </div>
      </div>
    </form>
  );
}
