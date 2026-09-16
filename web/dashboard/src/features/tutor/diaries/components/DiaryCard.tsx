import { AuthFileButton } from '@/shared/files';
import { Avatar, Badge, Card, ChipRow } from '@/shared/ui';
import { fmtDateOnly, fmtTime } from '../../format';
import { DIARY_STATUS_LABEL, type DiaryEntry, type DiaryReviewRequest } from '../types';
import { DiaryReviewControls } from './DiaryReviewControls';
import styles from './DiaryCard.module.css';

export interface DiaryCardProps {
  entry: DiaryEntry;
  pending: boolean;
  /** Xato matni (faqat shu karta uchun). */
  error?: string | undefined;
  onReview: (body: DiaryReviewRequest) => void;
}

/**
 * SPEC-SCREENS §6 — kundalik kartasi (tyutor varianti, tugmalar bilan).
 * Ko'rib chiqish qatori — umumiy `DiaryReviewControls` (talaba profilidagi kun oynasi ham shuni ishlatadi).
 */
export function DiaryCard({ entry, pending, error, onReview }: DiaryCardProps) {
  const s = DIARY_STATUS_LABEL[entry.status];
  const meta = `${entry.group} · ${fmtDateOnly(entry.date)} · ${fmtTime(entry.submittedAt)}`;

  return (
    <Card as="article" padded aria-label={`Kundalik: ${entry.studentName}`}>
      <header className={styles.head}>
        <div className={styles.person}>
          <Avatar name={entry.studentName} variant="card" />
          <div>
            <div className={styles.name}>{entry.studentName}</div>
            <div className={styles.meta}>{meta}</div>
          </div>
        </div>
        <Badge status={s.kind}>{s.label}</Badge>
      </header>
      <p className={styles.text}>{entry.text}</p>
      {entry.learned && (
        <p className={styles.learned}>
          <span className={styles.learnedLabel}>O'rganganim: </span>
          {entry.learned}
        </p>
      )}
      {entry.files.length > 0 && (
        <ChipRow className={styles.files}>
          {entry.files.map((f) => (
            <AuthFileButton key={f.url} url={f.url} name={f.name} />
          ))}
        </ChipRow>
      )}
      {entry.comment && (
        <p className={styles.tutorComment}>
          <span className={styles.learnedLabel}>Tyutor izohi: </span>
          {entry.comment}
        </p>
      )}
      <DiaryReviewControls
        entry={entry}
        pending={pending}
        error={error}
        scoreGroupLabel={`${entry.studentName} balli`}
        onReview={onReview}
      />
    </Card>
  );
}
