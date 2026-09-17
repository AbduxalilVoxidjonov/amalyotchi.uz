import { useEffect } from 'react';
import { useAuthFile } from './useAuthFile';
import styles from './AuthFileButton.module.css';

export interface AuthFileEmbedProps {
  /** `/api/files/{id}` */
  url: string;
  /** Fayl nomi (sarlavha va `alt`/`title`). */
  name: string;
  /** md — odatiy (420px) · lg — fayllar ustuni uchun kattaroq (640px). */
  size?: 'md' | 'lg';
}

/**
 * Faylni **joyida** ko'rsatadi (bosish shart emas): PDF — `<iframe>`, rasm — `<img>`.
 * Talaba kundalikni daftardan rasmga olib PDF qilib yuboradi, tyutor/admin kun oynasida
 * uni darhol o'qiydi. Fayl nomi ham havola — bosilsa yangi oynada to'liq ochiladi.
 * Boshqa turlar brauzerda ko'rsatilmaydi — yuklab olish havolasi qoladi.
 */
export function AuthFileEmbed({ url, name, size = 'md' }: AuthFileEmbedProps) {
  const file = useAuthFile(url);
  const { load } = file;

  useEffect(() => {
    void load();
  }, [load]);

  const isPdf = file.contentType === 'application/pdf';
  const isImage = file.contentType?.startsWith('image/') ?? false;

  return (
    <figure className={styles.embed} data-size={size}>
      <figcaption className={styles.embedHead}>
        {/* Fayl nomi — havola: bosilsa yangi oynada to'liq ochiladi (blob token bilan olingan). */}
        {file.objectUrl ? (
          <a
            className={styles.embedName}
            href={file.objectUrl}
            target="_blank"
            rel="noreferrer"
            title={`${name} — yangi oynada ochish`}
          >
            {name}
          </a>
        ) : (
          <span className={styles.embedName}>{name}</span>
        )}
        {file.objectUrl && (
          <span className={styles.embedActions}>
            <a className={styles.download} href={file.objectUrl} target="_blank" rel="noreferrer">
              Yangi oynada ochish
            </a>
            <a className={styles.download} href={file.objectUrl} download={name}>
              Yuklab olish
            </a>
          </span>
        )}
      </figcaption>

      {file.status === 'loading' && (
        <p className={styles.note} role="status">
          Yuklanmoqda…
        </p>
      )}
      {file.status === 'error' && (
        <p className={styles.errorBlock} role="alert">
          {file.error}
        </p>
      )}
      {file.objectUrl && isPdf && (
        <iframe className={styles.embedFrame} src={file.objectUrl} title={name} />
      )}
      {file.objectUrl && isImage && (
        <img className={styles.embedImage} src={file.objectUrl} alt={name} />
      )}
      {file.objectUrl && !isPdf && !isImage && (
        <p className={styles.note}>Bu turdagi fayl brauzerda ko‘rsatilmaydi — uni yuklab oling.</p>
      )}
    </figure>
  );
}
