import { useState, type FormEvent } from 'react';
import { errorMessage, isApiError } from '@/shared/api';
import { useChangePassword } from '@/features/auth/hooks';
import { Button, Card, CardHeader, Input } from '@/shared/ui';
import {
  EMPTY_PASSWORD_FORM,
  PASSWORD_MIN_LENGTH,
  validatePasswordForm,
  type PasswordFormErrors,
  type PasswordFormValues,
} from './validation';
import styles from './AccountSecurity.module.css';

/**
 * Parolni almashtirish: joriy + yangi + takror. Mijoz tekshiruvi → `POST /api/auth/change-password`
 * (joriy refresh token bilan — sessiya saqlanadi). 400 `errors.CurrentPassword/NewPassword` → maydon ostida.
 */
export function ChangePasswordCard() {
  const mutation = useChangePassword();
  const [values, setValues] = useState<PasswordFormValues>(EMPTY_PASSWORD_FORM);
  const [clientErrors, setClientErrors] = useState<PasswordFormErrors>({});
  const [visible, setVisible] = useState(false);

  const serverError = mutation.error;
  const apiErr = isApiError(serverError) && serverError.kind === 'validation' ? serverError : null;
  const errors: Record<keyof PasswordFormValues, string | undefined> = {
    currentPassword: clientErrors.currentPassword ?? apiErr?.fieldError('currentPassword'),
    newPassword: clientErrors.newPassword ?? apiErr?.fieldError('newPassword'),
    confirmPassword: clientErrors.confirmPassword,
  };
  const generalError =
    serverError && !(apiErr && (errors.currentPassword || errors.newPassword))
      ? errorMessage(serverError)
      : null;

  function set(key: keyof PasswordFormValues, value: string) {
    setValues((v) => ({ ...v, [key]: value }));
    setClientErrors((e) => ({ ...e, [key]: undefined }));
    if (mutation.isError || mutation.isSuccess) mutation.reset();
  }

  function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const next = validatePasswordForm(values);
    setClientErrors(next);
    if (Object.keys(next).length > 0) return;
    mutation.mutate(
      { currentPassword: values.currentPassword, newPassword: values.newPassword },
      { onSuccess: () => setValues(EMPTY_PASSWORD_FORM) },
    );
  }

  const type = visible ? 'text' : 'password';
  const disabled = mutation.isPending;

  return (
    <Card as="section" aria-labelledby="security-password" className={styles.card}>
      <CardHeader
        title={<span id="security-password">Parolni almashtirish</span>}
        subtitle={`Kamida ${PASSWORD_MIN_LENGTH} ta belgi. Boshqa qurilmalardagi sessiyalar yopiladi.`}
        actions={
          <Button
            size="sm"
            aria-pressed={visible}
            aria-controls="security-password-form"
            onClick={() => setVisible((v) => !v)}
          >
            {visible ? 'Parollarni yashirish' : "Parollarni ko'rsatish"}
          </Button>
        }
      />
      <form id="security-password-form" className={styles.form} noValidate onSubmit={onSubmit}>
        <Input
          label="Joriy parol"
          variant="form"
          type={type}
          autoComplete="current-password"
          value={values.currentPassword}
          onChange={(e) => set('currentPassword', e.target.value)}
          error={errors.currentPassword}
          disabled={disabled}
        />
        <Input
          label="Yangi parol"
          variant="form"
          type={type}
          autoComplete="new-password"
          value={values.newPassword}
          onChange={(e) => set('newPassword', e.target.value)}
          error={errors.newPassword}
          disabled={disabled}
        />
        <Input
          label="Yangi parolni takrorlang"
          variant="form"
          type={type}
          autoComplete="new-password"
          value={values.confirmPassword}
          onChange={(e) => set('confirmPassword', e.target.value)}
          error={errors.confirmPassword}
          disabled={disabled}
        />

        {generalError && (
          <p className={styles.formError} role="alert">
            {generalError}
          </p>
        )}
        <div className={styles.actions}>
          {mutation.isSuccess && (
            <span className={styles.success} role="status">
              Parol almashtirildi.
            </span>
          )}
          <Button type="submit" variant="primary" disabled={disabled}>
            {disabled ? 'Saqlanmoqda…' : 'Parolni almashtirish'}
          </Button>
        </div>
      </form>
    </Card>
  );
}
