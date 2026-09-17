import { useEffect, useId, useState, type ChangeEvent, type FormEvent } from 'react';
import { errorMessage, isApiError } from '@/shared/api';
import { Button, Input, Modal } from '@/shared/ui';
import { formatPhone } from '../../shared/format';
import { useCreateCompany, useUpdateCompany } from '../hooks';
import { companySchema, firstIssues, type CompanyFormValues } from '../schema';
import styles from './CompanyFormModal.module.css';

/** `Company` (ro'yxat qatori) da lat/lng va rahbar/mentor yo'q — tahrirlash `CompanyDetail` bilan ochiladi. */
export interface CompanyFormInitial {
  id: string;
  name: string;
  tin: string;
  activity: string;
  address: string;
  lat: number;
  lng: number;
  radiusM: number;
  supervisorName: string;
  supervisorPhone: string;
  mentorName: string | null;
  mentorPhone: string | null;
}

export interface CompanyFormModalProps {
  open: boolean;
  mode: 'create' | 'edit';
  /** `mode==='edit'` da forma shu qiymatlar bilan to'ldiriladi (detail so'rovi kelgach). */
  initial?: CompanyFormInitial | null;
  /** Tahrirlash uchun detail hali yuklanmoqda — maydonlar bloklanadi. */
  loading?: boolean;
  onClose: () => void;
}

type FieldKey = keyof CompanyFormValues;

const EMPTY_VALUES: CompanyFormValues = {
  name: '',
  tin: '',
  activity: '',
  address: '',
  lat: '',
  lng: '',
  radiusM: '',
  supervisorName: '',
  supervisorPhone: '',
  mentorName: '',
  mentorPhone: '',
};

function toValues(initial: CompanyFormInitial): CompanyFormValues {
  return {
    name: initial.name,
    tin: initial.tin,
    activity: initial.activity,
    address: initial.address,
    lat: String(initial.lat),
    lng: String(initial.lng),
    radiusM: String(initial.radiusM),
    supervisorName: initial.supervisorName,
    supervisorPhone: formatPhone(initial.supervisorPhone),
    mentorName: initial.mentorName ?? '',
    mentorPhone: initial.mentorPhone ? formatPhone(initial.mentorPhone) : '',
  };
}

/**
 * "Yangi korxona" / "Korxonani tahrirlash" — bitta forma, `mode` bo'yicha POST yoki PUT.
 * Klient validatsiyasi `companySchema` (backend `CompanyValidationRules` bilan bir xil matnlar),
 * serverdan kelgan 400 `errors` maydon ostida, qolgan xatolar (409 STIR takrori) — umumiy banner.
 */
