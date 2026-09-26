import { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { isApiError } from '@/shared/api';
import { Pill, PillGroup } from '@/shared/ui';
import { ErrorState, LoadingState } from '../components/PageStatus';
import { SettingsView } from './components/SettingsView';
import { useSettingsQuery, useUpdateSettings } from './hooks';
import { AccountSecurity } from './security/AccountSecurity';
import styles from './SettingsPage.module.css';
import type { AdminSettings } from './types';

type Values = Record<string, string>;

function serverValues(data: AdminSettings): Values {
  return Object.fromEntries(data.settings.map((s) => [s.key, s.value]));
}

const TABS = [
  { value: 'system', label: 'Tizim sozlamalari' },
  { value: 'security', label: 'Hisob xavfsizligi' },
] as const;

type SettingsTab = (typeof TABS)[number]['value'];

/**
 * Admin · Sozlamalar (SPEC-SCREENS §13): ikki varaq — "Tizim sozlamalari" (default) va
 * "Hisob xavfsizligi" (parol/login). Faol varaq URL'da: `?tab=security`.
 */
export function SettingsPage() {
  const [params, setParams] = useSearchParams();
  const tab: SettingsTab = params.get('tab') === 'security' ? 'security' : 'system';

  return (
    <div className={styles.page}>
      <PillGroup role="tablist" aria-label="Sozlamalar bo'limlari">
        {TABS.map((t) => (
          <Pill
            key={t.value}
            id={`settings-tab-${t.value}`}
            role="tab"
            shape="tab"
            active={tab === t.value}
            aria-controls={`settings-panel-${t.value}`}
            onClick={() =>
              setParams(
                (prev) => {
                  const next = new URLSearchParams(prev);
                  if (t.value === 'system') next.delete('tab');
                  else next.set('tab', t.value);
                  return next;
                },
                { replace: true },
              )
            }
          >
            {t.label}
          </Pill>
        ))}
      </PillGroup>
      <div id={`settings-panel-${tab}`} role="tabpanel" aria-labelledby={`settings-tab-${tab}`}>
        {tab === 'security' ? <AccountSecurity /> : <SystemSettings />}
      </div>
    </div>
  );
}

/** Tizim sozlamalari. Container: query + draft + PUT mutation (400 → maydon xatolari). */
function SystemSettings() {
  const query = useSettingsQuery();
  const update = useUpdateSettings();
  // Draft faqat foydalanuvchi o'zgartirgach paydo bo'ladi; null → server qiymatlari (effect'siz sinxron).
  const [draft, setDraft] = useState<Values | null>(null);

  const base = useMemo(() => (query.data ? serverValues(query.data) : null), [query.data]);

  if (query.isPending) return <LoadingState />;
  if (query.isError) return <ErrorState error={query.error} onRetry={() => void query.refetch()} />;
  if (!base) return null;

  const values = draft ?? base;
  const changed: Values = Object.fromEntries(
    Object.keys(values)
      .filter((k) => values[k] !== base[k])
      .map((k) => [k, values[k] ?? '']),
  );
  const isDirty = Object.keys(changed).length > 0;
  const fieldErrors =
    update.error && isApiError(update.error) && update.error.kind === 'validation'
      ? update.error.fieldErrors
      : {};

  return (
    <SettingsView
      data={query.data}
      values={values}
      fieldErrors={fieldErrors}
      isDirty={isDirty}
      isSaving={update.isPending}
      saveError={Object.keys(fieldErrors).length > 0 ? null : update.error}
      saved={update.isSuccess}
      onChange={(key, value) => setDraft({ ...values, [key]: value })}
      onSave={() => {
        if (!isDirty) return;
        update.mutate({ values: changed }, { onSuccess: () => setDraft(null) });
      }}
      onCancel={() => {
        setDraft(null);
        update.reset();
      }}
      // TODO: "Kun qo'shish" formasi dizaynda yo'q (❓) — hozircha no-op.
      onAddHoliday={() => undefined}
    />
  );
}

export default SettingsPage;
