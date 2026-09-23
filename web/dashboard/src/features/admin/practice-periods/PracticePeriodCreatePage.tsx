import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { usePageHeader } from '@/app/layout';
import { isApiError } from '@/shared/api';
import { Breadcrumb, Button, Card, CardBody, CardHeader } from '@/shared/ui';
import { GroupPicker } from './components/GroupPicker';
import { PeriodFields } from './components/PeriodFields';
import { ServerErrorBanner } from './components/ServerErrorBanner';
import { useCreatePracticePeriod } from './hooks';
import { PRACTICE_PERIODS_HREF, periodHref } from './paths';
import styles from './PracticePeriodCreatePage.module.css';
import type { PracticePeriodGroup } from './types';
import {
  EMPTY_PERIOD_FORM,
  validatePeriodForm,
  type PeriodFormErrors,
  type PeriodFormValues,
} from './validation';

/** Detail sahifasida bir martalik muvaffaqiyat xabari (loyihada toast yo'q). */
export interface PeriodFlashState {
  flash?: string;
}

/**
 * Admin · Yangi amaliyot davri. Chapda — davr (nom, sanalar), o'ngda — guruh tanlash
 * (kaskad, bir nechta yo'nalish/fakultet bo'ylab). Muvaffaqiyatda — davr sahifasiga.
 */
export function PracticePeriodCreatePage() {
  usePageHeader({ title: 'Yangi amaliyot davri' });
  const navigate = useNavigate();
  const create = useCreatePracticePeriod();

  const [values, setValues] = useState<PeriodFormValues>(EMPTY_PERIOD_FORM);
  const [groups, setGroups] = useState<PracticePeriodGroup[]>([]);
  const [touched, setTouched] = useState<Partial<Record<keyof PeriodFormValues, boolean>>>({});

  const clientErrors = validatePeriodForm(values);
  const apiError = isApiError(create.error) ? create.error : undefined;
  // Maydon xatosi: foydalanuvchi tekkan maydon uchun mijoz tekshiruvi, keyin server `errors`.
  const shown: PeriodFormErrors = {};
  for (const key of ['name', 'startDate', 'endDate'] as const) {
    // Tugash < boshlanish — darhol ko'rinsin (ikkala sana tanlangan bo'lsa).
    const eager = key === 'endDate' && values.startDate !== '' && values.endDate !== '';
    const msg = touched[key] || eager ? clientErrors[key] : undefined;
    const fromServer = apiError?.fieldError(key);
    const value = msg ?? fromServer;
    if (value) shown[key] = value;
  }

  const formValid = Object.keys(clientErrors).length === 0;
  const canSubmit = formValid && groups.length > 0 && !create.isPending;

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setTouched({ name: true, startDate: true, endDate: true });
    if (!canSubmit) return;
    const name = values.name.trim();
    create.mutate(
      {
        name,
        startDate: values.startDate,
        endDate: values.endDate,
        groupIds: groups.map((g) => g.id),
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
    ? 'Davr nomi va sanalarini to‘g‘ri kiriting.'
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
                onChange={setValues}
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
