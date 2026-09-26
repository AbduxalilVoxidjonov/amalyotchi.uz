import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { usePageHeader } from '@/app/layout';
import { isApiError } from '@/shared/api';
import { Breadcrumb, Button, Card, CardBody, CardHeader } from '@/shared/ui';
import { useSettingsQuery } from '../settings/hooks';
import { normalizeWeekdays } from '../shared/weekdays';
import { GroupPicker } from './components/GroupPicker';
import { PeriodFields } from './components/PeriodFields';
import { ServerErrorBanner } from './components/ServerErrorBanner';
import { useCreatePracticePeriod } from './hooks';
import { PRACTICE_PERIODS_HREF, periodHref } from './paths';
import styles from './PracticePeriodCreatePage.module.css';
import type { PracticePeriodGroup } from './types';
import {
  EMPTY_PERIOD_FORM,
  PERIOD_FORM_FIELDS,
  validatePeriodForm,
  type PeriodFormErrors,
  type PeriodFormField,
  type PeriodFormValues,
} from './validation';

/** Detail sahifasida bir martalik muvaffaqiyat xabari (loyihada toast yo'q). */
export interface PeriodFlashState {
  flash?: string;
}

/**
 * Admin · Yangi amaliyot davri. Chapda — davr (nom, sanalar, ish kunlari, ish vaqti), o'ngda —
 * guruh tanlash
 * (kaskad, bir nechta yo'nalish/fakultet bo'ylab). Muvaffaqiyatda — davr sahifasiga.
 */
export function PracticePeriodCreatePage() {
  usePageHeader({ title: 'Yangi amaliyot davri' });
  const navigate = useNavigate();
  const create = useCreatePracticePeriod();

  const settings = useSettingsQuery();

  const [draft, setDraft] = useState<PeriodFormValues>(EMPTY_PERIOD_FORM);
  const [groups, setGroups] = useState<PracticePeriodGroup[]>([]);
  const [touched, setTouched] = useState<Partial<Record<PeriodFormField, boolean>>>({});
  // Foydalanuvchi ish kunlariga tegmaguncha — global sozlamadagi `workDays` (bo'lsa) ko'rsatiladi.
  const [workDaysEdited, setWorkDaysEdited] = useState(false);

  const settingsWorkDays = normalizeWeekdays(
    settings.data?.settings.find((s) => s.key === 'workDays')?.value ?? '',
  );
  const values: PeriodFormValues =
    workDaysEdited || !settingsWorkDays ? draft : { ...draft, workDays: settingsWorkDays };

  function handleChange(next: PeriodFormValues) {
    if (next.workDays !== values.workDays) setWorkDaysEdited(true);
    setDraft(next);
  }

  const clientErrors = validatePeriodForm(values);
  const apiError = isApiError(create.error) ? create.error : undefined;
  // Maydon xatosi: foydalanuvchi tekkan maydon uchun mijoz tekshiruvi, keyin server `errors`.
  const shown: PeriodFormErrors = {};
  for (const key of PERIOD_FORM_FIELDS) {
    // Darhol ko'rinadiganlar: tugash < boshlanish (ikkala sana tanlangan bo'lsa); ish kunlari va
    // vaqt — ularning standart qiymati bor, xato faqat foydalanuvchi o'zgartirganda paydo bo'ladi.
    const eager =
      (key === 'endDate' && values.startDate !== '' && values.endDate !== '') ||
      key === 'workDays' ||
      key === 'dailyStart' ||
      key === 'dailyEnd';
    const msg = touched[key] || eager ? clientErrors[key] : undefined;
    const fromServer = apiError?.fieldError(key);
    const value = msg ?? fromServer;
    if (value) shown[key] = value;
  }

  const formValid = Object.keys(clientErrors).length === 0;
  const canSubmit = formValid && groups.length > 0 && !create.isPending;

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setTouched(Object.fromEntries(PERIOD_FORM_FIELDS.map((f) => [f, true])));
    if (!canSubmit) return;
    const name = values.name.trim();
    create.mutate(
      {
        name,
        startDate: values.startDate,
        endDate: values.endDate,
        groupIds: groups.map((g) => g.id),
        dailyStart: values.dailyStart,
        dailyEnd: values.dailyEnd,
        workDays: values.workDays,
      },
      {
        onSuccess: (created) =>
          navigate(periodHref(created), {
            state: { flash: `«${created.name}» davri yaratildi.` } satisfies PeriodFlashState,
          }),
      },
    );
  }

  const blocker = !formValid
    ? 'Davr nomi, sanalari, ish kunlari va vaqtini to‘g‘ri kiriting.'
    : groups.length === 0
      ? 'Kamida bitta guruh tanlang.'
      : null;

  return (
    <div className={styles.page}>
      <Breadcrumb
        items={[{ label: 'Amaliyot davrlari', to: PRACTICE_PERIODS_HREF }, { label: 'Yangi davr' }]}
      />
      <form className={styles.form} noValidate onSubmit={handleSubmit}>
        <ServerErrorBanner error={create.error} title="Davrni yaratib bo'lmadi" />

        <div className={styles.columns}>
          <Card className={styles.card} aria-labelledby="period-card-title">
            <CardHeader title={<span id="period-card-title">Davr</span>} />
            <CardBody>
              <PeriodFields
                idPrefix="period"
                values={values}
                onChange={handleChange}
                errors={shown}
                onBlurField={(f) => setTouched((t) => ({ ...t, [f]: true }))}
                disabled={create.isPending}
                showSettingsNote
              />
            </CardBody>
          </Card>

          <Card className={styles.card} aria-labelledby="groups-card-title">
            <CardHeader title={<span id="groups-card-title">Guruhlarni tanlash</span>} />
            <CardBody>
              <GroupPicker
                selected={groups}
                onChange={setGroups}
                startDate={values.startDate}
                endDate={values.endDate}
                disabled={create.isPending}
              />
            </CardBody>
          </Card>
        </div>

        <div className={styles.footer}>
          {blocker && <span className={styles.blocker}>{blocker}</span>}
          <Button asChild>
            <Link to={PRACTICE_PERIODS_HREF}>Bekor qilish</Link>
          </Button>
          <Button type="submit" variant="primary" disabled={!canSubmit}>
            {create.isPending ? 'Yaratilmoqda…' : 'Davr yaratish'}
          </Button>
        </div>
      </form>
    </div>
  );
}

export default PracticePeriodCreatePage;
