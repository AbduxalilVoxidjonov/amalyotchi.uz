import { ChangeLoginCard } from './ChangeLoginCard';
import { ChangePasswordCard } from './ChangePasswordCard';
import styles from './AccountSecurity.module.css';

/** Admin · Sozlamalar · "Hisob xavfsizligi" — o'z parolini va loginini almashtirish. */
export function AccountSecurity() {
  return (
    <div className={styles.grid}>
      <ChangePasswordCard />
      <ChangeLoginCard />
    </div>
  );
}
