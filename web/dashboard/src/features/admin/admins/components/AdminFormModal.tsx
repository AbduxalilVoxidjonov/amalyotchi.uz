import { useEffect, useId, useState, type ChangeEvent, type FormEvent } from 'react';
import { errorMessage, isApiError } from '@/shared/api';
import { Button, Input, Modal } from '@/shared/ui';
import { formatPhone } from '../../shared/format';
import { useCreateAdmin, useUpdateAdmin } from '../hooks';
import {
  adminCreateSchema,
  adminEditSchema,
  firstIssues,
  type AdminCreateFormValues,
} from '../schema';
import type { AdminAccount, AdminCreateResult } from '../types';
import styles from '../Admins.module.css';

export interface AdminFormModalProps {
  open: boolean;
  mode: 'create' | 'edit';
  /** `mode==='edit'` da forma shu qiymatlar bilan to'ldiriladi. */
  initial?: AdminAccount | null;
  onClose: () => void;
  /** Yaratilgandan keyin (vaqtinchalik parolni ko'rsatish uchun). */
  onCreated?: (created: AdminCreateResult) => void;
}

type FieldKey = keyof AdminCreateFormValues;

const EMPTY_VALUES: AdminCreateFormValues = {
  fullName: '',
  login: '',
  phoneNumber: '',
  password: '',
};

/**
 * "Yangi admin" / "Adminni tahrirlash" — `mode` bo'yicha POST yoki PUT.
 * Tahrirlashda login va parol yo'q (parol — alohida "Parolni tiklash"). Yaratishda parol
 * ixtiyoriy: bo'sh qolsa server vaqtinchalik parol yaratadi va u bir marta ko'rsatiladi.
 */
export function AdminFormModal({ open, mode, initial, onClose, onCreated }: AdminFormModalProps) {
  const formId = useId();
  const createAdmin = useCreateAdmin();
  const updateAdmin = useUpdateAdmin();
  const mutation = mode === 'edit' ? updateAdmin : createAdmin;

  const [values, setValues] = useState<AdminCreateFormValues>(EMPTY_VALUES);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<FieldKey, string>>>({});

  useEffect(() => {
    if (!open) return;
    setValues(
      mode === 'edit' && initial
        ? {
            ...EMPTY_VALUES,
            fullName: initial.fullName,
            phoneNumber: initial.phoneNumber ? formatPhone(initial.phoneNumber) : '',
          }
        : EMPTY_VALUES,
    );
    setFieldErrors({});
    createAdmin.reset();
    updateAdmin.reset();
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
    if (mode === 'edit' && initial) {
      const parsed = adminEditSchema.safeParse(values);
      if (!parsed.success) {
        setFieldErrors(firstIssues<FieldKey>(parsed.error.issues));
        return;
      }
      setFieldErrors({});
      updateAdmin.mutate({ id: initial.id, body: parsed.data }, { onSuccess: onClose });
      return;
    }
    const parsed = adminCreateSchema.safeParse(values);
    if (!parsed.success) {
      setFieldErrors(firstIssues<FieldKey>(parsed.error.issues));
      return;
    }
    setFieldErrors({});
    const { fullName, login, phoneNumber, password } = parsed.data;
    createAdmin.mutate(
      { fullName, login, phoneNumber, ...(password ? { password } : {}) },
      {
        onSuccess: (created) => {
          onClose();
          onCreated?.(created);
        },
      },
    );
  }

  const apiError = isApiError(mutation.error) ? mutation.error : undefined;
  // 409 (login band) — login maydoni ostida.
  const loginConflict =
    mode === 'create' && apiError?.status === 409 ? apiError.message : undefined;
  const errorFor = (key: FieldKey) => fieldErrors[key] ?? apiError?.fieldError(key);
  const shownErrors = {
    fullName: errorFor('fullName'),
    login: mode === 'create' ? (errorFor('login') ?? loginConflict) : undefined,
    phoneNumber: errorFor('phoneNumber'),
    password: mode === 'create' ? errorFor('password') : undefined,
  };
  const hasFieldError = Object.values(shownErrors).some(Boolean);
  const generalError =
    mutation.isError && !hasFieldError ? errorMessage(mutation.error) : undefined;

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title={mode === 'edit' ? 'Adminni tahrirlash' : 'Yangi admin'}
      width="440px"
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
          id={`${formId}-full-name`}
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
            id={`${formId}-login`}
            label="Login"
            variant="form"
            mono
            autoComplete="off"
            hint="Tizimga kirish identifikatori."
            value={values.login}
            onChange={field('login')}
            error={shownErrors.login}
            disabled={mutation.isPending}
          />
        )}
        <Input
          id={`${formId}-phone`}
          label="Telefon"
          variant="form"
          mono
          type="tel"
          autoComplete="off"
          placeholder="+998 90 123-45-67"
          hint="Ixtiyoriy."
          value={values.phoneNumber}
          onChange={field('phoneNumber')}
          error={shownErrors.phoneNumber}
          disabled={mutation.isPending}
        />
        {mode === 'create' && (
          <Input
            id={`${formId}-password`}
            label="Parol"
            variant="form"
            type="password"
            autoComplete="new-password"
            hint="Ixtiyoriy. Bo'sh qoldirilsa vaqtinchalik parol yaratiladi va bir marta ko'rsatiladi."
            value={values.password}
            onChange={field('password')}
            error={shownErrors.password}
            disabled={mutation.isPending}
          />
        )}
        {generalError && (
          <p role="alert" className={styles.error}>
            {generalError}
          </p>
        )}
      </form>
    </Modal>
  );
}
