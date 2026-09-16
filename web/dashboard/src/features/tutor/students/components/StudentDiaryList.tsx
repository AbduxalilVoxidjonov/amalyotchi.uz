import { Badge, Card, Chip, ChipRow } from '@/shared/ui';
import { fmtDateOnly, fmtTime } from '../../format';
import { DIARY_STATUS_LABEL, type DiaryEntry, type DiaryFile } from '../../diaries/types';
import { PhotoPreview } from './PhotoPreview';
import styles from './StudentDiaryList.module.css';

const IMAGE_RE = /\.(png|jpe?g|webp|heic|heif|gif)$/i;

const isImage = (f: DiaryFile): boolean => IMAGE_RE.test(f.name);

/**
 * Kundalik kartasi — FAQAT O'QISH rejimi (talaba profili uchun).
 * `diaries/components/DiaryCard.tsx` tyutor amallari (tasdiqlash/ball/qaytarish) bilan keladi;
 * bu yerda o'sha amallar kerak emas, shuning uchun `diaries/` papkasi o'zgartirilmadi.
 */
function ReadOnlyDiaryCard({ entry }: { entry: DiaryEntry }) {
  const s = DIARY_STATUS_LABEL[entry.status];
  const photos = entry.files.filter(isImage);
  const docs = entry.files.filter((f) => !isImage(f));

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

      {photos.length > 0 && (
        <div className={styles.photos}>
          {photos.map((f) => (
            <PhotoPreview key={f.url} url={f.url} label={f.name} size="card" />
          ))}
        </div>
      )}

      {docs.length > 0 && (
        <ChipRow className={styles.files}>
          {docs.map((f) => (
            <Chip key={f.url}>
              <a href={f.url} target="_blank" rel="noreferrer">
                {f.name}
              </a>
            </Chip>
          ))}
        </ChipRow>
      )}

      {entry.comment && (
        <p className={styles.note}>
          <span className={styles.noteLabel}>Tyutor izohi: </span>
          {entry.comment}
        </p>
      )}
    </Card>
  );
}

/** Talabaning kundaliklari (sana bo'yicha kamayish tartibida). */
export function StudentDiaryList({ entries }: { entries: readonly DiaryEntry[] }) {
  return (
    <div className={styles.list}>
      {entries.map((entry) => (
        <ReadOnlyDiaryCard key={entry.id} entry={entry} />
      ))}
    </div>
  );
}
