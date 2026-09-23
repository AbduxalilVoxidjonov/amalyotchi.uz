import { Card, EmptyState, ErrorState, LoadingState } from '@/shared/ui';
import { errorMessage } from '@/shared/api/client';
import { useAuthStore } from '@/shared/auth/store';
import { DiaryEntryCard } from '@/features/diary/components/DiaryEntryCard';
import { DiaryForm } from '@/features/diary/components/DiaryForm';
import { useCreateDiaryEntry, useDiaryQuery } from '@/features/diary/hooks';
import { groupDiaryByPeriod, rewriteFilesFor } from '@/features/diary/types';
import { useTodayQuery } from '@/features/today/hooks';
import styles from './pages.module.css';

/** SPEC-SCREENS §6 talaba varianti `kundaligim` — yangi yozuv + o'z yozuvlari. */
export function DiaryPage() {
  const diary = useDiaryQuery();
  const create = useCreateDiaryEntry();
  // Kundalik sozlamalari (minChars/maxFiles/pdfRequired) — bosh ekran bilan bir manba.
  const today = useTodayQuery().data;
  const studentName = useAuthStore((s) => s.user?.fullName) ?? 'Talaba';
  // v3.5: tarix davr bo'yicha (kuzgi, bahorgi); davr bitta bo'lsa sarlavhasiz tekis ro'yxat.
  const groups = diary.data ? groupDiaryByPeriod(diary.data) : [];

  return (
    <div className={styles.stack}>
      <Card padded="lg">
        <DiaryForm
          title="Yangi yozuv"
          {...(today && {
            minChars: today.diary.minChars,
            maxFiles: today.diary.maxFiles,
            pdfRequired: today.diary.pdfRequired,
            existingFiles: rewriteFilesFor(diary.data, today.date),
          })}
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
        {groups.length === 1 &&
          groups[0]!.entries.map((entry) => (
            <DiaryEntryCard key={entry.id} entry={entry} studentName={studentName} />
          ))}
        {groups.length > 1 &&
          groups.map((g) => (
            <section
              key={g.periodId}
              aria-labelledby={`diary-period-${g.periodId}`}
              className={styles.list}
            >
              <h3 id={`diary-period-${g.periodId}`} className={styles.groupTitle}>
                {g.periodName ?? 'Nomsiz davr'} · {g.entries.length}
              </h3>
              {g.entries.map((entry) => (
                <DiaryEntryCard key={entry.id} entry={entry} studentName={studentName} />
              ))}
            </section>
          ))}
      </section>
    </div>
  );
}

export default DiaryPage;
