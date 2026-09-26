import { AppLink, Button, Eyebrow } from '@/shared/ui';
import { formatDate } from '@/shared/lib/format';
import { canMarkNow, describeToday } from '../status';
import type { TodayDto } from '../types';
import styles from './TodaySummary.module.css';

/**
 * Bosh ekran (bugungi kun paneli) — bugungi davomatning QISQA holati. Belgilash oqimi faqat QR sahifasida:
 * belgilash (yoki ketishni belgilash) hozir mumkin bo'lsa — "QR orqali belgilash" → `/qr`
 * (`AppLink`: Telegram rejimida `href`siz tugma).
 */
export function TodaySummary({ today }: { today: TodayDto }) {
  const { title, note } = describeToday(today);
  return (
    <section className={styles.box} aria-labelledby="today-summary-title">
      <Eyebrow as="div" spacing="wide" margin="none">
        Bugun · {formatDate(today.date)}
      </Eyebrow>
      <h3 id="today-summary-title" className={styles.title}>
        {title}
      </h3>
      <p className={styles.note}>{note}</p>
      {today.checkin.suspicious && (
        <p className={styles.warn} role="note">
          Belgilanish shubhali deb belgilandi — tyutor tekshiradi.
        </p>
      )}
      {canMarkNow(today) && (
        <Button asChild variant="primary" radius="md2" block className={styles.action}>
          <AppLink to="/qr">QR orqali belgilash</AppLink>
        </Button>
      )}
    </section>
  );
}
