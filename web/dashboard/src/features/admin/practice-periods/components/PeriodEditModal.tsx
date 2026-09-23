import { useId, useState, type FormEvent } from 'react';
import { isApiError } from '@/shared/api';
import { Button, Modal } from '@/shared/ui';
import { useUpdatePracticePeriod } from '../hooks';
import type { PracticePeriodDetail } from '../types';
import { validatePeriodForm, type PeriodFormErrors, type PeriodFormValues } from '../validation';
import { PeriodFields } from './PeriodFields';
import styles from './PeriodDetail.module.css';
import { ServerErrorBanner } from './ServerErrorBanner';

export interface PeriodEditModalProps {
  period: PracticePeriodDetail;
  onClose: () => void;
}

/**
 * "Tahrirlash": nom va sanalar. Faol davrda boshlanish sanasi qulflangan; yopilgan davr uchun
 * modal umuman ochilmaydi (tugma yashiringan). Har ochilishda yangi mount — holat qayta boshlanadi.
 */
export function PeriodEditModal({ period, onClose }: PeriodEditModalProps) {
  const formId = useId();
  const update = useUpdatePracticePeriod(period.id);
  const [values, setValues] = useState<PeriodFormValues>({
    name: period.name,
    startDate: period.startDate,
    endDate: period.endDate,
  });
  const [submitted, setSubmitted] = useState(false);
  const startLocked = period.status === 'active';

  const clientErrors = validatePeriodForm(values);
  const apiError = isApiError(update.error) ? update.error : undefined;
  const shown: PeriodFormErrors = {};
  for (const key of ['name', 'startDate', 'endDate'] as const) {
    const msg =
      (submitted || key === 'endDate' ? clientErrors[key] : undefined) ?? apiError?.fieldError(key);
    if (msg) shown[key] = msg;
  }

  function close() {
    if (!update.isPending) onClose();
  }

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setSubmitted(true);
    if (Object.keys(clientErrors).length > 0) return;
    update.mutate(
      {
        name: values.name.trim(),
        startDate: startLocked ? period.startDate : values.startDate,
        endDate: values.endDate,
      },
      { onSuccess: onClose },
    );
  }

  return (
    <Modal
      open
      onClose={close}
      title="Davrni tahrirlash"
      width="min(560px, 94vw)"
      footer={
        <>
          <Button type="button" onClick={close} disabled={update.isPending}>
            Bekor qilish
          </Button>
          <Button type="submit" form={formId} variant="primary" disabled={update.isPending}>
            {update.isPending ? 'Saqlanmoqda…' : 'Saqlash'}
          </Button>
        </>
      }
    >
      <form id={formId} className={styles.modalBody} noValidate onSubmit={handleSubmit}>
        <ServerErrorBanner error={update.error} title="Saqlab bo'lmadi" />
        <PeriodFields
          idPrefix="edit-period"
          values={values}
          onChange={setValues}
          errors={shown}
          disabled={update.isPending}
          startLocked={startLocked}
        />
      </form>
    </Modal>
  );
}
