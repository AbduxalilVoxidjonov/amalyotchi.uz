import { useState, type FormEvent } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { errorMessage } from '@/shared/api/client';
import { homePathForRole, useAuth } from '@/shared/auth/useAuth';
import { Button, Checkbox, Input, Pill, PillGroup } from '@/shared/ui';
import { useLogin } from './hooks';
import styles from './LoginPage.module.css';
import { loginSchema, type LoginFormValues } from './schema';

/**
 * Login rol tugmalari (SPEC-SCREENS §1). ❓ Backend rolni credential'dan aniqlaydi — tanlov faqat
 * hint matnini o'zgartiradi. "Talaba" dashboard'da kirmaydi (TWA) — ko'rsatilmaydi.
 */
const ROLE_OPTIONS = [
  {
    id: 'tyutor',
    label: 'Tyutor',
    hint: "Tyutor hisobini admin yaratadi. Birinchi kirishda parol o'rnatiladi.",
  },
  {
    id: 'admin',
    label: 'Admin',
    hint: "Admin hisobi o'quv bo'limi tomonidan beriladi. Ikki faktorli tasdiq majburiy.",
  },
] as const;
type LoginRoleId = (typeof ROLE_OPTIONS)[number]['id'];

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
 * ❓ Dizaynda "HEMIS ID" — backend kontrakti telefon raqami (`phoneNumber`), shuning uchun label "Telefon raqami".
 */
export function LoginPage() {
  const { isAuthenticated, user } = useAuth();
  const location = useLocation();
  const login = useLogin();
  const [role, setRole] = useState<LoginRoleId>('tyutor');
  const [values, setValues] = useState<LoginFormValues>({ phoneNumber: '', password: '' });
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

  const activeRole = ROLE_OPTIONS.find((r) => r.id === role) ?? ROLE_OPTIONS[0];

  return (
    <div className={styles.root}>
      <aside className={styles.left}>
        <div>
          <div className={styles.logo}>Amaliyotchi</div>
          <div className={styles.logoSub}>TDIU · 2026-2027 o'quv yili</div>
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
          <p className={styles.subtitle}>
            Telefon raqami va parol orqali. Rolga qarab ish ekrani ochiladi.
          </p>

          <PillGroup className={styles.roles} stretch role="tablist" aria-label="Rol">
            {ROLE_OPTIONS.map((r) => (
              <Pill
                key={r.id}
                role="tab"
                shape="wide"
                active={role === r.id}
                onClick={() => setRole(r.id)}
              >
                {r.label}
              </Pill>
            ))}
          </PillGroup>
          <p className={styles.hint}>{activeRole.hint}</p>

          <Input
            id="phoneNumber"
            name="phoneNumber"
            type="tel"
            label="Telefon raqami"
            mono
            autoComplete="username"
            inputMode="tel"
            placeholder="+998 90 123 45 67"
            value={values.phoneNumber}
            onChange={(e) => setValues((v) => ({ ...v, phoneNumber: e.target.value }))}
            error={fieldErrors.phoneNumber}
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
              {errorMessage(login.error)}
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

          <p className={styles.note}>
            Xavfsizlik uchun bitta IP manzildan daqiqasiga 10 martadan ko'p kirish urinishi qabul
            qilinmaydi. Har bir urinish jurnalga yoziladi.
          </p>
        </form>
      </main>
    </div>
  );
}

export default LoginPage;
