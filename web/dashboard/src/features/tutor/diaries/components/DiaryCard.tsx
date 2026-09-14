import { useState } from 'react';
import { Avatar, Badge, Button, Card, Chip, ChipRow, Textarea } from '@/shared/ui';
import { fmtDateOnly, fmtTime } from '../../format';
import {
  DIARY_SCORES,
  DIARY_STATUS_LABEL,
  isDiaryReviewable,
  type DiaryEntry,
  type DiaryReviewRequest,
  type DiaryScore,
} from '../types';
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
 * Backend: approve/score → Tasdiqlangan; rewrite → izoh majburiy; ko'rib chiqilgan → 409 (tugmalar o'chadi).
 */
export function DiaryCard({ entry, pending, error, onReview }: DiaryCardProps) {
  const s = DIARY_STATUS_LABEL[entry.status];
  const meta = `${entry.group} · ${fmtDateOnly(entry.date)} · ${fmtTime(entry.submittedAt)}`;
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
            <Chip key={f.url}>
              <a href={f.url} target="_blank" rel="noreferrer">
                {f.name}
              </a>
            </Chip>
          ))}
        </ChipRow>
      )}
      {entry.comment && (
        <p className={styles.tutorComment}>
          <span className={styles.learnedLabel}>Tyutor izohi: </span>
          {entry.comment}
        </p>
      )}
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
        <span className={styles.scores} role="group" aria-label={`${entry.studentName} balli`}>
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
    </Card>
  );
}
