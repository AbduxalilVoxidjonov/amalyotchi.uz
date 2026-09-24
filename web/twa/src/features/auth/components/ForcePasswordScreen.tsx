import { useAuthStore } from '@/shared/auth/store';
import { Button } from '@/shared/ui';
import { useLogout } from '../hooks';
import styles from './AuthScreen.module.css';
import { ChangePasswordForm } from './ChangePasswordForm';

/**
 * `mustChangePassword === true` — ilovaga o'tkazilmaydi: avval vaqtinchalik parol almashtiriladi.
 * Muvaffaqiyatda bayroq tushadi va RootLayout AppShell'ni ko'rsatadi.
 */
export function ForcePasswordScreen() {
  const user = useAuthStore((s) => s.user);
  const logout = useLogout();

  return (
    <main className={styles.screen}>
      <div className={styles.inner}>
        <div className={styles.brand}>
          <div className={styles.logo}>Amaliyotchi</div>
          <div className={styles.logoSub}>{user?.fullName ?? 'Talaba kabineti'}</div>
        </div>
        <section className={styles.card} aria-labelledby="force-password-title">
          <div className={styles.form}>
            <h1 id="force-password-title" className={styles.title}>
              Yangi parol o'rnating
            </h1>
            <p className={styles.subtitle}>
              Siz vaqtinchalik parol bilan kirdingiz. Davom etish uchun o'zingizga yangi parol
              o'rnating.
            </p>
            <ChangePasswordForm submitLabel="Parolni saqlash va davom etish" />
          </div>
        </section>
        <Button type="button" size="sm" onClick={() => logout.mutate()} disabled={logout.isPending}>
          Chiqish
        </Button>
      </div>
    </main>
  );
}
