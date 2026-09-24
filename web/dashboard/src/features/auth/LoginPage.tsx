import { useState, type FormEvent } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { errorMessage } from '@/shared/api/client';
import { homePathForRole, useAuth } from '@/shared/auth/useAuth';
import { Button, Checkbox, Input } from '@/shared/ui';
import { getTwaUrl } from '@/shared/lib/env';
import { isStudentLoginRejected, useLogin } from './hooks';
import styles from './LoginPage.module.css';
import { loginSchema, type LoginFormValues } from './schema';

const PRINCIPLES = [
  {
    k: "Isbotsiz qayd yo'q",
    v: 'Har bir davomat yozuvi ortida vaqt belgisi, koordinata va masofa turadi.',
  },
  {
    k: "Har bir rol faqat o'z ko'lamini ko'radi",
    v: "Tyutor — o'z guruhlari, talaba — faqat o'zi.",
  },
  {
    k: "Har bir o'zgarish iz qoldiradi",
    v: "Kim, qachon, nimani o'zgartirdi — hammasi audit jurnalida.",
  },
];

/**
 * Kirish ekrani (SPEC-SCREENS §1 isLogin). Mantiq: zod validatsiya → useLogin → rolga qarab redirect.
 */
export function LoginPage() {
  const { isAuthenticated, user } = useAuth();
  const location = useLocation();
  const login = useLogin();
  const [values, setValues] = useState<LoginFormValues>({ hemisId: '', password: '' });
  // ❓ "Bu qurilmada eslab qolish" — hozircha faqat UI; refresh token har doim localStorage'da (store).
  const [remember, setRemember] = useState(true);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<keyof LoginFormValues, string>>>(
    {},
  );

  if (isAuthenticated && user) {
    const from = (location.state as { from?: string } | null)?.from;
    return <Navigate to={from && from !== '/login' ? from : homePathForRole(user.role)} replace />;
  }

  function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const parsed = loginSchema.safeParse(values);
    if (!parsed.success) {
      const next: Partial<Record<keyof LoginFormValues, string>> = {};
      for (const issue of parsed.error.issues) {
        const key = issue.path[0] as keyof LoginFormValues | undefined;
        if (key && !next[key]) next[key] = issue.message;
      }
      setFieldErrors(next);
      return;
    }
    setFieldErrors({});
    login.mutate(parsed.data);
  }

  return (
    <div className={styles.root}>
      <aside className={styles.left}>
        <div>
          <div className={styles.logo}>Amaliyotchi</div>
          <div className={styles.logoSub}>2026-2027 o'quv yili</div>
        </div>
        <p className={styles.hero}>
          Talabalar amaliyotini geo-lokatsiya orqali nazorat qiluvchi platforma
        </p>
        <dl className={styles.principles}>
          {PRINCIPLES.map((p) => (
            <div key={p.k} className={styles.principle}>
              <dt className={styles.principleK}>{p.k}</dt>
              <dd className={styles.principleV}>{p.v}</dd>
            </div>
          ))}
        </dl>
        <p className={styles.footnote}>
          Geolokatsiya ma'lumotlari faqat davomat nazorati uchun yig'iladi va talabaning yozma
          roziligi asosida saqlanadi.
        </p>
      </aside>

      <main className={styles.right}>
        <form
          className={styles.form}
          onSubmit={onSubmit}
          noValidate
          aria-describedby={login.isError ? 'login-error' : undefined}
        >
          <h1 className={styles.h1}>Tizimga kirish</h1>
          <p className={styles.subtitle}>HEMIS ID va parol orqali.</p>

          <Input
            id="hemisId"
            name="hemisId"
            type="text"
            label="HEMIS ID"
            mono
            autoComplete="username"
            inputMode="numeric"
            placeholder="123456789012"
            maxLength={20}
            value={values.hemisId}
            onChange={(e) => setValues((v) => ({ ...v, hemisId: e.target.value }))}
            error={fieldErrors.hemisId}
            wrapperClassName={styles.fieldId}
          />

          <Input
            id="password"
            name="password"
            type="password"
            label="Parol"
            labelEnd={
              // ❓ Parolni tiklash marshruti yo'q (SPEC-NAV 4.6)
              <a
                className={styles.forgot}
                href="#reset"
                onClick={(e) => e.preventDefault()}
                title="Tez orada"
              >
                Parolni tiklash
              </a>
            }
            autoComplete="current-password"
            placeholder="••••••••"
            value={values.password}
            onChange={(e) => setValues((v) => ({ ...v, password: e.target.value }))}
            error={fieldErrors.password}
            wrapperClassName={styles.fieldPassword}
          />

          <Checkbox
            label="Bu qurilmada eslab qolish"
            checked={remember}
            onChange={(e) => setRemember(e.target.checked)}
            wrapperClassName={styles.remember}
          />

          {login.isError && (
            <p id="login-error" role="alert" className={styles.serverError}>
              {isStudentLoginRejected(login.error) ? (
                <StudentAppNotice />
              ) : (
                errorMessage(login.error)
              )}
            </p>
          )}

          <Button
            type="submit"
            variant="primary"
            size="lg"
            block
            className={styles.submit}
            disabled={login.isPending}
          >
            {login.isPending ? 'Kirilmoqda…' : 'Kirish'}
          </Button>
        </form>
      </main>
    </div>
  );
}

/** Talaba dashboard'ga kirmaydi — TWA havolasi (`VITE_TWA_URL`) yoki Telegram bot. */
function StudentAppNotice() {
  const twaUrl = getTwaUrl();
  if (!twaUrl) return <>Talabalar Telegram bot orqali kiradi.</>;
  return (
    <>
      Talabalar uchun alohida ilova:{' '}
      <a className={styles.studentAppLink} href={twaUrl}>
        {twaUrl}
      </a>
    </>
  );
}

export default LoginPage;
