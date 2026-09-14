import { EmptyState } from '@/shared/ui';
import { QueryState } from '../components/QueryState';
import { mutationErrorMessage } from '../errors';
import { DiaryCard } from './components/DiaryCard';
import { useDiariesQuery, useDiaryReview } from './hooks';
import styles from './DiariesPage.module.css';

/** Tyutor · Kundalik hisobotlar (SPEC-SCREENS §6, tyutor varianti) — container. */
export function DiariesPage() {
  const query = useDiariesQuery();
  const review = useDiaryReview();
  const pendingId = review.isPending ? review.variables?.id : undefined;
  const errorId = review.isError ? review.variables?.id : undefined;

  return (
    <QueryState
      status={query.status}
      data={query.data}
      error={query.error}
      refetch={query.refetch}
      isEmpty={(d) => d.length === 0}
      empty={<EmptyState title="Tekshiriladigan kundaliklar yo'q" />}
    >
      {(entries) => (
        <div className={styles.list}>
          {entries.map((e) => (
            <DiaryCard
              key={e.id}
              entry={e}
              pending={pendingId === e.id}
              error={errorId === e.id ? mutationErrorMessage(review.error) : undefined}
              onReview={(body) => review.mutate({ id: e.id, body })}
            />
          ))}
        </div>
      )}
    </QueryState>
  );
}

export default DiariesPage;
