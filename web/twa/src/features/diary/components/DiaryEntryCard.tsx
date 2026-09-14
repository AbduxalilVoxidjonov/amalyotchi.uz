import { Avatar, Badge, Card, Chip, ChipRow } from '@/shared/ui';
import { formatDate, formatTime } from '@/shared/lib/format';
import { DIARY_STATUS, type DiaryEntryDto } from '../types';
import styles from './DiaryEntryCard.module.css';

export interface DiaryEntryCardProps {
  entry: DiaryEntryDto;
  /** Talaba ismi (auth store'dan). */
  studentName: string;
}

/**
 * SPEC-SCREENS §6 article — talaba varianti: tyutor tugmalari (Tasdiqlash/Ball/Izoh) YO'Q ❓,
 * o'rniga tyutor bergan ball va izoh ko'rsatiladi.
 */
export function DiaryEntryCard({ entry, studentName }: DiaryEntryCardProps) {
  const status = DIARY_STATUS[entry.status];
  return (
    <Card as="article" padded aria-label={`Kundalik · ${formatDate(entry.date)}`}>
      <div className={styles.head}>
        <div className={styles.person}>
          <Avatar name={studentName} variant="card" />
          <div className={styles.personText}>
            <div className={styles.name}>{studentName}</div>
            <div className={styles.meta}>
              {formatDate(entry.date)} · {formatTime(entry.submittedAt)}
            </div>
          </div>
        </div>
        <Badge status={status.kind}>{status.label}</Badge>
      </div>

      <p className={styles.text}>{entry.text}</p>
      {entry.learned && (
        <p className={styles.learned}>
          <span className={styles.learnedLabel}>O'rganilgan yangilik: </span>
          {entry.learned}
        </p>
      )}

      {entry.files.length > 0 && (
        <ChipRow className={styles.files}>
          {entry.files.map((f) => (
            <Chip key={f.id}>
              <a href={f.url} target="_blank" rel="noopener noreferrer">
                {f.name}
              </a>
            </Chip>
          ))}
        </ChipRow>
      )}

      {(entry.score !== null || entry.comment) && (
        <footer className={styles.foot}>
          {entry.score !== null && (
            <span className={styles.score}>
              Ball <b>{entry.score}</b>
            </span>
          )}
          {entry.comment && <span className={styles.comment}>Tyutor izohi: {entry.comment}</span>}
        </footer>
      )}
    </Card>
  );
}
