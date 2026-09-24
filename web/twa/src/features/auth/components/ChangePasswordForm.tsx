import { useState, type FormEvent } from 'react';
import { errorMessage, isApiError } from '@/shared/api/client';
import { Button, Input } from '@/shared/ui';
import { useChangePassword } from '../hooks';
import styles from './AuthScreen.module.css';

interface Values {
  currentPassword: string;
  newPassword: string;
  confirm: string;
}

type Errors = Partial<Record<keyof Values, string>>;

export const MIN_PASSWORD_LENGTH = 8;
export const MAX_PASSWORD_LENGTH = 128;

function validate(v: Values): Errors {
  const e: Errors = {};
  if (!v.currentPassword) e.currentPassword = 'Joriy parolni kiriting.';
  if (!v.newPassword) e.newPassword = 'Yangi parolni kiriting.';
  else if (v.newPassword.length < MIN_PASSWORD_LENGTH) {
    e.newPassword = `Parol kamida ${MIN_PASSWORD_LENGTH} ta belgidan iborat bo'lishi kerak.`;
  } else if (v.newPassword.length > MAX_PASSWORD_LENGTH) {
    e.newPassword = `Parol ${MAX_PASSWORD_LENGTH} belgidan oshmasligi kerak.`;
  } else if (v.newPassword === v.currentPassword) {
    e.newPassword = 'Yangi parol joriy paroldan farq qilishi kerak.';
  }
  if (!v.confirm) e.confirm = 'Yangi parolni takrorlang.';
  else if (v.newPassword && v.confirm !== v.newPassword) e.confirm = 'Parollar mos kelmadi.';
  return e;
}

export interface ChangePasswordFormProps {
  /** 204 dan keyin. */
  onSuccess?: () => void;
  onCancel?: () => void;
  submitLabel?: string;
}

/**
 * Parolni o'zgartirish (joriy · yangi · takror) → POST /api/auth/change-password.
 * Server 400 `errors.currentPassword` / `errors.newPassword` (camelCase yoki PascalCase) — maydon ostida.
 */
export function ChangePasswordForm({
  onSuccess,
  onCancel,
  submitLabel = 'Saqlash',
}: ChangePasswordFormProps) {
  const change = useChangePassword();
  const [values, setValues] = useState<Values>({
    currentPassword: '',
    newPassword: '',
    confirm: '',
  });
  const [errors, setErrors] = useState<Errors>({});

  const apiError = isApiError(change.error) ? change.error : null;
  const serverCurrent = apiError?.fieldError('currentPassword');
  const serverNew = apiError?.fieldError('newPassword');
  const generalError =
    change.isError && !serverCurrent && !serverNew ? errorMessage(change.error) : null;

  function set<K extends keyof Values>(key: K, value: string) {
    setValues((v) => ({ ...v, [key]: value }));
  }

  function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const next = validate(values);
    setErrors(next);
    if (Object.keys(next).length > 0) return;
    change.mutate(
      { currentPassword: values.currentPassword, newPassword: values.newPassword },
      { onSuccess: () => onSuccess?.() },
    );
  }

  return (
    <form className={styles.form} onSubmit={onSubmit} noValidate aria-label="Parolni o'zgartirish">
      <Input
        name="currentPassword"
        type="password"
        label="Joriy parol"
        autoComplete="current-password"
        value={values.currentPassword}
        onChange={(e) => set('currentPassword', e.target.value)}
        error={errors.currentPassword ?? serverCurrent}
      />
      <Input
        name="newPassword"
        type="password"
        label="Yangi parol"
        autoComplete="new-password"
        hint={`Kamida ${MIN_PASSWORD_LENGTH} ta belgi.`}
        value={values.newPassword}
        onChange={(e) => set('newPassword', e.target.value)}
        error={errors.newPassword ?? serverNew}
      />
      <Input
        name="confirm"
        type="password"
        label="Yangi parolni takrorlang"
        autoComplete="new-password"
        value={values.confirm}
        onChange={(e) => set('confirm', e.target.value)}
        error={errors.confirm}
      />

      {generalError && (
        <p role="alert" className={styles.serverError}>
          {generalError}
        </p>
      )}

      <div className={styles.actions}>
        <Button type="submit" variant="primary" size="lg" block disabled={change.isPending}>
          {change.isPending ? 'Saqlanmoqda…' : submitLabel}
        </Button>
        {onCancel && (
          <Button type="button" block onClick={onCancel} disabled={change.isPending}>
            Bekor qilish
          </Button>
        )}
      </div>
    </form>
  );
}
