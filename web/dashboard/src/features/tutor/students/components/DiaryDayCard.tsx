import { Badge, Card } from '@/shared/ui';
import { AuthFileEmbed } from '@/shared/files';
import { fmtDateOnly, fmtTime } from '../../format';
import { DiaryReviewControls } from '../../diaries/components/DiaryReviewControls';
import {
  DIARY_STATUS_LABEL,
  type DiaryEntry,
  type DiaryReviewRequest,
} from '../../diaries/types';
import styles from './DiaryDayCard.module.css';

export interface DiaryDayCardProps {
  entry: DiaryEntry;
  /**
   * Berilsa — karta ostida baholash qatori chiqadi (Tasdiqlash · Ball · Izoh · Qayta yozish).
   * Kun oynasida tyutor ham, admin ham shu orqali baho qo'yadi.
   */
  review?:
    | {
        pending: boolean;
        error?: string | undefined;
        onReview: (body: DiaryReviewRequest) => void;
      }
    | undefined;
}

/**
 * Talaba profilidagi kun oynasi uchun kundalik kartasi: sarlavha (sana, holat, ball), matn,
 * "o'rganganim", **biriktirilgan fayllar joyida ochiq** (talaba daftardagi kundalikni rasmga olib
 * PDF qilib yuboradi — `AuthFileEmbed` uni token bilan yuklab, shu yerda ko'rsatadi), tyutor izohi
 * va baholash qatori. Kundaliklar sahifasidagi karta — `diaries/components/DiaryCard.tsx`.
 */
export function DiaryDayCard({ entry, review }: DiaryDayCardProps) {
  const s = DIARY_STATUS_LABEL[entry.status];

  return (
    <Card as="article" padded aria-label={`Kundalik: ${fmtDateOnly(entry.date)}`}>
      <header className={styles.head}>
        <div>
          <div className={styles.date}>{fmtDateOnly(entry.date)}</div>
          <div className={styles.meta}>
            {entry.group} · yuborilgan {fmtTime(entry.submittedAt)}
          </div>
        </div>
        <div className={styles.headRight}>
          {entry.score !== null && (
            <span className={styles.score} aria-label="Ball">
              {entry.score}
            </span>
          )}
          <Badge status={s.kind}>{s.label}</Badge>
        </div>
      </header>

      <p className={styles.text}>{entry.text}</p>

      {entry.learned && (
        <p className={styles.note}>
          <span className={styles.noteLabel}>O'rganganim: </span>
          {entry.learned}
        </p>
      )}

      {entry.files.map((f) => (
        <AuthFileEmbed key={f.url} url={f.url} name={f.name} />
      ))}

      {entry.comment && (
        <p className={styles.note}>
          <span className={styles.noteLabel}>Tyutor izohi: </span>
          {entry.comment}
        </p>
      )}

      {review && (
        <DiaryReviewControls
          entry={entry}
          pending={review.pending}
          error={review.error}
          scoreGroupLabel={`${fmtDateOnly(entry.date)} balli`}
          onReview={review.onReview}
        />
      )}
    </Card>
  );
}
