import { useEffect, useId, useState, type ChangeEvent, type FormEvent } from 'react';
import { errorMessage, isApiError } from '@/shared/api';
import { Button, Input, Modal, Select } from '@/shared/ui';
import { formatPhone } from '../../shared/format';
import { useCreateTutor, useFacultyOptions, useUpdateTutor } from '../hooks';
import {
  firstIssues,
  tutorCreateSchema,
  tutorEditSchema,
  type TutorCreateFormValues,
} from '../schema';
import styles from './TutorForms.module.css';

/** `Tutor` (ro'yxat qatori) va `TutorDetail` ikkalasi ham shu shaklga mos. */
export interface TutorFormInitial {
  id: string;
  fullName: string;
  phone: string | null;
  facultyId: string | null;
}

export interface TutorFormModalProps {
  open: boolean;
  mode: 'create' | 'edit';
  /** `mode==='edit'` da forma shu qiymatlar bilan to'ldiriladi. */
  initial?: TutorFormInitial | null;
  onClose: () => void;
}

type FieldKey = keyof TutorCreateFormValues;

const EMPTY_VALUES: TutorCreateFormValues = {
  fullName: '',
  hemisId: '',
  phone: '',
  password: '',
  facultyId: '',
};

/**
 * "Yangi tyutor" / "Tyutorni tahrirlash" — `mode` bo'yicha POST yoki PUT.
 * Tahrirlashda HEMIS ID va parol maydonlari yo'q (parol — alohida "Parolni tiklash").
 */
export function TutorFormModal({ open, mode, initial, onClose }: TutorFormModalProps) {
  const formId = useId();
  const createTutor = useCreateTutor();
  const updateTutor = useUpdateTutor();
  const faculties = useFacultyOptions();
  const mutation = mode === 'edit' ? updateTutor : createTutor;

  const [values, setValues] = useState<TutorCreateFormValues>(EMPTY_VALUES);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<FieldKey, string>>>({});

  // Har ochilishda (yoki tahrirlanayotgan tyutor almashganda) formani qayta boshlash.
  useEffect(() => {
    if (!open) return;
    setValues(
      mode === 'edit' && initial
        ? {
            ...EMPTY_VALUES,
            fullName: initial.fullName,
            phone: initial.phone ? formatPhone(initial.phone) : '',
            facultyId: initial.facultyId ?? '',
          }
        : EMPTY_VALUES,
    );
    setFieldErrors({});
    createTutor.reset();
    updateTutor.reset();
    // Faqat ochilish/rejim/tahrirlanayotgan id o'zgarganda (mutatsiya obyektlari har render yangi).
  }, [open, mode, initial?.id]);

  function handleClose() {
    if (mutation.isPending) return;
    onClose();
  }

  function field(key: FieldKey) {
    return (e: ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
      setValues((v) => ({ ...v, [key]: e.target.value }));
  }

  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (mode === 'edit' && initial) {
      const parsed = tutorEditSchema.safeParse(values);
      if (!parsed.success) {
        setFieldErrors(firstIssues<FieldKey>(parsed.error.issues));
        return;
      }
      setFieldErrors({});
      const { fullName, phone, facultyId } = parsed.data;
      updateTutor.mutate(
        { id: initial.id, body: { fullName, phone: phone || null, facultyId } },
        { onSuccess: onClose },
      );
      return;
    }
    const parsed = tutorCreateSchema.safeParse(values);
    if (!parsed.success) {
      setFieldErrors(firstIssues<FieldKey>(parsed.error.issues));
      return;
    }
    setFieldErrors({});
    const { fullName, hemisId, phone, password, facultyId } = parsed.data;
    createTutor.mutate(
      { fullName, hemisId, phone: phone || null, password, facultyId },
      { onSuccess: onClose },
    );
  }

  const apiError = isApiError(mutation.error) ? mutation.error : undefined;
  const errorFor = (key: FieldKey) => fieldErrors[key] ?? apiError?.fieldError(key);
  const shownErrors = {
    fullName: errorFor('fullName'),
    hemisId: mode === 'create' ? errorFor('hemisId') : undefined,
    phone: errorFor('phone'),
    password: mode === 'create' ? errorFor('password') : undefined,
    facultyId: errorFor('facultyId'),
  };
  const hasFieldError = Object.values(shownErrors).some(Boolean);
  // Validatsiya xatosi maydonlarda ko'rsatiladi; qolgan holatlar (404/409/tarmoq) — umumiy banner.
  const generalError =
    mutation.isError && !hasFieldError ? errorMessage(mutation.error) : undefined;

  const facultyOptions = faculties.data ?? [];
  const facultyPlaceholder = faculties.isPending
    ? 'Yuklanmoqda…'
    : faculties.isError
      ? "Fakultetlarni yuklab bo'lmadi"
      : 'Tanlang';

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title={mode === 'edit' ? 'Tyutorni tahrirlash' : 'Yangi tyutor'}
      footer={
        <>
          <Button type="button" onClick={handleClose} disabled={mutation.isPending}>
            Bekor qilish
          </Button>
          <Button type="submit" form={formId} variant="primary" disabled={mutation.isPending}>
            {mutation.isPending ? 'Saqlanmoqda…' : 'Saqlash'}
          </Button>
        </>
      }
    >
      <form id={formId} className={styles.form} noValidate onSubmit={handleSubmit}>
        <Input
          id="tutor-full-name"
          label="FISH"
          variant="form"
          autoComplete="off"
          value={values.fullName}
          onChange={field('fullName')}
          error={shownErrors.fullName}
          disabled={mutation.isPending}
        />
        {mode === 'create' && (
          <Input
            id="tutor-hemis-id"
            label="HEMIS ID"
            variant="form"
            mono
            inputMode="numeric"
            autoComplete="off"
            maxLength={20}
            value={values.hemisId}
            onChange={field('hemisId')}
            error={shownErrors.hemisId}
            disabled={mutation.isPending}
          />
        )}
        <Input
          id="tutor-phone"
          label="Telefon"
          variant="form"
          mono
          type="tel"
          autoComplete="off"
          placeholder="+998 90 123-45-67"
          hint="Ixtiyoriy."
          value={values.phone}
          onChange={field('phone')}
          error={shownErrors.phone}
          disabled={mutation.isPending}
        />
        {mode === 'create' && (
          <Input
            id="tutor-password"
            label="Parol"
            variant="form"
            type="password"
            autoComplete="new-password"
            hint="Kamida 8 ta belgi."
            value={values.password}
            onChange={field('password')}
            error={shownErrors.password}
            disabled={mutation.isPending}
          />
        )}
        <Select
          id="tutor-faculty"
          label="Fakultet"
          variant="form"
          placeholder={facultyPlaceholder}
          options={facultyOptions}
          hint={
            mode === 'edit'
              ? "Guruhlar biriktirilgan bo'lsa fakultetni o'zgartirib bo'lmaydi."
              : undefined
          }
          value={values.facultyId}
          onChange={field('facultyId')}
          error={shownErrors.facultyId}
          disabled={mutation.isPending || faculties.isPending}
        />
        {generalError && (
          <p role="alert" className={styles.error}>
            {generalError}
          </p>
        )}
      </form>
    </Modal>
  );
}
