import { Card, EmptyState, ErrorState, LoadingState } from '@/shared/ui';
import { errorMessage } from '@/shared/api/client';
import { useAuthStore } from '@/shared/auth/store';
import { DiaryEntryCard } from '@/features/diary/components/DiaryEntryCard';
import { DiaryForm } from '@/features/diary/components/DiaryForm';
import { useCreateDiaryEntry, useDiaryQuery } from '@/features/diary/hooks';
import styles from './pages.module.css';

/** SPEC-SCREENS §6 talaba varianti `kundaligim` — yangi yozuv + o'z yozuvlari. */
export function DiaryPage() {
  const diary = useDiaryQuery();
  const create = useCreateDiaryEntry();
  const studentName = useAuthStore((s) => s.user?.fullName) ?? 'Talaba';

  return (
    <div className={styles.stack}>
      <Card padded="lg">
        <DiaryForm
          title="Yangi yozuv"
          pending={create.isPending}
          error={create.error}
          onSubmit={(input) => create.mutateAsync(input)}
        />
      </Card>

      <section aria-labelledby="diary-list-title" className={styles.list}>
        <h2 id="diary-list-title" className={styles.sectionTitle}>
          Yozuvlarim{diary.data ? ` · ${diary.data.length}` : ''}
        </h2>
        {diary.isPending && <LoadingState height={200} />}
        {diary.isError && (
          <ErrorState
            description={errorMessage(diary.error)}
            onRetry={() => void diary.refetch()}
          />
        )}
        {diary.data && diary.data.length === 0 && (
          <EmptyState
            title="Hali yozuv yo'q"
            description="Birinchi kundalik yozuvini yuqoridagi forma orqali yuboring."
          />
        )}
        {diary.data?.map((entry) => (
          <DiaryEntryCard key={entry.id} entry={entry} studentName={studentName} />
        ))}
      </section>
    </div>
  );
}

export default DiaryPage;
