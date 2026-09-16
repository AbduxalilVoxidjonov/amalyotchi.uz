import { Card, ErrorState, LoadingState } from '@/shared/ui';
import { errorMessage } from '@/shared/api/client';
import { CheckinCard } from '@/features/today/components/CheckinCard';
import { PlaceSummary } from '@/features/today/components/PlaceSummary';
import { useCheckinFlow, useTodayQuery } from '@/features/today/hooks';
import { DiaryForm } from '@/features/diary/components/DiaryForm';
import { useCreateDiaryEntry } from '@/features/diary/hooks';
import styles from './pages.module.css';

/** SPEC-SCREENS §8 `isTalaba` — Bosh ekran: check-in + bugungi kundalik + amaliyot joyi qisqacha. */
export function HomePage() {
  const today = useTodayQuery();
  const checkinFlow = useCheckinFlow();
  const createDiary = useCreateDiaryEntry();

  if (today.isPending) return <LoadingState height={320} />;
  if (today.isError) {
    return (
      <ErrorState description={errorMessage(today.error)} onRetry={() => void today.refetch()} />
    );
  }
  const data = today.data;

  return (
    <div className={styles.stack}>
      <CheckinCard today={data} flow={checkinFlow} />
      <Card padded="lg">
        <DiaryForm
          title="Bugungi kundalik"
          minChars={data.diary.minChars}
          maxFiles={data.diary.maxFiles}
          submittedToday={data.diary.submittedToday}
          pending={createDiary.isPending}
          error={createDiary.error}
          onSubmit={(input) => createDiary.mutateAsync(input)}
        />
        <PlaceSummary place={data.place} />
      </Card>
    </div>
  );
}

export default HomePage;
