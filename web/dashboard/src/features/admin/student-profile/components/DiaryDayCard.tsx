import { useLayoutEffect, useRef, useState } from 'react';
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
  /**
   * `false` — biriktirilgan fayllar bu kartada ko'rsatilmaydi. Kun oynasida ular o'ngdagi
   * "Yuborilgan fayllar" ustunida (selfilar bilan birga, kattaroq) turadi.
   */
  showFiles?: boolean;
  /**
   * `true` — ixcham ko'rinish (kun oynasi): kichikroq padding/shrift, matn 4 qatorga qisqartiriladi
   * va sig'masa "To'liq ko'rish" tugmasi bilan ochiladi. Matn DOM'da to'liq qoladi.
   */
  compact?: boolean;
}

/**
 * Talaba profilidagi kun oynasi uchun kundalik kartasi: sarlavha (sana, holat, ball), matn,
 * "o'rganganim", **biriktirilgan fayllar joyida ochiq** (talaba daftardagi kundalikni rasmga olib
 * PDF qilib yuboradi — `AuthFileEmbed` uni token bilan yuklab, shu yerda ko'rsatadi), tyutor izohi
 * va baholash qatori. Kundaliklar sahifasidagi karta — `diaries/components/DiaryCard.tsx`.
 */
export function DiaryDayCard({
  entry,
  review,
  showFiles = true,
  compact = false,
}: DiaryDayCardProps) {
  const s = DIARY_STATUS_LABEL[entry.status];
  const textRef = useRef<HTMLParagraphElement>(null);
  const [expanded, setExpanded] = useState(false);
  // Qisqartirilgan matn haqiqatan sig'madimi — faqat shunda "To'liq ko'rish" tugmasi chiqadi.
  const [overflows, setOverflows] = useState(false);

  useLayoutEffect(() => {
    setExpanded(false);
    const el = textRef.current;
    setOverflows(compact && el !== null && el.scrollHeight > el.clientHeight + 1);
  }, [compact, entry.text]);

  return (
    <Card
      as="article"
      padded={!compact}
      className={compact ? styles.compact : undefined}
      aria-label={`Kundalik: ${fmtDateOnly(entry.date)}`}
    >
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

      <p
        ref={textRef}
        id={`diary-text-${entry.id}`}
        className={styles.text}
        data-clamped={(compact && !expanded) || undefined}
      >
        {entry.text}
      </p>
      {compact && (overflows || expanded) && (
        <button
          type="button"
          className={styles.more}
          aria-expanded={expanded}
          aria-controls={`diary-text-${entry.id}`}
          onClick={() => setExpanded((v) => !v)}
        >
          {expanded ? 'Qisqartirish' : "To'liq ko'rish"}
        </button>
      )}

      {entry.learned && (
        <p className={styles.note}>
          <span className={styles.noteLabel}>O'rganganim: </span>
          {entry.learned}
        </p>
      )}

      {showFiles && entry.files.map((f) => <AuthFileEmbed key={f.url} url={f.url} name={f.name} />)}

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