export function CompanyFormModal({
  open,
  mode,
  initial,
  loading = false,
  onClose,
}: CompanyFormModalProps) {
  const formId = useId();
  const createCompany = useCreateCompany();
  const updateCompany = useUpdateCompany();
  const mutation = mode === 'edit' ? updateCompany : createCompany;
  const busy = mutation.isPending || loading;

  const [values, setValues] = useState<CompanyFormValues>(EMPTY_VALUES);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<FieldKey, string>>>({});

  // Har ochilishda (yoki tahrirlanayotgan korxona almashganda) formani qayta boshlash.
  useEffect(() => {
    if (!open) return;
    setValues(mode === 'edit' && initial ? toValues(initial) : EMPTY_VALUES);
    setFieldErrors({});
    createCompany.reset();
    updateCompany.reset();
    // Faqat ochilish/rejim/tahrirlanayotgan id o'zgarganda (mutatsiya obyektlari har render yangi).
  }, [open, mode, initial?.id]);

  function handleClose() {
    if (mutation.isPending) return;
    onClose();
  }

  function field(key: FieldKey) {
    return (e: ChangeEvent<HTMLInputElement>) =>
      setValues((v) => ({ ...v, [key]: e.target.value }));
  }

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const parsed = companySchema.safeParse(values);
    if (!parsed.success) {
      setFieldErrors(firstIssues<FieldKey>(parsed.error.issues));
      return;
    }
    setFieldErrors({});
    const body = parsed.data;
    if (mode === 'edit' && initial) {
      updateCompany.mutate({ id: initial.id, body }, { onSuccess: onClose });
    } else {
      createCompany.mutate(body, { onSuccess: onClose });
    }
  }

  const apiError = isApiError(mutation.error) ? mutation.error : undefined;
  const errorFor = (key: FieldKey) => fieldErrors[key] ?? apiError?.fieldError(key);
  const shownErrors: Record<FieldKey, string | undefined> = {
    name: errorFor('name'),
    tin: errorFor('tin'),
    activity: errorFor('activity'),
    address: errorFor('address'),
    lat: errorFor('lat'),
    lng: errorFor('lng'),
    radiusM: errorFor('radiusM'),
    supervisorName: errorFor('supervisorName'),
    supervisorPhone: errorFor('supervisorPhone'),
    mentorName: errorFor('mentorName'),
    mentorPhone: errorFor('mentorPhone'),
  };
  const hasFieldError = Object.values(shownErrors).some(Boolean);
  // Validatsiya xatosi maydonlarda ko'rsatiladi; qolgan holatlar (404/409/tarmoq) — umumiy banner.
  const generalError =
    mutation.isError && !hasFieldError ? errorMessage(mutation.error) : undefined;

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title={mode === 'edit' ? 'Korxonani tahrirlash' : 'Yangi korxona'}
      width="min(680px, 94vw)"
      footer={
        <>
          <Button type="button" onClick={handleClose} disabled={mutation.isPending}>
            Bekor qilish
          </Button>
          <Button type="submit" form={formId} variant="primary" disabled={busy}>
            {mutation.isPending ? 'Saqlanmoqda…' : 'Saqlash'}
          </Button>
        </>
      }
    >
      <form id={formId} className={styles.form} noValidate onSubmit={handleSubmit}>
        <Input
          id="company-name"
          label="Nomi"
          variant="form"
          autoComplete="off"
          value={values.name}
          onChange={field('name')}
          error={shownErrors.name}
          disabled={busy}
        />
        <div className={styles.row}>
          <Input
            id="company-tin"
            label="STIR"
            variant="form"
            mono
            inputMode="numeric"
            autoComplete="off"
            maxLength={11}
            placeholder="123456789"
            value={values.tin}
            onChange={field('tin')}
            error={shownErrors.tin}
            disabled={busy}
          />
          <Input
            id="company-activity"
            label="Faoliyat turi"
            variant="form"
            autoComplete="off"
            value={values.activity}
            onChange={field('activity')}
            error={shownErrors.activity}
            disabled={busy}
          />
        </div>
        <Input
          id="company-address"
          label="Manzil"
          variant="form"
          autoComplete="off"
          value={values.address}
          onChange={field('address')}
          error={shownErrors.address}
          disabled={busy}
        />
        <div className={styles.row3}>
          <Input
            id="company-lat"
            label="Kenglik (lat)"
            variant="form"
            mono
            inputMode="decimal"
            autoComplete="off"
            placeholder="41.3111"
            value={values.lat}
            onChange={field('lat')}
            error={shownErrors.lat}
            disabled={busy}
          />
          <Input
            id="company-lng"
            label="Uzunlik (lng)"
            variant="form"
            mono
            inputMode="decimal"
            autoComplete="off"
            placeholder="69.2797"
            value={values.lng}
            onChange={field('lng')}
            error={shownErrors.lng}
            disabled={busy}
          />
          <Input
            id="company-radius"
            label="Radius (m)"
            variant="form"
            mono
            inputMode="numeric"
            autoComplete="off"
            placeholder="200"
            hint="Ixtiyoriy — 50–1000."
            value={values.radiusM}
            onChange={field('radiusM')}
            error={shownErrors.radiusM}
            disabled={busy}
          />
        </div>
        <div className={styles.row}>
          <Input
            id="company-supervisor-name"
            label="Rahbar FISH"
            variant="form"
            autoComplete="off"
            value={values.supervisorName}
            onChange={field('supervisorName')}
            error={shownErrors.supervisorName}
            disabled={busy}
          />
          <Input
            id="company-supervisor-phone"
            label="Rahbar telefoni"
            variant="form"
            mono
            type="tel"
            autoComplete="off"
            placeholder="+998 90 123-45-67"
            value={values.supervisorPhone}
            onChange={field('supervisorPhone')}
            error={shownErrors.supervisorPhone}
            disabled={busy}
          />
        </div>
        <div className={styles.row}>
          <Input
            id="company-mentor-name"
            label="Mentor FISH"
            variant="form"
            autoComplete="off"
            hint="Ixtiyoriy."
            value={values.mentorName}
            onChange={field('mentorName')}
            error={shownErrors.mentorName}
            disabled={busy}
          />
          <Input
            id="company-mentor-phone"
            label="Mentor telefoni"
            variant="form"
            mono
            type="tel"
            autoComplete="off"
            placeholder="+998 90 123-45-67"
            hint="Ixtiyoriy."
            value={values.mentorPhone}
            onChange={field('mentorPhone')}
            error={shownErrors.mentorPhone}
            disabled={busy}
          />
        </div>
        {generalError && (
          <p role="alert" className={styles.error}>
            {generalError}
          </p>
        )}
      </form>
    </Modal>
  );
}
