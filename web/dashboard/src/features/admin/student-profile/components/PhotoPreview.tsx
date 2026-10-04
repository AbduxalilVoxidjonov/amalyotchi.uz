import { useState } from 'react';
import { Modal } from '@/shared/ui';
import { AuthImage } from './AuthImage';
import styles from './PhotoPreview.module.css';

/** Rasm yuklanguncha/xato holatida joy egallaydigan blok balandligi. */
const PLACEHOLDER_HEIGHT: Record<'thumb' | 'card' | 'tile' | 'wide', number> = {
  thumb: 54,
  card: 120,
  tile: 120,
  wide: 360,
};

export interface PhotoPreviewProps {
  /** `/api/files/{id}` */
  url: string;
  /** Rasm tavsifi (a11y + modal sarlavhasi), masalan "12.10.2026 check-in rasmi". */
  label: string;
  /**
   * thumb (jadval katagi, 54px) · card (140px) · tile (galereya katagi — kenglik 100%, 4:5 kesilgan)
   * · wide (ustun kengligida — kun oynasidagi fayllar).
   */
  size?: 'thumb' | 'card' | 'tile' | 'wide';
}

/** Token bilan yuklanadigan rasm + bosilganda kattalashtirish (Modal). */
export function PhotoPreview({ url, label, size = 'thumb' }: PhotoPreviewProps) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button
        type="button"
        className={styles.trigger}
        data-size={size}
        onClick={(e) => {
          // Jadval katagida — qator bosilishi (kun oynasi) bilan birga ochilmasin.
          e.stopPropagation();
          setOpen(true);
        }}
        aria-label={`${label} — kattalashtirish`}
        title={label}
      >
        <AuthImage src={url} alt={label} height={PLACEHOLDER_HEIGHT[size]} />
      </button>
      <Modal open={open} onClose={() => setOpen(false)} title={label} width="min(1100px, 94vw)">
        <AuthImage src={url} alt={label} height={420} className={styles.full} />
      </Modal>
    </>
  );
}
