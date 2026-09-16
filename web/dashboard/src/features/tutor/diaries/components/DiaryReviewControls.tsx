import { useState } from 'react';
import { Button, Textarea } from '@/shared/ui';
import {
  DIARY_SCORES,
  isDiaryReviewable,
  type DiaryEntry,
  type DiaryReviewRequest,
  type DiaryScore,
} from '../types';
import styles from './DiaryReviewControls.module.css';

export interface DiaryReviewControlsProps {
  entry: DiaryEntry;
  pending: boolean;
  /** So'rov xatosi (faqat shu yozuv uchun). */
  error?: string | undefined;
  /** Ball tugmalari guruhining a11y nomi ("Aliyev Akmal balli" / "12.10.2026 balli"). */
  scoreGroupLabel: string;
  onReview: (body: DiaryReviewRequest) => void;
}

/**
 * Kundalikni ko'rib chiqish boshqaruvi: Tasdiqlash · Ball 1–5 · Izoh · Qayta yozishga qaytarish.
 * Ikki joyda ishlatiladi — tyutorning "Kundaliklar" sahifasidagi karta (`DiaryCard`) va
 * talaba profilidagi kun oynasi (`students/components/DiaryDayCard`).
 * Backend: approve/score → Tasdiqlangan; rewrite → izoh majburiy; ko'rib chiqilgan → 409 (tugmalar o'chadi).
 */
export function DiaryReviewControls({
  entry,
  pending,
  error,
  scoreGroupLabel,
  onReview,
}: DiaryReviewControlsProps) {
  const reviewable = isDiaryReviewable(entry);
  const locked = pending || !reviewable;

  const [commentOpen, setCommentOpen] = useState(false);
  const [comment, setComment] = useState('');
  const [localError, setLocalError] = useState<string | undefined>(undefined);
  const trimmed = comment.trim();
  const withComment = (body: DiaryReviewRequest): DiaryReviewRequest =>
    trimmed ? { ...body, comment: trimmed } : body;

  const approve = () => onReview(withComment({ action: 'approve' }));
  const score = (n: DiaryScore) => onReview(withComment({ action: 'score', score: n }));
  const rewrite = () => {
    if (!trimmed) {
      setCommentOpen(true);
      setLocalError('Qayta yozish sababi (izoh) majburiy.');
      return;
    }
    setLocalError(undefined);
    onReview({ action: 'rewrite', comment: trimmed });
  };

  const shownError = localError ?? error;

  return (
    <>
      {commentOpen && reviewable && (
        <Textarea
          id={`diary-comment-${entry.id}`}
          variant="form"
          wrapperClassName={styles.commentField}
          label="Izoh"
          rows={2}
          value={comment}
          disabled={pending}
          error={localError}
          onChange={(e) => {
            setComment(e.target.value);
            if (localError) setLocalError(undefined);
          }}
        />
      )}
      <footer className={styles.footer}>
        <Button variant="primary" size="sm" disabled={locked} onClick={approve}>
          Tasdiqlash
        </Button>
        <span className={styles.scoreLabel}>Ball</span>
        <span className={styles.scores} role="group" aria-label={scoreGroupLabel}>
          {DIARY_SCORES.map((n) => (
            <Button
              key={n}
              variant="score"
              aria-pressed={entry.score === n}
              disabled={locked}
              onClick={() => score(n)}
            >
              {n}
            </Button>
          ))}
        </span>
        <Button
          size="sm"
          disabled={locked}
          aria-expanded={commentOpen}
          onClick={() => setCommentOpen((v) => !v)}
        >
          Izoh
        </Button>
        <Button size="sm" disabled={locked} onClick={rewrite}>
          Qayta yozishga qaytarish
        </Button>
        {shownError && !localError && (
          <span className={styles.error} role="alert">
            {shownError}
          </span>
        )}
      </footer>
    </>
  );
}
