import { useState } from 'react';
import { Chip, Modal } from '@/shared/ui';
import { useAuthFile } from './useAuthFile';
import styles from './AuthFileButton.module.css';

export interface AuthFileButtonProps {
  /** `/api/files/{id}` */
  url: string;
  /** Fayl nomi (tugma matni va modal sarlavhasi). */
  name: string;
  /** chip — fayllar qatorida; link — matn ichidagi havola ("shartnomani ochish"). */
  variant?: 'chip' | 'link';
  /** Tugma matni (default — `name`). */
  label?: string;
}

/**
 * Token bilan ochiladigan fayl. PDF va rasm modalda ko'rsatiladi (talaba kundalikni rasmga olib
 * PDF qilib yuborsa — tyutor/admin shu yerda o'qiydi), boshqa turlar yuklab olinadi.
 * Faqat shu ikki tur ichkarida ochiladi: HTML/SVG kabi fayllar skript ishlatishi mumkin, ular yuklanadi.
 */
export function AuthFileButton({ url, name, variant = 'chip', label }: AuthFileButtonProps) {
  const file = useAuthFile(url);
  const [open, setOpen] = useState(false);

  async function handleClick() {
    await file.load();
    setOpen(true);
  }

  const inline = file.status === 'ready' && file.contentType !== null;
  const isPdf = file.contentType === 'application/pdf';
  const isImage = file.contentType?.startsWith('image/') ?? false;

  const button = (
    <button
      type="button"
      className={variant === 'chip' ? styles.chipButton : styles.linkButton}
      onClick={() => void handleClick()}
      disabled={file.status === 'loading'}
    >
      {file.status === 'loading' ? 'Yuklanmoqda…' : (label ?? name)}
    </button>
  );

  return (
    <>
      {variant === 'chip' ? <Chip>{button}</Chip> : button}
      {file.error && (
        <span className={styles.error} role="alert">
          {file.error}
        </span>
      )}

      <Modal
        open={open && inline}
        onClose={() => setOpen(false)}
        title={name}
        width="min(900px, 94vw)"
        footer={
          file.objectUrl ? (
            <a className={styles.download} href={file.objectUrl} download={name}>
              Yuklab olish
            </a>
          ) : null
        }
      >
        {file.objectUrl && isPdf && (
          <iframe className={styles.frame} src={file.objectUrl} title={name} />
        )}
        {file.objectUrl && isImage && (
          <img className={styles.image} src={file.objectUrl} alt={name} />
        )}
        {file.objectUrl && !isPdf && !isImage && (
          <p className={styles.note}>
            Bu turdagi fayl brauzerda ko‘rsatilmaydi — uni yuklab oling.
          </p>
        )}
      </Modal>
    </>
  );
}
