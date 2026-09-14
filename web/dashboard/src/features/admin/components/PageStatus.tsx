import { isApiError } from '@/shared/api';
import { Button, EmptyState } from '@/shared/ui';
import styles from './PageStatus.module.css';

/** ❓ Dizaynda loading holati yo'q — oddiy matn (role=status). */
export function LoadingState({ label = 'Yuklanmoqda…' }: { label?: string }) {
  return (
    <div className={styles.loading} role="status" aria-live="polite">
      {label}
    </div>
  );
}

export interface ErrorStateProps {
  error: unknown;
  onRetry?: () => void;
  /** Jadval ichida (chegarasiz) ko'rsatish. */
  inline?: boolean;
}

/** `ApiError` bo'lsa server xabari, aks holda umumiy matn + "Qayta urinish". */
export function ErrorState({ error, onRetry, inline = false }: ErrorStateProps) {
  const message = isApiError(error) ? error.message : 'Kutilmagan xatolik yuz berdi.';
  return (
    <EmptyState
      role="alert"
      tone="plain"
      className={inline ? styles.inline : undefined}
      title="Ma'lumotni yuklab bo'lmadi"
      description={message}
      action={
        onRetry && (
          <Button size="sm" onClick={onRetry}>
            Qayta urinish
          </Button>
        )
      }
    />
  );
}
