import { Eyebrow } from '@/shared/ui';
import { AuthFileEmbed } from '@/shared/files';
import type { DiaryFile } from '../../diaries/types';
import { PhotoPreview } from './PhotoPreview';
import styles from './DayFilesPanel.module.css';

/** Check-in/check-out selfisi — token bilan yuklanadigan rasm. */
export interface DaySelfie {
  /** `/api/files/{id}` */
  url: string;
  /** "Kirish selfisi" · "Chiqish selfisi". */
  title: string;
  /** Belgilanish vaqti ("09:05"). */
  note: string;
  /** a11y yorlig'i: "07.09.2026 check-in rasmi". */
  label: string;
}

export interface DayFilesPanelProps {
  selfies: readonly DaySelfie[];
  /** O'sha kunga yozilgan kundalikka biriktirilgan fayllar (rasm/PDF). */
  attachments: readonly DiaryFile[];
  className?: string | undefined;
}

/**
 * Kun oynasining O'NG ustuni: talaba o'sha kuni yuborgan hamma fayl bir joyda va kattaroq —
 * check-in/check-out selfisi va kundalikka biriktirilgan fayllar (PDF/rasm joyida ochiq).
 * Qolgan ma'lumot (vaqt, masofa, xarita, kundalik matni, baholash) chap ustunda turadi.
 */
export function DayFilesPanel({ selfies, attachments, className }: DayFilesPanelProps) {
  const total = selfies.length + attachments.length;

  return (
    <section className={className} aria-label="Yuborilgan fayllar">
      <Eyebrow margin="none">
        Yuborilgan fayllar
        {total > 0 && <span className={styles.count}> · {total} ta</span>}
      </Eyebrow>

      {total === 0 ? (
        <p className={styles.empty}>
          Bu kunga fayl yuborilmagan — na selfi, na kundalik fayli.
        </p>
      ) : (
        <div className={styles.list}>
          {selfies.map((selfie) => (
            <figure key={selfie.url} className={styles.item}>
              <figcaption className={styles.caption}>
                <span className={styles.title}>{selfie.title}</span>
                <span className={styles.note}>{selfie.note}</span>
              </figcaption>
              <PhotoPreview url={selfie.url} label={selfie.label} size="wide" />
            </figure>
          ))}

          {attachments.length > 0 && (
            <div className={styles.attachments}>
              <Eyebrow margin="none">Kundalik fayllari</Eyebrow>
              {attachments.map((file) => (
                <AuthFileEmbed key={file.url} url={file.url} name={file.name} size="lg" />
              ))}
            </div>
          )}
        </div>
      )}
    </section>
  );
}
