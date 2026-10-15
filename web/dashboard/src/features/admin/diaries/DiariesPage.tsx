import { useState } from 'react';
import { Button, EmptyState } from '@/shared/ui';
import { QueryState } from '../components/QueryState';
import { mutationErrorMessage } from '../shared/errors';
import { DiaryCard } from './components/DiaryCard';
import { DIARIES_PAGE_SIZE, useDiariesQuery, useDiaryReview } from './hooks';
import styles from './DiariesPage.module.css';

/** Admin · Kundalik hisobotlar (SPEC-SCREENS §6, baholash tugmalari bilan) — container. */
export function DiariesPage() {
  const [limit, setLimit] = useState(DIARIES_PAGE_SIZE);
  const query = useDiariesQuery(limit);
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
          {entries.length >= limit && (
            <div className={styles.more}>
              <Button
                disabled={query.isPlaceholderData}
                onClick={() => setLimit((n) => n + DIARIES_PAGE_SIZE)}
              >
                {query.isPlaceholderData ? 'Yuklanmoqda…' : "Ko'proq ko'rsatish"}
              </Button>
            </div>
          )}
        </div>
      )}
    </QueryState>
  );
}

export default DiariesPage;
