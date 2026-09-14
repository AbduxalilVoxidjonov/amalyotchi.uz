import { Link } from 'react-router-dom';
import { Button, EmptyState } from '@/shared/ui';
import styles from './NotFoundPage.module.css';

export function NotFoundPage() {
  return (
    <main className={styles.main}>
      <EmptyState
        title="404 — Sahifa topilmadi"
        description="Bunday manzil yo'q yoki ko'chirilgan."
        action={
          <Button asChild>
            <Link to="/">Bosh sahifaga</Link>
          </Button>
        }
      />
    </main>
  );
}

export default NotFoundPage;
