import { AuthFileButton } from '@/shared/files';
import { Button, Card, CardFooter, CardHeader, Input, Select } from '@/shared/ui';
import { ErrorState } from '../../components/PageStatus';
import { formatDayMonth } from '../../shared/format';
import { UNIT_LABEL, type AdminSettings, type Setting } from '../types';
import styles from './SettingsView.module.css';

const BOOL_OPTIONS = [
  { value: 'true', label: 'Ha' },
  { value: 'false', label: "Yo'q" },
] as const;

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
  value: string;
  error: string | undefined;
  disabled: boolean;
  onChange: (value: string) => void;
}

/** Tur bo'yicha boshqaruv: int — raqam + birlik, bool — Ha/Yo'q, weekdays — "1,2,3,4,5,6" matni. ❓ Dizaynda readonly ko'rinish — shu uslub (mono, r7). */
function SettingControl({ setting, value, error, disabled, onChange }: SettingControlProps) {
  const id = `setting-${setting.key}`;
  if (setting.type === 'bool') {
    return (
      <div className={styles.settingField}>
        <Select
          id={id}
          mono
          options={BOOL_OPTIONS}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          disabled={disabled}
          error={error}
        />
        <span className={styles.unit} aria-hidden />
      </div>
    );
  }
  const unit = setting.unit ? (UNIT_LABEL[setting.unit] ?? setting.unit) : null;
  return (
    <div className={styles.settingField}>
      <Input
        id={id}
        variant="readonly"
        mono
        className={styles.settingInput ?? ''}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        disabled={disabled}
        error={error}
        {...(setting.type === 'int'
          ? {
              type: 'number' as const,
              inputMode: 'numeric' as const,
              ...(setting.min != null && { min: setting.min }),
              ...(setting.max != null && { max: setting.max }),
            }
          : { type: 'text' as const, placeholder: '1,2,3,4,5' })}
      />
      <span className={styles.unit} aria-hidden>
        {unit}
      </span>
    </div>
  );
}

/** SPEC-SCREENS §13 — Global qoidalar (forma) · Bayramlar · Hujjat shablonlari. */
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
  return (
    <div className={styles.root}>
      <Card as="section" aria-labelledby="settings-rules">
        <form
          // Chegaralar backend'da tekshiriladi (400 errors{key}) — brauzer tooltip'i submit'ni to'smasin.
          noValidate
          onSubmit={(e) => {
            e.preventDefault();
            onSave();
          }}
        >
          <CardHeader title={<span id="settings-rules">Global qoidalar</span>} />
          <div className={styles.rows}>
            {data.settings.map((s) => (
              <div key={s.key} className={styles.settingRow}>
                <div className={styles.settingText}>
                  <label htmlFor={`setting-${s.key}`} className={styles.settingName}>
                    {s.label}
                  </label>
                  <div className={styles.settingNote}>{s.note}</div>
                </div>
                <SettingControl
                  setting={s}
                  value={values[s.key] ?? s.value}
                  error={fieldErrors[s.key]?.join(' ')}
                  disabled={isSaving}
                  onChange={(v) => onChange(s.key, v)}
                />
              </div>
            ))}
          </div>
          {saveError ? (
            <div className={styles.error}>
              <ErrorState inline error={saveError} onRetry={onSave} />
            </div>
          ) : null}
          <CardFooter>
            <Button type="submit" variant="primary" disabled={isSaving || !isDirty}>
              {isSaving ? 'Saqlanmoqda…' : 'Saqlash'}
            </Button>
            <Button type="button" onClick={onCancel} disabled={isSaving || !isDirty}>
              Bekor qilish
            </Button>
            {saved && !isDirty && (
              <span className={styles.saved} role="status">
                Saqlandi
              </span>
            )}
          </CardFooter>
        </form>
      </Card>

      <div className={styles.column}>
        <Card as="section" aria-labelledby="settings-holidays">
          <CardHeader title={<span id="settings-holidays">Bayram va dam olish kunlari</span>} />
          <ul className={styles.list}>
            {data.holidays.length === 0 && (
              <li className={styles.holidayRow}>Bayram kunlari kiritilmagan.</li>
            )}
            {data.holidays.map((h) => (
              <li key={h.id} className={styles.holidayRow}>
                {formatDayMonth(h.date, !h.isRecurring)} · {h.name}
              </li>
            ))}
          </ul>
          <div className={styles.addRow}>
            <Button variant="dashed" onClick={onAddHoliday}>
              Kun qo'shish
            </Button>
          </div>
        </Card>

        <Card as="section" aria-labelledby="settings-templates">
          <CardHeader title={<span id="settings-templates">Hujjat shablonlari</span>} />
          <ul className={styles.list}>
            {data.templates.length === 0 && (
              <li className={styles.templateRow}>
                <span className={styles.file}>Shablonlar hali yuklanmagan.</span>
              </li>
            )}
            {data.templates.map((t) => (
              <li key={t.id} className={styles.templateRow}>
                {t.name} ·{' '}
                <AuthFileButton url={t.url} name={t.fileName} variant="link" />
              </li>
            ))}
          </ul>
        </Card>
      </div>
    </div>
  );
}
