import { useState, type FormEvent } from 'react';
import { isApiError } from '@/shared/api/client';
import { Button, Input } from '@/shared/ui';
import { loginErrorMessage, useWebLogin } from '../hooks';
import styles from './AuthScreen.module.css';

interface LoginValues {
  hemisId: string;
  password: string;
}

type LoginErrors = Partial<Record<keyof LoginValues, string>>;

/** Backend `LoginCommandValidator` bilan mos: HEMIS ID — 5–20 raqam. */
function validate(values: LoginValues): LoginErrors {
  const errors: LoginErrors = {};
  const hemisId = values.hemisId.trim();
  if (!hemisId) errors.hemisId = 'HEMIS ID ni kiriting.';
  else if (!/^\d{5,20}$/.test(hemisId)) {
    errors.hemisId = "HEMIS ID faqat raqamlardan iborat bo'lishi kerak (5–20 ta).";
  }
  if (!values.password) errors.password = 'Parolni kiriting.';
  return errors;
}

/**
 * Web-login (Telegram tashqarisida): HEMIS ID + parol → POST /api/auth/login.
 * Muvaffaqiyatda store 'authenticated' bo'ladi va RootLayout o'sha manzildagi sahifani ochadi.
 */
export function LoginScreen({ loggedOut = false }: { loggedOut?: boolean }) {
  const login = useWebLogin();
  const [values, setValues] = useState<LoginValues>({ hemisId: '', password: '' });
  const [errors, setErrors] = useState<LoginErrors>({});
  // Backend 400 `errors.HemisId` / `errors.Password` (PascalCase) — maydon ostida.
  const apiError = isApiError(login.error) ? login.error : null;
  const serverHemis = apiError?.fieldError('hemisId');
  const serverPassword = apiError?.fieldError('password');
  const showGeneral = login.isError && !serverHemis && !serverPassword;

  function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const next = validate(values);
    setErrors(next);
    if (Object.keys(next).length > 0) return;
    login.mutate({ hemisId: values.hemisId.trim(), password: values.password });
  }

  return (
    <main className={styles.screen}>
      <div className={styles.inner}>
        <div className={styles.brand}>
          <div className={styles.logo}>Amaliyotchi</div>
          <div className={styles.logoSub}>Talaba kabineti</div>
        </div>

        <form className={`${styles.card} ${styles.form}`} onSubmit={onSubmit} noValidate>
          <h1 className={styles.title}>Tizimga kirish</h1>
          <p className={styles.subtitle}>HEMIS ID va parol orqali kiring.</p>

          {loggedOut && !login.isError && (
            <p className={styles.notice} role="status">
              Hisobdan chiqdingiz.
            </p>
          )}

          <Input
            name="hemisId"
            label="HEMIS ID"
            mono
            autoComplete="username"
            inputMode="numeric"
            placeholder="123456789012"
            maxLength={20}
            value={values.hemisId}
            onChange={(e) => setValues((v) => ({ ...v, hemisId: e.target.value }))}
            error={errors.hemisId ?? serverHemis}
          />
          <Input
            name="password"
            type="password"
            label="Parol"
            autoComplete="current-password"
            placeholder="••••••••"
            value={values.password}
            onChange={(e) => setValues((v) => ({ ...v, password: e.target.value }))}
            error={errors.password ?? serverPassword}
          />

          {showGeneral && (
            <p role="alert" className={styles.serverError}>
              {loginErrorMessage(login.error)}
            </p>
          )}

          <Button type="submit" variant="primary" size="lg" block disabled={login.isPending}>
            {login.isPending ? 'Kirilmoqda…' : 'Kirish'}
          </Button>
        </form>

        <p className={styles.note}>
          Parolni tyutoringizdan oling. Bot tayyor bo'lgach ilovaga Telegram orqali ham kira olasiz.
        </p>
      </div>
    </main>
  );
}
