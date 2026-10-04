import type { ReactNode } from 'react';
import { errorMessage } from '@/shared/api';
import { Button, EmptyState } from '@/shared/ui';

/** Yuklanmoqda holati — dizaynda skeleton yo'q ❓; oddiy matnli plain EmptyState. */
export function LoadingState({ label = 'Yuklanmoqda…' }: { label?: string }) {
  return <EmptyState tone="plain" title={label} role="status" aria-live="polite" />;
}

export interface ErrorStateProps {
  error: unknown;
  onRetry?: (() => void) | undefined;
}

/** Xato holati — `ApiError` xabari + "Qayta urinish". */
export function ErrorState({ error, onRetry }: ErrorStateProps) {
  return (
    <EmptyState
      role="alert"
      title="Ma'lumotni yuklab bo'lmadi"
      description={errorMessage(error)}
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

/** Umumiy loading/error/empty shakli: `<QueryState query={q} empty=...>{(data) => ...}</QueryState>`. */
export interface QueryStateProps<T> {
  status: 'pending' | 'error' | 'success';
  data: T | undefined;
  error: unknown;
  refetch: () => unknown;
  isEmpty?: ((data: T) => boolean) | undefined;
  empty?: ReactNode;
  children: (data: T) => ReactNode;
}

export function QueryState<T>({
  status,
  data,
  error,
  refetch,
  isEmpty,
  empty,
  children,
}: QueryStateProps<T>) {
  if (status === 'pending') return <LoadingState />;
  if (status === 'error' || data === undefined)
    return <ErrorState error={error} onRetry={() => void refetch()} />;
  if (isEmpty?.(data)) return <>{empty ?? <EmptyState title="Ma'lumot yo'q" />}</>;
  return <>{children(data)}</>;
}
