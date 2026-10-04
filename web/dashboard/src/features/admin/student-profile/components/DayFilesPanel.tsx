import { useEffect, useState } from 'react';
import { Modal } from '@/shared/ui';
import { useAuthFile } from '@/shared/files';
import type { DiaryFile } from '../../diaries/types';
import styles from './DayFilesPanel.module.css';

export interface DayFilesPanelProps {
  /** O'sha kunga yozilgan kundalikka biriktirilgan fayllar (rasm/PDF). */
  attachments: readonly DiaryFile[];
  className?: string | undefined;
}

/**
 * Kun oynasidagi kundalik fayllari — ixcham ro'yxat (har fayl bir qator): nomi (havola — yangi
 * oynada to'liq ochiladi), "Ko'rish" (PDF/rasm shu yerda, modalda), "Yangi oynada ochish" va
 * "Yuklab olish". Fayl token bilan oldindan yuklanadi (`/api/files/{id}` Bearer talab qiladi).
 * Selfilar bu yerda emas — ular kirish/chiqish bloklarida kichik thumbnail sifatida turadi.
 */
export function DayFilesPanel({ attachments, className }: DayFilesPanelProps) {
  return (
    <section className={className} aria-label="Kundalik fayllari">
      {attachments.length === 0 ? (
        <p className={styles.empty}>Kundalikka fayl biriktirilmagan.</p>
      ) : (
        <>
          <h4 className={styles.heading}>
            Kundalik fayllari <span className={styles.count}>· {attachments.length} ta</span>
          </h4>
          <ul className={styles.list}>
            {attachments.map((file) => (
              <DayFileRow key={file.url} url={file.url} name={file.name} />
            ))}
          </ul>
        </>
      )}
    </section>
  );
}

function DayFileRow({ url, name }: { url: string; name: string }) {
  const file = useAuthFile(url);
  const { load } = file;
  const [open, setOpen] = useState(false);

  useEffect(() => {
    void load();
  }, [load]);

  const isPdf = file.contentType === 'application/pdf';
  const isImage = file.contentType?.startsWith('image/') ?? false;
  const canPreview = file.objectUrl !== null && (isPdf || isImage);

  return (
    <li className={styles.row}>
      {file.objectUrl ? (
        <a
          className={styles.name}
          href={file.objectUrl}
          target="_blank"
          rel="noreferrer"
          title={`${name} — yangi oynada ochish`}
        >
          {name}
        </a>
      ) : (
        <span className={styles.name}>{name}</span>
      )}

      <span className={styles.actions}>
        {file.status === 'loading' && (
          <span className={styles.muted} role="status">
            Yuklanmoqda…
          </span>
        )}
        {file.status === 'error' && (
          <span className={styles.error} role="alert">
            {file.error}
          </span>
        )}
        {canPreview && (
          <button
            type="button"
            className={styles.action}
            onClick={() => setOpen(true)}
            aria-label={`${name} — ko'rish`}
          >
            Ko'rish
          </button>
        )}
        {file.objectUrl && (
          <>
            <a className={styles.action} href={file.objectUrl} target="_blank" rel="noreferrer">
              Yangi oynada ochish
            </a>
            <a className={styles.action} href={file.objectUrl} download={name}>
              Yuklab olish
            </a>
          </>
        )}
      </span>

      <Modal
        open={open && canPreview}
        onClose={() => setOpen(false)}
        title={name}
        width="min(900px, 94vw)"
      >
        {file.objectUrl && isPdf && (
          <iframe className={styles.frame} src={file.objectUrl} title={name} />
        )}
        {file.objectUrl && isImage && (
          <img className={styles.image} src={file.objectUrl} alt={name} />
        )}
      </Modal>
    </li>
  );
}
