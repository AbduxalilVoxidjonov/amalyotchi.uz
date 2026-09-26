import { Card, EmptyState, ErrorState, LoadingState } from '@/shared/ui';
import { errorMessage } from '@/shared/api/client';
import { useAuthStore } from '@/shared/auth/store';
import {
  DiaryBlockedCard,
  type DiaryBlockedPhase,
} from '@/features/diary/components/DiaryBlockedCard';
import { DiaryEntryCard } from '@/features/diary/components/DiaryEntryCard';
import { DiaryForm } from '@/features/diary/components/DiaryForm';
import { useCreateDiaryEntry, useDiaryQuery } from '@/features/diary/hooks';
import { groupDiaryByPeriod, rewriteFilesFor } from '@/features/diary/types';
import { periodPhase } from '@/features/period/types';
import { useTodayQuery } from '@/features/today/hooks';
import type { TodayDto } from '@/features/today/types';
import styles from './pages.module.css';

/**
 * Forma o'rniga ko'rsatiladigan blok holati; `null` — forma ko'rinadi.
 * Server `canWriteDiary=false` ni bugungi yozuv allaqachon yuborilganda ham qaytaradi — faol davrda bu holat
 * avvalgidek formada (409 xabari) qoladi; blok faqat davr sababli (boshlanmagan / yakunlangan / davr yo'q).
 */
function diaryBlock(today: TodayDto): DiaryBlockedPhase | null {
  if (today.canWriteDiary !== false) return null;
  if (!today.period) return 'none';
  const phase = periodPhase(today.period, today.date);
  if (phase === 'upcoming') return 'upcoming';
  if (phase === 'ended') return 'ended';
  // Davr mijoz hisobida davom etmoqda: bugungisi yuborilgan → forma (avvalgidek); aks holda server sababi.
  if (today.diary.submittedToday) return null;
  return /boshlanmagan/i.test(today.diaryBlockedReason ?? '') ? 'upcoming' : 'ended';
}

/**
 * SPEC-SCREENS §6 talaba varianti `kundaligim` — yangi yozuv + o'z yozuvlari.
 * `today.canWriteDiary === false` (davr yakunlangan / boshlanmagan) — forma o'rniga ma'lumot kartasi,
 * yozuvlar o'qish uchun qoladi. Maydon kelmasa (eski backend) — forma ko'rinadi.
 */
export function DiaryPage() {
  const diary = useDiaryQuery();
  const create = useCreateDiaryEntry();
  // Kundalik sozlamalari (minChars/maxFiles/pdfRequired) — bosh ekran bilan bir manba.
  const todayQuery = useTodayQuery();
  const today = todayQuery.data;
  const studentName = useAuthStore((s) => s.user?.fullName) ?? 'Talaba';
  // v3.5: tarix davr bo'yicha (kuzgi, bahorgi); davr bitta bo'lsa sarlavhasiz tekis ro'yxat.
  const groups = diary.data ? groupDiaryByPeriod(diary.data) : [];
  // today yuklanguncha forma ko'rsatilmaydi ("miltillab" yo'qolmasin); xato bo'lsa — eski xatti-harakat.
  const block = today ? diaryBlock(today) : null;
  const blocked = block !== null;

  return (
    <div className={styles.stack}>
      {todayQuery.isPending ? (
        <LoadingState label="Kundalik formasi yuklanmoqda…" height={260} />
      ) : today && block ? (
        <DiaryBlockedCard
          phase={block}
          reason={today.diaryBlockedReason ?? null}
          hasEntries={(diary.data?.length ?? 0) > 0}
        />
      ) : (
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
      )}

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
            description={
              blocked
                ? 'Bu amaliyot davrida kundalik yozuvlari yuborilmagan.'
                : 'Birinchi kundalik yozuvini yuqoridagi forma orqali yuboring.'
            }
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
