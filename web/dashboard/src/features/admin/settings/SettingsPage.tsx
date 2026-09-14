import { useMemo, useState } from 'react';
import { isApiError } from '@/shared/api';
import { ErrorState, LoadingState } from '../components/PageStatus';
import { SettingsView } from './components/SettingsView';
import { useSettingsQuery, useUpdateSettings } from './hooks';
import type { AdminSettings } from './types';

type Values = Record<string, string>;

function serverValues(data: AdminSettings): Values {
  return Object.fromEntries(data.settings.map((s) => [s.key, s.value]));
}

/** Admin · Sozlamalar (SPEC-SCREENS §13). Container: query + draft + PUT mutation (400 → maydon xatolari). */
export function SettingsPage() {
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
