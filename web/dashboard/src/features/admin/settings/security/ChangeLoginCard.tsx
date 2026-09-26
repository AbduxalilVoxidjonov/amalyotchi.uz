import { useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { errorMessage, isApiError } from '@/shared/api';
import { useAuth } from '@/shared/auth/useAuth';
import { Button, Card, CardHeader, Input } from '@/shared/ui';
import { loginAvailabilityKeys, useChangeLogin, useLoginAvailability } from '@/features/auth/hooks';
import { useDebouncedValue } from '../../shared/useDebouncedValue';
import styles from './AccountSecurity.module.css';

/** Yangi login yozilgach `login-available` so'rovigacha kutish (ms). */
export const LOGIN_CHECK_DEBOUNCE_MS = 400;

const LOGIN_TAKEN = 'Bu login allaqachon band.';
const LOGIN_IS_CURRENT = 'Bu sizning joriy loginingiz.';

type Availability =
  | { state: 'idle' }
  | { state: 'checking' }
  | { state: 'ok'; normalized: string }
  | { state: 'invalid'; reason: string }
  | { state: 'error' };

/**
 * Loginni almashtirish: joriy login (o'qish uchun) + yangi login (debounce bilan `login-available`)
 * + joriy parol → `POST /api/auth/change-login`. Muvaffaqiyatda auth store `user` va `me` keshi yangilanadi.
 */
export function ChangeLoginCard() {
  const { user } = useAuth();
  const currentLogin = user?.hemisId ?? null;
  const queryClient = useQueryClient();
  const mutation = useChangeLogin();

  const [newLogin, setNewLogin] = useState('');
  const [currentPassword, setCurrentPassword] = useState('');
  const [passwordRequired, setPasswordRequired] = useState(false);

  const trimmed = newLogin.trim();
  const debounced = useDebouncedValue(trimmed, LOGIN_CHECK_DEBOUNCE_MS);
  const isSelf = trimmed !== '' && trimmed === currentLogin;
  const settled = debounced === trimmed;
  const query = useLoginAvailability(debounced, settled && !isSelf);

  const availability: Availability = (() => {
    if (trimmed === '') return { state: 'idle' };
    if (isSelf) return { state: 'invalid', reason: LOGIN_IS_CURRENT };
    if (!settled || query.isFetching || query.isPending) return { state: 'checking' };
    if (query.isError) return { state: 'error' };
    const data = query.data;
    if (data.available && data.normalized === currentLogin)
      return { state: 'invalid', reason: LOGIN_IS_CURRENT };
    if (data.available) return { state: 'ok', normalized: data.normalized };
    return { state: 'invalid', reason: data.reason ?? "Bu loginni ishlatib bo'lmaydi." };
  })();

  const error = mutation.error;
  const isConflict = isApiError(error) && error.kind === 'conflict';
  const validationErr = isApiError(error) && error.kind === 'validation' ? error : null;
  const newLoginError = isConflict
    ? error.message || LOGIN_TAKEN
    : validationErr?.fieldError('newLogin');
  const passwordError =
    (passwordRequired ? 'Joriy parolni kiriting.' : undefined) ??
    validationErr?.fieldError('currentPassword');
  const generalError =
    error && !isConflict && !newLoginError && !passwordError ? errorMessage(error) : null;

  const blocked =
    availability.state === 'idle' ||
    availability.state === 'checking' ||
    availability.state === 'invalid';
  const canSave = !blocked && !newLoginError && !mutation.isPending;

  function resetFeedback() {
    if (mutation.isError || mutation.isSuccess) mutation.reset();
  }

  function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!canSave) return;
    if (!currentPassword) {
      setPasswordRequired(true);
      return;
    }
    const login = availability.state === 'ok' ? availability.normalized : trimmed;
    mutation.mutate(
      { newLogin: login, currentPassword },
      {
        onSuccess: () => {
          setNewLogin('');
          setCurrentPassword('');
        },
        onError: (err) => {
          // Server qayta tekshirdi va band dedi — holat ko'rsatkichi ham yangilansin.
          if (isApiError(err) && err.kind === 'conflict')
            void queryClient.invalidateQueries({ queryKey: loginAvailabilityKeys.check(login) });
        },
      },
    );
  }

  return (
    <Card as="section" aria-labelledby="security-login" className={styles.card}>
      <CardHeader
        title={<span id="security-login">Loginni almashtirish</span>}
        subtitle="Login — tizimga kirish identifikatori (HEMIS ID)."
      />
      <form className={styles.form} noValidate onSubmit={onSubmit}>
        <Input label="Joriy login" variant="readonly" mono readOnly value={currentLogin ?? '—'} />
        <Input
          label="Yangi login"
          variant="form"
          mono
          autoComplete="off"
          spellCheck={false}
          value={newLogin}
          onChange={(e) => {
            setNewLogin(e.target.value);
            resetFeedback();
          }}
          error={newLoginError}
          hint={<AvailabilityStatus availability={availability} input={trimmed} />}
          disabled={mutation.isPending}
        />
        <Input
          label="Joriy parol"
          variant="form"
          type="password"
          autoComplete="current-password"
          value={currentPassword}
          onChange={(e) => {
            setCurrentPassword(e.target.value);
            setPasswordRequired(false);
            resetFeedback();
          }}
          error={passwordError}
          disabled={mutation.isPending}
        />

        {generalError && (
          <p className={styles.formError} role="alert">
            {generalError}
          </p>
        )}
        <div className={styles.actions}>
          {mutation.isSuccess && (
            <span className={styles.success} role="status">
              Login almashtirildi. Keyingi kirishda yangi logindan foydalaning.
            </span>
          )}
          <Button type="submit" variant="primary" disabled={!canSave}>
            {mutation.isPending ? 'Saqlanmoqda…' : 'Saqlash'}
          </Button>
        </div>
      </form>
    </Card>
  );
}

function AvailabilityStatus({
  availability,
  input,
}: {
  availability: Availability;
  input: string;
}) {
  switch (availability.state) {
    case 'idle':
      return null;
    case 'checking':
      return (
        <span className={styles.status} data-tone="muted" aria-live="polite">
          Tekshirilmoqda…
        </span>
      );
    case 'ok':
      return (
        <span className={styles.status} data-tone="ok" aria-live="polite">
          ✓ Login bo'sh
          {availability.normalized !== input && (
            <>
              {' '}
              — <span className={styles.mono}>{availability.normalized}</span> sifatida saqlanadi
            </>
          )}
        </span>
      );
    case 'invalid':
      return (
        <span className={styles.status} data-tone="bad" aria-live="polite">
          ✗ {availability.reason}
        </span>
      );
    case 'error':
      return (
        <span className={styles.status} data-tone="muted" aria-live="polite">
          Loginni tekshirib bo'lmadi — saqlashda server qayta tekshiradi.
        </span>
      );
  }
}
